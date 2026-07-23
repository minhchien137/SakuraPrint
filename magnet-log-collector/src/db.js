'use strict';
const sql = require('mssql');
const { COLUMNS } = require('./columns');

const DUPLICATE_KEY_ERROR_NUMBERS = new Set([2601, 2627]);

let poolPromise = null;

// Pool duoc tao 1 lan va tai su dung. Neu 1 truy van phat hien loi ket noi
// (mat DB), cache bi reset de chu ky sau tu tao lai pool moi thay vi giu mai
// 1 pool "chet".
function getPool(dbConfig, log) {
  if (!poolPromise) {
    const cfg = {
      server: dbConfig.server,
      database: dbConfig.database,
      user: dbConfig.user,
      password: dbConfig.password,
      // useUTC:false - QUAN TRONG: DATE_TIME duoc parse tu Excel thanh JS Date
      // theo GIO DIA PHUONG cua may chay service (vd 2026-01-05 13:59:00 gio
      // VN). Neu de mac dinh useUTC:true, driver tedious se quy doi Date do
      // sang UTC roi moi gui len SQL Server, lam gia tri DATETIME2 luu vao DB
      // bi LECH theo do lech mui gio cua may (vd -7 gio o VN). Tat useUTC de
      // driver gui dung cac thanh phan gio/phut/giay dia phuong nhu da parse.
      options: { ...(dbConfig.options || {}), useUTC: false },
      pool: dbConfig.pool,
      requestTimeout: dbConfig.requestTimeout,
      connectionTimeout: dbConfig.connectionTimeout
    };
    poolPromise = new sql.ConnectionPool(cfg).connect().catch((err) => {
      poolPromise = null; // cho phep thu lai o lan goi ke tiep
      throw err;
    });
  }
  return poolPromise;
}

function resetPool() {
  poolPromise = null;
}

function isConnectionError(err) {
  return err && (err.code === 'ECONNCLOSED' || err.code === 'ETIMEOUT' || err.code === 'ECONNREFUSED');
}

// Lay danh sach key (UNIT_SN + DATE_TIME) DA CO trong DB cho 1 file nguon -
// dung de biet dong nao trong file da luu roi (khong can insert lai), tranh
// phai thu insert-roi-bat-loi-trung cho toan bo dong cu moi chu ky (ton round
// trip khi file cang ve cuoi ngay cang nhieu dong).
async function getExistingKeys(dbConfig, table, sourceFileName, log) {
  const pool = await getPool(dbConfig, log);
  const result = await pool.request()
    .input('sourceFile', sql.NVarChar(260), sourceFileName)
    .query(`SELECT UNIT_SN, DATE_TIME FROM ${table} WHERE source_file = @sourceFile`);

  const keys = new Set();
  for (const row of result.recordset) {
    keys.add(`${row.UNIT_SN}|${row.DATE_TIME.getTime()}`);
  }
  return keys;
}

function keyOf(row) {
  return `${row.UNIT_SN}|${row.DATE_TIME.getTime()}`;
}

// Insert 1 batch dong (mang cac object values da map, CHUA CO trong DB theo
// getExistingKeys) vao bang dich, trong 1 transaction. Loi duplicate key (vi
// du 2 tien trinh cung insert 1 dong o cung thoi diem - hiem) van duoc bat va
// bo qua em nhu 1 lop phong ve cuoi cung, khong lam rollback ca transaction.
// Loi khac (mat ket noi, timeout...) se rollback toan bo batch va throw.
async function insertRows(dbConfig, table, sourceFileName, rows, log) {
  if (rows.length === 0) return { inserted: 0, duplicates: 0 };

  const pool = await getPool(dbConfig, log);
  const transaction = new sql.Transaction(pool);
  await transaction.begin();

  const columnList = [...COLUMNS, 'source_file'].join(', ');
  const paramList = [...COLUMNS, 'source_file'].map((c) => `@${c}`).join(', ');
  const insertSql = `INSERT INTO ${table} (${columnList}) VALUES (${paramList})`;

  let inserted = 0;
  let duplicates = 0;

  try {
    for (const row of rows) {
      const request = new sql.Request(transaction);
      for (const col of COLUMNS) {
        if (col === 'DATE_TIME') {
          request.input(col, sql.DateTime2, row[col]);
        } else {
          request.input(col, sql.NVarChar(sql.MAX), row[col]);
        }
      }
      request.input('source_file', sql.NVarChar(260), sourceFileName);

      try {
        await request.query(insertSql);
        inserted++;
      } catch (err) {
        if (err.number && DUPLICATE_KEY_ERROR_NUMBERS.has(err.number)) {
          duplicates++;
          log.debug(`Bo qua dong trung (UNIT_SN=${row.UNIT_SN}, DATE_TIME=${row.DATE_TIME.toISOString()}) - da ton tai trong DB.`);
        } else {
          throw err;
        }
      }
    }

    await transaction.commit();
    return { inserted, duplicates };
  } catch (err) {
    try {
      await transaction.rollback();
    } catch { /* transaction co the da bi huy boi loi ket noi - bo qua */ }

    if (isConnectionError(err)) resetPool();
    throw err;
  }
}

// Dem so dong hien co trong DB ung voi 1 file nguon - dung cho reconciliation
// (biet DA LUU bao nhieu dong, de so sanh voi so dong hop le doc duoc tu Excel).
async function getSourceFileCount(dbConfig, table, sourceFileName, log) {
  const pool = await getPool(dbConfig, log);
  const result = await pool.request()
    .input('sourceFile', sql.NVarChar(260), sourceFileName)
    .query(`SELECT COUNT(*) AS cnt FROM ${table} WHERE source_file = @sourceFile`);
  return result.recordset[0].cnt;
}

module.exports = { getPool, resetPool, insertRows, getExistingKeys, getSourceFileCount, keyOf };
