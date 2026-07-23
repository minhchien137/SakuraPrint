'use strict';

// Parse cell DATE_TIME - co the la:
//   1. Date thuc su cua Excel (SheetJS tra ve JS Date khi doc voi cellDates:true)
//   2. Chuoi dang "2026/1/5 1:59 PM" (hoac bien the co dau '-', co giay, 24h...)
// Tra ve Date hop le hoac null neu khong parse duoc.
function parseDateTimeValue(raw) {
  if (raw === null || raw === undefined || raw === '') return null;

  if (raw instanceof Date) {
    return isNaN(raw.getTime()) ? null : raw;
  }

  const s = String(raw).trim();
  if (!s) return null;

  // "yyyy/M/d h:mm[:ss] AM/PM" hoac "yyyy-M-d h:mm[:ss]" (24h neu khong co AM/PM)
  const m = s.match(/^(\d{4})[\/\-](\d{1,2})[\/\-](\d{1,2})[ T](\d{1,2}):(\d{2})(?::(\d{2}))?\s*(AM|PM)?$/i);
  if (m) {
    const [, yy, mo, dd, hh, mi, ss, ap] = m;
    let h = parseInt(hh, 10);
    const y = parseInt(yy, 10);
    const mon = parseInt(mo, 10);
    const d = parseInt(dd, 10);
    const min = parseInt(mi, 10);
    const sec = ss ? parseInt(ss, 10) : 0;
    if (ap) {
      const apUpper = ap.toUpperCase();
      if (apUpper === 'PM' && h < 12) h += 12;
      if (apUpper === 'AM' && h === 12) h = 0;
    }
    const dt = new Date(y, mon - 1, d, h, min, sec);
    return isNaN(dt.getTime()) ? null : dt;
  }

  // Fallback: de JS tu parse (ISO 8601, cac dinh dang chuan khac).
  const generic = new Date(s);
  return isNaN(generic.getTime()) ? null : generic;
}

module.exports = { parseDateTimeValue };
