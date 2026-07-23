'use strict';
const fs = require('fs');
const path = require('path');

function loadConfig(configPath) {
  const resolved = configPath || path.join(__dirname, '..', 'config.json');
  const raw = JSON.parse(fs.readFileSync(resolved, 'utf8'));

  const projectRoot = path.join(__dirname, '..');
  const resolvePath = (p) => (path.isAbsolute(p) ? p : path.join(projectRoot, p));

  if (!raw.sourceDir) throw new Error('config.json: thieu "sourceDir"');
  if (!raw.db || !raw.db.server || !raw.db.database || !raw.db.user) {
    throw new Error('config.json: thieu cau hinh "db" (server/database/user/password)');
  }

  return {
    sourceDir: raw.sourceDir,
    pollIntervalMs: Number(raw.pollIntervalMs) > 0 ? Number(raw.pollIntervalMs) : 30000,
    tempDir: resolvePath(raw.tempDir || './temp'),
    logDir: resolvePath(raw.logDir || './logs'),
    logLevel: raw.logLevel || 'info',
    timeZone: raw.timeZone || 'Asia/Bangkok',
    table: raw.table || '[svn_pentaho].[dbo].[SVN_MiddleDimensionCheckResult]',
    // 'auto' (mac dinh, tu nhan dien UTF-8/GBK), 'utf8', hoac 'gbk' - dung khi
    // tu nhan dien sai (chu Trung Quoc trong dashboard bi loi font).
    csvEncoding: raw.csvEncoding || 'auto',
    statusServer: {
      enabled: raw.statusServer?.enabled !== false,
      port: Number(raw.statusServer?.port) > 0 ? Number(raw.statusServer.port) : 8022,
      host: raw.statusServer?.host || '0.0.0.0'
    },
    db: {
      server: raw.db.server,
      database: raw.db.database,
      user: raw.db.user,
      password: raw.db.password,
      options: raw.db.options || { encrypt: true, trustServerCertificate: true },
      pool: raw.db.pool || { max: 5, min: 0, idleTimeoutMillis: 30000 },
      requestTimeout: Number(raw.db.requestTimeoutMs) || 15000,
      connectionTimeout: Number(raw.db.connectionTimeoutMs) || 15000
    }
  };
}

module.exports = { loadConfig };
