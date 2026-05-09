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

// Camera streams
let faceStream = null, coStream = null, enrStream = null;
let faceInterval = null, coInterval = null, enrInterval = null;
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
  if (photo) return `<div class="av-init" style="width:${size}px;height:${size}px"><img src="${photo}" alt="" style="width:100%;height:100%;object-fit:cover"></div>`;
  const initials = (name||'?').split(' ').map(w=>w[0]).join('').slice(0,2).toUpperCase();
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
function applyTheme(t) {
  document.documentElement.setAttribute('data-theme', t);
  localStorage.setItem('sx_theme', t);
  const lbl = document.getElementById('theme-label');
  if (lbl) lbl.textContent = t === 'dark' ? 'Karanlık' : 'Açık';
}
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

async function doLogin() {
  const email = document.getElementById('login-email').value.trim();
  const pass  = document.getElementById('login-pass').value;
  const errEl = document.getElementById('login-error');
  errEl.classList.add('hidden');
  if (!email || !pass) { errEl.textContent = 'E-posta ve şifre zorunludur.'; errEl.classList.remove('hidden'); return; }
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
    errEl.textContent = e.message || 'Giriş başarısız.';
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
  [faceStream, coStream, enrStream].forEach(s => s?.getTracks().forEach(t=>t.stop()));
  [faceInterval, coInterval, enrInterval].forEach(i => i && clearInterval(i));
  faceStream = coStream = enrStream = null;
}

// ── App start ───────────────────────────────────────────────────────
async function startApp() {
  document.getElementById('login-screen').classList.add('hidden');
  document.getElementById('app').classList.remove('hidden');
  buildNav();
  updateTopbarUser();
  await Promise.all([loadAllUsers(), loadDepts()]);
  const isAdmin = currentUser.role === 'Admin';
  navTo(isAdmin ? 'dashboard' : 'my-shifts');
}

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
  { section: 'Genel' },
  { id: 'dashboard',   label: 'Dashboard',         icon: ICONS.dashboard },
  { section: 'Personel' },
  { id: 'employees',   label: 'Personel',          icon: ICONS.users },
  { id: 'departments', label: 'Departmanlar',      icon: ICONS.building },
  { section: 'Operasyon' },
  { id: 'roster',      label: 'Vardiya Planlama',  icon: ICONS.calendar },
  { id: 'attendance',  label: 'Devam Takip',       icon: ICONS.check },
  { id: 'leaves',      label: 'İzin Yönetimi',     icon: ICONS.clipboard },
  { section: 'Raporlar' },
  { id: 'enroll',      label: 'Yüz Kaydı',         icon: ICONS.scan },
  { id: 'monthly',     label: 'Aylık Rapor',       icon: ICONS.chart },
];

const NAV_EMP = [
  { section: 'Vardiyalarım' },
  { id: 'my-shifts',     label: 'Vardiyalarım',       icon: ICONS.calendar },
  { id: 'my-attendance', label: 'Devam Durumum',      icon: ICONS.check },
  { id: 'my-leaves',     label: 'İzin Taleplerim',    icon: ICONS.clipboard },
];

function buildNav() {
  const items = currentUser.role === 'Admin' ? NAV_ADMIN : NAV_EMP;
  const html = items.map(it => {
    if (it.section) return `<div class="nav-section">${it.section}</div>`;
    return `<a class="nav-link" data-page="${it.id}" onclick="navTo('${it.id}')">${it.icon}<span>${it.label}</span></a>`;
  }).join('');
  document.getElementById('sidebar-nav').innerHTML = html;
}

const PAGE_TITLES = {
  'dashboard': 'Dashboard', 'employees': 'Personel', 'roster': 'Vardiya Planlama',
  'attendance': 'Devam Takip', 'leaves': 'İzin Yönetimi', 'my-leaves': 'İzin Taleplerim',
  'my-shifts': 'Vardiyalarım', 'my-attendance': 'Devam Durumum', 'profile': 'Profilim',
  'enroll': 'Yüz Kaydı', 'monthly': 'Aylık Rapor', 'departments': 'Departmanlar'
};

function navTo(id) { showPage(id); }

function showPage(id) {
  document.querySelectorAll('.page').forEach(p => p.classList.add('hidden'));
  const el = document.getElementById(`page-${id}`);
  if (el) el.classList.remove('hidden');
  document.querySelectorAll('.nav-link').forEach(a =>
    a.classList.toggle('active', a.dataset.page === id));
  document.getElementById('topbar-title').textContent = PAGE_TITLES[id] || '';
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
  }
}

// ── Topbar User ─────────────────────────────────────────────────────
function updateTopbarUser() {
  const u = currentUser;
  document.getElementById('topbar-avatar').innerHTML = avatar(u.fullName, u.photoBase64, 32);
  document.getElementById('topbar-name').textContent = u.fullName;
  document.getElementById('topbar-role').textContent = u.role === 'Admin' ? 'Yönetici' : 'Personel';
  document.getElementById('dropdown-header').innerHTML =
    `<strong>${u.fullName}</strong>${u.email}`;
}

// ── Dashboard ───────────────────────────────────────────────────────
async function loadDashboard() {
  // Hero
  const heroTitle = `Hoş geldin, ${currentUser.fullName.split(' ')[0]} 👋`;
  document.getElementById('hero-title').textContent = heroTitle;
  document.getElementById('hero-date').textContent =
    new Date().toLocaleDateString('tr-TR', { day:'numeric', month:'long', year:'numeric', weekday:'long' });

  try {
    const [stats, today] = await Promise.all([
      api('GET','/api/Attendance/dashboard'),
      api('GET','/api/Attendance/today')
    ]);

    const sg = document.getElementById('stat-grid');
    sg.innerHTML = [
      {label:'Toplam Personel', val: stats.totalActiveEmployees, icon: ICONS.team,    cls:'icon-blue'},
      {label:'Bugün Giriş',     val: stats.presentToday,         icon: ICONS.checkin, cls:'icon-green'},
      {label:'İzinli',          val: stats.onLeaveToday,         icon: ICONS.palm,    cls:'icon-amber'},
      {label:'Devamsız',        val: stats.absentToday,          icon: ICONS.cross,   cls:'icon-red'},
      {label:'Bekleyen İzin',   val: stats.pendingLeaveRequests, icon: ICONS.pending, cls:'icon-cyan'},
      {label:'Devam Oranı',     val: stats.attendanceRate+'%',   icon: ICONS.trend,   cls:'icon-violet'},
    ].map(s => `
      <div class="stat-card">
        <div class="stat-icon ${s.cls}">${s.icon}</div>
        <div class="stat-val">${s.val}</div>
        <div class="stat-lbl">${s.label}</div>
      </div>
    `).join('');

    const pl = document.getElementById('present-list');
    document.getElementById('present-count').textContent = today.length;
    pl.innerHTML = today.length ? today.slice(0,8).map(a => `
      <div class="present-row">
        ${avatar(a.userFullName, a.userPhoto, 36)}
        <div class="pr-info">
          <strong>${a.userFullName}</strong>
          <small>Giriş: ${fmtTime(a.checkIn)}${a.checkOut ? ' · Çıkış: ' + fmtTime(a.checkOut) : ''}</small>
        </div>
        ${a.checkOut ? '<span class="badge badge-emp">Tamamlandı</span>' : '<span class="badge badge-on">Aktif</span>'}
      </div>`).join('')
    : '<div class="empty">Bugün henüz giriş yapılmadı.</div>';

    const pending = await api('GET','/api/Leaves?status=Pending');
    const pd = document.getElementById('pending-leaves-dash');
    pd.innerHTML = pending.length ? pending.slice(0,5).map(l => `
      <div class="leave-row">
        ${avatar(l.userFullName, null, 32)}
        <div style="flex:1">
          <strong>${l.userFullName}</strong>
          <span style="display:block">${l.leaveType} · ${l.totalDays} gün · ${fmtDate(l.startDate)}</span>
        </div>
        <span class="badge badge-warn">Bekliyor</span>
      </div>`).join('')
    : '<div class="empty">Bekleyen talep yok.</div>';
  } catch(e) { toast(e.message,'err'); }
}

// ── Personel ────────────────────────────────────────────────────────
async function loadAllUsers() {
  try {
    if (currentUser?.role === 'Admin') {
      const res = await api('GET', `/api/Users?page=1&pageSize=200`);
      allUsers = res.items || [];
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
      <tr>
        <td><div class="name-cell">${avatar(u.fullName, u.photoBase64)}<span>${u.fullName}</span></div></td>
        <td>${u.email}</td>
        <td>${u.departmentName||'—'}</td>
        <td>${u.position||'—'}</td>
        <td><span class="badge ${u.role==='Admin'?'badge-admin':'badge-emp'}">${u.role==='Admin'?'Yönetici':'Personel'}</span></td>
        <td class="text-right">
          <div class="btn-group" style="justify-content:flex-end">
            <button class="btn btn-ghost btn-sm" onclick="openEmpModal(${u.id})">Düzenle</button>
            <button class="btn btn-sm" style="background:var(--err-soft);color:var(--err)" onclick="deleteEmployee(${u.id},'${u.fullName.replace(/'/g,"\\'")}')">Sil</button>
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

  const deptSel = document.getElementById('emp-dept');
  deptSel.innerHTML = '<option value="">— Departman Seçin —</option>' +
    allDepts.map(d=>`<option value="${d.id}" ${u?.departmentId===d.id?'selected':''}>${d.name}</option>`).join('');

  document.getElementById('emp-modal').classList.remove('hidden');
}
document.getElementById('add-emp-btn').addEventListener('click', () => openEmpModal());

async function saveEmployee() {
  const id = document.getElementById('emp-id').value;
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
      await api('PUT', `/api/Users/${id}`, body);
      toast('Personel güncellendi.');
    } else {
      body.password = pw;
      await api('POST', '/api/Users', body);
      toast('Personel eklendi.', 'ok');
    }
    closeModal('emp-modal');
    await loadAllUsers();
    loadEmployees();
  } catch(e) { toast(e.message,'err'); }
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
  const DAYS = ['Pzt','Sal','Çar','Per','Cum','Cmt','Paz'];
  const ws = rosterWeekStart;
  const we = new Date(ws); we.setDate(we.getDate()+6);
  document.getElementById('roster-week-label').textContent = `${fmtDate(ws)} – ${fmtDate(we)}`;

  try {
    const assignments = await api('GET', `/api/Shifts/weekly?weekStart=${fmtDateOnly(ws)}`);
    const userMap = {};
    assignments.forEach(a => {
      if (!userMap[a.userId]) userMap[a.userId] = { name: a.userFullName, photo: a.userPhoto, days: {} };
      userMap[a.userId].days[fmtDateOnly(a.date)] = a;
    });

    const head = document.getElementById('roster-head');
    head.innerHTML = '<th style="text-align:left">Personel</th>' +
      DAYS.map((d,i) => {
        const dt = new Date(ws); dt.setDate(dt.getDate()+i);
        return `<th>${d}<small>${fmtDate(dt)}</small></th>`;
      }).join('');

    const body = document.getElementById('roster-body');
    const rows = allUsers.map(u => {
      const days = userMap[u.id]?.days || {};
      const cells = Array.from({length:7},(_,i) => {
        const dt = new Date(ws); dt.setDate(dt.getDate()+i);
        const ds = fmtDateOnly(dt);
        const a = days[ds];
        const cell = a
          ? `<div class="shift-chip" style="background:${a.shiftColor}" onclick="openShiftModal('${ds}',${u.id},${a.id})">${a.shiftName}<small>${a.startTime}–${a.endTime}</small></div>`
          : `<div class="shift-empty" onclick="openShiftModal('${ds}',${u.id})">+</div>`;
        return `<td>${cell}</td>`;
      }).join('');
      return `<tr><td><div class="name-cell">${avatar(u.fullName,u.photoBase64)}<span>${u.fullName}</span></div></td>${cells}</tr>`;
    }).join('');
    body.innerHTML = rows || '<tr><td colspan="8" class="empty">Personel bulunamadı.</td></tr>';
  } catch(e) { toast(e.message,'err'); }
}
function rosterNav(d) { rosterWeekStart.setDate(rosterWeekStart.getDate()+d*7); loadRoster(); }

let pendingShiftDate = null, pendingShiftUserId = null;
async function openShiftModal(dateStr, userId, assignId) {
  pendingShiftDate = dateStr; pendingShiftUserId = userId;
  document.getElementById('shift-assign-id').value = assignId||'';
  const u = allUsers.find(x=>x.id===userId);
  document.getElementById('shift-cell-info').textContent = `${u?.fullName||''} • ${fmtDate(dateStr)}`;

  const shifts = await api('GET',`/api/Shifts/user/${userId}?from=${dateStr}&to=${dateStr}`).catch(()=>[]);
  const currentShiftId = shifts.length ? shifts[0].shiftId : null;

  const shiftTypes = [
    {id:1,name:'Sabah'},{id:2,name:'Öğleden Sonra'},{id:3,name:'Gece'},
    {id:4,name:'Tatil'},{id:5,name:'İzinli'},{id:6,name:'Part Time'},
    {id:7,name:'Sabah FM'},{id:8,name:'ÖS FM'},{id:9,name:'Gece FM'},
  ];
  document.getElementById('shift-type-sel').innerHTML =
    shiftTypes.map(s=>`<option value="${s.id}" ${s.id===currentShiftId?'selected':''}>${s.name}</option>`).join('');
  document.getElementById('shift-user-sel').innerHTML =
    allUsers.map(u2=>`<option value="${u2.id}" ${u2.id===userId?'selected':''}>${u2.fullName}</option>`).join('');
  document.getElementById('shift-note').value = shifts[0]?.note||'';
  document.getElementById('shift-delete-btn').classList.toggle('hidden', !assignId);
  document.getElementById('shift-modal').classList.remove('hidden');
}
async function saveShift() {
  const id = document.getElementById('shift-assign-id').value;
  const body = {
    userId:  +document.getElementById('shift-user-sel').value,
    shiftId: +document.getElementById('shift-type-sel').value,
    date:    pendingShiftDate,
    note:    document.getElementById('shift-note').value
  };
  try {
    if (id) await api('PUT', `/api/Shifts/${id}`, body);
    else    await api('POST','/api/Shifts',       body);
    closeModal('shift-modal');
    toast('Vardiya kaydedildi.');
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
  if (a.isLateArrival)    badges.push(`<span class="badge badge-warn">Geç +${a.lateMinutes}dk</span>`);
  if (a.isEarlyDeparture) badges.push(`<span class="badge badge-warn">Erken -${a.earlyMinutes}dk</span>`);
  if (a.isInvalidTime)    badges.push(`<span class="badge badge-err">Hatalı Saat</span>`);
  if (a.isShortDuration)  badges.push(`<span class="badge badge-err">Kısa Süre</span>`);
  if (!badges.length)     badges.push(`<span class="badge badge-ok">Normal</span>`);
  return `<tr>
    <td><div class="name-cell">${avatar(a.userFullName,a.userPhoto)}<span>${a.userFullName}</span></div></td>
    <td class="font-mono">${fmtTime(a.checkIn)}</td>
    <td class="font-mono">${a.checkOut?fmtTime(a.checkOut):'—'}</td>
    <td><span class="badge ${a.source==='FaceRecognition'?'badge-info':'badge-emp'}">${a.source==='FaceRecognition'?'Yüz Tanıma':'Manuel'}</span></td>
    <td class="font-mono">${a.workedHours!=null?a.workedHours.toFixed(1)+' sa':'—'}</td>
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
          <td>${a.source==='FaceRecognition'?'Yüz Tanıma':'Manuel'}</td>
          <td class="font-mono">${a.workedHours!=null?a.workedHours.toFixed(1)+' sa':'—'}</td>
          <td>${a.checkOut?'<span class="badge badge-ok">Tamamlandı</span>':'<span class="badge badge-on">Aktif</span>'}</td>
        </tr>`).join('')
      : '<tr><td colspan="5" class="empty">Bugün kayıt yok.</td></tr>';
  } catch(e) { toast(e.message,'err'); }
}
async function doCheckIn() { try { await api('POST','/api/Attendance/checkin'); toast('Giriş kaydedildi.'); loadMyAttendance(); } catch(e) { toast(e.message,'err'); } }
async function doCheckOut() { try { await api('POST','/api/Attendance/checkout'); toast('Çıkış kaydedildi.'); loadMyAttendance(); } catch(e) { toast(e.message,'err'); } }

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

// ── My Shifts ───────────────────────────────────────────────────────
async function loadMyShifts() {
  const DAYS = ['Pazartesi','Salı','Çarşamba','Perşembe','Cuma','Cumartesi','Pazar'];
  const ws = myShiftWeekStart;
  const we = new Date(ws); we.setDate(we.getDate()+6);
  document.getElementById('my-shift-label').textContent = `${fmtDate(ws)} – ${fmtDate(we)}`;
  try {
    const shifts = await api('GET', `/api/Shifts/my?from=${fmtDateOnly(ws)}&to=${fmtDateOnly(we)}`);
    document.getElementById('my-shift-tbody').innerHTML = shifts.length
      ? shifts.map(s => `<tr>
          <td class="font-mono">${fmtDate(s.date)}</td>
          <td>${DAYS[new Date(s.date).getDay()-1]||'—'}</td>
          <td><span class="shift-chip sm" style="background:${s.shiftColor}">${s.shiftName}</span></td>
          <td class="font-mono">${s.startTime}</td>
          <td class="font-mono">${s.endTime}</td>
        </tr>`).join('')
      : '<tr><td colspan="5" class="empty">Bu hafta vardiya atanmamış.</td></tr>';
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
    document.getElementById('profile-photo-role').textContent = u.role==='Admin'?'Yönetici':'Personel';

    document.getElementById('profile-info').innerHTML = `
      <table class="profile-table">
        <tr><th>E-posta</th><td>${u.email}</td></tr>
        <tr><th>Departman</th><td>${u.departmentName||'—'}</td></tr>
        <tr><th>Pozisyon</th><td>${u.position||'—'}</td></tr>
        <tr><th>Telefon</th><td>${u.phoneNumber||'—'}</td></tr>
        <tr><th>İşe Giriş</th><td>${u.hireDate?fmtDate(u.hireDate):'—'}</td></tr>
        <tr><th>Rol</th><td>${u.role==='Admin'?'Yönetici':'Personel'}</td></tr>
      </table>`;
  } catch(e) { toast(e.message,'err'); }
}
function handlePhoto(evt) {
  const file = evt.target.files[0];
  if (!file) return;
  if (file.size > 400_000) { toast('Fotoğraf 400 KB\'dan küçük olmalıdır.','err'); return; }
  const reader = new FileReader();
  reader.onload = async e => {
    try {
      await api('PUT', `/api/Users/${currentUser.userId}`, { photoBase64: e.target.result });
      currentUser.photoBase64 = e.target.result;
      sessionStorage.setItem('sx_user', JSON.stringify(currentUser));
      toast('Fotoğraf güncellendi.');
      loadProfile();
    } catch(ex) { toast(ex.message,'err'); }
  };
  reader.readAsDataURL(file);
}
async function saveProfile() {
  const pw = document.getElementById('new-pw').value;
  if (!pw) { toast('Şifre giriniz.','warn'); return; }
  if (pw.length < 6) { toast('Şifre en az 6 karakter olmalıdır.','err'); return; }
  try { await api('PUT', `/api/Users/${currentUser.userId}`, { newPassword: pw });
    document.getElementById('new-pw').value = ''; toast('Şifre güncellendi.'); }
  catch(e) { toast(e.message,'err'); }
}

// ── Departments ─────────────────────────────────────────────────────
async function loadDepts() {
  try { allDepts = await api('GET','/api/Departments'); return allDepts; }
  catch(e) { return []; }
}
function renderDepts() {
  document.getElementById('dept-tbody').innerHTML = allDepts.length
    ? allDepts.map(d => `<tr>
        <td class="font-mono">${d.id}</td>
        <td>${d.name}</td>
        <td class="text-right"><button class="btn btn-sm" style="background:var(--err-soft);color:var(--err)" onclick="deleteDept(${d.id},'${d.name.replace(/'/g,"\\'")}')">Sil</button></td>
      </tr>`).join('')
    : '<tr><td colspan="3" class="empty">Departman yok.</td></tr>';
}
function openDeptModal() {
  document.getElementById('dept-name').value = '';
  document.getElementById('dept-desc').value = '';
  document.getElementById('dept-modal').classList.remove('hidden');
}
async function saveDept() {
  const name = document.getElementById('dept-name').value.trim();
  const desc = document.getElementById('dept-desc').value.trim();
  if (!name) { toast('Departman adı zorunludur.','err'); return; }
  try { await api('POST','/api/Departments', { name, description: desc||null });
    toast('Departman eklendi.'); closeModal('dept-modal');
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
async function loadMonthlySummary() {
  const userId = document.getElementById('monthly-user-sel').value;
  const month = document.getElementById('monthly-month-sel').value;
  const year = document.getElementById('monthly-year-sel').value;
  try {
    const s = await api('GET', `/api/Users/${userId}/attendance-summary?year=${year}&month=${month}`);
    document.getElementById('monthly-result').innerHTML = `
      <div class="stat-grid" style="margin-top:20px">
        ${[
          ['Mevcut Gün',  s.presentDays,        ICONS.checkin,'icon-green'],
          ['İzin',        s.leaveDays,          ICONS.palm,   'icon-amber'],
          ['Devamsız',    s.absentDays,         ICONS.cross,  'icon-red'],
          ['Raporlu',     s.absentWithReport,   ICONS.clipboard,'icon-cyan'],
          ['Raporsuz',    s.absentWithoutReport,ICONS.cross,  'icon-amber'],
          ['Toplam Saat', s.totalWorkedHours+'sa',ICONS.trend,'icon-blue'],
          ['FM Saat',     s.totalOvertimeHours+'sa',ICONS.trend,'icon-violet'],
          ['FM Vardiya',  s.overtimeShiftCount, ICONS.calendar,'icon-blue'],
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

// ── Face Check-in ───────────────────────────────────────────────────
async function toggleCam() {
  if (faceStream) { stopFaceCam(); return; }
  const panel = document.getElementById('face-panel'); panel.classList.remove('hidden');
  try {
    await ensureModels(); await loadEnrolledFaces();
    if (!enrolledFaces.length) { toast('Kayıtlı yüz yok.','err'); panel.classList.add('hidden'); return; }
    faceStream = await navigator.mediaDevices.getUserMedia({video:{facingMode:'user'}});
    const vid = document.getElementById('face-video'); vid.srcObject = faceStream; await vid.play();
    const canvas = document.getElementById('face-canvas');
    canvas.width = vid.videoWidth || 320; canvas.height = vid.videoHeight || 240;
    runFaceRecognition(vid, canvas, 'checkin-face', 'face-status', stopFaceCam);
  } catch(e) { toast('Kamera hatası: '+e.message,'err'); panel.classList.add('hidden'); }
}
function stopFaceCam() {
  faceStream?.getTracks().forEach(t=>t.stop()); faceStream = null;
  clearInterval(faceInterval); faceInterval = null;
  document.getElementById('face-panel').classList.add('hidden');
}
async function toggleCoCam() {
  if (coStream) { stopCoCam(); return; }
  const panel = document.getElementById('face-co-panel'); panel.classList.remove('hidden');
  try {
    await ensureModels(); await loadEnrolledFaces();
    if (!enrolledFaces.length) { toast('Kayıtlı yüz yok.','err'); panel.classList.add('hidden'); return; }
    coStream = await navigator.mediaDevices.getUserMedia({video:{facingMode:'user'}});
    const vid = document.getElementById('face-co-video'); vid.srcObject = coStream; await vid.play();
    const canvas = document.getElementById('face-co-canvas');
    canvas.width = vid.videoWidth || 320; canvas.height = vid.videoHeight || 240;
    runFaceRecognition(vid, canvas, 'checkout-face', 'face-co-status', stopCoCam);
  } catch(e) { toast('Kamera hatası: '+e.message,'err'); panel.classList.add('hidden'); }
}
function stopCoCam() {
  coStream?.getTracks().forEach(t=>t.stop()); coStream = null;
  clearInterval(coInterval); coInterval = null;
  document.getElementById('face-co-panel').classList.add('hidden');
}
function runFaceRecognition(vid, canvas, endpoint, statusId, stopFn) {
  const matcher = new faceapi.FaceMatcher(
    enrolledFaces.map(f => new faceapi.LabeledFaceDescriptors(String(f.userId), [f.descriptor])), 0.5);
  const processedIds = new Set();
  const intervalId = setInterval(async () => {
    const det = await faceapi.detectSingleFace(vid, new faceapi.TinyFaceDetectorOptions())
      .withFaceLandmarks(true).withFaceDescriptor();
    const ctx = canvas.getContext('2d'); ctx.clearRect(0,0,canvas.width,canvas.height);
    if (!det) { document.getElementById(statusId).textContent = '⏳ Yüz aranıyor…'; return; }
    faceapi.draw.drawDetections(canvas,[det]);
    const match = matcher.findBestMatch(det.descriptor);
    if (match.label === 'unknown') { document.getElementById(statusId).textContent = '❓ Tanınamadı'; return; }
    const userId = +match.label;
    if (processedIds.has(userId)) return;
    processedIds.add(userId);
    const found = enrolledFaces.find(f=>f.userId===userId);
    document.getElementById(statusId).textContent = `✅ ${found?.name||userId} tanındı.`;
    try {
      await api('POST', `/api/Attendance/${endpoint}/${userId}`);
      toast(`${found?.name||userId} — ${endpoint.includes('checkin')?'Giriş':'Çıkış'} kaydedildi ✓`);
      setTimeout(() => { stopFn(); loadAttendance(); }, 1500);
    } catch(e) { toast(e.message,'err'); processedIds.delete(userId); }
  }, 300);
  if (endpoint.includes('checkout')) coInterval = intervalId;
  else faceInterval = intervalId;
}

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

// ── Init ────────────────────────────────────────────────────────────
tryRestoreSession();
