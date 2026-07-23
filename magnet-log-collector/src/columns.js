'use strict';
/**
 * Danh sach 45 cot dich (SVN_MiddleDimensionCheckResult), dung dung THU TU
 * cot trong file Excel nguon (trai -> phai). Header file Excel la tieng Trung
 * nen KHONG map theo ten header - chi map theo vi tri index 0..44.
 */
const COLUMNS = [
  'UNIT_SN', 'BARCODE_CONTENT1', 'STATUS', 'DATE_TIME', 'UT',
  'APolarity', 'ARESULT', 'BPolarity', 'BRESULT', 'CPolarity', 'CRESULT',
  'DPolarity', 'DRESULT', 'EPolarity', 'ERESULT', 'FPolarity', 'FRESULT',
  'GPolarity', 'GRESULT', 'HPolarity', 'HRESULT', 'IPolarity', 'IRESULT',
  'JPolarity', 'JRESULT',
  'Test_value_A', 'A_TEST_RESULT', 'Test_value_B', 'B_TEST_RESULT',
  'Test_value_C', 'C_TEST_RESULT', 'Test_value_D', 'D_TEST_RESULT',
  'Test_value_E', 'E_TEST_RESULT', 'Test_value_F', 'F_TEST_RESULT',
  'Test_value_G', 'G_TEST_RESULT', 'Test_value_H', 'H_TEST_RESULT',
  'Test_value_I', 'I_TEST_RESULT', 'Test_value_J', 'J_TEST_RESULT'
];

const UNIT_SN_INDEX = COLUMNS.indexOf('UNIT_SN');
const STATUS_INDEX = COLUMNS.indexOf('STATUS');
const DATE_TIME_INDEX = COLUMNS.indexOf('DATE_TIME');

if (COLUMNS.length !== 45) {
  throw new Error(`COLUMNS phai co dung 45 phan tu, hien co ${COLUMNS.length}`);
}

module.exports = { COLUMNS, UNIT_SN_INDEX, STATUS_INDEX, DATE_TIME_INDEX };
