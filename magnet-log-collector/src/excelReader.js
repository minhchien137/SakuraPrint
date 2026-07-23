'use strict';
const fs = require('fs');
const fsp = fs.promises;
const path = require('path');
const XLSX = require('xlsx');
const { COLUMNS, DATE_TIME_INDEX } = require('./columns');
const { csvRowToColumnsRow } = require('./csvMapper');

// Copy file nguon sang thu muc temp roi doc ban copy - file goc co the dang bi
// phan mem do mo/khoa (ghi de lien tuc). Neu copy fail vi file dang khoa
// (EBUSY/EPERM) hoac vua bi xoa (ENOENT) thi tra ve null de bo qua chu ky nay,
// KHONG throw (khong duoc lam chet vong lap chinh).
async function safeCopyFile(sourcePath, tempDir, log) {
  await fsp.mkdir(tempDir, { recursive: true });
  // QUAN TRONG: giu nguyen duoi file goc (.csv/.xlsx/.xls) o CUOI ten file
  // tam - readSourceRows() nhan dien dinh dang doc theo duoi file, neu de
  // ".tmp" o cuoi se doc nham dinh dang.
  const ext = path.extname(sourcePath);
  const baseNoExt = path.basename(sourcePath, ext);
  const tempPath = path.join(
    tempDir,
    `${baseNoExt}.${process.pid}.${Date.now()}${ext}`
  );
  try {
    await fsp.copyFile(sourcePath, tempPath);
    return tempPath;
  } catch (err) {
    if (err.code === 'EBUSY' || err.code === 'EPERM' || err.code === 'ENOENT') {
      log.debug(`Khong copy duoc file (${err.code}) - co the dang bi khoa boi phan mem do, thu lai chu ky sau: ${sourcePath}`);
      return null;
    }
    throw err;
  }
}

// Doc 1 workbook Excel (.xlsx/.xls, da copy an toan) va tra ve mang cac dong
// du lieu (khong gom header), moi dong la mang 45 phan tu string (hoac Date
// cho DATE_TIME), dung THU TU CỘT tu trai sang phai - KHONG doc theo ten
// header (header co the la tieng Trung).
//
// Voi cot DATE_TIME: lay gia tri "tho" (cell.v) - la Date thuc su neu SheetJS
// nhan dien duoc dinh dang ngay (nho doc voi cellDates:true), hoac chuoi neu
// cell luu dang text.
// Voi cac cot con lai: uu tien cell.w (chuoi da dinh dang hien thi, giu nguyen
// dau phay ngan cach hang nghin vd "190,734"), fallback ve cell.v neu khong co.
function readWorkbookRows(filePath) {
  const workbook = XLSX.readFile(filePath, { cellDates: true, raw: true });
  const sheetName = workbook.SheetNames[0];
  const sheet = workbook.Sheets[sheetName];
  if (!sheet || !sheet['!ref']) return { dataRows: [] };

  const range = XLSX.utils.decode_range(sheet['!ref']);
  const dataRows = [];

  // range.s.r = 0 la dong header -> du lieu bat dau tu dong index 1.
  for (let r = Math.max(range.s.r + 1, 1); r <= range.e.r; r++) {
    const row = new Array(COLUMNS.length).fill('');
    let rowHasAnyValue = false;

    for (let c = 0; c < COLUMNS.length; c++) {
      const addr = XLSX.utils.encode_cell({ r, c });
      const cell = sheet[addr];
      if (!cell) continue;

      rowHasAnyValue = true;
      if (c === DATE_TIME_INDEX) {
        row[c] = cell.v !== undefined ? cell.v : '';
      } else if (cell.w !== undefined) {
        row[c] = cell.w;
      } else if (cell.v !== undefined) {
        row[c] = String(cell.v);
      }
    }

    // Bo qua dong hoan toan trong (thuong la dong trailing sau du lieu that).
    if (rowHasAnyValue) dataRows.push(row);
  }

  return { dataRows };
}

// ── CSV ──────────────────────────────────────────────────────────────────
// File nguon thuc te cua tram magnet dimension check la CSV (khong phai
// Excel), 24 cot, header tieng Trung, xem chi tiet mapping trong csvMapper.js.

// Giai ma buffer file thanh text, tu nhan dien encoding:
//   1. Co BOM UTF-8 (EF BB BF) -> UTF-8, bo BOM.
//   2. Thu decode UTF-8 - neu xuat hien ky tu thay the (U+FFFD, dau hieu bytes
//      khong phai UTF-8 hop le) -> rat co the la GBK/GB2312 (may do Windows
//      Trung Quoc xuat CSV theo ANSI/GBK) -> decode lai bang iconv-lite.
//   3. forceEncoding ('utf8'|'gbk') trong config co the ep cung mot kieu,
//      dung khi tu nhan dien sai.
function decodeCsvBuffer(buf, forceEncoding) {
  if (forceEncoding === 'utf8') return buf.toString('utf8');
  if (forceEncoding === 'gbk') return require('iconv-lite').decode(buf, 'gbk');

  if (buf.length >= 3 && buf[0] === 0xef && buf[1] === 0xbb && buf[2] === 0xbf) {
    return buf.slice(3).toString('utf8');
  }

  const utf8Text = buf.toString('utf8');
  if (utf8Text.includes('�')) {
    try {
      return require('iconv-lite').decode(buf, 'gbk');
    } catch {
      return utf8Text;
    }
  }
  return utf8Text;
}

// Parser CSV toi gian nhung dung chuan RFC4180 (ho tro truong bao trong dau
// ngoac kep "...", ky tu phay/xuong dong nam trong ngoac kep, ngoac kep kep
// "" nghia la 1 dau ngoac kep literal trong truong).
function parseCsvLine(line) {
  const fields = [];
  let cur = '';
  let inQuotes = false;

  for (let i = 0; i < line.length; i++) {
    const c = line[i];
    if (inQuotes) {
      if (c === '"') {
        if (line[i + 1] === '"') { cur += '"'; i++; }
        else inQuotes = false;
      } else {
        cur += c;
      }
    } else if (c === '"') {
      inQuotes = true;
    } else if (c === ',') {
      fields.push(cur);
      cur = '';
    } else {
      cur += c;
    }
  }
  fields.push(cur);
  return fields;
}

function readCsvRows(filePath, csvEncoding) {
  const buf = fs.readFileSync(filePath);
  const text = decodeCsvBuffer(buf, csvEncoding);

  const lines = text.split(/\r\n|\n|\r/).filter((l) => l.length > 0);
  if (lines.length === 0) return { dataRows: [] };

  // Dong 1 la header (tieng Trung) - bo qua, khong map theo ten.
  const dataRows = lines.slice(1).map((line) => csvRowToColumnsRow(parseCsvLine(line)));
  return { dataRows };
}

// Dispatch theo duoi file thuc te - .csv dung parser CSV, .xlsx/.xls dung
// SheetJS. Ham nay la diem vao DUY NHAT ma collector.js/check.js goi de doc
// du lieu, khong quan tam dinh dang cu the ben duoi.
function readSourceRows(filePath, csvEncoding) {
  const ext = path.extname(filePath).toLowerCase();
  if (ext === '.csv') return readCsvRows(filePath, csvEncoding);
  return readWorkbookRows(filePath);
}

module.exports = { safeCopyFile, readWorkbookRows, readCsvRows, readSourceRows };
