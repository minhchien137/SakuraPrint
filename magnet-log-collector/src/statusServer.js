'use strict';
// May chu HTTP nho, tu than (khong phu thuoc goi ngoai, dung module 'http'
// co san cua Node) - phuc vu 1 trang dashboard de xem "da luu du du lieu hay
// chua" tren trinh duyet, khong can go lenh gi ca. Du lieu hien thi lay tu
// ctx.status - duoc collector.js cap nhat sau moi chu ky quet.

const http = require('http');

function renderHtml(status) {
  const files = Object.values(status.files || {}).sort((a, b) => (a.fileName < b.fileName ? 1 : -1));

  const rows = files.map((f) => {
    let statusCell;
    if (f.error) {
      statusCell = `<span class="badge badge-err">LỖI DB</span><div class="err-msg">${escapeHtml(f.error)}</div>`;
    } else if (f.ok) {
      statusCell = `<span class="badge badge-ok">✓ ĐỦ DỮ LIỆU</span>`;
    } else {
      statusCell = `<span class="badge badge-warn">⚠ THIẾU ${f.missing}</span>`;
    }

    const invalidCell = f.invalidCount > 0
      ? `<span class="invalid-count" title="${escapeHtml(f.invalidWarnings.join('\n'))}">${f.invalidCount} dòng lỗi ▾</span>`
      : '—';

    return `
      <tr>
        <td class="mono">${escapeHtml(f.fileName)}</td>
        <td class="num">${f.totalRows}</td>
        <td class="num">${f.validCount}</td>
        <td class="num">${invalidCell}</td>
        <td class="num">${f.dbCount === null ? '—' : f.dbCount}</td>
        <td>${statusCell}</td>
        <td class="muted">${formatTime(f.checkedAt)}</td>
      </tr>`;
  }).join('');

  const emptyRow = `<tr><td colspan="7" class="empty">Chưa có dữ liệu - đợi chu kỳ quét đầu tiên hoàn tất (tối đa vài chục giây sau khi service khởi động)...</td></tr>`;

  return `<!doctype html>
<html lang="vi">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>Magnet Log Collector - Trạng thái</title>
<meta http-equiv="refresh" content="15" />
<style>
  :root {
    --accent: #0F5FA6; --accent-dark: #0B4676;
    --ok: #059669; --warn: #D97706; --err: #DC2626;
    --gray-50:#F9FAFB; --gray-200:#E5E7EB; --gray-500:#6B7280; --gray-900:#111827;
  }
  * { box-sizing: border-box; }
  body {
    margin: 0; padding: 24px; background: var(--gray-50); color: var(--gray-900);
    font-family: -apple-system, Segoe UI, Inter, Arial, sans-serif;
  }
  .wrap { max-width: 1100px; margin: 0 auto; }
  h1 { font-size: 20px; margin: 0 0 4px; color: var(--accent-dark); }
  .sub { font-size: 12.5px; color: var(--gray-500); margin: 0 0 18px; }
  table { width: 100%; border-collapse: collapse; background: #fff; border-radius: 10px; overflow: hidden;
    box-shadow: 0 1px 3px rgba(15,95,166,0.1); font-size: 13.5px; }
  th { text-align: left; background: var(--accent); color: #fff; padding: 10px 12px; font-size: 11.5px;
    text-transform: uppercase; letter-spacing: 0.4px; }
  td { padding: 10px 12px; border-bottom: 1px solid var(--gray-200); vertical-align: top; }
  tr:last-child td { border-bottom: none; }
  .mono { font-family: 'Consolas', monospace; font-weight: 600; }
  .num { text-align: right; }
  .muted { color: var(--gray-500); font-size: 12px; white-space: nowrap; }
  .empty { text-align: center; padding: 30px; color: var(--gray-500); }
  .badge { display: inline-block; padding: 3px 10px; border-radius: 6px; font-size: 11.5px; font-weight: 800;
    letter-spacing: 0.3px; color: #fff; white-space: nowrap; }
  .badge-ok { background: var(--ok); }
  .badge-warn { background: var(--warn); }
  .badge-err { background: var(--err); }
  .err-msg { font-size: 11px; color: var(--err); margin-top: 3px; max-width: 260px; }
  .invalid-count { cursor: help; color: var(--warn); font-weight: 600; border-bottom: 1px dashed var(--warn); }
  .footer { margin-top: 14px; font-size: 11.5px; color: var(--gray-500); }
</style>
</head>
<body>
  <div class="wrap">
    <h1>Magnet Log Collector — Trạng thái lưu dữ liệu</h1>
    <p class="sub">Trang tự làm mới mỗi 15 giây. Bắt đầu chạy lúc: ${formatTime(status.startedAt)} — Cập nhật lần cuối: ${formatTime(status.updatedAt)}</p>
    <table>
      <thead>
        <tr>
          <th>File nguồn</th>
          <th style="text-align:right">Tổng dòng Excel</th>
          <th style="text-align:right">Hợp lệ</th>
          <th style="text-align:right">Bỏ qua (lỗi)</th>
          <th style="text-align:right">Đã lưu trong DB</th>
          <th>Trạng thái</th>
          <th>Kiểm tra lúc</th>
        </tr>
      </thead>
      <tbody>${rows || emptyRow}</tbody>
    </table>
    <p class="footer">Bảng đích: ${escapeHtml(status.table || '')}</p>
  </div>
</body>
</html>`;
}

function formatTime(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '—';
  return d.toLocaleString('vi-VN');
}

function escapeHtml(s) {
  return String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

function startStatusServer(ctx) {
  const { config, log } = ctx;
  const cfg = config.statusServer;
  if (!cfg || !cfg.enabled) {
    log.info('Status dashboard bi tat (statusServer.enabled = false trong config.json).');
    return null;
  }

  const server = http.createServer((req, res) => {
    const url = req.url.split('?')[0];

    if (url === '/api/status') {
      res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
      res.end(JSON.stringify({ ...ctx.status, table: config.table }));
      return;
    }

    if (url === '/' || url === '') {
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
      res.end(renderHtml({ ...ctx.status, table: config.table }));
      return;
    }

    res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
    res.end('Not found');
  });

  server.on('error', (err) => {
    log.error(`Status dashboard khong khoi dong duoc tren ${cfg.host}:${cfg.port} - ${err.message}. Service van tiep tuc chay binh thuong (chi mat trang xem, khong anh huong den viec luu du lieu).`);
  });

  server.listen(cfg.port, cfg.host, () => {
    log.info(`Status dashboard dang chay tai http://${cfg.host === '0.0.0.0' ? 'localhost' : cfg.host}:${cfg.port} (va tren cac dia chi IP LAN cua may nay neu firewall cho phep).`);
  });

  return server;
}

module.exports = { startStatusServer };
