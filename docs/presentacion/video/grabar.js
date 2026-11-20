// Graba el video del camino feliz de FlowDesk (escritorio + web) sobre la aplicación real y arma un MP4.
// Uso: node grabar.js [--prueba] [--sin-voz] [--reusar-escritorio]
//   --prueba: no graba; deja una captura por escena en ./capturas (revisión rápida del guion)
//   --sin-voz: solo subtítulos, aunque edge-tts esté instalado
//   --reusar-escritorio: no vuelve a manejar la app WPF; usa los cuadros de frames/escritorio
// Si la API (5080) o la web (5173) no están corriendo, las levanta con una base de demostración limpia.
const fs = require('fs'), path = require('path'), os = require('os'), crypto = require('crypto');
const { execFileSync, spawnSync, spawn } = require('child_process');
const { pathToFileURL } = require('url');

// Dependencias aquí (npm i) o en la carpeta de herramientas de video (VIDEO_TOOLS).
const TOOLS = process.env.VIDEO_TOOLS || path.join(os.homedir(), 'OneDrive', 'Documentos', 'video');
const req = (m) => { try { return require(m); } catch { return require(path.join(TOOLS, 'node_modules', m)); } };
const { chromium } = req('playwright-core');
const FFMPEG = req('ffmpeg-static');
const VOZ = require('./voz.js');

const args = process.argv.slice(2);
const PRUEBA = args.includes('--prueba');
const SIN_VOZ = args.includes('--sin-voz');
const REUSAR_ESCRITORIO = args.includes('--reusar-escritorio');
const VOICE = process.env.VOZ_TTS || 'es-CO-SalomeNeural';
const API = process.env.FLOWDESK_API || 'http://localhost:5080';
const WEB = process.env.FLOWDESK_WEB || 'http://localhost:5173';
const W = 1440, H = 900;
const RAIZ = path.join(__dirname, '..', '..', '..');
const SALIDA = path.join(__dirname, '..', 'videos');
const CAPTURAS = path.join(__dirname, 'capturas');
const FRAMES = path.join(__dirname, 'frames');
const ESCRITORIO = path.join(FRAMES, 'escritorio');
const EXE = path.join(RAIZ, 'desktop', 'FlowDesk.Desktop', 'bin', 'Debug', 'net10.0-windows', 'FlowDesk.Capture.exe');
const TITULO = 'Alta de proveedor Andes Ltda.';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ---------- Entorno: API y web ----------
const vivo = async (url) => { try { return (await fetch(url)).ok; } catch { return false; } };
async function esperarUrl(url, ms) { const fin = Date.now() + ms; while (Date.now() < fin) { if (await vivo(url)) return; await sleep(700); } throw new Error('No respondió ' + url); }
async function asegurarServicios() {
  const procesos = [];
  if (!(await vivo(`${API}/health`))) {
    console.log('Levantando la API con una base de demostración limpia…');
    fs.rmSync(path.join(RAIZ, 'backend', '.data', 'demo'), { recursive: true, force: true });
    procesos.push(spawn('dotnet', ['run', '--launch-profile', 'http'], {
      cwd: path.join(RAIZ, 'backend', 'FlowDesk.API'), stdio: 'ignore',
      env: {
        ...process.env, Worker__RunInApi: 'true', RateLimiting__LoginPermitsPerMinute: '1000',
        ConnectionStrings__FlowDesk: 'Data Source=../.data/demo/flowdesk-demo.db', Storage__LocalRootPath: '../.data/demo/storage',
      },
    }));
    await esperarUrl(`${API}/health`, 240000);
  }
  if (!(await vivo(WEB))) {
    console.log('Levantando la web (Vite)…');
    procesos.push(spawn('npm run dev', { cwd: path.join(RAIZ, 'frontend'), stdio: 'ignore', shell: true }));
    await esperarUrl(WEB, 120000);
  }
  return procesos;
}
const detener = (procesos) => procesos.forEach((p) => spawnSync('taskkill', ['/pid', String(p.pid), '/T', '/F'], { stdio: 'ignore' }));

// ---------- Parte de escritorio: la app WPF real, capturando solo su ventana ----------
function grabarEscritorio() {
  if (!fs.existsSync(EXE)) execFileSync('dotnet', ['build', path.join(RAIZ, 'desktop', 'FlowDesk.Desktop'), '-v', 'q'], { stdio: 'inherit' });
  fs.rmSync(ESCRITORIO, { recursive: true, force: true }); fs.mkdirSync(ESCRITORIO, { recursive: true });
  console.log('Parte de escritorio: manejando FlowDesk Capture (no uses el teclado ni el mouse durante ~1 minuto)…');
  const r = spawnSync('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join(__dirname, 'escritorio.ps1'),
    '-OutDir', ESCRITORIO, '-Exe', EXE, '-Title', TITULO], { stdio: 'inherit' });
  if (r.status !== 0) throw new Error('Falló la parte de escritorio (escritorio.ps1).');
}
function leerEscritorio() {
  const filas = fs.readFileSync(path.join(ESCRITORIO, 'frames.txt'), 'utf8').split(/\r?\n/).filter(Boolean)
    .map((l) => { const [archivo, ms, escena] = l.replace(/^﻿/, '').split('|'); return { archivo, ms: +ms, escena }; });
  const escenas = {};
  filas.forEach((f, i) => {
    const sig = filas[i + 1];
    const dt = Math.min(600, Math.max(40, sig ? sig.ms - f.ms : 600));
    (escenas[f.escena] ||= []).push({ src: pathToFileURL(path.join(ESCRITORIO, f.archivo)).href, dt });
  });
  return escenas;
}

// ---------- Voz (opcional) ----------
function buscarEdge() {
  if (SIN_VOZ) return null;
  if (process.env.EDGE_TTS) return [process.env.EDGE_TTS, []];
  for (const [c, a] of [['edge-tts', []], ['python', ['-m', 'edge_tts']], ['py', ['-m', 'edge_tts']]]) {
    if (spawnSync(c, [...a, '--help'], { stdio: 'ignore' }).status === 0) return [c, a];
  }
  return null;
}
const EDGE = buscarEdge();
const DIRVOZ = path.join(__dirname, 'voz');
function audio(texto) {
  fs.mkdirSync(DIRVOZ, { recursive: true });
  const f = path.join(DIRVOZ, crypto.createHash('sha1').update(VOICE + texto).digest('hex').slice(0, 16) + '.mp3');
  if (!fs.existsSync(f)) execFileSync(EDGE[0], [...EDGE[1], '--voice', VOICE, '--rate', '+4%', '--text', texto, '--write-media', f], { stdio: 'ignore' });
  const r = spawnSync(FFMPEG, ['-i', f], { encoding: 'utf8' });
  const m = (r.stderr || '').match(/Duration: (\d+):(\d+):([\d.]+)/);
  return { f, dur: m ? (+m[1]) * 3600 + (+m[2]) * 60 + parseFloat(m[3]) : 3 };
}
const lectura = (txt) => Math.max(3200, String(txt).split(/\s+/).length * 330);

// ---------- Capa de presentación con la paleta FlowDesk (sapote, gris agradable, negro azulado) ----------
const OVERLAY = `(() => {
  if (document.getElementById('vx')) return;
  const css = document.createElement('style');
  css.textContent = \`
  #vx-cap{position:fixed;left:284px;right:28px;bottom:22px;z-index:2147483600;background:rgba(27,37,51,.96);color:#fff;border-radius:18px;
    padding:14px 20px 14px 16px;display:flex;gap:16px;align-items:center;box-shadow:0 14px 34px rgba(27,37,51,.35);border-left:5px solid #D9734E;
    font-family:'Inter Variable','Segoe UI',system-ui,sans-serif;transition:opacity .35s, transform .35s;pointer-events:none}
  #vx-cap.wide{left:28px}
  #vx-cap.off{opacity:0;transform:translateY(12px)}
  #vx-n{flex:none;width:46px;height:46px;border-radius:50%;background:#D9734E;color:#fff;display:grid;place-items:center;font:800 19px 'Inter Variable','Segoe UI',sans-serif}
  #vx-t{font:800 20px/1.25 'Inter Variable','Segoe UI',sans-serif}
  #vx-d{font:500 16px/1.4 'Inter Variable','Segoe UI',sans-serif;color:rgba(255,255,255,.86);margin-top:2px}
  #vx-role{margin-left:auto;flex:none;font:700 13px 'Inter Variable','Segoe UI',sans-serif;background:#F6D3C4;color:#8a3c1f;border-radius:999px;padding:4px 12px}
  #vx-cur{position:fixed;left:0;top:0;z-index:2147483647;width:26px;height:26px;pointer-events:none;transition:left .65s cubic-bezier(.3,.7,.3,1), top .65s cubic-bezier(.3,.7,.3,1);filter:drop-shadow(0 2px 3px rgba(0,0,0,.4))}
  .vx-rip{position:fixed;z-index:2147483646;width:44px;height:44px;margin:-22px 0 0 -22px;border-radius:50%;border:3px solid #D9734E;pointer-events:none;animation:vxr .55s ease-out forwards}
  @keyframes vxr{from{transform:scale(.3);opacity:1}to{transform:scale(1.4);opacity:0}}
  #vx-card{position:fixed;inset:0;z-index:2147483645;display:grid;place-items:center;text-align:center;font-family:'Inter Variable','Segoe UI',sans-serif;transition:opacity .6s;
    background:radial-gradient(circle at 12% 86%,#D9734E 0 14%,transparent calc(14% + 1px)),radial-gradient(circle at 88% 74%,#273345 0 13%,transparent calc(13% + 1px)),
    radial-gradient(circle at 78% 18%,#F6D3C4 0 4.5%,transparent calc(4.5% + 1px)),radial-gradient(circle at 22% 22%,#8D9AAD 0 1.6%,transparent calc(1.6% + 1px)),#1B2533;color:#fff}
  #vx-card.off{opacity:0;pointer-events:none}
  #vx-card h1{font:800 64px/1.1 'Inter Variable','Segoe UI',sans-serif;letter-spacing:-1px;margin:0 0 18px}
  #vx-card p{font:500 24px/1.5 'Inter Variable','Segoe UI',sans-serif;margin:0 auto;max-width:920px;color:#d9dee7}
  #vx-card small{display:inline-block;margin-top:30px;font:800 14px 'Inter Variable','Segoe UI',sans-serif;letter-spacing:.12em;text-transform:uppercase;background:#D9734E;color:#fff;border-radius:999px;padding:6px 16px}\`;
  document.head.appendChild(css);
  const root = document.createElement('div'); root.id = 'vx';
  root.innerHTML = '<div id="vx-cap" class="off"><div id="vx-n"></div><div><div id="vx-t"></div><div id="vx-d"></div></div><div id="vx-role"></div></div>'
    + '<svg id="vx-cur" viewBox="0 0 24 24"><path d="M4 2l16 9-7 2-3 7z" fill="#fff" stroke="#1B2533" stroke-width="1.6" stroke-linejoin="round"/></svg>'
    + '<div id="vx-card" class="off"><div><h1 id="vx-ch"></h1><p id="vx-cp"></p><small id="vx-cs"></small></div></div>';
  document.body.appendChild(root);
  const s = window.__vx || {};
  const cur = document.getElementById('vx-cur'); cur.style.left = (s.x ?? 720) + 'px'; cur.style.top = (s.y ?? 450) + 'px';
  if (s.hideCursor) cur.style.display = 'none';
  if (s.cap) { const c = document.getElementById('vx-cap'); c.classList.remove('off'); c.classList.toggle('wide', !!s.cap.wide); for (const k of ['n','t','d','role']) document.getElementById('vx-' + k).textContent = s.cap[k] || ''; const r = document.getElementById('vx-role'); r.style.display = s.cap.role ? '' : 'none'; }
})()`;

// Escenario para reproducir los cuadros de la app de escritorio con la misma capa de presentación.
const ESCENARIO = `<!doctype html><html lang="es"><head><meta charset="utf-8"><title>FlowDesk Capture</title><style>
  html,body{margin:0;height:100%;background:#EEF0F3;overflow:hidden}
  #stage{position:fixed;left:0;right:0;top:62px;bottom:118px;display:grid;place-items:center}
  #frame{max-width:1360px;max-height:100%;border-radius:12px;box-shadow:0 18px 50px rgba(27,37,51,.28);background:#fff}
  #tag{position:fixed;top:18px;left:50%;transform:translateX(-50%);font:700 13px 'Segoe UI',sans-serif;background:#1B2533;color:#F6D3C4;border-radius:999px;padding:6px 14px;letter-spacing:.06em}
</style></head><body><div id="stage"><img id="frame" alt=""></div><div id="tag">APLICACIÓN DE ESCRITORIO · WPF</div>
<script>
  const frame = document.getElementById('frame');
  window.playScene = async (frames) => {
    const imgs = frames.map((f) => { const i = new Image(); i.src = f.src; return i; });
    for (let k = 0; k < frames.length; k++) {
      const t0 = performance.now();
      try { await imgs[k].decode(); } catch {}
      frame.src = imgs[k].src;
      const wait = frames[k].dt - (performance.now() - t0);
      if (wait > 0) await new Promise((r) => setTimeout(r, wait));
    }
  };
</script></body></html>`;

// ---------- Grabación ----------
async function grabar(browser, escritorio) {
  const TMP = path.join(FRAMES, 'video');
  fs.rmSync(TMP, { recursive: true, force: true }); fs.mkdirSync(TMP, { recursive: true });
  const escenarioHtml = path.join(FRAMES, 'escenario.html');
  fs.writeFileSync(escenarioHtml, ESCENARIO);

  const conVoz = !!EDGE && !PRUEBA;
  const narracion = []; let vozFin = 0, wall0 = 0;
  const esperarVoz = async () => { if (PRUEBA) return; const ms = vozFin - Date.now(); if (ms > 0) await sleep(ms); };
  const hablar = (t, d) => {
    if (PRUEBA) return;
    const texto = VOZ[t];
    if (conVoz && texto) { const a = audio(texto); narracion.push({ f: a.f, t: Date.now() }); vozFin = Date.now() + a.dur * 1000 + 450; }
    else vozFin = Date.now() + lectura(t + ' ' + (d || ''));
  };

  const context = await browser.newContext({ viewport: { width: W, height: H }, deviceScaleFactor: 1, locale: 'es-CO' });
  const page = await context.newPage();
  const errores = [];
  page.on('pageerror', (e) => errores.push(e.message));
  page.on('load', () => page.evaluate(OVERLAY).catch(() => {}));

  const frames = []; let cdp = null;
  const iniciarScreencast = async () => {
    if (PRUEBA) return;
    if (cdp) { await cdp.send('Page.stopScreencast').catch(() => {}); await cdp.detach().catch(() => {}); }
    cdp = await context.newCDPSession(page);
    cdp.on('Page.screencastFrame', async (f) => {
      const file = path.join(TMP, `f${String(frames.length).padStart(6, '0')}.jpg`);
      fs.writeFileSync(file, Buffer.from(f.data, 'base64'));
      if (!frames.length) wall0 = Date.now();
      frames.push({ file, t: Date.now() / 1000, w: Date.now() });
      cdp.send('Page.screencastFrameAck', { sessionId: f.sessionId }).catch(() => {});
    });
    await cdp.send('Page.startScreencast', { format: 'jpeg', quality: 88, maxWidth: W, maxHeight: H, everyNthFrame: 1 });
  };

  await page.goto(pathToFileURL(escenarioHtml).href);
  await page.evaluate(() => { window.__vx = { hideCursor: true }; });
  await page.evaluate(OVERLAY);
  await page.evaluate(() => { document.getElementById('vx-cur').style.display = 'none'; });
  await sleep(500);
  await iniciarScreencast();

  let shot = 0;
  fs.mkdirSync(CAPTURAS, { recursive: true });
  const slug = (t) => t.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-zA-Z0-9]+/g, '-').toLowerCase();
  const snap = async (n) => { if (PRUEBA) await page.screenshot({ path: path.join(CAPTURAS, `${String(++shot).padStart(2, '0')}-${n}.png`) }); };
  const hold = (ms) => sleep(PRUEBA ? Math.min(ms, 400) : ms);

  const v = { page, hold };
  v.cap = async (n, t, d, role, wide = false) => {
    await esperarVoz();
    await page.evaluate(([n, t, d, role, wide]) => {
      window.__vx = { ...(window.__vx || {}), cap: { n, t, d, role, wide } };
      const c = document.getElementById('vx-cap'); c.classList.remove('off'); c.classList.toggle('wide', wide);
      document.getElementById('vx-n').textContent = n; document.getElementById('vx-t').textContent = t;
      document.getElementById('vx-d').textContent = d; const r = document.getElementById('vx-role'); r.textContent = role || ''; r.style.display = role ? '' : 'none';
    }, [n, t, d, role, wide]);
    hablar(t, d);
    await snap(slug(t));
  };
  const card = async (h, p, s) => {
    await esperarVoz();
    await page.evaluate(([h, p, s]) => {
      document.getElementById('vx-ch').textContent = h; document.getElementById('vx-cp').textContent = p; document.getElementById('vx-cs').textContent = s || '';
      document.getElementById('vx-card').classList.remove('off');
      document.getElementById('vx-cap').classList.add('off'); if (window.__vx) window.__vx.cap = null;
    }, [h, p, s]);
    hablar(h, p);
    await snap(slug(h));
  };
  const cardOff = async () => { await esperarVoz(); await page.evaluate(() => document.getElementById('vx-card').classList.add('off')); };

  v.apuntar = async (sel) => {
    const loc = page.locator(sel).first();
    await loc.waitFor({ state: 'visible', timeout: 20000 });
    let b = null;
    for (let k = 0; k < 6 && !b; k++) { try { await loc.scrollIntoViewIfNeeded({ timeout: 2000 }); b = await loc.boundingBox(); } catch { await sleep(250); } }
    if (!b) throw new Error('no pude ubicar ' + sel);
    const x = b.x + Math.min(b.width / 2, 60), y = b.y + Math.min(b.height / 2, 40);
    await page.evaluate(([x, y]) => { window.__vx = { ...(window.__vx || {}), x, y, hideCursor: false }; const c = document.getElementById('vx-cur'); if (c) { c.style.display = ''; c.style.left = (x - 4) + 'px'; c.style.top = (y - 2) + 'px'; } }, [x, y]);
    await hold(700);
    return { loc, x, y };
  };
  const ripple = (x, y) => page.evaluate(([x, y]) => { const r = document.createElement('div'); r.className = 'vx-rip'; r.style.left = x + 'px'; r.style.top = y + 'px'; document.body.appendChild(r); setTimeout(() => r.remove(), 700); }, [x, y]);
  v.clic = async (sel, pausa = 700) => { const { loc, x, y } = await v.apuntar(sel); await ripple(x, y); await loc.click(); await hold(pausa); };
  v.escribir = async (sel, txt, pausa = 500, delay = 40) => { const { loc, x, y } = await v.apuntar(sel); await ripple(x, y); await loc.click(); await loc.pressSequentially(txt, { delay: PRUEBA ? 0 : delay }); await hold(pausa); };
  v.nav = (texto) => v.clic(`nav a:has-text("${texto}")`, 1500);
  v.salir = async () => {
    await v.clic('[data-testid="user-menu"]', 600);
    await v.clic('li:has-text("Cerrar sesión")', 600);
    await page.waitForURL('**/login');
  };
  v.login = async (rol) => {
    await esperarVoz();
    if (!page.url().endsWith('/login')) await v.salir();
    await v.clic(`.MuiChip-root:has-text("${rol}")`, 500);
    await v.clic('button:has-text("Ingresar")', 300);
    await page.getByText('Tiempo real activo').waitFor({ timeout: 20000 });
    await hold(1000);
  };
  v.confirmar = async (pausa = 1800) => v.clic('[role=dialog] button:has-text("Confirmar")', pausa);
  const escena = async (id) => {
    if (PRUEBA) { const f = (escritorio[id] || []).at(-1); if (f) await page.evaluate((src) => { document.getElementById('frame').src = src; }, f.src); await hold(300); return; }
    await page.evaluate((fr) => window.playScene(fr), escritorio[id] || []);
  };

  try {
    // Portada
    await card('El camino feliz', 'Un expediente de principio a fin: se digitaliza en el escritorio, se valida y se aprueba en la web, y queda auditado.', 'FlowDesk · Enterprise Document & Workflow');
    await hold(2600); await cardOff(); await hold(600);

    // ---- Parte 1 · Escritorio (cuadros reales de la app WPF) ----
    const D = [
      ['E1', 'FlowDesk Capture', 'La aplicación de escritorio para digitalizar documentos.'],
      ['E2', 'Crear el expediente', 'Título, solicitante y responsable. Nace en estado «Recibido».'],
      ['E3', 'Llegan los documentos', 'PDF e imágenes. Se valida la firma del archivo: la cédula falsa se rechaza antes de enviar.'],
      ['E4', 'Revisar antes de enviar', 'Vista previa del PDF y de la imagen.'],
      ['E5', 'Preparado para escáneres TWAIN', 'Con el simulador: dos páginas digitalizadas en un TIFF.'],
      ['E6', 'Enviar al servidor', 'Cada documento sube con su progreso.'],
      ['E7', 'Procesado en segundos', 'Hash SHA-256, formato revalidado y sin duplicados.'],
    ];
    for (let i = 0; i < D.length; i++) { await v.cap(i + 1, D[i][1], D[i][2], 'Operador', true); await escena(D[i][0]); }

    // ---- Parte 2 · Web ----
    await card('La aplicación web', 'Validación, aprobación, notificaciones y auditoría en React con MUI.', 'Parte 2 de 2');
    await hold(1500);
    await page.goto(`${WEB}/login`);
    await iniciarScreencast();
    await page.evaluate(OVERLAY);
    await page.evaluate(() => document.getElementById('vx-card').classList.remove('off'));
    await cardOff(); await hold(500);

    let n = D.length;
    await v.cap(++n, 'Entra la validadora', 'Tablero operativo en tiempo real.', 'Validadora', true);
    await v.login('Validador');
    await v.apuntar('main .MuiCard-root'); await hold(1600);

    await v.cap(++n, 'Su bandeja de trabajo', 'Estado, prioridad y responsable de cada expediente.', 'Validadora');
    await v.nav('Bandeja de trabajo');
    await v.apuntar('[data-testid=expedient-row]'); await hold(1600);

    await v.cap(++n, 'El expediente completo', 'Documentos con origen, versión y hash, junto al visor.', 'Validadora');
    await v.clic(`[data-testid=expedient-row]:has-text("${TITULO}")`, 2000);
    await v.clic('.MuiListItemButton-root:has-text("factura-proveedor.png")', 2200);
    await v.apuntar('text=SHA-256'); await hold(1400);

    await v.cap(++n, 'El workflow manda', 'Solo aparecen las transiciones que su rol permite.', 'Validadora');
    await v.clic('button:has-text("Iniciar validación")', 1200);
    await v.confirmar();
    await v.clic('button:has-text("Enviar a aprobación")', 1200);
    await v.confirmar(2200);

    await v.cap(++n, 'Entra el aprobador', 'Su bandeja de aprobaciones.', 'Aprobador');
    await v.login('Aprobador');
    await v.nav('Aprobaciones');
    await v.apuntar('[data-testid=expedient-row]'); await hold(1200);

    await v.cap(++n, 'Aprobar con observación', 'La decisión queda registrada con su responsable.', 'Aprobador');
    await v.clic(`[data-testid=expedient-row]:has-text("${TITULO}")`, 1800);
    await v.clic('main button:has-text("Aprobar")', 1000);
    await v.escribir('[role=dialog] textarea:not([aria-hidden])', 'Documentación completa y verificada.');
    await v.confirmar(2200);

    await v.cap(++n, 'Historial completo', 'Estados, responsables, fechas y observaciones.', 'Aprobador');
    await v.clic('[role=tab]:has-text("Historial")', 2400);
    await v.clic('[role=tab]:has-text("Aprobaciones")', 2000);

    await v.cap(++n, 'El operador fue notificado', 'Una notificación por cada cambio de estado.', 'Operador');
    await v.login('Operador');
    await v.clic('button[aria-label="Notificaciones"]', 2600);
    await page.keyboard.press('Escape'); await hold(500);

    await v.cap(++n, 'Todo queda auditado', 'Quién, qué, cuándo y con qué correlación. Solo inserción.', 'Administradora');
    await v.login('Admin');
    await v.nav('Auditoría');
    await v.apuntar('main table'); await hold(2600);

    await v.cap(++n, 'Usuarios y permisos', 'Roles y permisos validados por el backend en cada operación.', 'Administradora');
    await v.nav('Usuarios y roles');
    await v.apuntar('text=Matriz de permisos'); await hold(2400);

    await esperarVoz();
    await page.evaluate(() => document.getElementById('vx-cap').classList.add('off'));
    await hold(900);
  } catch (e) {
    await page.screenshot({ path: path.join(__dirname, 'error-camino.png') }).catch(() => {});
    throw e;
  }

  const wallFin = Date.now();
  if (cdp) await cdp.send('Page.stopScreencast').catch(() => {});
  await context.close();
  if (errores.length) console.log('  errores de la página:', errores);
  if (PRUEBA) return null;

  // Armar el MP4: cuadros con su duración real + narración en su instante.
  fs.mkdirSync(SALIDA, { recursive: true });
  const OUT = path.join(SALIDA, '01-camino-feliz.mp4');
  const lista = frames.map((f, i) => { const d = i + 1 < frames.length ? frames[i + 1].t - f.t : Math.max(0.5, (wallFin - f.w) / 1000); return `file '${f.file.replace(/\\/g, '/')}'\nduration ${Math.max(d, 0.001).toFixed(4)}`; });
  lista.push(`file '${frames[frames.length - 1].file.replace(/\\/g, '/')}'`);
  fs.writeFileSync(path.join(TMP, 'lista.txt'), lista.join('\n'));
  const video = path.join(TMP, 'video.mp4');
  execFileSync(FFMPEG, ['-y', '-f', 'concat', '-safe', '0', '-i', path.join(TMP, 'lista.txt'),
    '-vf', `fps=30,scale=${W}:${H}:flags=lanczos,format=yuv420p`, '-c:v', 'libx264', '-preset', 'slow', '-crf', '20', '-movflags', '+faststart', video], { stdio: 'ignore' });
  if (narracion.length) {
    const a = ['-y', '-i', video];
    narracion.forEach((x) => a.push('-i', x.f));
    const filtros = narracion.map((x, i) => `[${i + 1}:a]adelay=${Math.max(0, Math.round(x.t - wall0))}:all=1[a${i}]`);
    filtros.push(narracion.map((_, i) => `[a${i}]`).join('') + `amix=inputs=${narracion.length}:normalize=0:dropout_transition=0,loudnorm=I=-16:TP=-1.5[a]`);
    a.push('-filter_complex', filtros.join(';'), '-map', '0:v', '-map', '[a]', '-c:v', 'copy', '-c:a', 'aac', '-ar', '48000', '-b:a', '160k', '-movflags', '+faststart', OUT);
    execFileSync(FFMPEG, a, { stdio: 'ignore' });
  } else {
    fs.copyFileSync(video, OUT);
  }
  fs.rmSync(TMP, { recursive: true, force: true });
  return { OUT, seg: Math.round((wallFin - wall0) / 1000), voz: narracion.length };
}

process.on('unhandledRejection', (e) => { if (!/Target page, context or browser has been closed/.test(String(e?.message))) { console.error('FALLO:', e?.message ?? e); process.exit(1); } });

(async () => {
  console.log(PRUEBA ? 'Modo prueba (capturas, sin video)' : `Narración: ${EDGE ? 'sí (' + VOICE + ')' : 'no, solo subtítulos'}`);
  const procesos = await asegurarServicios();
  let browser;
  try {
    if (!REUSAR_ESCRITORIO || !fs.existsSync(path.join(ESCRITORIO, 'frames.txt'))) grabarEscritorio();
    const escritorio = leerEscritorio();
    browser = await chromium.launch({ channel: process.env.NAVEGADOR || 'msedge', headless: true, args: ['--allow-file-access-from-files'] });
    process.stdout.write('Grabando… ');
    const r = await grabar(browser, escritorio);
    console.log(r ? `${path.basename(r.OUT)} · ${r.seg} s${r.voz ? ' · con voz' : ''}` : 'capturas listas');
  } finally {
    if (browser) await browser.close();
    detener(procesos);
  }
})().catch((e) => { console.error('FALLO:', e.message); process.exit(1); });
