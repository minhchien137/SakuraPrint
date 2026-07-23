'use strict';
const { COLUMNS } = require('./columns');

// Thu tu cot THAT trong file CSV nguon (24 cot, header tieng Trung, xac nhan
// tu file mau thuc te ngay 2026-07-15):
//   条码,测试结果,测试时间,单位,A测试值,A极性,B测试值,B极性,...,J测试值,J极性
// (barcode, ket qua test tong, thoi gian test, don vi do (luon la "高斯" =
// Gauss), roi tung kenh A-J: gia tri do + cuc tinh).
//
// File nay KHONG co cot RESULT/TEST_RESULT rieng cho tung kenh (ARESULT,
// A_TEST_RESULT...) - nhung cot do trong bang dich se duoc de TRONG ('').
// Cot barcode duy nhat trong file dung chung cho ca UNIT_SN lan
// BARCODE_CONTENT1 (file khong co 2 cot barcode rieng).
const CSV_FIELD_ORDER = [
  'UNIT_SN',
  'STATUS',
  'DATE_TIME',
  'UT',
  'Test_value_A', 'APolarity',
  'Test_value_B', 'BPolarity',
  'Test_value_C', 'CPolarity',
  'Test_value_D', 'DPolarity',
  'Test_value_E', 'EPolarity',
  'Test_value_F', 'FPolarity',
  'Test_value_G', 'GPolarity',
  'Test_value_H', 'HPolarity',
  'Test_value_I', 'IPolarity',
  'Test_value_J', 'JPolarity'
];

const COLUMN_INDEX = {};
COLUMNS.forEach((c, i) => { COLUMN_INDEX[c] = i; });

const UNIT_SN_SLOT = COLUMN_INDEX.UNIT_SN;
const BARCODE_CONTENT1_SLOT = COLUMN_INDEX.BARCODE_CONTENT1;

// Chuyen 1 dong CSV thuc (mang 24 chuoi, dung thu tu CSV_FIELD_ORDER) thanh
// mang 45 phan tu dung vi tri COLUMNS (nhu 1 dong doc tu Excel) - de tai su
// dung nguyen ven pipeline validateRows/mapRow/insertRows hien co, khong
// phai viet lai logic rieng cho CSV.
function csvRowToColumnsRow(csvFields) {
  const row = new Array(COLUMNS.length).fill('');

  for (let i = 0; i < CSV_FIELD_ORDER.length; i++) {
    const targetCol = CSV_FIELD_ORDER[i];
    row[COLUMN_INDEX[targetCol]] = csvFields[i] ?? '';
  }

  row[BARCODE_CONTENT1_SLOT] = row[UNIT_SN_SLOT];

  return row;
}

module.exports = { csvRowToColumnsRow, CSV_FIELD_ORDER };
