'use strict';
const fs = require('fs');
const path = require('path');

const LEVELS = { debug: 10, info: 20, warn: 30, error: 40 };

// Ngay theo mui gio Viet Nam (doc lap he dieu hanh), dung de dat ten file
// log va lam moc "hom nay/hom qua" khi tim file Excel nguon.
function vnDateParts(timeZone, date = new Date()) {
  const fmt = new Intl.DateTimeFormat('en-CA', {
    timeZone, year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false
  });
  const parts = {};
  for (const p of fmt.formatToParts(date)) parts[p.type] = p.value;
  return parts; // { year, month, day, hour, minute, second }
}

function vnYyyyMmDd(timeZone, date = new Date()) {
  const p = vnDateParts(timeZone, date);
  return `${p.year}${p.month}${p.day}`;
}

class Logger {
  constructor({ logDir, level, timeZone }) {
    this.logDir = logDir;
    this.level = LEVELS[level] ? level : 'info';
    this.timeZone = timeZone || 'Asia/Bangkok';
    fs.mkdirSync(this.logDir, { recursive: true });
    this._currentDay = null;
    this._stream = null;
  }

  _ensureStream() {
    const day = vnYyyyMmDd(this.timeZone);
    if (day !== this._currentDay) {
      if (this._stream) this._stream.end();
      this._currentDay = day;
      const file = path.join(this.logDir, `collector-${day}.log`);
      this._stream = fs.createWriteStream(file, { flags: 'a' });
    }
    return this._stream;
  }

  _write(level, msg) {
    if (LEVELS[level] < LEVELS[this.level]) return;
    const p = vnDateParts(this.timeZone);
    const ts = `${p.year}-${p.month}-${p.day} ${p.hour}:${p.minute}:${p.second}`;
    const line = `[${ts}] [${level.toUpperCase()}] ${msg}`;
    try {
      this._ensureStream().write(line + '\n');
    } catch { /* ignore ghi log loi - khong duoc lam chet service vi log */ }
    if (level === 'error') console.error(line);
    else if (level === 'warn') console.warn(line);
    else console.log(line);
  }

  debug(msg) { this._write('debug', msg); }
  info(msg) { this._write('info', msg); }
  warn(msg) { this._write('warn', msg); }
  error(msg) { this._write('error', msg); }
}

module.exports = { Logger, vnYyyyMmDd, vnDateParts };
