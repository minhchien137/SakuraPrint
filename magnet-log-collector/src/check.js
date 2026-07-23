'use strict';
// Cong cu kiem tra thu cong: doc lai file Excel (khong insert gi), dem so
// dong hop le, roi hoi DB xem da luu duoc bao nhieu dong ung voi file do -
// cho biet ngay "du du lieu" hay "thieu bao nhieu dong" ma khong can mo
// trinh duyet (dung khi may khong co man hinh, hoac muon xem nhanh qua SSH).
//
// Cach dung:
//   node src/check.js            -> kiem tra file cua HOM NAY
//   npm run check                -> tuong duong lenh tren
//   node src/check.js 20260716   -> kiem tra 1 ngay cu the (yyyyMMdd)
//   node src/check.js all        -> kiem tra TAT CA file dang co trong sourceDir

const fs = require('fs');
const path = require('path');
const { loadConfig } = require('./config');
const { vnYyyyMmDd } = require('./logger');
const { findSourceFile } = require('./collector');
const { safeCopyFile, readSourceRows } = require('./excelReader');
const { validateRows } = require('./validate');
const { getSourceFileCount } = require('./db');

function listAllSourceBaseNames(sourceDir) {
  return fs.readdirSync(sourceDir)
    .filter((f) => /\.(csv|xlsx|xls)$/i.test(f))
    .map((f) => f.replace(/\.(csv|xlsx|xls)$/i, ''))
    .filter((v, i, arr) => arr.indexOf(v) === i)
    .sort();
}

async function checkOneFile(config, filePath) {
  const fileName = path.basename(filePath);

  const tempPath = await safeCopyFile(filePath, config.tempDir, console);
  if (!tempPath) {
    console.log(`\n===== ${fileName} =====`);
    console.log('File dang bi khoa (co the dang mo boi phan mem do) - thu lai sau.');
    return;
  }

  let dataRows;
  try {
    ({ dataRows } = readSourceRows(tempPath, config.csvEncoding));
  } finally {
    fs.promises.unlink(tempPath).catch(() => {});
  }

  const totalRows = dataRows.length;
  const { validCount, invalidWarnings } = validateRows(dataRows);
  const dbCount = await getSourceFileCount(config.db, config.table, fileName, console);

  console.log('');
  console.log(`===== ${fileName} =====`);
  console.log(`Tong so dong du lieu trong Excel      : ${totalRows}`);
  console.log(`So dong hop le (du dieu kien de luu)  : ${validCount}`);
  console.log(`So dong bi bo qua (thieu UNIT_SN/STATUS/DATE_TIME loi): ${invalidWarnings.length}`);
  invalidWarnings.forEach((w) => console.log(`   - ${w.message}`));
  console.log(`So dong hien co trong DB (source_file = ${fileName}) : ${dbCount}`);

  if (dbCount >= validCount) {
    console.log('==> DU DU LIEU (DB da luu >= so dong hop le trong file).');
  } else {
    console.log(`==> THIEU ${validCount - dbCount} dong - kiem tra logs/collector-yyyyMMdd.log de biet ly do,`);
    console.log('    hoac cho them 1 chu ky (service tu chay lai la tu vet du).');
  }
}

async function main() {
  const configPath = process.env.MLC_CONFIG || path.join(__dirname, '..', 'config.json');
  const config = loadConfig(configPath);

  const arg = process.argv[2];
  let baseNames;
  if (arg === 'all') {
    baseNames = listAllSourceBaseNames(config.sourceDir);
  } else if (arg) {
    baseNames = [arg];
  } else {
    baseNames = [vnYyyyMmDd(config.timeZone)];
  }

  for (const baseName of baseNames) {
    const filePath = findSourceFile(config.sourceDir, baseName);
    if (!filePath) {
      console.log(`\n===== ${baseName} =====`);
      console.log(`Khong tim thay file nguon (.csv/.xlsx/.xls) trong ${config.sourceDir}.`);
      continue;
    }
    await checkOneFile(config, filePath);
  }

  process.exit(0);
}

main().catch((err) => {
  console.error(`Loi khi kiem tra: ${err.stack || err.message}`);
  process.exit(1);
});
