'use strict';
const fs = require('fs');
const path = require('path');
const { vnYyyyMmDd } = require('./logger');
const { safeCopyFile, readSourceRows } = require('./excelReader');
const { validateRows } = require('./validate');
const { insertRows, getExistingKeys, getSourceFileCount, keyOf } = require('./db');

// .csv la dinh dang THUC TE cua tram magnet dimension check (xac nhan tu file
// mau thuc te) - uu tien tim truoc. Van giu .xlsx/.xls phong khi tram khac
// xuat Excel that.
const CANDIDATE_EXTENSIONS = ['.csv', '.xlsx', '.xls'];

// Tim file nguon theo ten ngay (yyyyMMdd), thu lan luot .csv, .xlsx, .xls -
// code tu nhan dien duoi thuc te, khong gia dinh truoc.
function findSourceFile(sourceDir, baseName) {
  for (const ext of CANDIDATE_EXTENSIONS) {
    const p = path.join(sourceDir, baseName + ext);
    if (fs.existsSync(p)) return p;
  }
  return null;
}

// Xu ly 1 file: doc + validate TOAN BO dong (khong can luu offset/state rieng
// - DB la nguon su that duy nhat), hoi DB xem dong nao (theo UNIT_SN +
// DATE_TIME) DA CO san, chi insert dong nao CHUA CO. Nho vay:
//   - Khong can state.json, khong co truong hop "file bi tao lai lam sai lech
//     offset" phai xu ly rieng.
//   - Service tat/bat lai (vd may xuong tat qua dem) van tu dong "vet" dung
//     phan con thieu cua NGAY HOM QUA khi chay lai, vi luon xet lai toan bo
//     file, khong dua vao vi tri da doc lan truoc.
// Tra ve null neu chu ky nay khong doc duoc gi (file dang khoa/loi doc) -
// caller GIU NGUYEN trang thai cu tren dashboard thay vi xoa trang.
async function processFile(ctx, filePath) {
  const { config, log } = ctx;
  const fileName = path.basename(filePath);

  const tempPath = await safeCopyFile(filePath, config.tempDir, log);
  if (!tempPath) return null; // file dang bi khoa boi phan mem do - bo qua chu ky nay, thu lai sau

  let dataRows;
  try {
    ({ dataRows } = readSourceRows(tempPath, config.csvEncoding));
  } catch (err) {
    log.error(`Loi doc file ${fileName}: ${err.message}`);
    return null;
  } finally {
    fs.promises.unlink(tempPath).catch(() => { /* don dep file tam - khong quan trong neu fail */ });
  }

  const totalRows = dataRows.length;
  const { mappedAll, validCount, invalidWarnings } = validateRows(dataRows);
  const validRows = mappedAll.filter(Boolean);

  let dbCount = null;
  let error = null;
  let inserted = 0;
  let duplicates = 0;
  let alreadyExisted = 0;

  try {
    const existingKeys = await getExistingKeys(config.db, config.table, fileName, log);
    alreadyExisted = existingKeys.size;
    const newRows = validRows.filter((r) => !existingKeys.has(keyOf(r)));

    const result = await insertRows(config.db, config.table, fileName, newRows, log);
    inserted = result.inserted;
    duplicates = result.duplicates;

    log.info(
      `${fileName}: tong ${totalRows} dong, hop le ${validCount}, da co san trong DB ${alreadyExisted}, ` +
      `insert moi ${inserted}, trung lap luc insert ${duplicates}, khong hop le (bo qua) ${invalidWarnings.length}.`
    );

    for (const w of invalidWarnings) log.warn(`${fileName}: ${w.message}`);

    // Dem lai chinh xac tu DB (khong tinh cong don tu bien dem cua rieng chu
    // ky nay) - day la con so dung cho dashboard/check, phan anh dung thuc te.
    dbCount = await getSourceFileCount(config.db, config.table, fileName, log);
  } catch (err) {
    error = err.message;
    log.error(`${fileName}: loi ket noi/ghi DB (${err.message}) - chu ky sau se tu thu lai (khong mat du lieu, khong dua vao state file nao ca).`);
  }

  return {
    fileName,
    totalRows,
    validCount,
    invalidCount: invalidWarnings.length,
    invalidWarnings: invalidWarnings.map((w) => w.message),
    dbCount,
    missing: dbCount === null ? null : Math.max(0, validCount - dbCount),
    ok: dbCount === null ? null : dbCount >= validCount,
    error,
    checkedAt: new Date().toISOString()
  };
}

async function runCycle(ctx) {
  const { config, log } = ctx;
  const today = vnYyyyMmDd(config.timeZone);
  const yesterday = vnYyyyMmDd(config.timeZone, new Date(Date.now() - 24 * 60 * 60 * 1000));

  for (const baseName of [yesterday, today]) {
    const filePath = findSourceFile(config.sourceDir, baseName);
    if (!filePath) {
      log.debug(`Khong tim thay file nguon cho ngay ${baseName} trong ${config.sourceDir}.`);
      continue;
    }
    const result = await processFile(ctx, filePath);
    if (result && ctx.status) {
      ctx.status.files[result.fileName] = result;
      ctx.status.updatedAt = new Date().toISOString();
    }
  }
}

module.exports = { runCycle, findSourceFile, processFile };
