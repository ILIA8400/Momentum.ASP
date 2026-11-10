/* Momentum (ASP.NET Core) — تعاملات سمت کلاینت
   پورت همان منطق app.js نسخهٔ اصلی: تم، Toast، انتخابگر تاریخ شمسی،
   محاسبهٔ خودکار مدت/درصد در فرم، ذخیرهٔ خودکار یادداشت، هدف‌ها و نقشهٔ مطالعه */
(() => {
  'use strict';
  const J = window.Jalali;
  const $ = (sel, el = document) => el.querySelector(sel);
  const $$ = (sel, el = document) => [...el.querySelectorAll(sel)];
  const FA = '۰۱۲۳۴۵۶۷۸۹';
  const fa = (n) => String(n).replace(/\d/g, (d) => FA[d]);
  const en = (s) => String(s ?? '').replace(/[۰-۹]/g, (d) => FA.indexOf(d));

  // ---------- توکن ضدجعل برای فراخوانی‌های Ajax ----------
  const token = $('meta[name="request-token"]')?.content || '';

  async function api(method, url, body) {
    const res = await fetch(url, {
      method,
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
        'RequestVerificationToken': token
      },
      body: body ? new URLSearchParams(body).toString() : undefined
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) { toast(data.error || 'خطا', true); throw new Error(data.error); }
    return data;
  }

  // ---------- Toast ----------
  let toastTimer;
  function toast(msg, err = false) {
    const t = $('#toast');
    if (!t || !msg) return;
    t.innerHTML = (err ? ic('x') : ic('check')) + esc(msg);
    t.className = 'toast show' + (err ? ' err' : '');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => (t.className = 'toast'), 2600);
  }
  const toastEl = $('#toast');
  if (toastEl) {
    if (toastEl.dataset.err) toast(toastEl.dataset.err, true);
    else if (toastEl.dataset.msg) toast(toastEl.dataset.msg);
  }

  // ---------- آیکون‌ها (چند مورد لازم سمت کلاینت) ----------
  const ICONS = {
    check: '<path d="M20 6 9 17l-5-5"/>',
    x: '<path d="M18 6 6 18M6 6l12 12"/>',
    calendar: '<rect x="3" y="4" width="18" height="18" rx="2"/><path d="M8 2v4M16 2v4M3 10h18"/>',
    right: '<path d="m9 18 6-6-6-6"/>',
    left: '<path d="m15 18-6-6 6-6"/>',
  };
  function ic(name, cls = '') {
    return `<svg class="ic ${cls}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${ICONS[name] || ''}</svg>`;
  }
  const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  // ---------- تم ----------
  const THEMES = [['system', 'monitor'], ['light', 'sun'], ['dark', 'moon']];
  const THEME_TITLES = { system: 'خودکار (سیستم)', light: 'روشن', dark: 'تاریک' };
  function getTheme() { try { return localStorage.getItem('theme') || 'system'; } catch (e) { return 'system'; } }
  function applyTheme(t) {
    if (t === 'light' || t === 'dark') document.documentElement.dataset.theme = t;
    else delete document.documentElement.dataset.theme;
    try { localStorage.setItem('theme', t); } catch (e) { }
    const sel = $('#themeSel'); if (sel) sel.value = t;
  }
  function cycleTheme() {
    const i = THEMES.findIndex((x) => x[0] === getTheme());
    const next = THEMES[(i + 1) % THEMES.length][0];
    applyTheme(next);
    toast(`تم ${THEME_TITLES[next]}`);
  }
  applyTheme(getTheme());
  $('#themeBtn')?.addEventListener('click', cycleTheme);
  $('#themeSel')?.addEventListener('change', (e) => applyTheme(e.target.value));

  // ---------- جملهٔ روز ----------
  const quoteEl = $('#quoteText');
  if (quoteEl) {
    let offset = 0;
    let quotes = [];
    try { quotes = JSON.parse(quoteEl.dataset.quotes || '[]'); } catch (e) { }
    $('#nextQuote')?.addEventListener('click', () => {
      if (!quotes.length) return;
      offset = (offset + 1) % quotes.length;
      quoteEl.textContent = quotes[(quoteIndex() + offset) % quotes.length];
    });
    function quoteIndex() {
      const seed = J.diffDays('1400-01-01', J.todayJalali());
      return ((seed % quotes.length) + quotes.length) % quotes.length;
    }
  }

  // ---------- تیک «انجام شد» (روزانه و داشبورد) ----------
  $$('.js-toggle').forEach((chk) => {
    chk.addEventListener('change', async () => {
      const form = chk.closest('.js-toggle-form');
      if (!form) return;
      const id = form.querySelector('[name=id]').value;
      const done = form.querySelector('[name=done]').value === 'true';
      try {
        await api('POST', `/Day/Toggle`, { id: +id, done });
        location.reload();
      } catch (e) { /* toast قبلاً نمایش داده شد */ }
    });
  });

  // ---------- یادداشت روز (ذخیرهٔ خودکار) ----------
  const ta = $('#dayNote'), st = $('#noteStatus');
  if (ta && st) {
    let timer, last = ta.value;
    const save = async () => {
      if (ta.value === last) return;
      st.textContent = 'در حال ذخیره…';
      try {
        await api('POST', '/Day/SaveNote', { date: ta.dataset.date, text: ta.value });
        last = ta.value;
        st.textContent = 'ذخیره شد ✓';
        setTimeout(() => (st.textContent = ''), 1500);
      } catch (e) { st.textContent = 'ذخیره نشد!'; }
    };
    ta.addEventListener('input', () => { clearTimeout(timer); st.textContent = '…'; timer = setTimeout(save, 700); });
    ta.addEventListener('blur', () => { clearTimeout(timer); save(); });
  }

  // ---------- هدف‌ها (تبدیل ساعت به دقیقه و ذخیرهٔ خودکار) ----------
  $$('.js-goal').forEach((inp) => {
    inp.addEventListener('change', async () => {
      const minutes = Math.round((parseFloat(en(inp.value)) || 0) * 60);
      try {
        await api('POST', '/Goals/Set', {
          period: inp.dataset.period, key: inp.dataset.key,
          subjectId: +inp.dataset.goal, minutes
        });
        toast('هدف ذخیره شد');
        setTimeout(() => location.reload(), 500);
      } catch (e) { }
    });
  });

  // ---------- نقشهٔ مطالعه (تولتیپ + اندازه + کلیک) ----------
  const tip = $('#hmTip'), wrap = $('.hm-wrap');
  if (wrap && tip) {
    const fit = () => {
      const n = +wrap.dataset.weeks || 1;
      const w = wrap.clientWidth - 30;
      const size = Math.max(7, Math.min(18, Math.floor((w - (n - 1) * 3) / n)));
      wrap.style.setProperty('--hm', size + 'px');
    };
    fit();
    window.addEventListener('resize', fit, { passive: true });
    wrap.addEventListener('mouseover', (e) => {
      const c = e.target.closest('.hm-cell[data-tip]'); if (!c) return;
      tip.textContent = c.dataset.tip; tip.style.display = 'block';
      const r = c.getBoundingClientRect(), wr = wrap.getBoundingClientRect();
      tip.style.left = `${r.left - wr.left + r.width / 2}px`;
      tip.style.top = `${r.top - wr.top - 8}px`;
    });
    wrap.addEventListener('mouseout', (e) => { if (e.target.closest('.hm-cell')) tip.style.display = 'none'; });
    wrap.addEventListener('click', (e) => {
      const c = e.target.closest('.hm-cell[data-d]');
      if (c) location.href = `/Day?date=${c.dataset.d}`;
    });
  }

  // ---------- تأیید حذف‌ها ----------
  $$('.js-confirm').forEach((f) => f.addEventListener('submit', (e) => {
    if (!confirm(f.dataset.confirm || 'مطمئنی؟')) e.preventDefault();
  }));

  // ---------- شمارندهٔ ردیف‌های انتخاب‌شده در پیش‌نمایش اکسل ----------
  const impTable = $('.imp-table');
  if (impTable) {
    const refresh = () => {
      const n = $$('input[name=selected]:checked', impTable).length;
      const countEl = $('#impCount'); if (countEl) countEl.textContent = fa(n);
      const btn = $('#impCommit'); if (btn) btn.disabled = n === 0;
    };
    $$('input[name=selected]', impTable).forEach((c) => c.addEventListener('change', refresh));
    $('#impAll')?.addEventListener('change', (e) => {
      $$('input[name=selected]', impTable).forEach((c) => (c.checked = e.target.checked));
      refresh();
    });
    impTable.closest('form')?.addEventListener('submit', (e) => {
      if (!$$('input[name=selected]:checked', impTable).length) { e.preventDefault(); toast('حداقل یک ردیف انتخاب کن', true); }
    });
    refresh();
  }

  // ---------- فرم آیتم مطالعه: محاسبهٔ خودکار مدت و درصد ----------
  const form = $('#entryForm');
  if (form) {
    const f = (n) => form.elements[n];
    const num = (n) => +en(f(n)?.value) || 0;
    let manualTotal = !!f('Id')?.value && num('Minutes') > 0;
    const diff = (a, b) => {
      const m = (t) => +t.slice(0, 2) * 60 + +t.slice(3);
      let x = m(b) - m(a); if (x < 0) x += 1440; return x;
    };
    const hm = (min) => {
      min = min | 0;
      const h = Math.floor(min / 60), m = min % 60;
      if (!h) return `${fa(m)} دقیقه`;
      if (!m) return `${fa(h)} ساعت`;
      return `${fa(h)}:${fa(String(m).padStart(2, '0'))}`;
    };
    const pctOf = (c, w, t) => (!t || c + w === 0 ? null : Math.round(((3 * c - w) / (3 * t)) * 1000) / 10);
    const recalc = () => {
      const parts = num('StudyMin') + num('ReviewMin') + num('TestMin');
      const s = f('StartTime')?.value || '', e = f('EndTime')?.value || '';
      const range = s && e ? diff(s, e) : 0;
      if (!manualTotal) {
        if (parts) f('Minutes').value = parts;
        else if (range) f('Minutes').value = range;
      }
      const hint = [];
      if (parts) hint.push(`جزئیات: ${hm(parts)}`);
      if (range) hint.push(`بازه: ${hm(range)}`);
      const tot = num('Minutes');
      if (tot) hint.unshift(`= ${hm(tot)}`);
      const th = $('#totalHint'); if (th) th.textContent = hint.join(' • ');
      const p = pctOf(num('Correct'), num('Wrong'), num('Tests'));
      const ph = $('#pctHint');
      if (ph) {
        ph.textContent = p == null ? '—' : `${fa(p)}٪`;
        ph.style.color = p == null ? '' : p >= 50 ? 'var(--ok)' : p < 20 ? 'var(--danger)' : '';
      }
      const tests = num('Tests'), correct = num('Correct');
      const sp = tests > 0 ? Math.round((correct / tests) * 1000) / 10 : null;
      const sh = $('#pctSimpleHint');
      if (sh) {
        sh.textContent = sp == null ? '—' : `${fa(sp)}٪`;
        sh.style.color = sp == null ? '' : sp >= 50 ? 'var(--ok)' : sp < 20 ? 'var(--danger)' : '';
      }
    };
    ['StudyMin', 'ReviewMin', 'TestMin', 'StartTime', 'EndTime', 'Tests', 'Correct', 'Wrong']
      .forEach((n) => f(n)?.addEventListener('input', recalc));
    f('Minutes')?.addEventListener('input', () => { manualTotal = !!f('Minutes').value; recalc(); });
    recalc();
  }

  // ---------- انتخابگر تاریخ شمسی ----------
  let pickerEl = null;
  function closePicker() {
    pickerEl?.remove(); pickerEl = null;
    document.removeEventListener('mousedown', onDocClick);
  }
  function onDocClick(e) { if (pickerEl && !pickerEl.contains(e.target)) closePicker(); }

  async function openPicker(anchor, value, onPick) {
    closePicker();
    const cur = J.parse(value) || J.parse(J.todayJalali());
    pickerEl = document.createElement('div');
    pickerEl.className = 'dp';
    document.body.appendChild(pickerEl);
    const r = anchor.getBoundingClientRect();
    pickerEl.style.top = `${r.bottom + window.scrollY + 6}px`;
    pickerEl.style.left = `${Math.max(8, Math.min(r.left + window.scrollX, document.documentElement.clientWidth - 320))}px`;
    setTimeout(() => document.addEventListener('mousedown', onDocClick));

    const draw = async () => {
      const mk = `${cur.jy}-${String(cur.jm).padStart(2, '0')}`;
      const len = J.jalaliMonthLength(cur.jy, cur.jm);
      let marked = new Set();
      try {
        const res = await fetch(`/Stats/Marked?from=${mk}-01&to=${mk}-${String(len).padStart(2, '0')}`);
        marked = new Set(await res.json());
      } catch (e) { }
      const off = J.weekday(`${mk}-01`);
      let cells = J.WEEKDAYS.map((w) => `<div class="w">${w[0]}</div>`).join('');
      for (let i = 0; i < off; i++) cells += '<div></div>';
      for (let d = 1; d <= len; d++) {
        const ds = `${mk}-${String(d).padStart(2, '0')}`;
        cells += `<button data-d="${ds}" class="${ds === value ? 'sel' : ''} ${ds === J.todayJalali() ? 'tdy' : ''} ${marked.has(ds) ? 'has' : ''}">${fa(d)}</button>`;
      }
      pickerEl.innerHTML = `<div class="dp-head">
          <button class="btn icon" data-m="-1">${ic('right')}</button>
          <b>${J.MONTHS[cur.jm - 1]} ${fa(cur.jy)}</b>
          <button class="btn icon" data-m="1">${ic('left')}</button>
        </div>
        <div class="dp-grid">${cells}</div>
        <div class="dp-foot"><button class="btn sm ghost" data-today>برو به امروز</button><button class="btn sm ghost" data-close>بستن</button></div>`;
      $$('[data-m]', pickerEl).forEach((b) => b.addEventListener('click', () => {
        cur.jm += +b.dataset.m;
        if (cur.jm > 12) { cur.jm = 1; cur.jy++; }
        if (cur.jm < 1) { cur.jm = 12; cur.jy--; }
        draw();
      }));
      $$('[data-d]', pickerEl).forEach((b) => b.addEventListener('click', () => { closePicker(); onPick(b.dataset.d); }));
      $('[data-today]', pickerEl).addEventListener('click', () => { closePicker(); onPick(J.todayJalali()); });
      $('[data-close]', pickerEl).addEventListener('click', closePicker);
    };
    draw();
  }

  // دکمه‌های انتخاب تاریخ: جابه‌جایی در همان صفحه با پارامتر date
  $$('.js-pick').forEach((btn) => {
    btn.addEventListener('click', () => {
      openPicker(btn, btn.dataset.date || J.todayJalali(), (d) => {
        const target = btn.dataset.target;
        if (target === 'from' || target === 'to') {
          const url = new URL(location.href);
          url.searchParams.set('kind', 'custom');
          let from = url.searchParams.get('from') || J.todayJalali();
          let to = url.searchParams.get('to') || J.todayJalali();
          if (target === 'from') from = d; else to = d;
          if (from > to) [from, to] = [to, from];
          url.searchParams.set('from', from);
          url.searchParams.set('to', to);
          location.href = url.toString();
        } else if (target === 'examDate') {
          const input = document.getElementById('examDate');
          if (input) { input.value = d; input.form.submit(); }
        } else {
          const url = new URL(location.href);
          url.searchParams.set('date', d);
          location.href = url.toString();
        }
      });
    });
  });

  // تغییر تاریخ کنکور از داشبورد
  $('#examDate')?.addEventListener('click', (e) => {
    e.preventDefault();
    openPicker(e.currentTarget, e.currentTarget.dataset.date || J.todayJalali(), async (d) => {
      try {
        await api('POST', '/Home/SetExamDate', { date: d });
        toast('تاریخ کنکور ذخیره شد');
        setTimeout(() => location.reload(), 500);
      } catch (err) { }
    });
  });

  // یادداشت روز — ذخیرهٔ خودکار (debounce روی تایپ + blur)
  const dayNote = $('#dayNote');
  if (dayNote) {
    let noteTimer, lastSaved = dayNote.value;
    const saveNote = async () => {
      if (dayNote.value === lastSaved) return;
      const val = dayNote.value;
      try {
        await api('POST', '/Day/SaveNote', { date: dayNote.dataset.date, text: val });
        lastSaved = val;
        const hint = $('#noteHint');
        if (hint) { hint.textContent = 'ذخیره شد ✓'; setTimeout(() => { hint.textContent = 'خودکار ذخیره می‌شود'; }, 1500); }
      } catch (err) { }
    };
    dayNote.addEventListener('input', () => { clearTimeout(noteTimer); noteTimer = setTimeout(saveNote, 900); });
    dayNote.addEventListener('blur', () => { clearTimeout(noteTimer); saveNote(); });
  }
})();
