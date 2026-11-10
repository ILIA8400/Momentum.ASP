'use strict';
// تبدیل جلالی <-> میلادی (الگوریتم jalaali-js، MIT)
const div = (a, b) => ~~(a / b);
const mod = (a, b) => a - ~~(a / b) * b;

function jalCal(jy) {
  const breaks = [-61, 9, 38, 199, 426, 686, 756, 818, 1111, 1181, 1210, 1635, 2060, 2097, 2192, 2262, 2324, 2394, 2456, 3178];
  const gy = jy + 621;
  let leapJ = -14, jp = breaks[0], jm, jump = 0, n, i;
  for (i = 1; i < breaks.length; i++) {
    jm = breaks[i]; jump = jm - jp;
    if (jy < jm) break;
    leapJ += div(jump, 33) * 8 + div(mod(jump, 33), 4);
    jp = jm;
  }
  n = jy - jp;
  leapJ += div(n, 33) * 8 + div(mod(n, 33) + 3, 4);
  if (mod(jump, 33) === 4 && jump - n === 4) leapJ += 1;
  const leapG = div(gy, 4) - div((div(gy, 100) + 1) * 3, 4) - 150;
  const march = 20 + leapJ - leapG;
  if (jump - n < 6) n = n - jump + div(jump + 4, 33) * 33;
  let leap = mod(mod(n + 1, 33) - 1, 4);
  if (leap === -1) leap = 4;
  return { leap, gy, march };
}
function g2d(gy, gm, gd) {
  let d = div((gy + div(gm - 8, 6) + 100100) * 1461, 4) + div(153 * mod(gm + 9, 12) + 2, 5) + gd - 34840408;
  d = d - div(div(gy + 100100 + div(gm - 8, 6), 100) * 3, 4) + 752;
  return d;
}
function d2g(jdn) {
  let j = 4 * jdn + 139361631;
  j = j + div(div(4 * jdn + 183187720, 146097) * 3, 4) * 4 - 3908;
  const i = div(mod(j, 1461), 4) * 5 + 308;
  const gd = div(mod(i, 153), 5) + 1;
  const gm = mod(div(i, 153), 12) + 1;
  const gy = div(j, 1461) - 100100 + div(8 - gm, 6);
  return { gy, gm, gd };
}
function j2d(jy, jm, jd) {
  const r = jalCal(jy);
  return g2d(r.gy, 3, r.march) + (jm - 1) * 31 - div(jm, 7) * (jm - 7) + jd - 1;
}
function d2j(jdn) {
  const gy = d2g(jdn).gy;
  let jy = gy - 621;
  const r = jalCal(jy);
  const jdn1f = g2d(gy, 3, r.march);
  let k = jdn - jdn1f;
  if (k >= 0) {
    if (k <= 185) return { jy, jm: 1 + div(k, 31), jd: mod(k, 31) + 1 };
    k -= 186;
  } else {
    jy -= 1; k += 179;
    if (r.leap === 1) k += 1;
  }
  return { jy, jm: 7 + div(k, 30), jd: mod(k, 30) + 1 };
}

const pad = (n) => String(n).padStart(2, '0');
function toJalali(gy, gm, gd) { return d2j(g2d(gy, gm, gd)); }
function toGregorian(jy, jm, jd) { return d2g(j2d(jy, jm, jd)); }
function jalaliMonthLength(jy, jm) {
  if (jm <= 6) return 31;
  if (jm <= 11) return 30;
  return jalCal(jy).leap === 0 ? 30 : 29;
}
function format(j) { return `${j.jy}-${pad(j.jm)}-${pad(j.jd)}`; }
function parse(s) {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s);
  if (!m) return null;
  const j = { jy: +m[1], jm: +m[2], jd: +m[3] };
  if (j.jm < 1 || j.jm > 12 || j.jd < 1 || j.jd > jalaliMonthLength(j.jy, j.jm)) return null;
  return j;
}
function addDays(s, n) { return format(d2j(j2d(...Object.values(parse(s))) + n)); }
// ۰ = شنبه ... ۶ = جمعه
function weekday(s) {
  const j = parse(s);
  const g = toGregorian(j.jy, j.jm, j.jd);
  const dow = new Date(Date.UTC(g.gy, g.gm - 1, g.gd)).getUTCDay(); // 0=Sun
  return (dow + 1) % 7;
}
function weekStart(s) { return addDays(s, -weekday(s)); }
function diffDays(a, b) { const pa = parse(a), pb = parse(b); return j2d(pb.jy, pb.jm, pb.jd) - j2d(pa.jy, pa.jm, pa.jd); }
function todayJalali() {
  const d = new Date();
  return format(toJalali(d.getFullYear(), d.getMonth() + 1, d.getDate()));
}
const MONTHS = ['فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور', 'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند'];
const WEEKDAYS = ['شنبه', 'یکشنبه', 'دوشنبه', 'سه‌شنبه', 'چهارشنبه', 'پنجشنبه', 'جمعه'];

const api = { toJalali, toGregorian, jalaliMonthLength, format, parse, addDays, diffDays, weekday, weekStart, todayJalali, MONTHS, WEEKDAYS };
if (typeof module !== 'undefined') module.exports = api;
if (typeof window !== 'undefined') window.Jalali = api;
