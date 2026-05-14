'use strict';

// ── API ─────────────────────────────────────────────────────────────
const API_BASE = document.querySelector('meta[name="api-base"]')?.content || '';

// ── State ───────────────────────────────────────────────────────────
let currentUser = null;
let authToken = null;
let refreshToken = null;
let enrolledFaces = [];
let allUsers = [];
let allDepts = [];
let rosterWeekStart  = getMondayOf(new Date());
let myShiftWeekStart = getMondayOf(new Date());
let empCurrentPage = 1;
let empTotalPages  = 1;
const EMP_PAGE_SIZE = 50;
let currentPage = null;

// Camera streams (sadece yüz kaydı için — devam takip artık kiosk üzerinden)
let enrStream = null;
let enrInterval = null;
let modelsLoaded = false;

// ── ICONS (Lucide-style) ────────────────────────────────────────────
const ICONS = {
  dashboard:  '<svg viewBox="0 0 20 20" fill="none"><rect x="3" y="3" width="6" height="8" rx="1.5" stroke="currentColor" stroke-width="1.6"/><rect x="3" y="13" width="6" height="4" rx="1.5" stroke="currentColor" stroke-width="1.6"/><rect x="11" y="3" width="6" height="4" rx="1.5" stroke="currentColor" stroke-width="1.6"/><rect x="11" y="9" width="6" height="8" rx="1.5" stroke="currentColor" stroke-width="1.6"/></svg>',
  users:      '<svg viewBox="0 0 20 20" fill="none"><circle cx="8" cy="7" r="3" stroke="currentColor" stroke-width="1.6"/><path d="M2 17a6 6 0 0112 0M14 5.5a2.5 2.5 0 010 5M18 16.5a4.5 4.5 0 00-3-4.24" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>',
  calendar:   '<svg viewBox="0 0 20 20" fill="none"><rect x="3" y="4" width="14" height="13" rx="2" stroke="currentColor" stroke-width="1.6"/><path d="M3 8h14M7 2v3M13 2v3" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>',
  check:      '<svg viewBox="0 0 20 20" fill="none"><path d="M4 10l4 4 8-8" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  clipboard:  '<svg viewBox="0 0 20 20" fill="none"><rect x="4" y="3" width="12" height="15" rx="1.5" stroke="currentColor" stroke-width="1.6"/><path d="M7 2h6v3H7zM7 9h6M7 12h6M7 15h4" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>',
  scan:       '<svg viewBox="0 0 20 20" fill="none"><path d="M3 7V5a2 2 0 012-2h2M13 3h2a2 2 0 012 2v2M17 13v2a2 2 0 01-2 2h-2M7 17H5a2 2 0 01-2-2v-2" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/><circle cx="10" cy="10" r="2.5" stroke="currentColor" stroke-width="1.6"/></svg>',
  chart:      '<svg viewBox="0 0 20 20" fill="none"><path d="M3 17h14M6 13v4M10 8v9M14 11v6" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>',
  building:   '<svg viewBox="0 0 20 20" fill="none"><rect x="3" y="3" width="14" height="14" rx="1.5" stroke="currentColor" stroke-width="1.6"/><path d="M7 7h2M7 10h2M7 13h2M11 7h2M11 10h2M11 13h2" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>',
  // Stat icons
  team:       '<svg viewBox="0 0 20 20" fill="none"><circle cx="7" cy="7" r="3" stroke="currentColor" stroke-width="1.8"/><path d="M2 17a5 5 0 0110 0M13 6a3 3 0 010 6M18 16a4 4 0 00-3-3.87" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>',
  checkin:    '<svg viewBox="0 0 20 20" fill="none"><path d="M10 2a8 8 0 100 16 8 8 0 000-16zM7 10l2 2 4-4" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>',
  palm:       '<svg viewBox="0 0 20 20" fill="none"><path d="M10 3v14M5 8c2-3 8-3 10 0M3 17h14" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>',
  cross:      '<svg viewBox="0 0 20 20" fill="none"><circle cx="10" cy="10" r="8" stroke="currentColor" stroke-width="1.8"/><path d="M7 7l6 6M13 7l-6 6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>',
  pending:    '<svg viewBox="0 0 20 20" fill="none"><circle cx="10" cy="10" r="8" stroke="currentColor" stroke-width="1.8"/><path d="M10 6v4l2.5 2.5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/></svg>',
  trend:      '<svg viewBox="0 0 20 20" fill="none"><path d="M3 14l4-4 3 3 7-7M13 6h4v4" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>'
};

// ── Utilities ───────────────────────────────────────────────────────
/**
 * XSS koruması — kullanıcı verisini innerHTML'e koyarken her zaman escape edin.
 * Kullanım: `<td>${esc(user.fullName)}</td>`
 */
function esc(s) {
  if (s == null) return '';
  return String(s)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}
/** URL attribute'larını sınırla (javascript: gibi şemaları engelle) */
function safeUrl(u) {
  if (typeof u !== 'string') return '';
  const s = u.trim().toLowerCase();
  if (s.startsWith('javascript:') || s.startsWith('data:text/html')) return '';
  return u;
}
function getMondayOf(d) {
  const dt = new Date(d);
  const day = dt.getDay();
  const diff = dt.getDate() - day + (day === 0 ? -6 : 1);
  dt.setDate(diff);
  dt.setHours(0,0,0,0);
  return dt;
}
function fmtDate(d) {
  return new Date(d).toLocaleDateString('tr-TR');
}
function fmtDateOnly(d) {
  if (!d) return '';
  const dt = new Date(d);
  return `${dt.getFullYear()}-${String(dt.getMonth()+1).padStart(2,'0')}-${String(dt.getDate()).padStart(2,'0')}`;
}
function fmtTime(d) {
  return new Date(d).toLocaleTimeString('tr-TR',{hour:'2-digit',minute:'2-digit'});
}
function avatar(name, photo, size=32) {
  if (photo) {
    const safePhoto = safeUrl(photo);
    if (safePhoto) return `<div class="av-init" style="width:${size}px;height:${size}px"><img src="${esc(safePhoto)}" alt="" style="width:100%;height:100%;object-fit:cover"></div>`;
  }
  const initials = esc((name||'?').split(' ').map(w=>w[0]).join('').slice(0,2).toUpperCase());
  const hue = [...(name||'')].reduce((a,c)=>a+c.charCodeAt(0),0) % 360;
  return `<div class="av-init" style="width:${size}px;height:${size}px;background:hsl(${hue},48%,52%);font-size:${Math.round(size*0.36)}px">${initials}</div>`;
}
function toast(msg, type='ok') {
  const el = document.createElement('div');
  el.className = `toast toast-${type}`;
  el.textContent = msg;
  document.getElementById('toast-container').appendChild(el);
  setTimeout(()=>{ el.style.opacity = '0'; setTimeout(()=>el.remove(), 300); }, 3200);
}

// ── Theme ───────────────────────────────────────────────────────────
function getStoredTheme() { return localStorage.getItem('sx_theme') || 'dark'; }
function applyTheme(theme) {
  document.documentElement.setAttribute('data-theme', theme);
  localStorage.setItem('sx_theme', theme);
  const lbl = document.getElementById('theme-label');
  if (lbl) lbl.textContent = theme === 'dark'
    ? (window.t ? t('menu.theme_dark') : 'Karanlık')
    : (window.t ? t('menu.theme_light') : 'Açık');
}

// Dil değiştiğinde tetiklenir (i18n.js'den çağrılır)
window.onLanguageChanged = function(lang) {
  // Aktif lang butonunu işaretle
  document.querySelectorAll('.lang-opt').forEach(b => {
    b.classList.toggle('active', b.dataset.lang === lang);
  });
  // Tema label'ını güncelle
  applyTheme(localStorage.getItem('sx_theme') || 'dark');
  // Topbar role'ünü güncelle
  if (currentUser) {
    const r = document.getElementById('topbar-role');
    if (r) r.textContent = currentUser.role === 'Admin' ? t('topbar.admin') : t('topbar.employee');
  }
  // Sidebar nav'i yeniden çiz
  if (currentUser) buildNav();
  // Sayfa başlığını güncelle
  if (currentPage) {
    const titleKey = PAGE_TITLE_KEYS[currentPage];
    const titleEl = document.getElementById('topbar-title');
    if (titleEl && titleKey) titleEl.textContent = t(titleKey);
  }
  // Aktif sayfayı yeniden yükle (dinamik metinler için)
  if (currentPage) showPage(currentPage);
  // Notification'ları yeniden render et
  if (typeof renderNotifications === 'function') renderNotifications();
};

// Sayfa açıldığında aktif lang butonu işaretlensin
document.addEventListener('DOMContentLoaded', () => {
  const lang = (typeof getLang === 'function') ? getLang() : 'tr';
  document.querySelectorAll('.lang-opt').forEach(b => {
    b.classList.toggle('active', b.dataset.lang === lang);
  });
});
function cycleTheme() {
  const cur = getStoredTheme();
  applyTheme(cur === 'dark' ? 'light' : 'dark');
}
applyTheme(getStoredTheme());
document.addEventListener('click', e => {
  if (e.target.closest('#theme-toggle')) cycleTheme();
});

// ── User Dropdown ───────────────────────────────────────────────────
function closeUserMenu() { document.getElementById('user-menu')?.classList.remove('open'); }
document.addEventListener('click', e => {
  const menu = document.getElementById('user-menu');
  if (!menu) return;
  if (e.target.closest('#user-trigger')) {
    menu.classList.toggle('open');
  } else if (!e.target.closest('#user-dropdown')) {
    menu.classList.remove('open');
  }
});

// Mobile sidebar toggle
document.addEventListener('click', e => {
  if (e.target.closest('#mobile-menu-btn')) {
    document.getElementById('sidebar')?.classList.toggle('open');
  } else if (window.innerWidth <= 900 && !e.target.closest('#sidebar')) {
    document.getElementById('sidebar')?.classList.remove('open');
  }
});

// ── API ─────────────────────────────────────────────────────────────
async function api(method, path, body) {
  const opts = { method, headers: { 'Content-Type': 'application/json' } };
  if (authToken) opts.headers['Authorization'] = `Bearer ${authToken}`;
  if (body !== undefined) opts.body = JSON.stringify(body);

  let res = await fetch(API_BASE + path, opts);

  if (res.status === 401 && refreshToken) {
    const rf = await fetch(API_BASE + '/api/Auth/refresh', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken })
    });
    if (rf.ok) {
      const data = await rf.json();
      authToken = data.token; refreshToken = data.refreshToken;
      sessionStorage.setItem('sx_token', authToken);
      sessionStorage.setItem('sx_refresh', refreshToken);
      opts.headers['Authorization'] = `Bearer ${authToken}`;
      res = await fetch(API_BASE + path, opts);
    } else { logout(); return; }
  }

  if (res.status === 204) return null;
  const json = await res.json();
  if (!res.ok) throw new Error(json?.message || `HTTP ${res.status}`);
  return json;
}

// ── Auth ────────────────────────────────────────────────────────────
document.getElementById('login-btn').addEventListener('click', doLogin);
document.getElementById('login-pass').addEventListener('keydown', e => e.key==='Enter' && doLogin());
document.getElementById('login-email').addEventListener('keydown', e => e.key==='Enter' && doLogin());

// Şifreyi göster/gizle toggle
function togglePassword() {
  const inp = document.getElementById('login-pass');
  const eye = document.getElementById('eye-icon');
  if (!inp || !eye) return;
  const hidden = inp.type === 'password';
  inp.type = hidden ? 'text' : 'password';
  eye.innerHTML = hidden
    ? '<path d="M2 10s3-6 8-6c2 0 3.7.8 5 1.8M18 10s-3 6-8 6c-2 0-3.7-.8-5-1.8M3 3l14 14" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/>'
    : '<path d="M2 10s3-6 8-6 8 6 8 6-3 6-8 6-8-6-8-6z" stroke="currentColor" stroke-width="1.6"/><circle cx="10" cy="10" r="2.5" stroke="currentColor" stroke-width="1.6"/>';
}

async function doLogin() {
  const email = document.getElementById('login-email').value.trim();
  const pass  = document.getElementById('login-pass').value;
  const errEl = document.getElementById('login-error');
  errEl.classList.add('hidden');
  if (!email || !pass) { errEl.textContent = t('login.err_credentials'); errEl.classList.remove('hidden'); return; }
  try {
    const data = await api('POST', '/api/Auth/login', { email, password: pass });
    authToken    = data.token;
    refreshToken = data.refreshToken;
    currentUser  = data;
    sessionStorage.setItem('sx_token',   authToken);
    sessionStorage.setItem('sx_refresh', refreshToken);
    sessionStorage.setItem('sx_user',    JSON.stringify(data));
    startApp();
  } catch(e) {
    errEl.textContent = e.message || t('toast.error');
    errEl.classList.remove('hidden');
  }
}

function logout() {
  if (refreshToken) api('POST','/api/Auth/revoke',{refreshToken}).catch(()=>{});
  authToken = refreshToken = currentUser = null;
  sessionStorage.clear();
  stopAllCams();
  closeUserMenu();
  document.getElementById('app').classList.add('hidden');
  document.getElementById('login-screen').classList.remove('hidden');
}

function stopAllCams() {
  enrStream?.getTracks().forEach(t => t.stop());
  if (enrInterval) clearInterval(enrInterval);
  enrStream = null; enrInterval = null;
}

// ── App start ───────────────────────────────────────────────────────
async function startApp() {
  document.getElementById('login-screen').classList.add('hidden');
  document.getElementById('app').classList.remove('hidden');
  buildNav();
  updateTopbarUser();
  await Promise.all([loadAllUsers(), loadDepts()]);
  const isAdmin = currentUser.role === 'Admin';
  navTo(isAdmin ? 'dashboard' : 'my-dashboard');
  // Bildirimler — hem admin hem personel için açık (içerik role bazlı)
  startNotificationPolling();
}

// ── Notification Center ────────────────────────────────────────────
let notifInterval = null;
let lastNotifData = { pending: [], lates: [] };

function startNotificationPolling() {
  refreshNotifications();
  if (notifInterval) clearInterval(notifInterval);
  notifInterval = setInterval(refreshNotifications, 15000); // 15 saniye — hızlı bildirim
}

async function refreshNotifications() {
  try {
    const isAdmin = currentUser?.role === 'Admin';
    if (isAdmin) {
      const [pending, today, swapPending] = await Promise.all([
        api('GET', '/api/Leaves?status=Pending').catch(()=>[]),
        api('GET', '/api/Attendance/today').catch(()=>[]),
        api('GET', '/api/ShiftSwap?status=AcceptedByTarget').catch(()=>[])
      ]);
      const lates = (today || []).filter(a => a.isLateArrival);
      lastNotifData = { kind: 'admin', pending: pending || [], lates, swapPending: swapPending || [] };
    } else {
      // Personel için: kendi izin durumum + yaklaşan vardiyalar + swap durumu
      const today = new Date();
      const tomorrow = new Date(); tomorrow.setDate(today.getDate()+1);
      const week     = new Date(); week.setDate(today.getDate()+7);
      const [myLeaves, myShifts, mySwapOut, mySwapIn] = await Promise.all([
        api('GET', '/api/Leaves/my').catch(()=>[]),
        api('GET', `/api/Shifts/my?from=${fmtDateOnly(today)}&to=${fmtDateOnly(week)}`).catch(()=>[]),
        api('GET', '/api/ShiftSwap/my-outgoing').catch(()=>[]),
        api('GET', '/api/ShiftSwap/my-incoming').catch(()=>[])
      ]);
      // Son 7 günde işlem gören kendi izinlerim
      const sevenDays = 7*24*60*60*1000;
      const recentReviewed = (myLeaves || []).filter(l => {
        if (l.status === 'Pending') return false;
        const created = new Date(l.createdAt);
        return (Date.now() - created.getTime()) < sevenDays;
      });
      // Bana yöneltilen + bekleyen swap talepleri
      const incomingPending = (mySwapIn || []).filter(s => s.status === 'Pending');
      // Kendi swap'lerimin admin tarafından sonuçlananları (son 7 gün)
      const myResolvedSwaps = (mySwapOut || []).filter(s => {
        if (!['ApprovedByAdmin','RejectedByAdmin','AcceptedByTarget'].includes(s.status)) return false;
        const ref = s.reviewedAt || s.respondedAt || s.createdAt;
        return (Date.now() - new Date(ref).getTime()) < sevenDays;
      });
      // Yarın için vardiyam (eğer varsa) ve bugünkü vardiyam
      const upcomingShifts = (myShifts || []).filter(s => {
        const d = new Date(s.date);
        const todayStr = fmtDateOnly(today);
        const tmrStr   = fmtDateOnly(tomorrow);
        return fmtDateOnly(d) === todayStr || fmtDateOnly(d) === tmrStr;
      });
      lastNotifData = { kind: 'employee', myLeaves: recentReviewed, upcomingShifts, incomingPending, myResolvedSwaps };
    }
    renderNotifications();
  } catch(_) { /* sessiz */ }
}

function renderNotifications() {
  const d = lastNotifData;
  const badge = document.getElementById('notif-badge');
  const list  = document.getElementById('notif-list');
  const items = [];

  if (d.kind === 'admin') {
    (d.pending || []).forEach(l => items.push({
      icon: '📋',
      title: t('notif.leave_pending', { name: l.userFullName }),
      body: `${leaveTypeI18n(l.leaveType)} · ${l.totalDays} ${t('pv.days')} · ${fmtDate(l.startDate)}`,
      onclick: `navTo('leaves');closeNotifMenu()`
    }));
    (d.lates || []).forEach(a => items.push({
      icon: '⏰',
      title: t('notif.late', { name: a.userFullName }),
      body: `${fmtTime(a.checkIn)} — ${t('badge.late_min', { m: a.lateMinutes })}`,
      onclick: `navTo('attendance');closeNotifMenu()`
    }));
    (d.swapPending || []).forEach(s => items.push({
      icon: '🔄',
      title: t('notif.swap_admin', { name: s.requesterName }),
      body: `${fmtDate(s.requesterDate)} · ${shiftNameById(s.requesterShiftId, s.requesterShiftName)}`,
      onclick: `navTo('swap-admin');closeNotifMenu()`
    }));
  } else if (d.kind === 'employee') {
    (d.myLeaves || []).forEach(l => {
      const icon = l.status === 'Approved' ? '✅' : '❌';
      items.push({
        icon,
        title: l.status === 'Approved' ? t('notif.leave_approved') : t('notif.leave_rejected'),
        body: `${leaveTypeI18n(l.leaveType)} · ${fmtDate(l.startDate)} - ${fmtDate(l.endDate)}`,
        onclick: `navTo('my-leaves');closeNotifMenu()`
      });
    });
    (d.upcomingShifts || []).forEach(s => {
      const d2 = new Date(s.date);
      const todayStr = fmtDateOnly(new Date());
      const label = fmtDateOnly(d2) === todayStr ? t('mydash.days_today') : t('mydash.days_tomorrow');
      items.push({
        icon: '⏰',
        title: t('notif.shift_upcoming', { label, shift: shiftNameById(s.shiftId, s.shiftName) }),
        body: `${s.startTime} – ${s.endTime}`,
        onclick: `navTo('my-shifts');closeNotifMenu()`
      });
    });
    (d.incomingPending || []).forEach(s => items.push({
      icon: '🔄',
      title: t('notif.swap_incoming', { name: s.requesterName }),
      body: `${fmtDate(s.requesterDate)} · ${shiftNameById(s.requesterShiftId, s.requesterShiftName)}`,
      onclick: `navTo('my-swaps');closeNotifMenu()`
    }));
    (d.myResolvedSwaps || []).forEach(s => {
      const icon = s.status === 'ApprovedByAdmin' ? '✅' : (s.status === 'AcceptedByTarget' ? '⏳' : '❌');
      const titleKey = s.status === 'ApprovedByAdmin' ? 'notif.swap_approved'
                     : s.status === 'AcceptedByTarget' ? 'notif.swap_my_accepted'
                     : 'notif.swap_rejected';
      items.push({
        icon,
        title: t(titleKey),
        body: `${fmtDate(s.requesterDate)} · ${shiftNameById(s.requesterShiftId, s.requesterShiftName)}`,
        onclick: `navTo('my-swaps');closeNotifMenu()`
      });
    });
  }

  const total = items.length;
  badge.textContent = total > 9 ? '9+' : String(total);
  badge.classList.toggle('hidden', total === 0);
  document.getElementById('notif-count-text').textContent =
    total === 0 ? t('menu.no_notifs') : t('menu.notif_count', { count: total });

  list.innerHTML = items.length ? items.map(it => `
    <button class="notif-item" onclick="${it.onclick}">
      <span class="notif-icon">${it.icon}</span>
      <div class="notif-content">
        <div class="notif-title">${esc(it.title)}</div>
        <div class="notif-body">${esc(it.body)}</div>
      </div>
    </button>
  `).join('') : `<div class="empty">${t('menu.no_notifs')} 🎉</div>`;
}

function closeNotifMenu() { document.getElementById('notif-menu')?.classList.remove('open'); }
document.addEventListener('click', e => {
  const menu = document.getElementById('notif-menu');
  if (!menu) return;
  if (e.target.closest('#notif-trigger')) {
    menu.classList.toggle('open');
  } else if (!e.target.closest('#notif-dropdown')) {
    menu.classList.remove('open');
  }
});

function tryRestoreSession() {
  const t = sessionStorage.getItem('sx_token');
  const r = sessionStorage.getItem('sx_refresh');
  const u = sessionStorage.getItem('sx_user');
  if (t && r && u) {
    authToken = t; refreshToken = r; currentUser = JSON.parse(u);
    startApp();
  }
}

// ── Navigation ──────────────────────────────────────────────────────
const NAV_ADMIN = [
  { section: 'nav.general' },
  { id: 'dashboard',      key: 'nav.dashboard',      icon: ICONS.dashboard },
  { section: 'nav.personnel' },
  { id: 'employees',      key: 'nav.employees',      icon: ICONS.users },
  { id: 'departments',    key: 'nav.departments',    icon: ICONS.building },
  { section: 'nav.operations' },
  { id: 'roster',         key: 'nav.roster',         icon: ICONS.calendar },
  { id: 'attendance',     key: 'nav.attendance',     icon: ICONS.check },
  { id: 'leaves',         key: 'nav.leaves',         icon: ICONS.clipboard },
  { id: 'overtime-admin', key: 'nav.overtime_admin', icon: ICONS.trend },
  { id: 'swap-admin',     key: 'nav.swap_admin',     icon: ICONS.scan },
  { section: 'nav.reports' },
  { id: 'enroll',         key: 'nav.enroll',         icon: ICONS.scan },
  { id: 'monthly',        key: 'nav.monthly',        icon: ICONS.chart },
];

const NAV_EMP = [
  { section: 'nav.personal' },
  { id: 'my-dashboard',  key: 'nav.my_dashboard',  icon: ICONS.dashboard },
  { id: 'my-shifts',     key: 'nav.my_shifts',     icon: ICONS.calendar },
  { id: 'my-attendance', key: 'nav.my_attendance', icon: ICONS.check },
  { id: 'my-leaves',     key: 'nav.my_leaves',     icon: ICONS.clipboard },
  { id: 'my-overtime',   key: 'nav.my_overtime',   icon: ICONS.trend },
  { id: 'my-swaps',      key: 'nav.my_swaps',      icon: ICONS.scan },
  { id: 'my-monthly',    key: 'nav.my_monthly',    icon: ICONS.chart },
  { section: 'nav.team' },
  { id: 'roster',        key: 'nav.weekly_plan',   icon: ICONS.calendar },
];

// ── Vardiya kategorileri (Shift Id'ye göre) ─────────────────────────
// 1-3: Vardiyalar | 4-5: Tatil/İzin | 6: Part Time (Vardiya) | 7-9: Fazla Mesai
const SHIFT_TYPES = [
  { id:1, nameKey:'shift.name.morning',     cat:'shift',    color:'#f59e0b', startTime:'08:00', endTime:'16:00' },
  { id:2, nameKey:'shift.name.afternoon',   cat:'shift',    color:'#4f6ef7', startTime:'14:00', endTime:'22:00' },
  { id:3, nameKey:'shift.name.night',       cat:'shift',    color:'#a78bfa', startTime:'22:00', endTime:'06:00' },
  { id:6, nameKey:'shift.name.parttime',    cat:'shift',    color:'#14b8a6', startTime:'08:00', endTime:'12:00' },
  { id:4, nameKey:'shift.name.holiday',     cat:'leave',    color:'#ef4444', startTime:'—',     endTime:'—'     },
  { id:5, nameKey:'shift.name.leaved',      cat:'leave',    color:'#22c55e', startTime:'—',     endTime:'—'     },
  { id:7, nameKey:'shift.name.morningOT',   cat:'overtime', color:'#f97316', startTime:'16:00', endTime:'18:00' },
  { id:8, nameKey:'shift.name.afternoonOT', cat:'overtime', color:'#6366f1', startTime:'22:00', endTime:'00:00' },
  { id:9, nameKey:'shift.name.nightOT',     cat:'overtime', color:'#ec4899', startTime:'06:00', endTime:'08:00' },
];
function shiftCatLabel(cat) { return t('shift.cat.' + cat); }
function shiftTypeName(shiftType) { return shiftType.nameKey ? t(shiftType.nameKey) : (shiftType.name || ''); }
function getShiftCategory(shiftId) {
  const x = SHIFT_TYPES.find(s => s.id === shiftId);
  return x?.cat || 'shift';
}

// Server'dan gelen shift verisini (shiftId ile) i18n'li isme çevirir
function shiftNameById(shiftId, fallback) {
  const st = SHIFT_TYPES.find(x => x.id === shiftId);
  return st?.nameKey ? t(st.nameKey) : (fallback || '—');
}

// Gün isimleri (Monday-Sunday = 0-6, mantıken Pazartesi başlangıç)
const DAY_KEYS_FULL  = ['day.full.mon','day.full.tue','day.full.wed','day.full.thu','day.full.fri','day.full.sat','day.full.sun'];
const DAY_KEYS_SHORT = ['day.mon','day.tue','day.wed','day.thu','day.fri','day.sat','day.sun'];
function dayFullName(monBasedIdx) { return t(DAY_KEYS_FULL[monBasedIdx] || 'day.full.mon'); }
// JS Date.getDay(): 0=Pazar..6=Cumartesi → biz Pazartesi=0 istiyoruz
function dayFullFromDate(d) { return dayFullName((d.getDay() + 6) % 7); }

// Server'ın leaveType alanı (Yıllık/Sağlık/Mazeret) için lookup
function leaveTypeI18n(srvType) {
  const map = { 'Yıllık': 'leave.type_a', 'Sağlık': 'leave.type_b', 'Mazeret': 'leave.type_c' };
  return map[srvType] ? t(map[srvType]) : (srvType || '');
}

function buildNav() {
  const items = currentUser.role === 'Admin' ? NAV_ADMIN : NAV_EMP;
  const html = items.map(it => {
    if (it.section) return `<div class="nav-section">${esc(t(it.section))}</div>`;
    return `<a class="nav-link" data-page="${it.id}" onclick="navTo('${it.id}')">${it.icon}<span>${esc(t(it.key))}</span></a>`;
  }).join('');
  document.getElementById('sidebar-nav').innerHTML = html;
}

const PAGE_TITLE_KEYS = {
  'dashboard': 'nav.dashboard',
  'employees': 'nav.employees',
  'departments': 'nav.departments',
  'roster': 'nav.roster',
  'attendance': 'nav.attendance',
  'leaves': 'nav.leaves',
  'overtime-admin': 'nav.overtime_admin',
  'swap-admin': 'nav.swap_admin',
  'enroll': 'nav.enroll',
  'monthly': 'nav.monthly',
  'my-dashboard': 'nav.my_dashboard',
  'my-shifts': 'nav.my_shifts',
  'my-attendance': 'nav.my_attendance',
  'my-leaves': 'nav.my_leaves',
  'my-overtime': 'nav.my_overtime',
  'my-swaps': 'nav.my_swaps',
  'my-monthly': 'nav.my_monthly',
  'profile': 'menu.profile'
};

function navTo(id) { showPage(id); }

function showPage(id) {
  document.querySelectorAll('.page').forEach(p => p.classList.add('hidden'));
  const el = document.getElementById(`page-${id}`);
  if (el) el.classList.remove('hidden');
  document.querySelectorAll('.nav-link').forEach(a =>
    a.classList.toggle('active', a.dataset.page === id));
  const titleKey = PAGE_TITLE_KEYS[id];
  document.getElementById('topbar-title').textContent = titleKey ? t(titleKey) : '';
  currentPage = id;
  if (window.innerWidth <= 900) document.getElementById('sidebar')?.classList.remove('open');

  switch(id) {
    case 'dashboard':   loadDashboard(); break;
    case 'employees':   loadEmployees(); break;
    case 'roster':      loadRoster();    break;
    case 'attendance':  loadAttendance();break;
    case 'leaves':      loadLeaves();    break;
    case 'my-leaves':   loadMyLeaves();  break;
    case 'my-shifts':   loadMyShifts();  break;
    case 'my-attendance': loadMyAttendance(); break;
    case 'profile':     loadProfile();   break;
    case 'enroll':      loadEnrList();   break;
    case 'monthly':     initMonthly();   break;
    case 'departments': loadDepts().then(renderDepts); break;
    case 'my-dashboard':    loadMyDashboard(); break;
    case 'my-monthly':      loadMyMonthly(); break;
    case 'my-overtime':     loadMyOvertime(); break;
    case 'my-swaps':        loadMySwaps('outgoing'); break;
    case 'overtime-admin':  loadAdminOvertime(); break;
    case 'swap-admin':      loadAdminSwaps(); break;
  }
}

// ── Topbar User ─────────────────────────────────────────────────────
function updateTopbarUser() {
  const u = currentUser;
  document.getElementById('topbar-avatar').innerHTML = avatar(u.fullName, u.photoBase64, 32);
  document.getElementById('topbar-name').textContent = u.fullName;
  document.getElementById('topbar-role').textContent = u.role === 'Admin' ? t('topbar.admin') : t('topbar.employee');
  document.getElementById('dropdown-header').innerHTML =
    `<strong>${u.fullName}</strong>${u.email}`;
}

// ── Dashboard ───────────────────────────────────────────────────────
async function loadDashboard() {
  // Hero
  const firstName = currentUser.fullName.split(' ')[0];
  const hour = new Date().getHours();
  const greetKey = hour < 6 ? 'dash.good_night' : hour < 12 ? 'dash.good_morning' : hour < 18 ? 'dash.good_day' : 'dash.good_evening';
  document.getElementById('hero-title').textContent = `${t(greetKey)}, ${firstName} 👋`;
  document.getElementById('hero-sub').textContent = t('dash.sub');
  const localeStr = getLang() === 'en' ? 'en-US' : 'tr-TR';
  document.getElementById('hero-date').textContent =
    new Date().toLocaleDateString(localeStr, { day:'numeric', month:'long', year:'numeric', weekday:'long' });
  startHeroClock();

  try {
    const [stats, today, pending] = await Promise.all([
      api('GET','/api/Attendance/dashboard'),
      api('GET','/api/Attendance/today'),
      api('GET','/api/Leaves?status=Pending')
    ]);

    // Stat kartlar — daha zengin görsel
    const sg = document.getElementById('stat-grid');
    sg.innerHTML = [
      {label:t('dash.total_emp'),       val: stats.totalActiveEmployees, icon: ICONS.team,    cls:'icon-blue',   hint:t('dash.hint_active')},
      {label:t('dash.present_today'),   val: stats.presentToday,         icon: ICONS.checkin, cls:'icon-green',  hint:t('dash.hint_working')},
      {label:t('dash.on_leave'),        val: stats.onLeaveToday,         icon: ICONS.palm,    cls:'icon-amber',  hint:t('dash.hint_approved')},
      {label:t('dash.absent'),          val: stats.absentToday,          icon: ICONS.cross,   cls:'icon-red',    hint:t('dash.hint_unreported')},
      {label:t('dash.pending_leaves'),  val: stats.pendingLeaveRequests, icon: ICONS.pending, cls:'icon-cyan',   hint:t('dash.hint_waiting')},
      {label:t('dash.attendance_rate'), val: stats.attendanceRate+'%',   icon: ICONS.trend,   cls:'icon-violet', hint:t('dash.hint_today')},
    ].map(s => `
      <div class="stat-card stat-card-rich">
        <div class="stat-icon ${s.cls}">${s.icon}</div>
        <div class="stat-content">
          <div class="stat-val">${s.val}</div>
          <div class="stat-lbl">${s.label}</div>
        </div>
        <div class="stat-hint">${s.hint}</div>
      </div>
    `).join('');

    // Bugünkü aktivite (ilk 8)
    const pl = document.getElementById('present-list');
    document.getElementById('present-count').textContent = today.length;
    pl.innerHTML = today.length ? today.slice(0,6).map(a => `
      <div class="present-row">
        ${avatar(a.userFullName, a.userPhoto, 36)}
        <div class="pr-info">
          <strong>${esc(a.userFullName)}</strong>
          <small>→ ${fmtTime(a.checkIn)}${a.checkOut ? ' · ← ' + fmtTime(a.checkOut) : ''}</small>
        </div>
        ${a.checkOut ? `<span class="badge badge-emp">${t('att.completed')}</span>` : `<span class="badge badge-on">${t('att.active')}</span>`}
      </div>`).join('')
    : `<div class="empty">${t('dash.no_attendance')}</div>`;

    // Geç kalanlar
    const lates = today.filter(a => a.isLateArrival);
    document.getElementById('late-count').textContent = lates.length;
    document.getElementById('late-list').innerHTML = lates.length ? lates.slice(0,6).map(a => `
      <div class="present-row">
        ${avatar(a.userFullName, a.userPhoto, 36)}
        <div class="pr-info">
          <strong>${esc(a.userFullName)}</strong>
          <small>${fmtTime(a.checkIn)} · <span class="late-mins">+${a.lateMinutes} ${t('common.minutes')}</span></small>
        </div>
        <span class="badge badge-warn">${getLang()==='en'?'Late':'Geç'}</span>
      </div>`).join('')
    : `<div class="empty">${t('dash.no_late')}</div>`;

    // Bekleyen izinler
    const pd = document.getElementById('pending-leaves-dash');
    pd.innerHTML = pending.length ? pending.slice(0,5).map(l => `
      <div class="leave-row">
        ${avatar(l.userFullName, null, 32)}
        <div style="flex:1;min-width:0">
          <strong>${esc(l.userFullName)}</strong>
          <span style="display:block;font-size:12px;color:var(--text-3)">${esc(l.leaveType)} · ${l.totalDays} ${t('common.day')}</span>
        </div>
        <span class="badge badge-warn">${t('leave.status_pending')}</span>
      </div>`).join('')
    : `<div class="empty">${t('dash.no_pending')}</div>`;

    // Yaklaşan izinler (7 gün)
    const all = await api('GET','/api/Leaves?status=Approved').catch(()=>[]);
    const upcoming = (all || []).filter(l => {
      const start = new Date(l.startDate);
      const today = new Date(); today.setHours(0,0,0,0);
      const week = new Date(); week.setDate(week.getDate()+7);
      return start >= today && start <= week;
    });
    document.getElementById('upcoming-leaves').innerHTML = upcoming.length ? upcoming.slice(0,6).map(l => {
      const days = Math.ceil((new Date(l.startDate) - new Date()) / (1000*60*60*24));
      const dayLabel = days <= 0 ? t('common.today')
                     : (getLang()==='en' ? `in ${days} days` : `${days} gün sonra`);
      return `<div class="upcoming-row">
        ${avatar(l.userFullName, null, 32)}
        <div style="flex:1;min-width:0">
          <strong>${esc(l.userFullName)}</strong>
          <span style="display:block;font-size:12px;color:var(--text-3)">${esc(l.leaveType)} · ${fmtDate(l.startDate)} - ${fmtDate(l.endDate)}</span>
        </div>
        <span class="badge badge-info">${dayLabel}</span>
      </div>`;
    }).join('') : `<div class="empty">${t('dash.no_upcoming')}</div>`;

    // Bu hafta vardiya dağılımı
    await renderShiftDistribution();
  } catch(e) { toast(e.message,'err'); }
}

let heroClockInterval = null;
function startHeroClock() {
  if (heroClockInterval) { clearInterval(heroClockInterval); heroClockInterval = null; }
  const el = document.getElementById('hero-clock');
  if (el) {
    const locale = getLang()==='en' ? 'en-US' : 'tr-TR';
    el.textContent = new Date().toLocaleTimeString(locale, { hour:'2-digit', minute:'2-digit' });
  }
}

async function renderShiftDistribution() {
  const ws = getMondayOf(new Date());
  try {
    const assignments = await api('GET', `/api/Shifts/weekly?weekStart=${fmtDateOnly(ws)}`);
    const labelShift = t('dash.shift_regular'), labelLeave = t('dash.shift_leave'), labelOT = t('dash.shift_overtime');
    const buckets = { [labelShift]: 0, [labelLeave]: 0, [labelOT]: 0 };
    const colors = { [labelShift]: '#4f6ef7', [labelLeave]: '#ef4444', [labelOT]: '#f97316' };
    (assignments || []).forEach(a => {
      const cat = getShiftCategory(a.shiftId);
      if (cat === 'shift')        buckets[labelShift]++;
      else if (cat === 'leave')   buckets[labelLeave]++;
      else if (cat === 'overtime') buckets[labelOT]++;
    });
    const total = Object.values(buckets).reduce((a,b)=>a+b,0);
    const el = document.getElementById('shift-distribution');
    if (!total) { el.innerHTML = `<div class="empty">${t('dash.no_dist')}</div>`; return; }
    el.innerHTML = Object.entries(buckets).map(([name, count]) => {
      const pct = Math.round((count/total)*100);
      return `<div class="dist-row">
        <div class="dist-label"><span class="dist-dot" style="background:${colors[name]}"></span>${name}</div>
        <div class="dist-bar"><div class="dist-fill" style="width:${pct}%;background:${colors[name]}"></div></div>
        <div class="dist-val"><strong>${count}</strong><small>${pct}%</small></div>
      </div>`;
    }).join('');
  } catch(_) {
    document.getElementById('shift-distribution').innerHTML = '<div class="empty">Yüklenemedi.</div>';
  }
}

// ── Personel ────────────────────────────────────────────────────────
async function loadAllUsers() {
  try {
    if (currentUser?.role === 'Admin') {
      const res = await api('GET', `/api/Users?page=1&pageSize=200`);
      allUsers = res.items || [];
    } else {
      // Personel kullanıcılar için: backend admin-only endpoint'i çağrılamaz.
      // Sadece haftalık vardiya planından isim/foto'yu çıkararak takım listesi oluşturuyoruz.
      try {
        const ws = getMondayOf(new Date());
        const weekly = await api('GET', `/api/Shifts/weekly?weekStart=${fmtDateOnly(ws)}`);
        const map = {};
        (weekly || []).forEach(a => {
          if (!map[a.userId]) map[a.userId] = {
            id: a.userId,
            fullName: a.userFullName,
            photoBase64: a.userPhoto,
            departmentName: a.departmentName
          };
        });
        // Kendisi listede yoksa ekle
        if (currentUser && !map[currentUser.userId]) {
          map[currentUser.userId] = {
            id: currentUser.userId,
            fullName: currentUser.fullName,
            photoBase64: currentUser.photoBase64,
            departmentName: ''
          };
        }
        allUsers = Object.values(map).sort((a,b)=>a.fullName.localeCompare(b.fullName,'tr'));
      } catch(_) {
        allUsers = currentUser ? [{
          id: currentUser.userId, fullName: currentUser.fullName,
          photoBase64: currentUser.photoBase64
        }] : [];
      }
    }
  } catch(e) { console.error(e); }
}

async function loadEmployees(page) {
  if (page !== undefined) empCurrentPage = page;
  try {
    const res = await api('GET', `/api/Users?page=${empCurrentPage}&pageSize=${EMP_PAGE_SIZE}`);
    empTotalPages = res.totalPages || 1;
    document.getElementById('user-page-info').textContent =
      `Sayfa ${res.page} / ${res.totalPages} (${res.totalCount} personel)`;
    document.getElementById('emp-prev-btn').disabled = !res.hasPrev;
    document.getElementById('emp-next-btn').disabled = !res.hasNext;

    const tbody = document.getElementById('emp-tbody');
    tbody.innerHTML = res.items.map(u => `
      <tr class="emp-row" onclick="viewEmployeeProfile(${u.id})" style="cursor:pointer">
        <td><div class="name-cell">${avatar(u.fullName, u.photoBase64)}<span>${esc(u.fullName)}</span></div></td>
        <td>${esc(u.email)}</td>
        <td>${esc(u.departmentName||'—')}</td>
        <td>${esc(u.position||'—')}</td>
        <td><span class="badge ${u.role==='Admin'?'badge-admin':'badge-emp'}">${u.role==='Admin'?'Yönetici':'Personel'}</span></td>
        <td class="text-right" onclick="event.stopPropagation()">
          <div class="btn-group" style="justify-content:flex-end">
            <button class="btn btn-ghost btn-sm" onclick="openEmpModal(${u.id})">Düzenle</button>
            <button class="btn btn-sm" style="background:var(--err-soft);color:var(--err)" onclick="deleteEmployee(${u.id},'${esc(u.fullName).replace(/'/g,"\\'")}')">Sil</button>
          </div>
        </td>
      </tr>`).join('');

    allUsers = res.items;
    populateUserSelects();
  } catch(e) { toast(e.message,'err'); }
}

function empChangePage(delta) {
  const next = empCurrentPage + delta;
  if (next < 1 || next > empTotalPages) return;
  loadEmployees(next);
}

function filterEmployees() {
  const q = document.getElementById('emp-search').value.toLowerCase();
  document.querySelectorAll('#emp-tbody tr').forEach(tr => {
    tr.style.display = tr.textContent.toLowerCase().includes(q) ? '' : 'none';
  });
}

function openEmpModal(id) {
  const u = id ? allUsers.find(x=>x.id===id) : null;
  document.getElementById('emp-modal-title').textContent = u ? 'Personel Düzenle' : 'Personel Ekle';
  document.getElementById('emp-id').value    = u?.id || '';
  document.getElementById('emp-name').value  = u?.fullName || '';
  document.getElementById('emp-email').value = u?.email  || '';
  document.getElementById('emp-pw').value    = '';
  document.getElementById('emp-role').value  = u?.role  || 'Employee';
  document.getElementById('emp-pos').value   = u?.position || '';
  document.getElementById('emp-hire').value  = u?.hireDate ? fmtDateOnly(u.hireDate) : '';
  document.getElementById('emp-phone').value = u?.phoneNumber || '';
  document.getElementById('emp-pw-label').querySelector('.form-label').textContent =
    u ? 'Yeni Şifre (boş bırakılabilir)' : 'Şifre *';

  // Foto önizleme + state
  document.getElementById('emp-photo-data').value = u?.photoBase64 || '';
  renderEmpPhotoPreview(u?.photoBase64, u?.fullName);

  // File input'u resetle (aynı dosya tekrar seçilebilsin)
  const fileInput = document.getElementById('emp-photo-input');
  if (fileInput) fileInput.value = '';

  const deptSel = document.getElementById('emp-dept');
  deptSel.innerHTML = '<option value="">— Departman Seçin —</option>' +
    allDepts.map(d=>`<option value="${d.id}" ${u?.departmentId===d.id?'selected':''}>${d.name}</option>`).join('');

  document.getElementById('emp-modal').classList.remove('hidden');
}
document.getElementById('add-emp-btn').addEventListener('click', () => openEmpModal());

function renderEmpPhotoPreview(photoBase64, name) {
  const preview = document.getElementById('emp-photo-preview');
  const clearBtn = document.getElementById('emp-photo-clear');
  if (photoBase64) {
    preview.innerHTML = `<img src="${photoBase64}" alt="" />`;
    clearBtn.style.display = '';
  } else {
    preview.innerHTML = avatar(name || '?', null, 72);
    clearBtn.style.display = 'none';
  }
}

function handleEmpPhoto(evt) {
  const file = evt.target.files[0];
  if (!file) return;
  if (file.size > 400_000) { toast('Fotoğraf 400 KB\'dan küçük olmalıdır.','err'); return; }
  const reader = new FileReader();
  reader.onload = e => {
    document.getElementById('emp-photo-data').value = e.target.result;
    const name = document.getElementById('emp-name').value;
    renderEmpPhotoPreview(e.target.result, name);
  };
  reader.readAsDataURL(file);
}

function clearEmpPhoto() {
  document.getElementById('emp-photo-data').value = '';
  const name = document.getElementById('emp-name').value;
  renderEmpPhotoPreview(null, name);
  const fi = document.getElementById('emp-photo-input'); if (fi) fi.value = '';
}

async function saveEmployee() {
  const id = document.getElementById('emp-id').value;
  const photoData = document.getElementById('emp-photo-data').value;
  const body = {
    fullName: document.getElementById('emp-name').value.trim(),
    email:    document.getElementById('emp-email').value.trim(),
    role:     document.getElementById('emp-role').value,
    departmentId: +document.getElementById('emp-dept').value || null,
    position: document.getElementById('emp-pos').value.trim() || null,
    hireDate: document.getElementById('emp-hire').value || null,
    phoneNumber: document.getElementById('emp-phone').value.trim() || null,
  };
  const pw = document.getElementById('emp-pw').value;
  try {
    if (id) {
      if (pw) body.newPassword = pw;
      // Foto güncellemesi (boş string → kaldır, dolu → güncelle)
      body.photoBase64 = photoData || null;
      await api('PUT', `/api/Users/${id}`, body);
      toast('Personel güncellendi.');
    } else {
      body.password = pw;
      await api('POST', '/api/Users', body);
      // Yeni kullanıcı için foto ayrı bir PUT ile eklenmeli (CreateUserDto'da photo yok)
      if (photoData) {
        try {
          const created = await api('GET','/api/Users?page=1&pageSize=1');
          const newId = created?.items?.find(x => x.email === body.email)?.id;
          if (newId) {
            await api('PUT', `/api/Users/${newId}`, { photoBase64: photoData });
          }
        } catch(_) { /* foto eklenmese de personel oluşturuldu */ }
      }
      toast('Personel eklendi.', 'ok');
    }
    closeModal('emp-modal');
    await loadAllUsers();
    loadEmployees();
  } catch(e) { toast(e.message,'err'); }
}

// Read-only personel profili (admin için detay göster, sonra düzenleye geçilebilir)
async function viewEmployeeProfile(id) {
  const u = allUsers.find(x => x.id === id);
  if (!u) return;
  const now = new Date();

  // Modalı aç
  document.getElementById('emp-view-modal').classList.remove('hidden');
  const monthLabel = now.toLocaleDateString(getLang()==='en'?'en-US':'tr-TR', {month:'long', year:'numeric'});
  document.getElementById('emp-view-content').innerHTML = `
    <div class="profile-view-head">
      <div class="profile-view-avatar">${avatar(u.fullName, u.photoBase64, 96)}</div>
      <div class="profile-view-meta">
        <h2 class="profile-view-name">${esc(u.fullName)}</h2>
        <div class="profile-view-role">
          <span class="badge ${u.role==='Admin'?'badge-admin':'badge-emp'}">${u.role==='Admin'?t('role.admin'):t('role.employee')}</span>
          ${u.departmentName ? `<span class="profile-view-dept">${esc(u.departmentName)}</span>` : ''}
        </div>
        <div class="profile-view-pos">${esc(u.position || t('profile.no_position'))}</div>
      </div>
    </div>
    <div class="profile-view-grid">
      <div class="pv-info">
        <span class="pv-label">${t('profile.lbl_email')}</span>
        <span class="pv-val">${esc(u.email)}</span>
      </div>
      <div class="pv-info">
        <span class="pv-label">${t('profile.lbl_phone')}</span>
        <span class="pv-val">${esc(u.phoneNumber || '—')}</span>
      </div>
      <div class="pv-info">
        <span class="pv-label">${t('profile.lbl_hire')}</span>
        <span class="pv-val">${u.hireDate ? fmtDate(u.hireDate) : '—'}</span>
      </div>
      <div class="pv-info">
        <span class="pv-label">${t('profile.lbl_status')}</span>
        <span class="pv-val">${u.isActive ? `<span class="badge badge-ok">${t('badge.active')}</span>` : `<span class="badge badge-err">${t('badge.inactive')}</span>`}</span>
      </div>
    </div>
    <div class="pv-section">
      <h4>${t('profile.view_month_att')} (${monthLabel})</h4>
      <div id="pv-monthly" class="pv-monthly-loading">${t('common.loading_dots')}</div>
    </div>
    <div class="pv-section">
      <h4>${t('profile.view_recent')}</h4>
      <div id="pv-leaves" class="pv-leaves-loading">${t('common.loading_dots')}</div>
    </div>
  `;

  // Edit butonunu bağla
  document.getElementById('emp-view-edit-btn').onclick = () => {
    closeModal('emp-view-modal');
    openEmpModal(id);
  };

  // Async olarak istatistikleri çek
  try {
    const [summary, leaves] = await Promise.all([
      api('GET', `/api/Users/${id}/attendance-summary?year=${now.getFullYear()}&month=${now.getMonth()+1}`).catch(()=>null),
      api('GET', '/api/Leaves').catch(()=>[])
    ]);

    if (summary) {
      document.getElementById('pv-monthly').innerHTML = `
        <div class="pv-stats">
          <div class="pv-stat"><strong>${summary.presentDays}</strong><small>${t('mydash.present')}</small></div>
          <div class="pv-stat"><strong>${summary.leaveDays}</strong><small>${t('mydash.leaved')}</small></div>
          <div class="pv-stat"><strong>${summary.absentDays}</strong><small>${t('mydash.absent2')}</small></div>
          <div class="pv-stat"><strong>${(summary.totalWorkedHours||0).toFixed(1)}</strong><small>${t('pv.hours')}</small></div>
          <div class="pv-stat"><strong>${(summary.totalOvertimeHours||0).toFixed(1)}</strong><small>${t('monthly.ot_hours')}</small></div>
          <div class="pv-stat"><strong>${summary.overtimeShiftCount}</strong><small>${t('monthly.ot_count')}</small></div>
        </div>`;
    } else {
      document.getElementById('pv-monthly').innerHTML = `<div class="empty">${t('pv.no_month')}</div>`;
    }

    const myLeaves = (leaves || []).filter(l => l.userId === id).slice(0, 5);
    document.getElementById('pv-leaves').innerHTML = myLeaves.length
      ? myLeaves.map(l => `
          <div class="leave-mini">
            <div class="lm-info">
              <strong>${esc(leaveTypeI18n(l.leaveType))}</strong>
              <small>${fmtDate(l.startDate)} - ${fmtDate(l.endDate)} · ${l.totalDays} ${t('pv.days')}</small>
            </div>
            ${statusBadge(l.status)}
          </div>`).join('')
      : `<div class="empty">${t('pv.no_leaves')}</div>`;
  } catch(_) { /* sessiz */ }
}

async function deleteEmployee(id, name) {
  if (!confirm(`"${name}" silinecek. Onaylıyor musunuz?`)) return;
  try {
    await api('DELETE', `/api/Users/${id}`);
    toast('Personel pasife alındı.');
    await loadAllUsers(); loadEmployees();
  } catch(e) { toast(e.message,'err'); }
}

// ── Roster ──────────────────────────────────────────────────────────
async function loadRoster() {
  const dayKeys = ['day.mon','day.tue','day.wed','day.thu','day.fri','day.sat','day.sun'];
  const ws = rosterWeekStart;
  const we = new Date(ws); we.setDate(we.getDate()+6);
  document.getElementById('roster-week-label').textContent = `${fmtDate(ws)} – ${fmtDate(we)}`;

  const isAdmin = currentUser?.role === 'Admin';
  const rosterCard = document.querySelector('#page-roster .card');
  if (rosterCard) rosterCard.classList.toggle('readonly-roster', !isAdmin);

  // Admin-only kontrolleri göster/gizle
  document.querySelectorAll('#page-roster .admin-only').forEach(el => {
    el.classList.toggle('hidden', !isAdmin);
  });

  // Departman filtresini doldur (admin için) — mevcut seçimi koru
  if (isAdmin) {
    const filter = document.getElementById('roster-dept-filter');
    if (filter && allDepts.length) {
      const currentVal = filter.value;
      filter.innerHTML = `<option value="">${t('roster.all_depts')}</option>` +
        allDepts.map(d => `<option value="${d.id}">${esc(d.name)}</option>`).join('');
      // Seçim hâlâ listede mevcutsa geri yükle (yoksa "Tüm Departmanlar" kalır)
      if (currentVal && [...filter.options].some(o => o.value === currentVal)) {
        filter.value = currentVal;
      }
    }
  }

  // Personel için sayfa altyazısı güncelle + filtre butonu opsiyonel
  const subEl = document.querySelector('#page-roster .page-sub');
  if (subEl) subEl.textContent = t(isAdmin ? 'roster.sub_admin' : 'roster.sub_emp');

  try {
    const assignments = await api('GET', `/api/Shifts/weekly?weekStart=${fmtDateOnly(ws)}`);
    // userId → days → [assignments]
    const userMap = {};
    assignments.forEach(a => {
      if (!userMap[a.userId]) userMap[a.userId] = { name: a.userFullName, photo: a.userPhoto, days: {} };
      const ds = fmtDateOnly(a.date);
      if (!userMap[a.userId].days[ds]) userMap[a.userId].days[ds] = [];
      userMap[a.userId].days[ds].push(a);
    });

    const head = document.getElementById('roster-head');
    head.innerHTML = `<th style="text-align:left">${t('roster.col_person')}</th>` +
      dayKeys.map((dk,i) => {
        const dt = new Date(ws); dt.setDate(dt.getDate()+i);
        return `<th>${t(dk)}<small>${fmtDate(dt)}</small></th>`;
      }).join('');

    // Departman filtresini uygula (admin)
    const deptFilter = +document.getElementById('roster-dept-filter')?.value || 0;
    let usersOrdered = [...allUsers];
    if (isAdmin && deptFilter) {
      usersOrdered = usersOrdered.filter(u => u.departmentId === deptFilter);
    }
    // Personelse: kendisini en üste al, diğerlerini soluk göster
    if (!isAdmin) {
      usersOrdered.sort((a,b) => (a.id===currentUser.userId?-1: b.id===currentUser.userId?1:0));
    }

    const body = document.getElementById('roster-body');
    const rows = usersOrdered.map(u => {
      const isMe = !isAdmin && u.id === currentUser.userId;
      const days = userMap[u.id]?.days || {};
      const cells = Array.from({length:7},(_,i) => {
        const dt = new Date(ws); dt.setDate(dt.getDate()+i);
        const ds = fmtDateOnly(dt);
        const dayAssignments = days[ds] || [];

        let cellHtml = '';
        if (dayAssignments.length) {
          // Sıralama: Normal vardiya / İzin önce, Fazla mesai sonra (üstte normal, altta FM)
          const sorted = [...dayAssignments].sort((x, y) => {
            const xCat = getShiftCategory(x.shiftId);
            const yCat = getShiftCategory(y.shiftId);
            const order = { shift: 0, leave: 0, overtime: 1 };
            return (order[xCat] ?? 0) - (order[yCat] ?? 0);
          });
          cellHtml = sorted.map(a => {
            const cat = getShiftCategory(a.shiftId);
            const catBadge = cat==='overtime' ? '<span class="fm-badge">FM</span>' : '';
            const onclick = isAdmin
              ? `onclick="openShiftModal('${ds}',${u.id},${a.id})"`
              : '';
            const shiftLabel = shiftNameById(a.shiftId, a.shiftName);
            return `<div class="shift-chip cat-${cat}" style="background:${a.shiftColor}" ${onclick} title="${esc(shiftLabel)} ${a.startTime}–${a.endTime}">
                <span class="chip-name">${esc(shiftLabel)}${catBadge}</span>
                <small>${a.startTime}–${a.endTime}</small>
              </div>`;
          }).join('');
          // Admin: bu hücreye yeni FM eklemek için + butonu (sadece FM yoksa ve base shift varsa)
          const hasOvertime  = dayAssignments.some(a => getShiftCategory(a.shiftId) === 'overtime');
          const hasBaseShift = dayAssignments.some(a => getShiftCategory(a.shiftId) !== 'overtime');
          if (isAdmin && !hasOvertime && hasBaseShift) {
            cellHtml += `<div class="shift-add-more" onclick="openShiftModal('${ds}',${u.id})" title="${t('shift.cat.overtime')}">${t('roster.add_ot')}</div>`;
          }
        } else {
          cellHtml = isAdmin
            ? `<div class="shift-empty" onclick="openShiftModal('${ds}',${u.id})">+</div>`
            : `<div class="shift-blank">—</div>`;
        }
        return `<td>${cellHtml}</td>`;
      }).join('');
      const rowCls = isMe ? 'row-me' : '';
      // Admin için personel ismine tıklanabilir profil görüntüleme
      const nameCell = isAdmin
        ? `<div class="name-cell name-cell-clickable" onclick="viewEmployeeProfile(${u.id})" title="${t('view.emp_detail')}">${avatar(u.fullName,u.photoBase64)}<span>${esc(u.fullName)}</span></div>`
        : `<div class="name-cell">${avatar(u.fullName,u.photoBase64)}<span>${esc(u.fullName)}${isMe?' <small class="me-tag">'+t('roster.me')+'</small>':''}</span></div>`;
      return `<tr class="${rowCls}"><td>${nameCell}</td>${cells}</tr>`;
    }).join('');
    body.innerHTML = rows || `<tr><td colspan="8" class="empty">${t('common.empty')}</td></tr>`;
  } catch(e) { toast(e.message,'err'); }
}
function rosterNav(d) { rosterWeekStart.setDate(rosterWeekStart.getDate()+d*7); loadRoster(); }

// Önceki haftanın planını bu haftaya kopyala
async function copyPreviousWeek() {
  const target = new Date(rosterWeekStart);
  const source = new Date(rosterWeekStart); source.setDate(source.getDate() - 7);
  const targetStr = fmtDateOnly(target);
  const sourceStr = fmtDateOnly(source);

  if (!confirm(
    `${fmtDate(source)} – ${fmtDate(new Date(source.getTime()+6*86400000))} haftası, ` +
    `şu anki haftanın (${fmtDate(target)}) üzerine kopyalanacak. ` +
    `Bu haftaki tüm mevcut atamalar SİLİNECEK. Devam edilsin mi?`)) return;

  try {
    const result = await api('POST', '/api/Shifts/copy-week', {
      sourceWeekStart: sourceStr,
      targetWeekStart: targetStr
    });
    toast(`${result.copied} vardiya kopyalandı.`, 'ok');
    loadRoster();
  } catch(e) { toast(e.message, 'err'); }
}

let pendingShiftDate = null, pendingShiftUserId = null;
let currentShiftCat = 'shift';
let dayExistingShifts = [];

async function openShiftModal(dateStr, userId, assignId) {
  pendingShiftDate = dateStr; pendingShiftUserId = userId;
  document.getElementById('shift-assign-id').value = assignId||'';
  const u = allUsers.find(x=>x.id===userId);
  document.getElementById('shift-cell-info').innerHTML =
    `<strong>${u?.fullName||''}</strong> · ${fmtDate(dateStr)}`;

  // Bu gün için mevcut tüm atamalar
  const shifts = await api('GET',`/api/Shifts/user/${userId}?from=${dateStr}&to=${dateStr}`).catch(()=>[]);
  dayExistingShifts = shifts;

  // Modal başlığı: ekleme mi düzenleme mi?
  const titleEl = document.getElementById('shift-modal-title');
  let currentShift = null;
  if (assignId) {
    currentShift = shifts.find(s => s.id === assignId);
    titleEl.textContent = t('shift.title_edit');
  } else {
    titleEl.textContent = shifts.length ? t('shift.title_extra') : t('shift.title_assign');
  }

  // Mevcut atamalar listesi (sadece eklerken — düzenlemede gerek yok)
  const existingWrap = document.getElementById('shift-existing-wrap');
  const existingList = document.getElementById('shift-existing-list');
  if (!assignId && shifts.length) {
    existingWrap.classList.remove('hidden');
    existingList.innerHTML = shifts.map(s => {
      const cat = getShiftCategory(s.shiftId);
      return `<div class="existing-shift-item">
        <span class="shift-chip sm cat-${cat}" style="background:${s.shiftColor}">${esc(shiftNameById(s.shiftId, s.shiftName))}<small>${s.startTime}–${s.endTime}</small></span>
        <button type="button" class="btn-icon-mini" onclick="openShiftModal('${dateStr}',${userId},${s.id})" title="${t('common.edit')}">✎</button>
      </div>`;
    }).join('');
  } else {
    existingWrap.classList.add('hidden');
  }

  // FM kuralı: aynı güne sadece 1 fazla mesai eklenebilir
  const hasOvertime = shifts.some(s => getShiftCategory(s.shiftId) === 'overtime');
  // FM kuralı: FM eklemek için mevcut bir normal vardiya/tatil olmalı
  const hasBaseShift = shifts.some(s => getShiftCategory(s.shiftId) !== 'overtime');
  const otTab = document.querySelector('.shift-cat-tab[data-cat="overtime"]');
  if (otTab) {
    if (assignId) {
      // Düzenleme modunda kısıtlamayı gevşet (mevcut FM'i düzenleyebilirsin)
      otTab.classList.remove('disabled-tab');
      otTab.removeAttribute('title');
    } else if (hasOvertime) {
      otTab.classList.add('disabled-tab');
      otTab.setAttribute('title', 'Bu güne zaten fazla mesai eklenmiş');
    } else if (!hasBaseShift) {
      otTab.classList.add('disabled-tab');
      otTab.setAttribute('title', 'Fazla mesai için önce normal vardiya atayın');
    } else {
      otTab.classList.remove('disabled-tab');
      otTab.removeAttribute('title');
    }
  }

  // Personel seçimi
  document.getElementById('shift-user-sel').innerHTML =
    allUsers.map(u2=>`<option value="${u2.id}" ${u2.id===userId?'selected':''}>${u2.fullName}</option>`).join('');
  // Personel düzenlerken kilitli kalsın (yanlışlıkla başka birine atanmasın)
  document.getElementById('shift-user-sel').disabled = !!assignId;

  // Düzenleme modunda mevcut kategoriyi/türü göster
  if (currentShift) {
    currentShiftCat = getShiftCategory(currentShift.shiftId);
    switchShiftCat(currentShiftCat, currentShift.shiftId);
  } else {
    // Yeni ekleme: önceden bir vardiya varsa, yenisi varsayılan olarak Fazla Mesai olsun
    currentShiftCat = shifts.length ? 'overtime' : 'shift';
    switchShiftCat(currentShiftCat);
  }

  document.getElementById('shift-note').value = currentShift?.note || '';
  document.getElementById('shift-delete-btn').classList.toggle('hidden', !assignId);
  document.getElementById('shift-modal').classList.remove('hidden');
}

function switchShiftCat(cat, selectedId) {
  // Disabled tab'a tıklanmayı engelle
  const targetTab = document.querySelector(`.shift-cat-tab[data-cat="${cat}"]`);
  if (targetTab && targetTab.classList.contains('disabled-tab')) {
    toast(targetTab.getAttribute('title') || 'Bu kategoriye eklenemez', 'warn');
    return;
  }
  currentShiftCat = cat;
  document.querySelectorAll('.shift-cat-tab').forEach(t => {
    t.classList.toggle('active', t.dataset.cat === cat);
  });

  const grid = document.getElementById('shift-type-grid');
  const items = SHIFT_TYPES.filter(s => s.cat === cat);
  // İlk seçili: parametre varsa onu, yoksa kategorinin ilkini
  const selId = selectedId || items[0]?.id;
  document.getElementById('shift-type-sel').value = selId || '';
  grid.innerHTML = items.map(s => `
    <div class="shift-type-card ${s.id===selId?'selected':''}" data-id="${s.id}" onclick="selectShiftType(${s.id})">
      <span class="cat-dot" style="background:${s.color}"></span>
      <div class="stc-info">
        <strong>${shiftTypeName(s)}</strong>
        <small>${s.startTime} – ${s.endTime}</small>
      </div>
      <svg class="stc-check" viewBox="0 0 20 20" fill="none"><path d="M5 10l3 3 7-7" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"/></svg>
    </div>
  `).join('');
}

function selectShiftType(id) {
  document.getElementById('shift-type-sel').value = id;
  document.querySelectorAll('#shift-type-grid .shift-type-card').forEach(c => {
    c.classList.toggle('selected', +c.dataset.id === id);
  });
}

async function saveShift() {
  const id = document.getElementById('shift-assign-id').value;
  const shiftId = +document.getElementById('shift-type-sel').value;
  if (!shiftId) { toast(t('toast.shift_pick_type'),'warn'); return; }

  // FM kontrolü: aynı güne sadece 1 FM ve FM için base shift gerekir
  const cat = getShiftCategory(shiftId);
  if (cat === 'overtime' && !id) {
    const hasOvertime  = dayExistingShifts.some(s => getShiftCategory(s.shiftId) === 'overtime');
    const hasBaseShift = dayExistingShifts.some(s => getShiftCategory(s.shiftId) !== 'overtime');
    if (hasOvertime)  { toast(t('toast.ot_already'), 'err'); return; }
    if (!hasBaseShift){ toast(t('toast.ot_need_base'), 'err'); return; }
  }

  const body = {
    userId:  +document.getElementById('shift-user-sel').value,
    shiftId,
    date:    pendingShiftDate,
    note:    document.getElementById('shift-note').value || null
  };
  try {
    if (id) await api('PUT', `/api/Shifts/${id}`, body);
    else    await api('POST','/api/Shifts',       body);
    closeModal('shift-modal');
    toast('Vardiya kaydedildi.', 'ok');
    loadRoster();
  } catch(e) { toast(e.message,'err'); }
}
async function deleteShift() {
  const id = document.getElementById('shift-assign-id').value;
  if (!id || !confirm('Bu vardiyayı silmek istiyor musunuz?')) return;
  try { await api('DELETE', `/api/Shifts/${id}`); closeModal('shift-modal'); toast('Vardiya silindi.'); loadRoster(); }
  catch(e) { toast(e.message,'err'); }
}

// ── Attendance ──────────────────────────────────────────────────────
async function loadAttendance() {
  try {
    const logs = await api('GET','/api/Attendance/today');
    document.getElementById('att-tbody').innerHTML = logs.length ? logs.map(attRow).join('')
      : '<tr><td colspan="6" class="empty">Bugün henüz giriş yok.</td></tr>';
  } catch(e) { toast(e.message,'err'); }
}
function attRow(a) {
  const badges = [];
  if (a.isLateArrival)    badges.push(`<span class="badge badge-warn">${t('badge.late_min',{m:a.lateMinutes})}</span>`);
  if (a.isEarlyDeparture) badges.push(`<span class="badge badge-warn">${t('badge.early_min',{m:a.earlyMinutes})}</span>`);
  if (a.isInvalidTime)    badges.push(`<span class="badge badge-err">${t('badge.invalid_time')}</span>`);
  if (a.isShortDuration)  badges.push(`<span class="badge badge-err">${t('badge.short_dur')}</span>`);
  if (!badges.length)     badges.push(`<span class="badge badge-ok">${t('badge.normal')}</span>`);
  return `<tr>
    <td><div class="name-cell">${avatar(a.userFullName,a.userPhoto)}<span>${a.userFullName}</span></div></td>
    <td class="font-mono">${fmtTime(a.checkIn)}</td>
    <td class="font-mono">${a.checkOut?fmtTime(a.checkOut):'—'}</td>
    <td><span class="badge ${a.source==='FaceRecognition'?'badge-info':'badge-emp'}">${a.source==='FaceRecognition'?t('badge.face_rec'):t('badge.manual')}</span></td>
    <td class="font-mono">${a.workedHours!=null?a.workedHours.toFixed(1)+' '+t('badge.hour_short'):'—'}</td>
    <td>${badges.join(' ')}</td>
  </tr>`;
}

async function loadMyAttendance() {
  try {
    const logs = await api('GET','/api/Attendance/my-today');
    document.getElementById('my-att-tbody').innerHTML = logs.length
      ? logs.map(a => `<tr>
          <td class="font-mono">${fmtTime(a.checkIn)}</td>
          <td class="font-mono">${a.checkOut?fmtTime(a.checkOut):'—'}</td>
          <td>${a.source==='FaceRecognition'?t('badge.face_rec'):t('badge.manual')}</td>
          <td class="font-mono">${a.workedHours!=null?a.workedHours.toFixed(1)+' '+t('badge.hour_short'):'—'}</td>
          <td>${a.checkOut?`<span class="badge badge-ok">${t('badge.completed')}</span>`:`<span class="badge badge-on">${t('badge.active_now')}</span>`}</td>
        </tr>`).join('')
      : `<tr><td colspan="5" class="empty">${t('common.no_today_log')}</td></tr>`;
  } catch(e) { toast(e.message,'err'); }
}
async function doCheckIn() { try { await api('POST','/api/Attendance/checkin'); toast(t('toast.checkin_saved')); loadMyAttendance(); } catch(e) { toast(e.message,'err'); } }
async function doCheckOut() { try { await api('POST','/api/Attendance/checkout'); toast(t('toast.checkout_saved')); loadMyAttendance(); } catch(e) { toast(e.message,'err'); } }

// ── Leaves ──────────────────────────────────────────────────────────
async function loadLeaves() {
  const status = document.getElementById('leave-filter')?.value||'';
  try {
    const leaves = await api('GET', `/api/Leaves${status?`?status=${status}`:''}`);
    document.getElementById('leave-tbody').innerHTML = leaves.length
      ? leaves.map(l => `<tr>
          <td><div class="name-cell">${avatar(l.userFullName,null)}<span>${l.userFullName}</span></div></td>
          <td>${l.leaveType}</td>
          <td>${fmtDate(l.startDate)}</td>
          <td>${fmtDate(l.endDate)}</td>
          <td class="font-mono">${l.totalDays}</td>
          <td>${statusBadge(l.status)}</td>
          <td class="text-right">${l.status==='Pending'
            ? `<div class="btn-group" style="justify-content:flex-end">
                <button class="btn btn-sm" style="background:var(--ok-soft);color:var(--ok)" onclick="reviewLeave(${l.id},'Approved')">Onayla</button>
                <button class="btn btn-sm" style="background:var(--err-soft);color:var(--err)" onclick="reviewLeave(${l.id},'Rejected')">Reddet</button>
              </div>` : '—'}</td>
        </tr>`).join('')
      : '<tr><td colspan="7" class="empty">Kayıt bulunamadı.</td></tr>';
  } catch(e) { toast(e.message,'err'); }
}
async function reviewLeave(id, status) {
  if (status==='Rejected' && !confirm('Reddetmek istediğinize emin misiniz?')) return;
  try { await api('PATCH', `/api/Leaves/${id}/review`, {status}); toast(status==='Approved'?'Onaylandı.':'Reddedildi.'); loadLeaves(); }
  catch(e) { toast(e.message,'err'); }
}
function statusBadge(s) {
  const map = {Pending:'badge-warn',Approved:'badge-ok',Rejected:'badge-err'};
  const labels = {Pending:'Bekliyor',Approved:'Onaylandı',Rejected:'Reddedildi'};
  return `<span class="badge ${map[s]||''}">${labels[s]||s}</span>`;
}

async function loadMyLeaves() {
  try {
    const leaves = await api('GET','/api/Leaves/my');
    document.getElementById('my-leave-tbody').innerHTML = leaves.length
      ? leaves.map(l => `<tr>
          <td>${l.leaveType}</td>
          <td>${fmtDate(l.startDate)}</td>
          <td>${fmtDate(l.endDate)}</td>
          <td class="font-mono">${l.totalDays}</td>
          <td>${statusBadge(l.status)}</td>
          <td>${fmtDate(l.createdAt)}</td>
        </tr>`).join('')
      : '<tr><td colspan="6" class="empty">İzin talebiniz yok.</td></tr>';
  } catch(e) { toast(e.message,'err'); }
}
function openLeaveModal() { document.getElementById('leave-modal').classList.remove('hidden'); }
async function submitLeave() {
  const body = {
    leaveType:        document.getElementById('leave-type').value,
    startDate:        document.getElementById('leave-start').value,
    endDate:          document.getElementById('leave-end').value,
    description:      document.getElementById('leave-desc').value,
    hasMedicalReport: document.getElementById('leave-report').checked
  };
  if (!body.startDate || !body.endDate) { toast('Tarihler zorunludur.','err'); return; }
  try { await api('POST','/api/Leaves', body); toast('Talep gönderildi.'); closeModal('leave-modal'); loadMyLeaves(); }
  catch(e) { toast(e.message,'err'); }
}

// ════════ PERSONEL: GENEL BAKIŞ ════════════════════════════════════
async function loadMyDashboard() {
  const firstName = currentUser.fullName.split(' ')[0];
  const hour = new Date().getHours();
  const greetKey = hour < 6 ? 'dash.good_night' : hour < 12 ? 'dash.good_morning' : hour < 18 ? 'dash.good_day' : 'dash.good_evening';
  document.getElementById('my-hero-title').textContent = `${t(greetKey)}, ${esc(firstName)}`;
  const subEl = document.getElementById('my-hero-sub');
  if (subEl) subEl.textContent = t('mydash.sub');
  const locale = getLang()==='en' ? 'en-US' : 'tr-TR';
  document.getElementById('my-hero-date').textContent =
    new Date().toLocaleDateString(locale, { day:'numeric', month:'long', year:'numeric', weekday:'long' });

  // Sabit saat (animasyon yok, sayfa açılışında bir kere)
  const clockEl = document.getElementById('my-hero-clock');
  if (clockEl) clockEl.textContent = new Date().toLocaleTimeString(locale, { hour:'2-digit', minute:'2-digit' });
  if (heroClockInterval) { clearInterval(heroClockInterval); heroClockInterval = null; }

  try {
    const ws = getMondayOf(new Date());
    const we = new Date(ws); we.setDate(we.getDate()+6);
    const wsStr = fmtDateOnly(ws);
    const weStr = fmtDateOnly(we);

    // Önümüzdeki 30 gün — sonraki vardiya için
    const next30 = new Date(); next30.setDate(next30.getDate()+30);

    const now = new Date();
    const [thisWeek, futureShifts, myLeaves, summary, balance] = await Promise.all([
      api('GET', `/api/Shifts/my?from=${wsStr}&to=${weStr}`).catch(()=>[]),
      api('GET', `/api/Shifts/my?from=${fmtDateOnly(now)}&to=${fmtDateOnly(next30)}`).catch(()=>[]),
      api('GET', '/api/Leaves/my').catch(()=>[]),
      api('GET', `/api/Users/${currentUser.userId}/attendance-summary?year=${now.getFullYear()}&month=${now.getMonth()+1}`).catch(()=>null),
      api('GET', '/api/LeaveBalance/me').catch(()=>null)
    ]);

    // Mini istatistikler
    const totalShifts    = thisWeek.length;
    const overtimeShifts = thisWeek.filter(s => getShiftCategory(s.shiftId) === 'overtime').length;
    const leaveShifts    = thisWeek.filter(s => getShiftCategory(s.shiftId) === 'leave').length;
    const pendingLeaves  = myLeaves.filter(l => l.status === 'Pending').length;

    document.getElementById('my-stat-grid').innerHTML = [
      { label: t('mydash.stat_balance'),    val: (balance?.remainingDays ?? 0)+'/'+(balance?.annualAllowance ?? 14), icon: ICONS.palm, cls:'icon-green', hint:t('mydash.hint_year',{year: balance?.year || new Date().getFullYear()}) },
      { label: t('mydash.stat_shifts'),     val: totalShifts,    icon: ICONS.calendar, cls:'icon-blue',  hint:t('mydash.hint_total')    },
      { label: t('mydash.stat_overtime'),   val: overtimeShifts, icon: ICONS.trend,    cls:'icon-amber', hint:t('mydash.hint_week')  },
      { label: t('mydash.stat_pending'),    val: pendingLeaves,  icon: ICONS.pending,  cls:'icon-cyan',  hint:t('mydash.hint_leave')      },
      { label: t('mydash.stat_month_att'),  val: (summary?.presentDays ?? 0)+' '+t('common.day'), icon: ICONS.checkin, cls:'icon-violet', hint:t('mydash.present') },
      { label: t('mydash.stat_month_hrs'),  val: (summary?.totalWorkedHours ?? 0).toFixed(1)+' '+t('badge.hour_short'), icon: ICONS.trend, cls:'icon-red', hint:t('mydash.hint_total') },
    ].map(s => `
      <div class="stat-card stat-card-rich">
        <div class="stat-icon ${s.cls}">${s.icon}</div>
        <div class="stat-content">
          <div class="stat-val">${s.val}</div>
          <div class="stat-lbl">${s.label}</div>
        </div>
        <div class="stat-hint">${s.hint}</div>
      </div>
    `).join('');

    // Bu hafta vardiyalarım
    document.getElementById('my-week-shifts').innerHTML = thisWeek.length
      ? thisWeek.slice(0,6).map(s => {
          const dt = new Date(s.date);
          const isToday = fmtDateOnly(dt) === fmtDateOnly(new Date());
          const cat = getShiftCategory(s.shiftId);
          const catBadge = cat === 'overtime' ? '<span class="fm-badge">FM</span>' : '';
          return `<div class="ms-row ${isToday?'ms-today':''}">
            <div class="ms-day">
              <strong>${dayFullFromDate(dt)}</strong>
              <small>${fmtDate(dt)}${isToday?' · '+t('mydash.today_marker'):''}</small>
            </div>
            <div class="ms-chip" style="background:${s.shiftColor}">${esc(shiftNameById(s.shiftId, s.shiftName))}${catBadge}</div>
            <div class="ms-time font-mono">${s.startTime}–${s.endTime}</div>
          </div>`;
        }).join('')
      : `<div class="empty">${t('mydash.no_week')}</div>`;

    // Sonraki vardiya
    const upcomingShifts = (futureShifts || []).filter(s => {
      const d = new Date(s.date);
      return d >= new Date(new Date().toDateString());
    }).sort((a,b) => new Date(a.date) - new Date(b.date));
    const next = upcomingShifts[0];
    document.getElementById('my-next-shift').innerHTML = next ? (() => {
      const dt = new Date(next.date);
      const days = Math.ceil((dt - new Date(new Date().toDateString())) / 86400000);
      const dayLabel = days === 0 ? t('mydash.days_today') : days === 1 ? t('mydash.days_tomorrow') : t('mydash.days_after',{n:days});
      return `<div class="next-shift">
        <div class="ns-day">${esc(dayLabel)}</div>
        <div class="ns-chip" style="background:${next.shiftColor}">${esc(shiftNameById(next.shiftId, next.shiftName))}</div>
        <div class="ns-time font-mono">${next.startTime} – ${next.endTime}</div>
        <div class="ns-date">${fmtDate(dt)}</div>
      </div>`;
    })() : `<div class="empty">${t('mydash.no_future')}</div>`;

    // İzin durumum özeti
    const recent = myLeaves.slice(0,4);
    document.getElementById('my-leaves-summary').innerHTML = recent.length ? recent.map(l => `
      <div class="leave-mini">
        <div class="lm-info">
          <strong>${esc(leaveTypeI18n(l.leaveType))}</strong>
          <small>${fmtDate(l.startDate)} - ${fmtDate(l.endDate)}</small>
        </div>
        ${statusBadge(l.status)}
      </div>
    `).join('') : `<div class="empty">${t('mydash.no_leaves')}</div>`;

    // Bu ay özet
    if (summary) {
      const total = (summary.presentDays || 0) + (summary.leaveDays || 0) + (summary.absentDays || 0);
      const presentPct = total ? Math.round((summary.presentDays/total)*100) : 0;
      const leavePct   = total ? Math.round((summary.leaveDays/total)*100) : 0;
      const absentPct  = total ? Math.round((summary.absentDays/total)*100) : 0;
      document.getElementById('my-month-summary').innerHTML = `
        <div class="month-bars">
          <div class="dist-row">
            <div class="dist-label"><span class="dist-dot" style="background:#34D399"></span>${t('mydash.present')}</div>
            <div class="dist-bar"><div class="dist-fill" style="width:${presentPct}%;background:#34D399"></div></div>
            <div class="dist-val"><strong>${summary.presentDays}</strong><small>${presentPct}%</small></div>
          </div>
          <div class="dist-row">
            <div class="dist-label"><span class="dist-dot" style="background:#22D3EE"></span>${t('mydash.leaved')}</div>
            <div class="dist-bar"><div class="dist-fill" style="width:${leavePct}%;background:#22D3EE"></div></div>
            <div class="dist-val"><strong>${summary.leaveDays}</strong><small>${leavePct}%</small></div>
          </div>
          <div class="dist-row">
            <div class="dist-label"><span class="dist-dot" style="background:#F87171"></span>${t('mydash.absent2')}</div>
            <div class="dist-bar"><div class="dist-fill" style="width:${absentPct}%;background:#F87171"></div></div>
            <div class="dist-val"><strong>${summary.absentDays}</strong><small>${absentPct}%</small></div>
          </div>
        </div>
        <div class="month-totals">
          <div><strong>${(summary.totalWorkedHours ?? 0).toFixed(1)}</strong><small>${t('monthly.total_hours')}</small></div>
          <div><strong>${(summary.totalOvertimeHours ?? 0).toFixed(1)}</strong><small>${t('shift.cat.overtime')}</small></div>
          <div><strong>${summary.overtimeShiftCount ?? 0}</strong><small>${t('monthly.ot_count')}</small></div>
        </div>`;
    } else {
      document.getElementById('my-month-summary').innerHTML = '<div class="empty">Bu ay için kayıt yok.</div>';
    }
  } catch(e) { toast(e.message, 'err'); }
}

// ════════ PERSONEL: AYLIK ÖZETİM ════════
function loadMyMonthly() {
  const ms = document.getElementById('my-month-sel');
  const ys = document.getElementById('my-year-sel');
  if (ms && !ms.options.length) {
    const now = new Date();
    const MONTHS = ['Ocak','Şubat','Mart','Nisan','Mayıs','Haziran','Temmuz','Ağustos','Eylül','Ekim','Kasım','Aralık'];
    ms.innerHTML = MONTHS.map((m,i)=>`<option value="${i+1}" ${i+1===now.getMonth()+1?'selected':''}>${m}</option>`).join('');
    const y = now.getFullYear();
    ys.innerHTML = [y-1,y,y+1].map(yr=>`<option value="${yr}" ${yr===y?'selected':''}>${yr}</option>`).join('');
  }
  loadMyMonthlyData();
}
async function loadMyMonthlyData() {
  const year  = document.getElementById('my-year-sel').value;
  const month = document.getElementById('my-month-sel').value;
  try {
    const s = await api('GET', `/api/Users/${currentUser.userId}/attendance-summary?year=${year}&month=${month}`);
    const hr = t('badge.hour_short');
    document.getElementById('my-monthly-result').innerHTML = `
      <div class="stat-grid" style="margin-top:20px">
        ${[
          [t('monthly.present_days'), s.presentDays,         ICONS.checkin,    'icon-green'],
          [t('monthly.leave_days'),   s.leaveDays,           ICONS.palm,       'icon-amber'],
          [t('monthly.absent_days'),  s.absentDays,          ICONS.cross,      'icon-red'],
          [t('monthly.with_report'),  s.absentWithReport,    ICONS.clipboard,  'icon-cyan'],
          [t('monthly.no_report'),    s.absentWithoutReport, ICONS.cross,      'icon-amber'],
          [t('monthly.total_hours'),  s.totalWorkedHours.toFixed(1)+' '+hr,   ICONS.trend, 'icon-blue'],
          [t('monthly.ot_hours'),     s.totalOvertimeHours.toFixed(1)+' '+hr, ICONS.trend, 'icon-violet'],
          [t('monthly.ot_count'),     s.overtimeShiftCount,  ICONS.calendar,   'icon-blue'],
        ].map(([lbl,val,icon,cls])=>`
          <div class="stat-card stat-card-rich">
            <div class="stat-icon ${cls}">${icon}</div>
            <div class="stat-content">
              <div class="stat-val">${val}</div>
              <div class="stat-lbl">${lbl}</div>
            </div>
          </div>`).join('')}
      </div>`;
  } catch(e) { toast(e.message,'err'); }
}
async function exportMyMonthly() {
  const year  = document.getElementById('my-year-sel').value;
  const month = document.getElementById('my-month-sel').value;
  try {
    const res = await fetch(API_BASE + `/api/Users/${currentUser.userId}/attendance-summary/export?year=${year}&month=${month}`, {
      headers: { 'Authorization': 'Bearer ' + authToken }
    });
    if (!res.ok) throw new Error(t('toast.report_dl_err'));
    const blob = await res.blob();
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href = url; a.download = `devam-raporu-${year}-${String(month).padStart(2,'0')}.csv`;
    document.body.appendChild(a); a.click(); a.remove();
    URL.revokeObjectURL(url);
    toast(t('toast.report_downloading'), 'ok');
  } catch(e) { toast(e.message, 'err'); }
}

// ── My Shifts ───────────────────────────────────────────────────────
async function loadMyShifts() {
  const ws = myShiftWeekStart;
  const we = new Date(ws); we.setDate(we.getDate()+6);
  document.getElementById('my-shift-label').textContent = `${fmtDate(ws)} – ${fmtDate(we)}`;
  try {
    const shifts = await api('GET', `/api/Shifts/my?from=${fmtDateOnly(ws)}&to=${fmtDateOnly(we)}`);
    document.getElementById('my-shift-tbody').innerHTML = shifts.length
      ? shifts.map(s => `<tr>
          <td class="font-mono">${fmtDate(s.date)}</td>
          <td>${dayFullFromDate(new Date(s.date))}</td>
          <td><span class="shift-chip sm" style="background:${s.shiftColor}">${esc(shiftNameById(s.shiftId, s.shiftName))}</span></td>
          <td class="font-mono">${s.startTime}</td>
          <td class="font-mono">${s.endTime}</td>
        </tr>`).join('')
      : `<tr><td colspan="5" class="empty">${t('common.empty_week')}</td></tr>`;
  } catch(e) { toast(e.message,'err'); }
}
function myShiftNav(d) { myShiftWeekStart.setDate(myShiftWeekStart.getDate()+d*7); loadMyShifts(); }

// ── Profile ─────────────────────────────────────────────────────────
async function loadProfile() {
  try {
    const u = await api('GET','/api/Users/me');
    currentUser = {...currentUser, ...u};
    sessionStorage.setItem('sx_user', JSON.stringify(currentUser));
    updateTopbarUser();

    document.getElementById('profile-avatar').innerHTML = avatar(u.fullName, u.photoBase64, 96);
    document.getElementById('profile-photo-name').textContent = u.fullName;
    document.getElementById('profile-photo-role').textContent = u.role==='Admin'?t('role.admin'):t('role.employee');

    document.getElementById('profile-info').innerHTML = `
      <table class="profile-table">
        <tr><th>${t('profile.lbl_email')}</th><td>${u.email}</td></tr>
        <tr><th>${t('profile.lbl_dept')}</th><td>${u.departmentName||'—'}</td></tr>
        <tr><th>${t('profile.lbl_pos')}</th><td>${u.position||'—'}</td></tr>
        <tr><th>${t('profile.lbl_phone')}</th><td>${u.phoneNumber||'—'}</td></tr>
        <tr><th>${t('profile.lbl_hire')}</th><td>${u.hireDate?fmtDate(u.hireDate):'—'}</td></tr>
        <tr><th>${t('profile.lbl_role')}</th><td>${u.role==='Admin'?t('role.admin'):t('role.employee')}</td></tr>
      </table>`;
  } catch(e) { toast(e.message,'err'); }
}
function handlePhoto(evt) {
  const file = evt.target.files[0];
  if (!file) return;
  if (file.size > 400_000) { toast(t('toast.photo_too_big'),'err'); return; }
  const reader = new FileReader();
  reader.onload = async e => {
    try {
      await api('PUT', `/api/Users/${currentUser.userId}`, { photoBase64: e.target.result });
      currentUser.photoBase64 = e.target.result;
      sessionStorage.setItem('sx_user', JSON.stringify(currentUser));
      toast(t('toast.photo_updated'));
      loadProfile();
    } catch(ex) { toast(ex.message,'err'); }
  };
  reader.readAsDataURL(file);
}
async function saveProfile() {
  const pw = document.getElementById('new-pw').value;
  if (!pw) { toast(t('toast.password_enter'),'warn'); return; }
  if (pw.length < 6) { toast(t('toast.password_short'),'err'); return; }
  try { await api('PUT', `/api/Users/${currentUser.userId}`, { newPassword: pw });
    document.getElementById('new-pw').value = ''; toast(t('toast.password_updated')); }
  catch(e) { toast(e.message,'err'); }
}

// ── Departments ─────────────────────────────────────────────────────
async function loadDepts() {
  try { allDepts = await api('GET','/api/Departments'); return allDepts; }
  catch(e) { return []; }
}
// Modern departman renkleri — her departman benzersiz bir gradient alır
const DEPT_GRADIENTS = [
  { from:'#7B7FE0', to:'#22D3EE', icon:'🧑‍💼' },
  { from:'#F97316', to:'#FBBF24', icon:'⚡' },
  { from:'#34D399', to:'#22D3EE', icon:'🌿' },
  { from:'#EC4899', to:'#A78BFA', icon:'✨' },
  { from:'#6366F1', to:'#8B5CF6', icon:'🎯' },
  { from:'#EF4444', to:'#F97316', icon:'🔥' },
  { from:'#14B8A6', to:'#22D3EE', icon:'💎' },
  { from:'#A855F7', to:'#EC4899', icon:'🌸' },
];
function deptStyle(idx) {
  return DEPT_GRADIENTS[idx % DEPT_GRADIENTS.length];
}

function renderDepts() {
  const grid    = document.getElementById('dept-grid');
  const summary = document.getElementById('dept-summary');
  if (!allDepts.length) {
    grid.innerHTML = `<div class="empty-state">
      <div class="empty-icon">🏢</div>
      <h3>${t('dept.empty_title')}</h3>
      <p>${t('dept.empty_sub')}</p>
      <button class="btn btn-primary" onclick="openDeptModal()">${t('dept.empty_btn')}</button>
    </div>`;
    if (summary) summary.innerHTML = '';
    return;
  }

  const totalEmployees = allDepts.reduce((sum, d) => sum + (d.employeeCount || 0), 0);
  const populated      = allDepts.filter(d => (d.employeeCount || 0) > 0).length;

  summary.innerHTML = `
    <div class="dept-summary-item">
      <div class="dsi-val">${allDepts.length}</div>
      <div class="dsi-lbl">${t('dept.summary_total')}</div>
    </div>
    <div class="dept-summary-item">
      <div class="dsi-val">${totalEmployees}</div>
      <div class="dsi-lbl">${t('dept.summary_emp')}</div>
    </div>
    <div class="dept-summary-item">
      <div class="dsi-val">${populated} / ${allDepts.length}</div>
      <div class="dsi-lbl">${t('dept.summary_active')}</div>
    </div>`;

  grid.innerHTML = allDepts.map((d, idx) => {
    const count = d.employeeCount ?? 0;
    const s     = deptStyle(idx);
    const isEmpty = count === 0;
    const label = isEmpty ? t('dept.no_emp_full')
               : count === 1 ? t('dept.emp_one_full')
               : t('dept.emp_n_full', { count });
    // Bu departmandaki personellerin küçük avatar dizimi (top 5)
    const members = allUsers.filter(u => u.departmentId === d.id).slice(0, 5);
    const memberAvatars = members.length ? members.map(m => `
      <div class="dept-avatar" title="${esc(m.fullName)}">${avatar(m.fullName, m.photoBase64, 28)}</div>
    `).join('') : '';
    const extra = count > 5 ? `<div class="dept-avatar dept-avatar-more">+${count - 5}</div>` : '';

    return `<div class="dept-card ${isEmpty?'dept-card-empty':''}">
      <div class="dept-card-bg" style="background: linear-gradient(135deg, ${s.from} 0%, ${s.to} 100%)"></div>
      <div class="dept-card-head">
        <div class="dept-emoji">${s.icon}</div>
        <button class="dept-card-menu" onclick="deleteDept(${d.id},'${esc(d.name).replace(/'/g,"\\'")}')" title="Departmanı sil">
          <svg viewBox="0 0 20 20" fill="none"><path d="M5 7h10M8 4h4l1 3M7 7v9a1 1 0 001 1h4a1 1 0 001-1V7" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/></svg>
        </button>
      </div>
      <h3 class="dept-card-name">${esc(d.name)}</h3>
      <div class="dept-card-count">
        ${count > 0 ? `<span class="dcc-num">${count}</span><span class="dcc-lbl">${label.replace(count, '').trim()}</span>` : `<span class="dcc-empty">${label}</span>`}
      </div>
      ${members.length ? `<div class="dept-members">${memberAvatars}${extra}</div>` : '<div class="dept-members-empty">—</div>'}
    </div>`;
  }).join('');
}
function openDeptModal() {
  document.getElementById('dept-name').value = '';
  document.getElementById('dept-desc').value = '';
  document.getElementById('dept-modal').classList.remove('hidden');
}
async function saveDept() {
  const name = document.getElementById('dept-name').value.trim();
  const desc = document.getElementById('dept-desc').value.trim();
  if (!name) { toast(t('toast.dept_name_req'),'err'); return; }
  try { await api('POST','/api/Departments', { name, description: desc||null });
    toast(t('toast.dept_added')); closeModal('dept-modal');
    await loadDepts(); renderDepts(); populateUserSelects(); }
  catch(e) { toast(e.message,'err'); }
}
async function deleteDept(id, name) {
  if (!confirm(`"${name}" silinecek. Bağlı personeller departmansız kalacak.`)) return;
  try { await api('DELETE', `/api/Departments/${id}`); toast('Departman silindi.');
    await loadDepts(); renderDepts(); }
  catch(e) { toast(e.message,'err'); }
}

// ── Monthly ─────────────────────────────────────────────────────────
function initMonthly() {
  const us = document.getElementById('monthly-user-sel');
  us.innerHTML = allUsers.map(u=>`<option value="${u.id}">${u.fullName}</option>`).join('');
  const ms = document.getElementById('monthly-month-sel');
  const now = new Date();
  const MONTHS = ['Ocak','Şubat','Mart','Nisan','Mayıs','Haziran','Temmuz','Ağustos','Eylül','Ekim','Kasım','Aralık'];
  ms.innerHTML = MONTHS.map((m,i)=>`<option value="${i+1}" ${i+1===now.getMonth()+1?'selected':''}>${m}</option>`).join('');
  const ys = document.getElementById('monthly-year-sel');
  const y = now.getFullYear();
  ys.innerHTML = [y-1,y,y+1].map(yr=>`<option value="${yr}" ${yr===y?'selected':''}>${yr}</option>`).join('');
}
async function exportAdminMonthly() {
  const userId = document.getElementById('monthly-user-sel').value;
  const month  = document.getElementById('monthly-month-sel').value;
  const year   = document.getElementById('monthly-year-sel').value;
  if (!userId) { toast('Önce personel seçin.', 'warn'); return; }
  try {
    const res = await fetch(API_BASE + `/api/Users/${userId}/attendance-summary/export?year=${year}&month=${month}`, {
      headers: { 'Authorization': 'Bearer ' + authToken }
    });
    if (!res.ok) throw new Error('İndirme başarısız');
    const blob = await res.blob();
    const url  = URL.createObjectURL(blob);
    const user = allUsers.find(u => u.id === +userId);
    const safeName = (user?.fullName || 'personel').replace(/[^a-zA-Z0-9çğıöşüÇĞİÖŞÜ]/g, '_');
    const a    = document.createElement('a');
    a.href = url;
    a.download = `${safeName}-${year}-${String(month).padStart(2,'0')}.csv`;
    document.body.appendChild(a); a.click(); a.remove();
    URL.revokeObjectURL(url);
    toast(t('toast.report_downloading'), 'ok');
  } catch(e) { toast(e.message, 'err'); }
}

async function loadMonthlySummary() {
  const userId = document.getElementById('monthly-user-sel').value;
  const month = document.getElementById('monthly-month-sel').value;
  const year = document.getElementById('monthly-year-sel').value;
  try {
    const s = await api('GET', `/api/Users/${userId}/attendance-summary?year=${year}&month=${month}`);
    const hr2 = t('badge.hour_short');
    document.getElementById('monthly-result').innerHTML = `
      <div class="stat-grid" style="margin-top:20px">
        ${[
          [t('monthly.present_days'), s.presentDays,        ICONS.checkin,'icon-green'],
          [t('mydash.stat_leave'),    s.leaveDays,          ICONS.palm,   'icon-amber'],
          [t('monthly.absent_days'),  s.absentDays,         ICONS.cross,  'icon-red'],
          [t('monthly.with_report'),  s.absentWithReport,   ICONS.clipboard,'icon-cyan'],
          [t('monthly.no_report'),    s.absentWithoutReport,ICONS.cross,  'icon-amber'],
          [t('monthly.total_hours'),  s.totalWorkedHours+hr2,ICONS.trend, 'icon-blue'],
          [t('monthly.ot_hours'),     s.totalOvertimeHours+hr2,ICONS.trend,'icon-violet'],
          [t('monthly.ot_count'),     s.overtimeShiftCount, ICONS.calendar,'icon-blue'],
        ].map(([lbl,val,icon,cls])=>`
          <div class="stat-card">
            <div class="stat-icon ${cls}">${icon}</div>
            <div class="stat-val">${val}</div>
            <div class="stat-lbl">${lbl}</div>
          </div>`).join('')}
      </div>`;
  } catch(e) { toast(e.message,'err'); }
}

// ── Face Enrollment (encrypted backend) ─────────────────────────────
async function ensureModels() {
  if (modelsLoaded) return;
  const MODEL_URL = 'https://cdn.jsdelivr.net/gh/justadudewhohacks/face-api.js@0.22.2/weights';
  await Promise.all([
    faceapi.nets.tinyFaceDetector.loadFromUri(MODEL_URL),
    faceapi.nets.faceLandmark68TinyNet.loadFromUri(MODEL_URL),
    faceapi.nets.faceRecognitionNet.loadFromUri(MODEL_URL),
  ]);
  modelsLoaded = true;
}
async function loadEnrolledFaces() {
  try {
    const data = await api('GET', '/api/FaceData');
    enrolledFaces = (data || []).map(f => ({
      userId: f.userId, name: f.userFullName, photo: f.userPhoto,
      descriptor: new Float32Array(f.descriptor), enrolledAt: f.enrolledAt
    }));
  } catch(e) { console.warn('Yüz verisi yüklenemedi:', e.message); enrolledFaces = []; }
}
async function loadEnrList() {
  await loadEnrolledFaces();
  const el = document.getElementById('enr-list');
  if (!el) return;
  document.getElementById('enr-count').textContent = enrolledFaces.length;
  const sel = document.getElementById('enr-user-select');
  if (sel) sel.innerHTML = '<option value="">— Personel Seçin —</option>' +
    allUsers.map(u => `<option value="${u.id}">${u.fullName}</option>`).join('');
  if (!enrolledFaces.length) {
    el.innerHTML = '<div class="empty">Henüz kayıtlı yüz yok.</div>';
    return;
  }
  el.innerHTML = enrolledFaces.map(f => `
    <div class="el-it">
      ${avatar(f.name, f.photo, 40)}
      <div class="el-info">
        <strong>${f.name}</strong>
        <small>Kayıt: ${f.enrolledAt ? fmtDate(f.enrolledAt) : '—'}</small>
      </div>
      <button class="el-del" onclick="delEnr(${f.userId})" title="Sil">✕</button>
    </div>`).join('');
}
async function startEnrCam() {
  try {
    await ensureModels();
    enrStream = await navigator.mediaDevices.getUserMedia({video:{facingMode:'user'}});
    const vid = document.getElementById('enr-video');
    vid.srcObject = enrStream; await vid.play();
    document.getElementById('enr-start-btn').classList.add('hidden');
    document.getElementById('enr-stop-btn').classList.remove('hidden');
    document.getElementById('enr-capture-btn').classList.remove('hidden');
    const canvas = document.getElementById('enr-canvas');
    canvas.width = vid.videoWidth || 320; canvas.height = vid.videoHeight || 240;
    enrInterval = setInterval(async () => {
      const det = await faceapi.detectSingleFace(vid, new faceapi.TinyFaceDetectorOptions())
        .withFaceLandmarks(true).withFaceDescriptor();
      const ctx = canvas.getContext('2d'); ctx.clearRect(0,0,canvas.width,canvas.height);
      if (det) { faceapi.draw.drawDetections(canvas,[det]);
        document.getElementById('enr-status').textContent = '✅ Yüz algılandı — kaydetmeye hazır.'; }
      else { document.getElementById('enr-status').textContent = '⏳ Yüz aranıyor…'; }
    }, 200);
  } catch(e) { toast('Kamera açılamadı: '+e.message,'err'); }
}
async function captureEnroll() {
  const uid = +document.getElementById('enr-user-select').value;
  if (!uid) { toast('Önce personel seçin.','err'); return; }
  const vid = document.getElementById('enr-video');
  const det = await faceapi.detectSingleFace(vid, new faceapi.TinyFaceDetectorOptions())
    .withFaceLandmarks(true).withFaceDescriptor();
  if (!det) { toast('Yüz algılanamadı.','err'); return; }
  try {
    await api('POST', '/api/FaceData', { userId: uid, descriptor: Array.from(det.descriptor) });
    toast('Yüz verisi güvenli şekilde kaydedildi ✓');
    stopEnrCam(); await loadEnrList();
  } catch(e) { toast('Kayıt hatası: '+e.message,'err'); }
}
async function delEnr(userId) {
  if (!confirm('Bu yüz verisi silinecek. Onaylıyor musunuz?')) return;
  try { await api('DELETE', `/api/FaceData/${userId}`); toast('Yüz verisi silindi.'); await loadEnrList(); }
  catch(e) { toast(e.message,'err'); }
}
function stopEnrCam() {
  enrStream?.getTracks().forEach(t=>t.stop()); enrStream = null;
  clearInterval(enrInterval); enrInterval = null;
  const vid = document.getElementById('enr-video'); if (vid) vid.srcObject = null;
  const c = document.getElementById('enr-canvas'); if (c) c.getContext('2d').clearRect(0,0,c.width,c.height);
  document.getElementById('enr-start-btn').classList.remove('hidden');
  document.getElementById('enr-stop-btn').classList.add('hidden');
  document.getElementById('enr-capture-btn').classList.add('hidden');
  document.getElementById('enr-status').textContent = '';
}

// Devam takip kameraları kaldırıldı — giriş/çıkış işlemleri /kiosk üzerinden yapılır.
// Yüz kaydı (enroll) admin için "Yüz Kaydı" sayfasında olmaya devam eder.

// ── Modal helpers ───────────────────────────────────────────────────
function closeModal(id) { document.getElementById(id).classList.add('hidden'); }
document.querySelectorAll('.modal-overlay').forEach(m => {
  m.addEventListener('click', e => { if (e.target===m) m.classList.add('hidden'); });
});
function populateUserSelects() {
  ['shift-user-sel','monthly-user-sel'].forEach(id => {
    const el = document.getElementById(id);
    if (el) el.innerHTML = allUsers.map(u=>`<option value="${u.id}">${u.fullName}</option>`).join('');
  });
}

// ════════════════════════════════════════════════════════════════
// MESAİ TALEPLERİ (Overtime Requests)
// ════════════════════════════════════════════════════════════════
async function loadMyOvertime() {
  try {
    const items = await api('GET', '/api/Overtime/my');
    const tbody = document.getElementById('my-overtime-tbody');
    tbody.innerHTML = items.length ? items.map(o => `
      <tr>
        <td class="font-mono">${fmtDate(o.date)}</td>
        <td><span class="shift-chip sm" style="background:${o.shiftColor}">${esc(o.shiftName)}</span></td>
        <td class="font-mono">${o.shiftStartTime}–${o.shiftEndTime}</td>
        <td>${esc(o.reason || '—')}</td>
        <td>${overtimeStatusBadge(o.status)}</td>
        <td class="text-sub">${fmtDate(o.createdAt)}</td>
      </tr>`).join('') : '<tr><td colspan="6" class="empty">Henüz mesai talebin yok.</td></tr>';
  } catch(e) { toast(e.message, 'err'); }
}

function openOvertimeModal() {
  // Varsayılan: yarın
  const tomorrow = new Date(); tomorrow.setDate(tomorrow.getDate()+1);
  document.getElementById('ot-date').value   = fmtDateOnly(tomorrow);
  document.getElementById('ot-shift').value  = '7';
  document.getElementById('ot-reason').value = '';
  document.getElementById('overtime-modal').classList.remove('hidden');
}

async function submitOvertimeRequest() {
  const body = {
    date:    document.getElementById('ot-date').value,
    shiftId: +document.getElementById('ot-shift').value,
    reason:  document.getElementById('ot-reason').value || null
  };
  if (!body.date) { toast('Tarih seçin.', 'warn'); return; }
  try {
    await api('POST', '/api/Overtime', body);
    toast('Mesai talebin gönderildi.', 'ok');
    closeModal('overtime-modal');
    loadMyOvertime();
  } catch(e) { toast(e.message, 'err'); }
}

async function loadAdminOvertime() {
  const status = document.getElementById('overtime-filter')?.value || '';
  try {
    const items = await api('GET', `/api/Overtime${status?`?status=${status}`:''}`);
    const tbody = document.getElementById('overtime-admin-tbody');
    tbody.innerHTML = items.length ? items.map(o => `
      <tr>
        <td><div class="name-cell">${avatar(o.userName, null)}<span>${esc(o.userName)}</span></div></td>
        <td class="font-mono">${fmtDate(o.date)}</td>
        <td><span class="shift-chip sm" style="background:${o.shiftColor}">${esc(o.shiftName)}</span></td>
        <td class="font-mono">${o.shiftStartTime}–${o.shiftEndTime}</td>
        <td>${esc(o.reason || '—')}</td>
        <td>${overtimeStatusBadge(o.status)}</td>
        <td class="text-right">${o.status==='Pending'?`
          <div class="btn-group" style="justify-content:flex-end">
            <button class="btn btn-sm" style="background:var(--ok-soft);color:var(--ok)" onclick="approveOvertime(${o.id})">Onayla</button>
            <button class="btn btn-sm" style="background:var(--err-soft);color:var(--err)" onclick="rejectOvertime(${o.id})">Reddet</button>
          </div>`:'—'}</td>
      </tr>`).join('') : '<tr><td colspan="7" class="empty">Kayıt yok.</td></tr>';
  } catch(e) { toast(e.message, 'err'); }
}

async function approveOvertime(id) {
  if (!confirm('Mesai talebini onaylıyor musunuz? Onaylanan talep otomatik olarak vardiya planına eklenir.')) return;
  try { await api('POST', `/api/Overtime/${id}/approve`); toast('Onaylandı, vardiyaya eklendi.', 'ok'); loadAdminOvertime(); }
  catch(e) { toast(e.message, 'err'); }
}
async function rejectOvertime(id) {
  if (!confirm('Mesai talebini reddetmek istiyor musunuz?')) return;
  try { await api('POST', `/api/Overtime/${id}/reject`); toast('Reddedildi.'); loadAdminOvertime(); }
  catch(e) { toast(e.message, 'err'); }
}

function overtimeStatusBadge(s) {
  const map = { Pending: 'badge-warn', Approved: 'badge-ok', Rejected: 'badge-err' };
  const lbl = { Pending: 'Bekliyor', Approved: 'Onaylandı', Rejected: 'Reddedildi' };
  return `<span class="badge ${map[s]||''}">${lbl[s]||s}</span>`;
}

// ════════════════════════════════════════════════════════════════
// VARDİYA DEĞİŞİM TALEPLERİ (Shift Swap)
// ════════════════════════════════════════════════════════════════
let currentSwapTab = 'outgoing';
let currentSwapMode = 'direct';

function switchSwapTab(tab) {
  currentSwapTab = tab;
  document.querySelectorAll('.swap-tab').forEach(t => t.classList.toggle('active', t.dataset.swapTab === tab));
  const tableCard = document.getElementById('my-swap-table-card');
  const openWrap  = document.getElementById('open-swap-grid-wrap');
  if (tab === 'open') {
    tableCard?.classList.add('hidden');
    openWrap?.classList.remove('hidden');
    loadOpenListings();
  } else {
    openWrap?.classList.add('hidden');
    tableCard?.classList.remove('hidden');
    loadMySwaps(tab);
  }
}

function switchSwapMode(mode) {
  currentSwapMode = mode;
  document.querySelectorAll('#swap-modal [data-swap-mode]').forEach(b =>
    b.classList.toggle('active', b.dataset.swapMode === mode));
  document.getElementById('swap-direct-fields').classList.toggle('hidden', mode !== 'direct');
  document.getElementById('swap-open-fields').classList.toggle('hidden', mode !== 'open');
}

async function loadMySwaps(tab) {
  tab = tab || currentSwapTab;
  try {
    const url = tab === 'outgoing' ? '/api/ShiftSwap/my-outgoing' : '/api/ShiftSwap/my-incoming';
    const items = await api('GET', url);
    const tbody = document.getElementById('my-swap-tbody');
    tbody.innerHTML = items.length ? items.map(s => {
      const isOutgoing = s.requesterId === currentUser.userId;
      const otherName  = isOutgoing ? (s.targetUserName || t('swap.s.Open')) : s.requesterName;
      const otherUid   = isOutgoing ? s.targetUserId : s.requesterId;
      const otherUser  = otherUid ? allUsers.find(u => u.id === otherUid) : null;
      const otherCell  = otherUid
        ? `<div class="name-cell">${avatar(otherName, otherUser?.photoBase64)}<span>${esc(otherName)}</span></div>`
        : `<span class="text-sub">${esc(otherName)}</span>`;
      const reqShift   = `${fmtDate(s.requesterDate)} · ${shiftNameById(s.requesterShiftId, s.requesterShiftName)}`;
      const tgtShift   = s.targetDate ? `${fmtDate(s.targetDate)} · ${shiftNameById(s.targetShiftId, s.targetShiftName)}`
                        : (s.desiredShiftId ? `→ ${shiftNameById(s.desiredShiftId, s.desiredShiftName)}` : '—');
      const myShift    = isOutgoing ? reqShift : tgtShift;
      const theirShift = isOutgoing ? tgtShift : reqShift;

      let actions = '—';
      if (!isOutgoing && s.status === 'Pending') {
        actions = `<div class="btn-group" style="justify-content:flex-end">
          <button class="btn btn-sm" style="background:var(--ok-soft);color:var(--ok)" onclick="respondSwap(${s.id},'Accept')">${t('swap.accept')}</button>
          <button class="btn btn-sm" style="background:var(--err-soft);color:var(--err)" onclick="respondSwap(${s.id},'Reject')">${t('common.reject')}</button>
        </div>`;
      } else if (isOutgoing && (s.status === 'Open' || s.status === 'Pending' || s.status === 'AcceptedByTarget')) {
        actions = `<button class="btn btn-sm btn-ghost" onclick="cancelSwap(${s.id})">${t('common.cancel')}</button>`;
      }
      return `<tr>
        <td>${esc(myShift)}</td>
        <td>${otherCell}</td>
        <td>${esc(theirShift)}</td>
        <td>${swapStatusBadge(s.status)}</td>
        <td class="text-right">${actions}</td>
      </tr>`;
    }).join('') : `<tr><td colspan="5" class="empty">${tab==='outgoing'?t('swap.no_outgoing'):t('swap.no_incoming')}</td></tr>`;
  } catch(e) { toast(e.message, 'err'); }
}

async function loadOpenListings() {
  const wrap = document.getElementById('open-swap-grid-wrap');
  try {
    const items = await api('GET', '/api/ShiftSwap/open');
    if (!items.length) {
      wrap.innerHTML = `<div class="card"><div class="empty">${t('swap.no_open')}</div></div>`;
      return;
    }
    wrap.innerHTML = `<div class="dept-grid">${items.map(s => {
      const reqShift     = shiftNameById(s.requesterShiftId, s.requesterShiftName);
      const desired      = s.desiredShiftId ? shiftNameById(s.desiredShiftId, s.desiredShiftName) : t('swap.any_shift');
      const reqUser      = allUsers.find(u => u.id === s.requesterId);
      return `<div class="card open-swap-card">
        <div class="card-head" style="border:0;padding-bottom:8px;gap:10px">
          <div class="name-cell">${avatar(s.requesterName, reqUser?.photoBase64, 32)}<strong>${esc(s.requesterName)}</strong></div>
          <span class="badge badge-warn">${t('swap.s.Open')}</span>
        </div>
        <div style="padding:0 16px 14px">
          <div class="open-swap-row">
            <small class="text-sub">${t('swap.col_my')}</small>
            <div class="ms-chip" style="background:${s.requesterShiftColor};margin-top:4px">${esc(reqShift)}</div>
            <div class="font-mono text-sub" style="font-size:12px;margin-top:4px">${fmtDate(s.requesterDate)}</div>
          </div>
          <div class="open-swap-row" style="margin-top:10px">
            <small class="text-sub">${t('swap.wants')}</small>
            <div class="ms-chip" style="background:${s.desiredShiftColor || '#6B6B72'};margin-top:4px">${esc(desired)}</div>
          </div>
          ${s.reason ? `<p style="margin-top:10px;font-size:13px;color:var(--text-2)">"${esc(s.reason)}"</p>` : ''}
          <div class="open-swap-foot" style="margin-top:14px;display:flex;justify-content:space-between;align-items:center">
            <small class="text-sub">${t('swap.posted')}: ${fmtDate(s.createdAt)}</small>
            <button class="btn btn-primary btn-sm" onclick="claimOpenSwap(${s.id})">${t('swap.claim')}</button>
          </div>
        </div>
      </div>`;
    }).join('')}</div>`;
  } catch(e) { toast(e.message, 'err'); }
}

async function openSwapModal() {
  // 30 günlük vardiyalarımı çek
  const today = new Date();
  const week  = new Date(); week.setDate(today.getDate()+30);
  try {
    const myShifts = await api('GET', `/api/Shifts/my?from=${fmtDateOnly(today)}&to=${fmtDateOnly(week)}`);
    const future   = (myShifts || []).filter(s => new Date(s.date) >= new Date(new Date().toDateString()));
    if (!future.length) { toast(t('swap.no_future'), 'warn'); return; }

    document.getElementById('swap-my-shift').innerHTML = future.map(s =>
      `<option value="${s.id}">${fmtDate(s.date)} · ${shiftNameById(s.shiftId, s.shiftName)} (${s.startTime}–${s.endTime})</option>`).join('');

    // Diğer personel listesi (kendisi hariç + aynı departman)
    const others = allUsers.filter(u => u.id !== currentUser.userId && u.departmentId === currentUser.departmentId);
    document.getElementById('swap-target-user').innerHTML = others.map(u =>
      `<option value="${u.id}">${esc(u.fullName)}</option>`).join('');

    document.getElementById('swap-target-shift').innerHTML = `<option value="">${t('swap.cover_default')}</option>`;
    document.getElementById('swap-reason').value = '';

    // Açık ilan için "geçmek istediği vardiya türü" listesi (sadece shift kategorisi)
    const sel = document.getElementById('swap-desired-shift');
    if (sel) {
      sel.innerHTML = `<option value="">${t('swap.desired_any')}</option>` +
        SHIFT_TYPES.filter(x => x.cat === 'shift').map(x =>
          `<option value="${x.id}">${esc(shiftTypeName(x))} (${x.startTime}–${x.endTime})</option>`).join('');
    }

    // Varsayılan mod = direct
    switchSwapMode('direct');

    // İlk seçili kullanıcının vardiyalarını yükle
    if (others.length) await loadSwapTargetShifts();

    document.getElementById('swap-modal').classList.remove('hidden');
  } catch(e) { toast(e.message, 'err'); }
}

async function loadSwapTargetShifts() {
  const targetUserId = +document.getElementById('swap-target-user').value;
  if (!targetUserId) return;
  const today = new Date();
  const week  = new Date(); week.setDate(today.getDate()+30);
  try {
    const shifts = await api('GET', `/api/Shifts/user/${targetUserId}?from=${fmtDateOnly(today)}&to=${fmtDateOnly(week)}`);
    const future = (shifts || []).filter(s => new Date(s.date) >= new Date(new Date().toDateString()));
    document.getElementById('swap-target-shift').innerHTML = `<option value="">${t('swap.cover_default')}</option>` +
      future.map(s => `<option value="${s.id}">${fmtDate(s.date)} · ${shiftNameById(s.shiftId, s.shiftName)} (${s.startTime}–${s.endTime})</option>`).join('');
  } catch(_) { /* sessiz */ }
}

async function submitSwapRequest() {
  const requesterShiftAssignmentId = +document.getElementById('swap-my-shift').value;
  if (!requesterShiftAssignmentId) { toast(t('toast.shift_pick_type'), 'warn'); return; }
  const reason = document.getElementById('swap-reason').value || null;

  try {
    if (currentSwapMode === 'open') {
      const desiredShiftId = +document.getElementById('swap-desired-shift').value || null;
      await api('POST', '/api/ShiftSwap/open', { requesterShiftAssignmentId, desiredShiftId, reason });
      toast(t('swap.open_listed'), 'ok');
      closeModal('swap-modal');
      switchSwapTab('outgoing');
    } else {
      const body = {
        requesterShiftAssignmentId,
        targetUserId:            +document.getElementById('swap-target-user').value,
        targetShiftAssignmentId: +document.getElementById('swap-target-shift').value || null,
        reason
      };
      if (!body.targetUserId) { toast(t('toast.shift_pick_type'), 'warn'); return; }
      await api('POST', '/api/ShiftSwap', body);
      toast(t('swap.sent'), 'ok');
      closeModal('swap-modal');
      switchSwapTab('outgoing');
    }
  } catch(e) { toast(e.message, 'err'); }
}

async function claimOpenSwap(id) {
  if (!confirm(t('swap.claim_confirm'))) return;
  try {
    await api('POST', `/api/ShiftSwap/${id}/claim`);
    toast(t('swap.claimed'), 'ok');
    loadOpenListings();
    refreshNotifications();
  } catch(e) { toast(e.message, 'err'); }
}

async function respondSwap(id, response) {
  if (response === 'Reject' && !confirm(t('swap.reject_confirm'))) return;
  if (response === 'Accept' && !confirm(t('swap.accept_confirm'))) return;
  try {
    await api('POST', `/api/ShiftSwap/${id}/respond`, { response });
    toast(response === 'Accept' ? t('swap.accepted_admin') : t('swap.rejected'), 'ok');
    loadMySwaps(currentSwapTab);
    refreshNotifications();
  } catch(e) { toast(e.message, 'err'); }
}

async function cancelSwap(id) {
  if (!confirm(t('swap.cancel_confirm'))) return;
  try {
    await api('DELETE', `/api/ShiftSwap/${id}`);
    toast(t('swap.canceled'));
    loadMySwaps(currentSwapTab);
  } catch(e) { toast(e.message, 'err'); }
}

async function loadAdminSwaps() {
  const status = document.getElementById('swap-filter')?.value || '';
  try {
    const items = await api('GET', `/api/ShiftSwap${status?`?status=${status}`:''}`);
    const tbody = document.getElementById('swap-admin-tbody');
    tbody.innerHTML = items.length ? items.map(s => {
      const reqShift = `${fmtDate(s.requesterDate)} · ${shiftNameById(s.requesterShiftId, s.requesterShiftName)}`;
      const tgtShift = s.targetDate ? `${fmtDate(s.targetDate)} · ${shiftNameById(s.targetShiftId, s.targetShiftName)}` : t('swap.one_way');
      const tgtName  = s.targetUserName || t('swap.s.Open');
      const reqUser  = allUsers.find(u => u.id === s.requesterId);
      const tgtUser  = s.targetUserId ? allUsers.find(u => u.id === s.targetUserId) : null;
      const tgtCell  = s.targetUserId
        ? `<div class="name-cell">${avatar(tgtName, tgtUser?.photoBase64)}<span>${esc(tgtName)}</span></div>`
        : `<span class="badge badge-warn">${t('swap.s.Open')}</span>`;
      let actions = '—';
      if (s.status === 'AcceptedByTarget') {
        actions = `<div class="btn-group" style="justify-content:flex-end">
          <button class="btn btn-sm" style="background:var(--ok-soft);color:var(--ok)" onclick="approveSwap(${s.id})">${t('common.approve')}</button>
          <button class="btn btn-sm" style="background:var(--err-soft);color:var(--err)" onclick="rejectSwap(${s.id})">${t('common.reject')}</button>
        </div>`;
      } else if (s.status === 'Pending') {
        actions = `<small class="text-sub">${t('swap.waiting_emp')}</small>`;
      } else if (s.status === 'Open') {
        actions = `<small class="text-sub">${t('swap.s.Open')}</small>`;
      }
      return `<tr>
        <td><div class="name-cell">${avatar(s.requesterName, reqUser?.photoBase64)}<span>${esc(s.requesterName)}</span></div></td>
        <td>${esc(reqShift)}</td>
        <td>${tgtCell}</td>
        <td>${esc(tgtShift)}</td>
        <td>${swapStatusBadge(s.status)}</td>
        <td class="text-right">${actions}</td>
      </tr>`;
    }).join('') : `<tr><td colspan="6" class="empty">${t('common.empty')}</td></tr>`;
  } catch(e) { toast(e.message, 'err'); }
}

async function approveSwap(id) {
  if (!confirm(t('swap.approve_confirm'))) return;
  try { await api('POST', `/api/ShiftSwap/${id}/approve`); toast(t('swap.approved'), 'ok'); loadAdminSwaps(); refreshNotifications(); }
  catch(e) { toast(e.message, 'err'); }
}
async function rejectSwap(id) {
  if (!confirm(t('swap.reject_admin_confirm'))) return;
  try { await api('POST', `/api/ShiftSwap/${id}/reject`); toast(t('swap.rejected')); loadAdminSwaps(); refreshNotifications(); }
  catch(e) { toast(e.message, 'err'); }
}

function swapStatusBadge(s) {
  const map = {
    Open:                { cls:'badge-warn', key:'swap.s.Open' },
    Pending:             { cls:'badge-warn', key:'swap.s.Pending' },
    AcceptedByTarget:    { cls:'badge-info', key:'swap.s.AcceptedByTarget' },
    RejectedByTarget:    { cls:'badge-err',  key:'swap.s.RejectedByTarget' },
    ApprovedByAdmin:     { cls:'badge-ok',   key:'swap.s.ApprovedByAdmin' },
    RejectedByAdmin:     { cls:'badge-err',  key:'swap.s.RejectedByAdmin' },
    CancelledByRequester:{ cls:'badge-emp',  key:'swap.s.CancelledByRequester' },
  };
  const x = map[s];
  return x ? `<span class="badge ${x.cls}">${t(x.key)}</span>` : `<span class="badge">${s}</span>`;
}

// ── Init ────────────────────────────────────────────────────────────
tryRestoreSession();
