'use strict';

// ════════════════════════════════════════════════════════════
// Shiftex Kiosk — Tablet/Telefon Yüz Tanıma Turnikesi
// Sürekli kamera, otomatik tanıma, otomatik in/out, büyük feedback
// ════════════════════════════════════════════════════════════

const API_BASE = document.querySelector('meta[name="api-base"]').content;

let kioskToken    = null;
let kioskDeviceId = null;
let knownFaces    = [];        // { userId, name, photo, descriptor }
let camStream     = null;
let scanInterval  = null;
let isProcessing  = false;
let recentItems   = [];

const COOLDOWN_MS         = 8000;  // aynı kişiye iki kez tetiklenmesin (yüz aynı kare içinde 8 sn boyunca)
const FACE_MATCH_DISTANCE = 0.5;   // ne kadar düşükse o kadar sıkı
const SCAN_INTERVAL_MS    = 400;
const recentCooldown      = new Map(); // userId → lastProcessedAt

// ── Yardımcılar ────────────────────────────────────────────
function getStored(key) { return localStorage.getItem(key); }
function setStored(key, val) { localStorage.setItem(key, val); }
function clearStored(key) { localStorage.removeItem(key); }

function fmtTime(d = new Date()) {
  return d.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
}
function fmtDate(d = new Date()) {
  return d.toLocaleDateString('tr-TR', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
}

// Saati canlı güncelle
function tickClock() {
  const now = new Date();
  document.getElementById('kh-time').textContent = now.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  document.getElementById('kh-date').textContent = fmtDate(now);
}
setInterval(tickClock, 1000);
tickClock();

// ── Kiosk Login ────────────────────────────────────────────
async function kioskLogin() {
  const errEl    = document.getElementById('kiosk-auth-err');
  const deviceId = document.getElementById('kiosk-device-id').value.trim();
  const pin      = document.getElementById('kiosk-pin').value;

  errEl.classList.add('hidden');
  if (!deviceId || !pin) {
    errEl.textContent = 'Cihaz adı ve PIN zorunludur.';
    errEl.classList.remove('hidden');
    return;
  }

  try {
    const res = await fetch(API_BASE + '/api/Kiosk/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ deviceId, kioskPin: pin })
    });
    const data = await res.json();
    if (!res.ok) {
      errEl.textContent = data.message || 'Giriş başarısız.';
      errEl.classList.remove('hidden');
      return;
    }

    kioskToken    = data.token;
    kioskDeviceId = data.deviceId;
    setStored('kiosk_token', kioskToken);
    setStored('kiosk_device', kioskDeviceId);

    await startKiosk();
  } catch (e) {
    errEl.textContent = e.message || 'Bağlantı hatası.';
    errEl.classList.remove('hidden');
  }
}

async function kioskExit() {
  if (!confirm('Turnikeden çıkmak istiyor musunuz?')) return;
  stopCam();
  clearStored('kiosk_token');
  clearStored('kiosk_device');
  location.reload();
}

// ── Kiosk Başlangıç ────────────────────────────────────────
async function startKiosk() {
  document.getElementById('kiosk-auth').classList.add('hidden');
  document.getElementById('kiosk-main').classList.remove('hidden');
  document.getElementById('kh-device').textContent = kioskDeviceId;

  // Yüklenme indikatörü
  showStatus('Yüz tanıma modelleri yükleniyor…', 'busy');

  try {
    await loadFaceApiModels();
    showStatus('Personel veritabanı yükleniyor…', 'busy');
    await loadKnownFaces();
    showStatus('Kamera başlatılıyor…', 'busy');
    await startCamera();
    showStatus('Sistem hazır', 'active');
    startScanLoop();
  } catch (e) {
    showStatus('HATA: ' + e.message, 'error');
    console.error(e);
  }
}

function showStatus(text, kind) {
  const el = document.getElementById('kh-status');
  const dot = el.querySelector('.status-dot');
  dot.className = 'status-dot ' + (kind === 'active' ? 'status-active' : kind === 'busy' ? 'status-busy' : 'status-error');
  el.querySelector('span:last-child').textContent = text;
}

async function loadFaceApiModels() {
  const MODEL_URL = 'https://cdn.jsdelivr.net/gh/justadudewhohacks/face-api.js@0.22.2/weights';
  await Promise.all([
    faceapi.nets.tinyFaceDetector.loadFromUri(MODEL_URL),
    faceapi.nets.faceLandmark68TinyNet.loadFromUri(MODEL_URL),
    faceapi.nets.faceRecognitionNet.loadFromUri(MODEL_URL),
  ]);
}

async function loadKnownFaces() {
  const res = await fetch(API_BASE + '/api/Kiosk/face-descriptors', {
    headers: { 'Authorization': 'Bearer ' + kioskToken }
  });
  if (!res.ok) throw new Error('Personel veritabanı yüklenemedi (HTTP ' + res.status + ')');
  const data = await res.json();
  knownFaces = (data || []).map(f => ({
    userId: f.userId,
    name:   f.userFullName,
    photo:  f.userPhoto,
    descriptor: new Float32Array(f.descriptor)
  }));
  if (!knownFaces.length) throw new Error('Sistemde kayıtlı yüz yok');
}

async function startCamera() {
  camStream = await navigator.mediaDevices.getUserMedia({
    video: { facingMode: 'user', width: { ideal: 1280 }, height: { ideal: 720 } },
    audio: false
  });
  const vid = document.getElementById('kiosk-video');
  vid.srcObject = camStream;
  await vid.play();

  const canvas = document.getElementById('kiosk-canvas');
  canvas.width  = vid.videoWidth  || 1280;
  canvas.height = vid.videoHeight || 720;
}

function stopCam() {
  camStream?.getTracks().forEach(t => t.stop());
  camStream = null;
  if (scanInterval) clearInterval(scanInterval);
  scanInterval = null;
}

// ── Sürekli Tarama ────────────────────────────────────────
function startScanLoop() {
  const matcher = new faceapi.FaceMatcher(
    knownFaces.map(f => new faceapi.LabeledFaceDescriptors(String(f.userId), [f.descriptor])),
    FACE_MATCH_DISTANCE
  );
  const vid    = document.getElementById('kiosk-video');
  const canvas = document.getElementById('kiosk-canvas');
  const ctx    = canvas.getContext('2d');

  scanInterval = setInterval(async () => {
    if (isProcessing) return;
    if (vid.paused || vid.ended) return;

    const det = await faceapi.detectSingleFace(vid, new faceapi.TinyFaceDetectorOptions({ inputSize: 320 }))
      .withFaceLandmarks(true).withFaceDescriptor();

    ctx.clearRect(0, 0, canvas.width, canvas.height);
    if (!det) {
      setPrompt('idle');
      return;
    }

    // Algılanan yüzü kutu çiz
    drawFaceBox(ctx, det.detection.box);
    setPrompt('scanning');

    const match = matcher.findBestMatch(det.descriptor);
    if (match.label === 'unknown') {
      setPrompt('unknown');
      return;
    }

    const userId = +match.label;

    // Cooldown kontrolü
    const lastTime = recentCooldown.get(userId);
    if (lastTime && Date.now() - lastTime < COOLDOWN_MS) return;
    recentCooldown.set(userId, Date.now());

    await processAttendance(userId);
  }, SCAN_INTERVAL_MS);
}

function drawFaceBox(ctx, box) {
  ctx.strokeStyle = '#22D3EE';
  ctx.lineWidth = 4;
  ctx.strokeRect(box.x, box.y, box.width, box.height);
  // Köşe işaretleri
  const len = 24;
  ctx.beginPath();
  ctx.moveTo(box.x, box.y + len); ctx.lineTo(box.x, box.y); ctx.lineTo(box.x + len, box.y);
  ctx.moveTo(box.x + box.width - len, box.y); ctx.lineTo(box.x + box.width, box.y); ctx.lineTo(box.x + box.width, box.y + len);
  ctx.moveTo(box.x, box.y + box.height - len); ctx.lineTo(box.x, box.y + box.height); ctx.lineTo(box.x + len, box.y + box.height);
  ctx.moveTo(box.x + box.width - len, box.y + box.height); ctx.lineTo(box.x + box.width, box.y + box.height); ctx.lineTo(box.x + box.width, box.y + box.height - len);
  ctx.lineWidth = 6;
  ctx.stroke();
}

function setPrompt(state) {
  const el = document.getElementById('kiosk-prompt');
  el.className = 'kiosk-prompt';
  const title = el.querySelector('.kp-title');
  const sub   = el.querySelector('.kp-sub');
  if (state === 'idle') {
    el.classList.add('kiosk-prompt-idle');
    title.textContent = 'Yüzünüzü kameraya gösterin';
    sub.textContent = 'Sistem sizi otomatik olarak tanıyacaktır';
  } else if (state === 'scanning') {
    el.classList.add('kiosk-prompt-scanning');
    title.textContent = 'Yüz algılandı';
    sub.textContent = 'Tanımlanıyor…';
  } else if (state === 'unknown') {
    el.classList.add('kiosk-prompt-unknown');
    title.textContent = 'Tanımlanamadı';
    sub.textContent = 'Lütfen kayıtlı bir personel olduğunuzdan emin olun';
  }
}

// ── Devam İşlemi ───────────────────────────────────────────
async function processAttendance(userId) {
  isProcessing = true;
  try {
    const res = await fetch(API_BASE + '/api/Kiosk/attend', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + kioskToken },
      body: JSON.stringify({ userId })
    });
    const data = await res.json();
    if (!res.ok) {
      showResult({ error: true, message: data.message || 'İşlem başarısız' });
      return;
    }
    const user = knownFaces.find(f => f.userId === userId);
    const action = data.action; // checkin | checkout
    showResult({
      name: user?.name || 'Personel',
      photo: user?.photo,
      action,
      time: new Date()
    });
    pushRecent({
      name: user?.name || 'Personel',
      photo: user?.photo,
      action,
      time: new Date()
    });
  } catch (e) {
    showResult({ error: true, message: e.message });
  } finally {
    setTimeout(() => { isProcessing = false; }, 1500);
  }
}

function showResult({ error, message, name, photo, action, time }) {
  const overlay = document.getElementById('kiosk-result');
  const greet   = document.getElementById('kr-greeting');
  const userEl  = document.getElementById('kr-user');
  const actEl   = document.getElementById('kr-action');
  const clockEl = document.getElementById('kr-clock');
  const check   = document.getElementById('kr-check');

  if (error) {
    greet.textContent = 'Hata';
    userEl.textContent = message || '—';
    actEl.textContent = '';
    clockEl.textContent = fmtTime();
    check.className = 'kr-check kr-check-error';
    overlay.classList.remove('hidden');
    setTimeout(() => overlay.classList.add('hidden'), 3000);
    return;
  }

  const firstName = (name || '').split(' ')[0];
  if (action === 'checkin') {
    greet.textContent = `Hoş geldin, ${firstName}!`;
    actEl.textContent = '✓ Giriş kaydedildi';
    check.className = 'kr-check kr-check-in';
    playBeep('in');
  } else {
    greet.textContent = `İyi günler, ${firstName}!`;
    actEl.textContent = '✓ Çıkış kaydedildi';
    check.className = 'kr-check kr-check-out';
    playBeep('out');
  }
  userEl.textContent = name;
  clockEl.textContent = fmtTime(time);
  overlay.classList.remove('hidden');
  setTimeout(() => overlay.classList.add('hidden'), 4500);
}

function pushRecent(item) {
  recentItems.unshift(item);
  if (recentItems.length > 5) recentItems.pop();
  const list = document.getElementById('kiosk-recent-list');
  list.innerHTML = recentItems.map(it => {
    const icon = it.action === 'checkin' ? '→' : '←';
    const cls = it.action === 'checkin' ? 'kr-item-in' : 'kr-item-out';
    const label = it.action === 'checkin' ? 'Giriş' : 'Çıkış';
    const avatar = it.photo
      ? `<img src="${it.photo}" alt="" />`
      : `<span class="kr-init">${it.name.slice(0,1)}</span>`;
    return `<div class="kr-item ${cls}">
      <div class="kri-av">${avatar}</div>
      <div class="kri-name">${it.name}</div>
      <div class="kri-action">${icon} ${label}</div>
      <div class="kri-time">${fmtTime(it.time)}</div>
    </div>`;
  }).join('');
}

// ── Ses (basit beep — WebAudio) ────────────────────────────
let audioCtx;
function playBeep(kind) {
  try {
    audioCtx = audioCtx || new (window.AudioContext || window.webkitAudioContext)();
    const o = audioCtx.createOscillator();
    const g = audioCtx.createGain();
    o.connect(g); g.connect(audioCtx.destination);
    o.frequency.value = kind === 'in' ? 880 : 587; // A5 vs D5
    g.gain.setValueAtTime(0.0, audioCtx.currentTime);
    g.gain.linearRampToValueAtTime(0.15, audioCtx.currentTime + 0.02);
    g.gain.exponentialRampToValueAtTime(0.001, audioCtx.currentTime + 0.4);
    o.start(); o.stop(audioCtx.currentTime + 0.4);
  } catch (_) { /* ses opsiyonel */ }
}

// ── Otomatik restore ───────────────────────────────────────
window.addEventListener('DOMContentLoaded', async () => {
  const t = getStored('kiosk_token');
  const d = getStored('kiosk_device');
  if (t && d) {
    kioskToken = t;
    kioskDeviceId = d;
    await startKiosk();
  }
});

// Ekran kararmasını engelle (Wake Lock API — destekliyorsa)
async function preventSleep() {
  try {
    if ('wakeLock' in navigator) {
      const wl = await navigator.wakeLock.request('screen');
      document.addEventListener('visibilitychange', async () => {
        if (document.visibilityState === 'visible') {
          try { await navigator.wakeLock.request('screen'); } catch (_) {}
        }
      });
    }
  } catch (_) {}
}
preventSleep();
