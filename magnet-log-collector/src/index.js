'use strict';
const path = require('path');
const { loadConfig } = require('./config');
const { Logger } = require('./logger');
const { runCycle } = require('./collector');
const { startStatusServer } = require('./statusServer');

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function main() {
  const configPath = process.env.MLC_CONFIG || path.join(__dirname, '..', 'config.json');
  const config = loadConfig(configPath);
  const log = new Logger({ logDir: config.logDir, level: config.logLevel, timeZone: config.timeZone });

  // Bat unhandledRejection/uncaughtException - chi LOG, KHONG thoat process.
  // Day la background service chay ngam qua NSSM, loi tam thoi (mang, DB,
  // file bi khoa...) khong duoc phep lam chet service.
  process.on('unhandledRejection', (reason) => {
    log.error(`unhandledRejection: ${reason && reason.stack ? reason.stack : reason}`);
  });
  process.on('uncaughtException', (err) => {
    log.error(`uncaughtException: ${err.stack || err.message}`);
  });

  log.info(`magnet-log-collector khoi dong. sourceDir=${config.sourceDir}, pollIntervalMs=${config.pollIntervalMs}, table=${config.table}`);

  // Trang thai "da luu du du lieu chua" cho tung file (hom nay/hom qua) - duoc
  // collector.js cap nhat sau moi chu ky, statusServer.js doc de hien thi
  // dashboard tren trinh duyet. Khong ghi ra dia - mat di khi restart service
  // la binh thuong, vi chu ky dau tien sau khi khoi dong se tinh lai ngay.
  const status = { startedAt: new Date().toISOString(), updatedAt: null, files: {} };
  const ctx = { config, log, status };

  startStatusServer(ctx);

  // Vong lap async tuan tu (khong dung setInterval) de dam bao khong bao gio
  // co 2 chu ky chong len nhau - chu ky ke tiep chi bat dau sau khi chu ky
  // truoc (bao gom insert DB) da hoan tat hoan toan.
  for (;;) {
    const t0 = Date.now();
    try {
      await runCycle(ctx);
    } catch (err) {
      log.error(`Loi khong mong doi trong runCycle (da bat, service tiep tuc chay): ${err.stack || err.message}`);
    }
    const elapsed = Date.now() - t0;
    await sleep(Math.max(0, config.pollIntervalMs - elapsed));
  }
}

main().catch((err) => {
  // Chi loi luc KHOI DONG (vd config.json sai dinh dang) moi lam thoat
  // process - day la loi cau hinh can nguoi van hanh sua thu cong.
  console.error(`Khong the khoi dong magnet-log-collector: ${err.stack || err.message}`);
  process.exit(1);
});
