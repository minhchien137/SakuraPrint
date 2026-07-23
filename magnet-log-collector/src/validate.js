'use strict';
const { mapRow } = require('./mapper');

// Validate TOAN BO cac dong du lieu cua 1 file (khong chi phan dong moi) -
// dung chung cho ca luong insert (collector.js) lan luong kiem tra read-only
// (check.js, status dashboard). Tra ve:
//   mappedAll       : mang cung do dai dataRows, moi phan tu la object values
//                      da map (neu dong hop le) hoac null (neu bi bo qua).
//   validCount      : so dong hop le.
//   invalidWarnings : mang { index, message } cho tung dong bi bo qua - index
//                      la vi tri 0-based trong dataRows (dung de loc "dong
//                      moi" o collector.js), message la canh bao kem so dong Excel.
function validateRows(dataRows) {
  let validCount = 0;
  const invalidWarnings = [];

  const mappedAll = dataRows.map((row, i) => {
    const excelRowNumber = i + 2; // +1 header, +1 doi 0-index -> 1-index
    const { values, warning } = mapRow(row, excelRowNumber);
    if (warning) {
      invalidWarnings.push({ index: i, message: warning });
      return null;
    }
    validCount++;
    return values;
  });

  return { mappedAll, validCount, invalidWarnings };
}

module.exports = { validateRows };
