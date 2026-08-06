// ── Config ────────────────────────────────────────────────────────────────────
const NODE_SERVICE = 'http://localhost:8021';

// ── DOM refs ──────────────────────────────────────────────────────────────────
const $content      = document.getElementById('content');
const $ip           = document.getElementById('printer-ip');
const $port         = document.getElementById('printer-port');
const $size         = document.getElementById('label-size');
const $statusBox    = document.getElementById('status-box');
const $zplSect      = document.getElementById('zpl-section');
const $zplOut       = document.getElementById('zpl-out');
const $spinner      = document.getElementById('spinner');
const $btnTxt       = document.getElementById('btn-txt');
const $btnPrint     = document.getElementById('btn-print');
const $scanPill     = document.getElementById('scan-pill');
const $scanTxt      = document.getElementById('scan-txt');
const $ipStatus     = document.getElementById('ip-status');
const $nodeDot      = document.getElementById('node-dot');
const $nodeTxt      = document.getElementById('node-txt');

// USB refs
const $modeRadios   = document.querySelectorAll('input[name="print-mode"]');
const $tcpBlock     = document.getElementById('tcp-block');
const $usbBlock     = document.getElementById('usb-block');
const $usbSelect    = document.getElementById('usb-select');
const $btnRefreshUsb = document.getElementById('btn-refresh-usb');
const $usbStatus    = document.getElementById('usb-status');

// ── Print mode toggle ──────────────────────────────────────────────────────────
function getPrintMode() {
  for (const r of $modeRadios) if (r.checked) return r.value;
  return 'tcp';
}

$modeRadios.forEach(r => {
  r.addEventListener('change', () => {
    const mode = getPrintMode();
    $tcpBlock.style.display = mode === 'tcp' ? '' : 'none';
    $usbBlock.style.display = mode === 'usb' ? '' : 'none';
    if (mode === 'usb') loadUsbPrinters();
  });
});

// ── USB printer loader ─────────────────────────────────────────────────────────
async function loadUsbPrinters() {
  $usbSelect.innerHTML = '<option value="">⏳ Đang tìm máy in…</option>';
  $usbSelect.disabled  = true;
  $usbStatus.textContent = '';

  try {
    const r    = await fetch(`${NODE_SERVICE}/usb-printers`, { signal: AbortSignal.timeout(6000) });
    const data = await r.json();

    if (!data.printers || data.printers.length === 0) {
      $usbSelect.innerHTML = '<option value="">— Không tìm thấy máy in —</option>';
      $usbStatus.textContent = 'Không có máy in nào được phát hiện';
      $usbStatus.className   = 'usb-status bad';
      return;
    }

    $usbSelect.innerHTML = '<option value="">— Chọn máy in —</option>' +
      data.printers.map(p =>
        `<option value="${escHtml(p)}">${escHtml(p)}</option>`
      ).join('');
    $usbSelect.disabled  = false;
    $usbStatus.textContent = `✓  Tìm thấy ${data.printers.length} máy in`;
    $usbStatus.className   = 'usb-status ok';
  } catch (err) {
    $usbSelect.innerHTML = '<option value="">— Lỗi tải danh sách —</option>';
    $usbStatus.textContent = 'Không thể kết nối localhost:8021';
    $usbStatus.className   = 'usb-status bad';
  }
}

$btnRefreshUsb.addEventListener('click', loadUsbPrinters);

// ── Scan indicator ─────────────────────────────────────────────────────────────
let scanTimer;
$content.addEventListener('input', () => {
  $scanPill.classList.add('active');
  $scanTxt.textContent = 'Đang nhận…';
  clearTimeout(scanTimer);
  scanTimer = setTimeout(() => {
    $scanPill.classList.remove('active');
    $scanTxt.textContent = 'Sẵn sàng';
  }, 1400);
});

// ── IP validation ──────────────────────────────────────────────────────────────
$ip.addEventListener('input', () => {
  const v  = $ip.value.trim();
  const ok = /^(\d{1,3}\.){3}\d{1,3}$/.test(v);
  $ipStatus.textContent = v === '' ? '' : ok ? `✓  ${v}` : '✗  Định dạng IP không hợp lệ';
  $ipStatus.className   = 'ip-status ' + (v === '' ? '' : ok ? 'ok' : 'bad');
});

// ── Node.js health check ───────────────────────────────────────────────────────
async function checkNode() {
  try {
    const r = await fetch(`${NODE_SERVICE}/health`, { signal: AbortSignal.timeout(2500) });
    if (r.ok) {
      $nodeDot.className = 'node-dot alive';
      $nodeTxt.textContent = 'localhost:8021 — Online';
      return true;
    }
  } catch { /* offline */ }
  $nodeDot.className = 'node-dot dead';
  $nodeTxt.textContent = 'localhost:8021 — Offline';
  return false;
}
checkNode();
setInterval(checkNode, 8000);

// ── Helpers ────────────────────────────────────────────────────────────────────
function getDims() {
  const [w, h] = ($size.value || '4x2').split('x').map(Number);
  return { labelWidth: w || 4, labelHeight: h || 2 };
}

function setLoading(on) {
  $btnPrint.disabled      = on;
  $spinner.style.display  = on ? 'block' : 'none';
  $btnTxt.textContent     = on ? 'Đang gửi…' : '🖨 Gửi lệnh in';
}

function showStatus(msg, type) {
  $statusBox.innerHTML = `<span>${type === 'success' ? '✓' : '✗'}</span><span>${msg}</span>`;
  $statusBox.className = `show ${type}`;
}

function escHtml(str) {
  return str.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

// ── Step 1: Gọi ASP.NET (cloud) để sinh ZPL ───────────────────────────────────
async function generateZpl() {
  const content = $content.value.trim();
  if (!content) { showStatus('Vui lòng nhập nội dung nhãn.', 'error'); return null; }

  // Dùng APP_URLS.generateZpl (được sinh bởi Razor, tự động kèm PathBase)
  // Fallback về '/Print/GenerateZpl' nếu chạy local không có PathBase
  const url = (window.APP_URLS && window.APP_URLS.generateZpl)
    ? window.APP_URLS.generateZpl
    : '/Print/GenerateZpl';

  const res  = await fetch(url, {
    method : 'POST',
    headers: { 'Content-Type': 'application/json' },
    body   : JSON.stringify({ content, ...getDims() })
  });

  const data = await res.json();
  if (!res.ok || !data.success) {
    showStatus(data.error || 'Lỗi sinh ZPL.', 'error');
    return null;
  }
  return data.zpl;
}

// ── Step 2a: Browser → localhost:8021 → TCP → máy in ─────────────────────────
async function sendToNodeTcp(zpl) {
  const printerIp   = $ip.value.trim();
  const printerPort = parseInt($port.value) || 9100;

  if (!printerIp) { showStatus('Vui lòng nhập địa chỉ IP máy in.', 'error'); return null; }

  const res  = await fetch(`${NODE_SERVICE}/print`, {
    method : 'POST',
    headers: { 'Content-Type': 'application/json' },
    body   : JSON.stringify({ zpl, printerIp, printerPort })
  });

  const data = await res.json();
  if (!res.ok || !data.success) {
    showStatus(data.error || 'Node service trả về lỗi.', 'error');
    return null;
  }
  return data;
}

// ── Step 2b: Browser → localhost:8021 → USB → máy in ─────────────────────────
async function sendToNodeUsb(zpl) {
  const printerName = $usbSelect.value;
  if (!printerName) {
    showStatus('Vui lòng chọn máy in USB.', 'error');
    return null;
  }

  const res  = await fetch(`${NODE_SERVICE}/print-usb`, {
    method : 'POST',
    headers: { 'Content-Type': 'application/json' },
    body   : JSON.stringify({ zpl, printerName })
  });

  const data = await res.json();
  if (!res.ok || !data.success) {
    showStatus(data.error || 'Node service trả về lỗi.', 'error');
    return null;
  }
  return data;
}

// ── Preview only ───────────────────────────────────────────────────────────────
async function doPreview() {
  const zpl = await generateZpl();
  if (!zpl) return;
  $zplOut.textContent  = zpl;
  $zplSect.className   = 'section show';
  $statusBox.className = '';
}

// ── Full print flow ────────────────────────────────────────────────────────────
async function doPrint() {
  setLoading(true);
  $statusBox.className = '';

  try {
    const zpl = await generateZpl();
    if (!zpl) { setLoading(false); return; }

    $zplOut.textContent = zpl;
    $zplSect.className  = 'section show';

    const mode   = getPrintMode();
    const result = mode === 'usb'
      ? await sendToNodeUsb(zpl)
      : await sendToNodeTcp(zpl);

    if (!result) { setLoading(false); return; }

    if (mode === 'usb') {
      showStatus(
        `Đã gửi <strong>${result.bytesSent} bytes</strong> đến ` +
        `<strong>${escHtml($usbSelect.value)}</strong> — ${result.duration}ms`,
        'success'
      );
    } else {
      showStatus(
        `Đã gửi <strong>${result.bytesSent} bytes</strong> đến ` +
        `<strong>${$ip.value.trim()}:${$port.value || 9100}</strong> — ${result.duration}ms`,
        'success'
      );
    }
  } catch (err) {
    const msg = err.message.includes('fetch') || err.message.includes('Failed')
      ? 'Không thể kết nối <strong>localhost:8021</strong>. Hãy chạy <code>npm run dev</code> trên máy này.'
      : err.message;
    showStatus(msg, 'error');
  } finally {
    setLoading(false);
  }
}

// ── Enter to print (barcode scanner) ──────────────────────────────────────────
$content.addEventListener('keydown', e => {
  if (e.key === 'Enter' && !e.shiftKey && $content.value.trim()) {
    e.preventDefault();
    doPrint();
  }
});