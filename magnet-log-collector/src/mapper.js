'use strict';
const { COLUMNS, UNIT_SN_INDEX, STATUS_INDEX, DATE_TIME_INDEX } = require('./columns');
const { parseDateTimeValue } = require('./dateParser');

// Map 1 dong tho (mang 45 phan tu, dung thu tu cot Excel) thanh object
// { values: {...45 cot...}, warning: null } hoac { values: null, warning: '...' }
// neu dong bi bo qua (thieu UNIT_SN/STATUS, hoac DATE_TIME khong parse duoc).
function mapRow(rawRow, rowNumberForLog) {
  const unitSn = (rawRow[UNIT_SN_INDEX] ?? '').toString().trim();
  const status = (rawRow[STATUS_INDEX] ?? '').toString().trim();

  if (!unitSn) {
    return { values: null, warning: `Dong ${rowNumberForLog}: thieu UNIT_SN - bo qua dong.` };
  }
  if (!status) {
    return { values: null, warning: `Dong ${rowNumberForLog}: thieu STATUS - bo qua dong.` };
  }

  const dateTime = parseDateTimeValue(rawRow[DATE_TIME_INDEX]);
  if (!dateTime) {
    return {
      values: null,
      warning: `Dong ${rowNumberForLog}: khong parse duoc DATE_TIME (gia tri goc: "${rawRow[DATE_TIME_INDEX]}") - bo qua dong.`
    };
  }

  const values = {};
  for (let i = 0; i < COLUMNS.length; i++) {
    if (i === DATE_TIME_INDEX) {
      values[COLUMNS[i]] = dateTime;
    } else {
      values[COLUMNS[i]] = (rawRow[i] ?? '').toString();
    }
  }
  values.UNIT_SN = unitSn;
  values.STATUS = status;

  return { values, warning: null };
}

module.exports = { mapRow };
