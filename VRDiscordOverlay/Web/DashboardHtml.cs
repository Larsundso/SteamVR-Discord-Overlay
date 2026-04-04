namespace VRDiscordOverlay.Web;

public static class DashboardHtml
{
    public const string Page = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Discord VC Overlay</title>
<style>
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body { background: #1a1b1e; color: #dcddde; font-family: 'Segoe UI', sans-serif; display: flex; height: 100vh; }
  .sidebar { width: 280px; background: #2b2d31; padding: 16px; display: flex; flex-direction: column; gap: 12px; overflow-y: auto; border-right: 1px solid #3f4147; }
  .main { flex: 1; display: flex; flex-direction: column; }
  .console { flex: 1; padding: 12px 16px; overflow-y: auto; font-family: 'Cascadia Code', 'Consolas', monospace; font-size: 13px; line-height: 1.6; }
  .console .log { color: #b5bac1; }
  .console .log.join { color: #43b581; }
  .console .log.left { color: #f04747; }
  .console .log.error { color: #ed4245; }
  .console .log.info { color: #5865f2; }

  h2 { font-size: 11px; text-transform: uppercase; letter-spacing: 0.8px; color: #8e9297; margin-bottom: 4px; }
  .section { background: #313338; border-radius: 8px; padding: 12px; }

  .control-row { display: flex; align-items: center; justify-content: space-between; padding: 4px 0; }
  .control-row label { font-size: 13px; color: #b5bac1; }
  .control-row .value { font-size: 13px; color: #dcddde; font-weight: 600; min-width: 50px; text-align: right; }

  .btn-row { display: flex; gap: 6px; }
  .btn { background: #4e5058; border: none; color: #dcddde; padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 12px; font-family: inherit; }
  .btn:hover { background: #5865f2; }
  .btn.sm { padding: 2px 8px; font-size: 16px; min-width: 28px; }
  .btn.danger { background: #ed4245; color: white; }
  .btn.danger:hover { background: #c73e3e; }

  .slider-row { display: flex; align-items: center; gap: 8px; padding: 4px 0; }
  .slider-row label { font-size: 13px; color: #b5bac1; min-width: 50px; }
  input[type=range] { flex: 1; accent-color: #5865f2; }
  .slider-val { font-size: 12px; color: #8e9297; min-width: 36px; text-align: right; flex-shrink: 0; }

  .toggle-row { display: flex; align-items: center; justify-content: space-between; padding: 6px 0; }
  .toggle-row label { font-size: 13px; color: #b5bac1; }
  .toggle { position: relative; width: 40px; height: 22px; }
  .toggle input { display: none; }
  .toggle .slider { position: absolute; inset: 0; background: #4e5058; border-radius: 11px; cursor: pointer; transition: .2s; }
  .toggle .slider:before { content: ''; position: absolute; width: 16px; height: 16px; left: 3px; top: 3px; background: white; border-radius: 50%; transition: .2s; }
  .toggle input:checked + .slider { background: #5865f2; }
  .toggle input:checked + .slider:before { transform: translateX(18px); }

  .status-bar { padding: 8px 16px; background: #2b2d31; border-top: 1px solid #3f4147; font-size: 12px; color: #8e9297; display: flex; justify-content: space-between; }
  .dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; margin-right: 6px; }
  .dot.green { background: #43b581; }
  .dot.yellow { background: #faa81a; }
  .dot.red { background: #ed4245; }
  .dot.gray { background: #747f8d; }
</style>
</head>
<body>

<div class="sidebar">
  <div>
    <h2>Position</h2>
    <div class="section">
      <div class="control-row"><label>X</label><div class="btn-row"><button class="btn sm" onclick="adj('OverlayX',-0.02)">-</button><span class="value" id="vX">0</span><button class="btn sm" onclick="adj('OverlayX',0.02)">+</button></div></div>
      <div class="control-row"><label>Y</label><div class="btn-row"><button class="btn sm" onclick="adj('OverlayY',-0.02)">-</button><span class="value" id="vY">0</span><button class="btn sm" onclick="adj('OverlayY',0.02)">+</button></div></div>
      <div class="control-row"><label>Z</label><div class="btn-row"><button class="btn sm" onclick="adj('OverlayZ',-0.05)">-</button><span class="value" id="vZ">0</span><button class="btn sm" onclick="adj('OverlayZ',0.05)">+</button></div></div>
    </div>
  </div>

  <div>
    <h2>Rotation</h2>
    <div class="section">
      <div class="control-row"><label>Yaw</label><div class="btn-row"><button class="btn sm" onclick="adj('OverlayYaw',-2)">-</button><span class="value" id="vYaw">0</span><button class="btn sm" onclick="adj('OverlayYaw',2)">+</button></div></div>
      <div class="control-row"><label>Pitch</label><div class="btn-row"><button class="btn sm" onclick="adj('OverlayPitch',-2)">-</button><span class="value" id="vPitch">0</span><button class="btn sm" onclick="adj('OverlayPitch',2)">+</button></div></div>
    </div>
  </div>

  <div>
    <h2>Display</h2>
    <div class="section">
      <div class="slider-row">
        <label>Scale</label>
        <input type="range" min="0.1" max="1.0" step="0.02" id="sWidth" oninput="set('OverlayWidth',this.value)">
        <span class="slider-val" id="vWidth">0.4</span>
      </div>
      <div class="slider-row">
        <label>Opacity</label>
        <input type="range" min="0.1" max="1.0" step="0.05" id="sOpacity" oninput="set('OverlayOpacity',this.value)">
        <span class="slider-val" id="vOpacity">1.0</span>
      </div>
      <div class="control-row">
        <label>Muted threshold</label>
        <div class="btn-row"><button class="btn sm" onclick="adj('MutedUserThreshold',-1)">-</button><span class="value" id="vThreshold">5</span><button class="btn sm" onclick="adj('MutedUserThreshold',1)">+</button></div>
      </div>
      <div class="toggle-row">
        <label>Show only unmuted</label>
        <label class="toggle"><input type="checkbox" id="tUnmuted" onchange="set('ShowOnlyUnmuted',this.checked)"><span class="slider"></span></label>
      </div>
      <div class="toggle-row">
        <label>Auto-start with SteamVR</label>
        <label class="toggle"><input type="checkbox" id="tAutoStart" onchange="set('AutoStartWithSteamVR',this.checked)"><span class="slider"></span></label>
      </div>
    </div>
  </div>

  <div>
    <h2>Discord</h2>
    <div class="section">
      <div class="control-row">
        <label>Pipe</label>
        <div class="btn-row"><button class="btn sm" onclick="cyclePipe(-1)">-</button><span class="value" id="vPipe">auto</span><button class="btn sm" onclick="cyclePipe(1)">+</button></div>
      </div>
      <button class="btn danger" style="width:100%;margin-top:8px" onclick="cmd('reauth')">Re-authorize Discord</button>
    </div>
  </div>
</div>

<div class="main">
  <div class="console" id="console"></div>
  <div class="status-bar">
    <span><span class="dot gray" id="connDot"></span><span id="connText">Connecting...</span></span>
    <span id="pipeText"></span>
  </div>
</div>

<script>
let settings = {};
let ws;

function connect() {
  ws = new WebSocket(`ws://${location.host}/ws`);
  ws.onopen = () => addLog('[dashboard] Connected to app');
  ws.onmessage = (e) => {
    try {
      const msg = JSON.parse(e.data);
      if (msg.type === 'log') addLog(msg.message);
      else if (msg.type === 'state') updateState(msg.data);
    } catch(err) {
      addLog('[dashboard] Parse error: ' + err);
    }
  };
  ws.onerror = (e) => addLog('[dashboard] WebSocket error');
  ws.onclose = () => { addLog('[dashboard] Disconnected, reconnecting...'); setTimeout(connect, 1000); };
}

function addLog(text) {
  const el = document.getElementById('console');
  const div = document.createElement('div');
  div.className = 'log';
  if (text.startsWith('+')) div.className += ' join';
  else if (text.startsWith('-')) div.className += ' left';
  else if (text.toLowerCase().includes('error') || text.toLowerCase().includes('fail')) div.className += ' error';
  else if (text.includes('Authenticated') || text.includes('Connected') || text.includes('ready')) div.className += ' info';
  div.textContent = text;
  el.appendChild(div);
  el.scrollTop = el.scrollHeight;
}

function updateState(s) {
  const dot = document.getElementById('connDot');
  const txt = document.getElementById('connText');
  if (s.voiceState) {
    const st = s.voiceState;
    dot.className = 'dot ' + ({VOICE_CONNECTED:'green',CONNECTED:'green',VOICE_CONNECTING:'yellow',CONNECTING:'yellow',AUTHENTICATING:'yellow',AWAITING_ENDPOINT:'yellow',NO_ROUTE:'red'}[st] || 'gray');
    txt.textContent = st.replace(/_/g,' ').toLowerCase();
  }
  if (s.channel) txt.textContent = s.channel + ' — ' + (s.voiceState||'').replace(/_/g,' ').toLowerCase();
  if (s.pipe !== undefined) document.getElementById('pipeText').textContent = 'pipe ' + s.pipe;
}

async function loadSettings() {
  const r = await fetch('/api/settings');
  settings = await r.json();
  document.getElementById('vX').textContent = settings.OverlayX?.toFixed(2);
  document.getElementById('vY').textContent = settings.OverlayY?.toFixed(2);
  document.getElementById('vZ').textContent = settings.OverlayZ?.toFixed(2);
  document.getElementById('vYaw').textContent = settings.OverlayYaw?.toFixed(0);
  document.getElementById('vPitch').textContent = settings.OverlayPitch?.toFixed(0);
  document.getElementById('vWidth').textContent = settings.OverlayWidth?.toFixed(2);
  document.getElementById('sWidth').value = settings.OverlayWidth;
  document.getElementById('vOpacity').textContent = settings.OverlayOpacity?.toFixed(2);
  document.getElementById('sOpacity').value = settings.OverlayOpacity;
  document.getElementById('vThreshold').textContent = settings.MutedUserThreshold;
  document.getElementById('tUnmuted').checked = settings.ShowOnlyUnmuted;
  document.getElementById('tAutoStart').checked = settings.AutoStartWithSteamVR;
  document.getElementById('vPipe').textContent = settings.DiscordPipe === -1 ? 'auto' : settings.DiscordPipe;
}

async function adj(key, delta) {
  settings[key] = (settings[key] || 0) + delta;
  await set(key, settings[key]);
}

async function set(key, value) {
  if (typeof value === 'string' && !isNaN(value)) value = parseFloat(value);
  settings[key] = value;
  await fetch('/api/settings', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify({[key]: value}) });
  loadSettings();
}

async function cmd(name) {
  await fetch(`/api/command/${name}`, { method: 'POST' });
}

function cyclePipe(dir) {
  const cur = settings.DiscordPipe ?? -1;
  let next = cur + dir;
  if (next > 9) next = -1;
  if (next < -1) next = 9;
  set('DiscordPipe', next);
}

loadSettings();
connect();
</script>
</body>
</html>
""";
}
