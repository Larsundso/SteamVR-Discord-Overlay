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

  /* ===== Content split: channel browser + console ===== */

  .content-area {
    flex: 1;
    display: flex;
    flex-direction: row;
    min-height: 0;
  }

  .channel-browser {
    width: 220px;
    flex-shrink: 0;
    background: #2b2d31;
    border-right: 1px solid #3f4147;
    display: flex;
    flex-direction: column;
    overflow: hidden;
  }

  .channel-browser-header {
    padding: 12px 12px 8px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    border-bottom: 1px solid #3f4147;
  }

  .channel-browser-header h2 {
    margin-bottom: 0;
  }

  .active-subs { padding: 4px 8px 0; }
  .active-subs:empty { display: none; }
  .active-sub { display: flex; align-items: center; justify-content: space-between; padding: 3px 6px; margin: 2px 0; background: #313338; border-radius: 4px; font-size: 12px; color: #b5bac1; }
  .active-sub .sub-name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .active-sub .sub-name .hash { color: #5865f2; font-weight: 700; margin-right: 2px; }
  .active-sub .sub-x { background: none; border: none; color: #8e9297; cursor: pointer; font-size: 14px; padding: 0 4px; flex-shrink: 0; }
  .active-sub .sub-x:hover { color: #ed4245; }

  .channel-search-wrap { padding: 6px 10px 2px; }
  .channel-search {
    width: 100%;
    padding: 5px 8px;
    background: #1a1b1e;
    border: 1px solid #3f4147;
    border-radius: 4px;
    color: #dcddde;
    font-size: 12px;
    font-family: inherit;
    outline: none;
  }
  .channel-search:focus { border-color: #5865f2; }
  .channel-search::placeholder { color: #5d6169; }

  .channel-browser-count {
    font-size: 11px;
    color: #747f8d;
    background: #4e5058;
    padding: 1px 6px;
    border-radius: 8px;
    font-weight: 600;
  }

  .channel-list {
    flex: 1;
    overflow-y: auto;
    padding: 4px 0;
  }

  .channel-list::-webkit-scrollbar,
  .console::-webkit-scrollbar {
    width: 6px;
  }

  .channel-list::-webkit-scrollbar-track,
  .console::-webkit-scrollbar-track {
    background: transparent;
  }

  .channel-list::-webkit-scrollbar-thumb,
  .console::-webkit-scrollbar-thumb {
    background: #4e5058;
    border-radius: 3px;
  }

  .channel-list::-webkit-scrollbar-thumb:hover,
  .console::-webkit-scrollbar-thumb:hover {
    background: #5865f2;
  }

  /* Guild (server) headers */
  .guild-header {
    display: flex;
    align-items: center;
    gap: 4px;
    padding: 6px 12px;
    cursor: pointer;
    user-select: none;
    font-size: 11px;
    text-transform: uppercase;
    letter-spacing: 0.8px;
    color: #8e9297;
    transition: color 0.15s;
  }

  .guild-header:hover {
    color: #dcddde;
  }

  .guild-header .arrow {
    font-size: 10px;
    width: 12px;
    text-align: center;
    transition: transform 0.15s;
    flex-shrink: 0;
  }

  .guild-header.collapsed .arrow {
    transform: rotate(0deg);
  }

  .guild-header:not(.collapsed) .arrow {
    transform: rotate(90deg);
  }

  .guild-header .guild-name {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    flex: 1;
  }

  .guild-channels {
    overflow: hidden;
    transition: max-height 0.2s ease;
  }

  .guild-channels.collapsed {
    max-height: 0 !important;
  }

  /* Channel row with checkbox */
  .channel-item {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 3px 12px 3px 20px;
    cursor: pointer;
    font-size: 13px;
    color: #747f8d;
    transition: background 0.1s, color 0.1s;
    user-select: none;
  }

  .channel-item:hover {
    background: #35373c;
    color: #dcddde;
  }

  .channel-item.subscribed {
    color: #b5bac1;
  }

  .channel-item input[type="checkbox"] {
    appearance: none;
    -webkit-appearance: none;
    width: 16px;
    height: 16px;
    border: 2px solid #4e5058;
    border-radius: 3px;
    background: transparent;
    cursor: pointer;
    flex-shrink: 0;
    position: relative;
    transition: background 0.15s, border-color 0.15s;
  }

  .channel-item input[type="checkbox"]:checked {
    background: #5865f2;
    border-color: #5865f2;
  }

  .channel-item input[type="checkbox"]:checked::after {
    content: '';
    position: absolute;
    left: 3px;
    top: 0px;
    width: 5px;
    height: 9px;
    border: solid white;
    border-width: 0 2px 2px 0;
    transform: rotate(45deg);
  }

  .channel-item input[type="checkbox"]:hover {
    border-color: #5865f2;
  }

  .channel-item .hash {
    color: #4e5058;
    font-weight: 700;
    font-size: 15px;
    line-height: 1;
    flex-shrink: 0;
  }

  .channel-item.subscribed .hash {
    color: #5865f2;
  }

  .channel-item .channel-name {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    flex: 1;
  }

  /* Loading and empty states */
  .channel-browser-empty {
    padding: 16px 12px;
    text-align: center;
    color: #747f8d;
    font-size: 12px;
    line-height: 1.5;
  }

  .channel-browser-loading {
    padding: 16px 12px;
    text-align: center;
    color: #8e9297;
    font-size: 12px;
  }

  .channel-browser-loading::after {
    content: '';
    display: inline-block;
    width: 12px;
    height: 12px;
    border: 2px solid #4e5058;
    border-top-color: #5865f2;
    border-radius: 50%;
    margin-left: 6px;
    vertical-align: middle;
    animation: spin 0.8s linear infinite;
  }

  @keyframes spin {
    to { transform: rotate(360deg); }
  }

  /* ===== Chat messages in console ===== */

  .console .msg {
    padding: 2px 0;
    line-height: 1.5;
    word-wrap: break-word;
  }

  .console .msg .msg-channel {
    color: #5865f2;
    font-weight: 600;
    font-size: 12px;
  }

  .console .msg .msg-author {
    color: #faa81a;
    font-weight: 600;
  }

  .console .msg .msg-content {
    color: #dcddde;
  }

  .console .msg .msg-edited {
    color: #747f8d;
    font-size: 10px;
    font-style: italic;
    margin-left: 4px;
  }

  .console .msg.deleted {
    opacity: 0.45;
    text-decoration: line-through;
    text-decoration-color: #ed4245;
  }

  .console .msg.deleted .msg-content::after {
    content: ' (deleted)';
    color: #ed4245;
    font-size: 10px;
    font-style: italic;
    text-decoration: none;
    display: inline;
  }

  /* ===== Setup wizard ===== */
  .setup-wizard {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 24px;
    overflow-y: auto;
  }
  .setup-inner {
    background: #2b2d31;
    border-radius: 12px;
    padding: 32px;
    max-width: 520px;
    width: 100%;
  }
  .setup-inner a { text-decoration: none; }
  .setup-inner a:hover { text-decoration: underline; }
  .setup-inner input:focus { border-color: #5865f2; }

  /* ===== Responsive: narrow screens ===== */

  @media (max-width: 800px) {
    .content-area {
      flex-direction: column;
    }

    .channel-browser {
      width: 100%;
      max-height: 200px;
      border-right: none;
      border-bottom: 1px solid #3f4147;
    }
  }
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
      <div class="slider-row"><label>Yaw</label><input type="range" min="-180" max="180" step="1" id="sYaw" oninput="set('OverlayYaw',this.value)"><span class="slider-val" id="vYaw">0</span></div>
      <div class="slider-row"><label>Pitch</label><input type="range" min="-180" max="180" step="1" id="sPitch" oninput="set('OverlayPitch',this.value)"><span class="slider-val" id="vPitch">0</span></div>
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
    <h2>Mute Button</h2>
    <div class="section">
      <div class="toggle-row"><label>Enabled</label><label class="toggle"><input type="checkbox" id="tMuteEnabled" onchange="setNested('MuteButton','Enabled',this.checked)"><span class="slider"></span></label></div>
      <div class="control-row"><label>Attach to</label><select id="sMuteAttach" onchange="setNested('MuteButton','AttachTo',this.value)" style="background:#4e5058;border:none;color:#dcddde;padding:4px 8px;border-radius:4px;font-size:12px"><option value="left">Left Controller</option><option value="right">Right Controller</option><option value="hmd">HMD</option><option value="playspace">Playspace</option></select></div>
      <div class="control-row"><label>X</label><div class="btn-row"><button class="btn sm" onclick="adjNested('MuteButton','X',-0.01)">-</button><span class="value" id="vMuteX">0</span><button class="btn sm" onclick="adjNested('MuteButton','X',0.01)">+</button></div></div>
      <div class="control-row"><label>Y</label><div class="btn-row"><button class="btn sm" onclick="adjNested('MuteButton','Y',-0.01)">-</button><span class="value" id="vMuteY">0</span><button class="btn sm" onclick="adjNested('MuteButton','Y',0.01)">+</button></div></div>
      <div class="control-row"><label>Z</label><div class="btn-row"><button class="btn sm" onclick="adjNested('MuteButton','Z',-0.01)">-</button><span class="value" id="vMuteZ">0</span><button class="btn sm" onclick="adjNested('MuteButton','Z',0.01)">+</button></div></div>
      <div class="slider-row"><label>Yaw</label><input type="range" min="-180" max="180" step="1" id="sMuteYaw" oninput="setNested('MuteButton','Yaw',this.value)"><span class="slider-val" id="vMuteYaw">0</span></div>
      <div class="slider-row"><label>Pitch</label><input type="range" min="-180" max="180" step="1" id="sMutePitch" oninput="setNested('MuteButton','Pitch',this.value)"><span class="slider-val" id="vMutePitch">0</span></div>
      <div class="slider-row"><label>Rotation</label><input type="range" min="-180" max="180" step="1" id="sMuteRot" oninput="setNested('MuteButton','Rotation',this.value)"><span class="slider-val" id="vMuteRot">0</span></div>
      <div class="slider-row"><label>Scale</label><input type="range" min="0.01" max="0.1" step="0.005" id="sMuteScale" oninput="setNested('MuteButton','Scale',this.value)"><span class="slider-val" id="vMuteScale">0.04</span></div>
      <div class="slider-row"><label>Opacity</label><input type="range" min="0.1" max="1.0" step="0.05" id="sMuteOpacity" oninput="setNested('MuteButton','Opacity',this.value)"><span class="slider-val" id="vMuteOpacity">0.9</span></div>
    </div>
  </div>

  <div>
    <h2>Deafen Button</h2>
    <div class="section">
      <div class="toggle-row"><label>Enabled</label><label class="toggle"><input type="checkbox" id="tDeafenEnabled" onchange="setNested('DeafenButton','Enabled',this.checked)"><span class="slider"></span></label></div>
      <div class="control-row"><label>Attach to</label><select id="sDeafenAttach" onchange="setNested('DeafenButton','AttachTo',this.value)" style="background:#4e5058;border:none;color:#dcddde;padding:4px 8px;border-radius:4px;font-size:12px"><option value="left">Left Controller</option><option value="right">Right Controller</option><option value="hmd">HMD</option><option value="playspace">Playspace</option></select></div>
      <div class="control-row"><label>X</label><div class="btn-row"><button class="btn sm" onclick="adjNested('DeafenButton','X',-0.01)">-</button><span class="value" id="vDeafenX">0</span><button class="btn sm" onclick="adjNested('DeafenButton','X',0.01)">+</button></div></div>
      <div class="control-row"><label>Y</label><div class="btn-row"><button class="btn sm" onclick="adjNested('DeafenButton','Y',-0.01)">-</button><span class="value" id="vDeafenY">0</span><button class="btn sm" onclick="adjNested('DeafenButton','Y',0.01)">+</button></div></div>
      <div class="control-row"><label>Z</label><div class="btn-row"><button class="btn sm" onclick="adjNested('DeafenButton','Z',-0.01)">-</button><span class="value" id="vDeafenZ">0</span><button class="btn sm" onclick="adjNested('DeafenButton','Z',0.01)">+</button></div></div>
      <div class="slider-row"><label>Yaw</label><input type="range" min="-180" max="180" step="1" id="sDeafenYaw" oninput="setNested('DeafenButton','Yaw',this.value)"><span class="slider-val" id="vDeafenYaw">0</span></div>
      <div class="slider-row"><label>Pitch</label><input type="range" min="-180" max="180" step="1" id="sDeafenPitch" oninput="setNested('DeafenButton','Pitch',this.value)"><span class="slider-val" id="vDeafenPitch">0</span></div>
      <div class="slider-row"><label>Rotation</label><input type="range" min="-180" max="180" step="1" id="sDeafenRot" oninput="setNested('DeafenButton','Rotation',this.value)"><span class="slider-val" id="vDeafenRot">0</span></div>
      <div class="slider-row"><label>Scale</label><input type="range" min="0.01" max="0.1" step="0.005" id="sDeafenScale" oninput="setNested('DeafenButton','Scale',this.value)"><span class="slider-val" id="vDeafenScale">0.04</span></div>
      <div class="slider-row"><label>Opacity</label><input type="range" min="0.1" max="1.0" step="0.05" id="sDeafenOpacity" oninput="setNested('DeafenButton','Opacity',this.value)"><span class="slider-val" id="vDeafenOpacity">0.9</span></div>
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
  <div class="content-area">
    <div class="channel-browser">
      <div class="channel-browser-header">
        <h2>Channels</h2>
        <span class="channel-browser-count" id="subCount">0</span>
      </div>
      <div class="active-subs" id="activeSubs"></div>
      <div class="channel-search-wrap">
        <input type="text" class="channel-search" id="guildSearch" placeholder="Filter servers..." oninput="applyFilters()">
        <input type="text" class="channel-search" id="channelSearch" placeholder="Filter channels..." oninput="applyFilters()" style="margin-top:4px">
      </div>
      <div class="channel-list" id="channelList">
        <div class="channel-browser-empty" id="channelEmpty">
          Connect to Discord to browse channels
        </div>
      </div>
    </div>
    <div class="console" id="console"></div>
    <div class="setup-wizard" id="setupWizard" style="display:none">
      <div class="setup-inner">
        <h2 style="font-size:18px;color:#dcddde;text-transform:none;letter-spacing:0;margin-bottom:16px">Discord App Setup</h2>
        <p style="color:#b5bac1;font-size:13px;line-height:1.6;margin-bottom:12px">Each user needs their own Discord app. Follow these steps:</p>
        <ol style="color:#b5bac1;font-size:13px;line-height:2;padding-left:20px;margin-bottom:20px">
          <li>Go to <a href="https://discord.com/developers/applications" target="_blank" style="color:#5865f2">discord.com/developers/applications</a></li>
          <li>Click <b style="color:#dcddde">New Application</b> and give it any name</li>
          <li>Copy the <b style="color:#dcddde">Application ID</b> from the General Information page</li>
          <li>Go to <b style="color:#dcddde">OAuth2</b> in the sidebar</li>
          <li>Click <b style="color:#dcddde">Reset Secret</b> and copy the new Client Secret</li>
          <li>Add <code style="background:#1a1b1e;padding:2px 6px;border-radius:3px">http://localhost</code> as a Redirect URI and save</li>
          <li>Paste both values below and click Save</li>
        </ol>
        <div style="margin-bottom:10px">
          <label style="font-size:12px;color:#8e9297;display:block;margin-bottom:4px">Application ID (Client ID)</label>
          <input type="text" id="setupClientId" placeholder="e.g. 1234567890123456789" style="width:100%;padding:8px 10px;background:#1a1b1e;border:1px solid #3f4147;border-radius:4px;color:#dcddde;font-size:13px;font-family:inherit;outline:none">
        </div>
        <div style="margin-bottom:16px">
          <label style="font-size:12px;color:#8e9297;display:block;margin-bottom:4px">Client Secret</label>
          <input type="text" id="setupClientSecret" placeholder="e.g. AbCdEfGhIjKlMnOpQrStUvWxYz" style="width:100%;padding:8px 10px;background:#1a1b1e;border:1px solid #3f4147;border-radius:4px;color:#dcddde;font-size:13px;font-family:inherit;outline:none">
        </div>
        <button class="btn" style="width:100%;padding:10px;background:#5865f2;font-size:14px" onclick="saveDiscordApp()">Save & Connect</button>
        <p id="setupError" style="color:#ed4245;font-size:12px;margin-top:8px;display:none"></p>
      </div>
    </div>
  </div>
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
      else if (msg.type === 'message_create') addMessage(msg.data);
      else if (msg.type === 'message_update') updateMessage(msg.data);
      else if (msg.type === 'message_delete') deleteMessage(msg.data);
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

let guildsLoaded = false;

function updateState(s) {
  const dot = document.getElementById('connDot');
  const txt = document.getElementById('connText');
  if (s.voiceState) {
    const st = s.voiceState;
    dot.className = 'dot ' + ({VOICE_CONNECTED:'green',CONNECTED:'green',VOICE_CONNECTING:'yellow',CONNECTING:'yellow',AUTHENTICATING:'yellow',AWAITING_ENDPOINT:'yellow',NO_ROUTE:'red'}[st] || 'gray');
    txt.textContent = st.replace(/_/g,' ').toLowerCase();

    if ((st === 'CONNECTED' || st === 'VOICE_CONNECTED') && !guildsLoaded) {
      loadGuilds();
    }
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
  document.getElementById('sYaw').value = settings.OverlayYaw;
  document.getElementById('vPitch').textContent = settings.OverlayPitch?.toFixed(0);
  document.getElementById('sPitch').value = settings.OverlayPitch;
  document.getElementById('vWidth').textContent = settings.OverlayWidth?.toFixed(2);
  document.getElementById('sWidth').value = settings.OverlayWidth;
  document.getElementById('vOpacity').textContent = settings.OverlayOpacity?.toFixed(2);
  document.getElementById('sOpacity').value = settings.OverlayOpacity;
  document.getElementById('vThreshold').textContent = settings.MutedUserThreshold;
  document.getElementById('tUnmuted').checked = settings.ShowOnlyUnmuted;
  document.getElementById('tAutoStart').checked = settings.AutoStartWithSteamVR;
  document.getElementById('vPipe').textContent = settings.DiscordPipe === -1 ? 'auto' : settings.DiscordPipe;

  var mb = settings.MuteButton || {};
  document.getElementById('tMuteEnabled').checked = mb.Enabled !== false;
  document.getElementById('sMuteAttach').value = mb.AttachTo || 'left';
  document.getElementById('vMuteX').textContent = (mb.X || 0).toFixed(2);
  document.getElementById('vMuteY').textContent = (mb.Y || 0).toFixed(2);
  document.getElementById('vMuteZ').textContent = (mb.Z || 0).toFixed(2);
  document.getElementById('vMuteYaw').textContent = (mb.Yaw || 0).toFixed(0);
  document.getElementById('sMuteYaw').value = mb.Yaw || 0;
  document.getElementById('vMutePitch').textContent = (mb.Pitch || 0).toFixed(0);
  document.getElementById('sMutePitch').value = mb.Pitch || 0;
  document.getElementById('vMuteRot').textContent = (mb.Rotation || 0).toFixed(0);
  document.getElementById('sMuteRot').value = mb.Rotation || 0;
  document.getElementById('sMuteScale').value = mb.Scale || 0.04;
  document.getElementById('vMuteScale').textContent = (mb.Scale || 0.04).toFixed(3);
  document.getElementById('sMuteOpacity').value = mb.Opacity || 0.9;
  document.getElementById('vMuteOpacity').textContent = (mb.Opacity || 0.9).toFixed(2);

  var db = settings.DeafenButton || {};
  document.getElementById('tDeafenEnabled').checked = db.Enabled !== false;
  document.getElementById('sDeafenAttach').value = db.AttachTo || 'left';
  document.getElementById('vDeafenX').textContent = (db.X || 0).toFixed(2);
  document.getElementById('vDeafenY').textContent = (db.Y || 0).toFixed(2);
  document.getElementById('vDeafenZ').textContent = (db.Z || 0).toFixed(2);
  document.getElementById('vDeafenYaw').textContent = (db.Yaw || 0).toFixed(0);
  document.getElementById('sDeafenYaw').value = db.Yaw || 0;
  document.getElementById('vDeafenPitch').textContent = (db.Pitch || 0).toFixed(0);
  document.getElementById('sDeafenPitch').value = db.Pitch || 0;
  document.getElementById('vDeafenRot').textContent = (db.Rotation || 0).toFixed(0);
  document.getElementById('sDeafenRot').value = db.Rotation || 0;
  document.getElementById('sDeafenScale').value = db.Scale || 0.04;
  document.getElementById('vDeafenScale').textContent = (db.Scale || 0.04).toFixed(3);
  document.getElementById('sDeafenOpacity').value = db.Opacity || 0.9;
  document.getElementById('vDeafenOpacity').textContent = (db.Opacity || 0.9).toFixed(2);
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
  if (name === 'reauth') guildsLoaded = false;
  await fetch(`/api/command/${name}`, { method: 'POST' });
}

async function setNested(parent, key, value) {
  if (typeof value === 'string' && !isNaN(value)) value = parseFloat(value);
  if (!settings[parent]) settings[parent] = {};
  settings[parent][key] = value;
  await fetch('/api/settings', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify({[parent]: settings[parent]}) });
  loadSettings();
}

async function adjNested(parent, key, delta) {
  if (!settings[parent]) settings[parent] = {};
  settings[parent][key] = (settings[parent][key] || 0) + delta;
  await setNested(parent, key, settings[parent][key]);
}

function cyclePipe(dir) {
  const cur = settings.DiscordPipe ?? -1;
  let next = cur + dir;
  if (next > 9) next = -1;
  if (next < -1) next = 9;
  set('DiscordPipe', next);
}

/* ===== Channel browser ===== */

let guildsData = {};
let subscribedIds = new Set();
let channelNameMap = {};
let channelGuildMap = {};

async function loadGuilds() {
  const el = document.getElementById('channelList');
  const empty = document.getElementById('channelEmpty');
  if (empty) empty.textContent = '';
  el.innerHTML = '<div class="channel-browser-loading">Loading</div>';

  try {
    const [guildsResp, subsResp] = await Promise.all([
      fetch('/api/guilds'),
      fetch('/api/subscriptions')
    ]);
    if (!guildsResp.ok) {
      el.innerHTML = '<div class="channel-browser-empty">Not connected yet</div>';
      return;
    }
    const guildsObj = await guildsResp.json();
    const subs = subsResp.ok ? await subsResp.json() : {};
    subscribedIds = new Set(Object.keys(subs));
    for (var sid in subs) channelNameMap[sid] = subs[sid];
    updateSubCount();

    const guilds = guildsObj.guilds || [];
    if (guilds.length === 0) {
      el.innerHTML = '<div class="channel-browser-empty">No servers found</div>';
      return;
    }

    guildsLoaded = true;
    el.innerHTML = '';

    for (const guild of guilds) {
      const guildId = guild.id;
      const guildName = guild.name || 'Unknown Server';

      const header = document.createElement('div');
      header.className = 'guild-header collapsed';
      header.innerHTML = '<span class="arrow">\u25B8</span><span class="guild-name">' + escapeHtml(guildName) + '</span>';

      const channelsContainer = document.createElement('div');
      channelsContainer.className = 'guild-channels collapsed';

      header.addEventListener('click', async function() {
        const isCollapsed = header.classList.toggle('collapsed');
        if (isCollapsed) {
          channelsContainer.classList.add('collapsed');
        } else {
          channelsContainer.classList.remove('collapsed');
          if (channelsContainer.children.length === 0) {
            await loadChannels(guildId, channelsContainer);
          }
        }
      });

      el.appendChild(header);
      el.appendChild(channelsContainer);
      guildsData[guildId] = { name: guildName, channels: [] };
    }
  } catch (err) {
    el.innerHTML = '<div class="channel-browser-empty">Failed to load servers</div>';
  }
}

async function loadChannels(guildId, container) {
  container.innerHTML = '<div class="channel-browser-loading">Loading</div>';

  try {
    const resp = await fetch('/api/guilds/' + guildId + '/channels');
    const data = await resp.json();
    const channels = data.channels || [];

    const textChannels = channels
      .filter(function(c) { return c.type === 0; })
      .sort(function(a, b) { return a.name.localeCompare(b.name); });

    container.innerHTML = '';

    if (textChannels.length === 0) {
      container.innerHTML = '<div class="channel-browser-empty" style="padding:4px 20px;text-align:left">No text channels</div>';
      return;
    }

    for (const ch of textChannels) {
      channelNameMap[ch.id] = ch.name;
      channelGuildMap[ch.id] = guildsData[guildId]?.name || '';

      const item = document.createElement('label');
      item.className = 'channel-item' + (subscribedIds.has(ch.id) ? ' subscribed' : '');

      const cb = document.createElement('input');
      cb.type = 'checkbox';
      cb.checked = subscribedIds.has(ch.id);
      cb.addEventListener('change', function() {
        toggleChannel(ch.id, cb.checked, item);
      });

      const hash = document.createElement('span');
      hash.className = 'hash';
      hash.textContent = '#';

      const name = document.createElement('span');
      name.className = 'channel-name';
      name.textContent = ch.name;

      item.appendChild(cb);
      item.appendChild(hash);
      item.appendChild(name);
      container.appendChild(item);
    }

    guildsData[guildId].channels = textChannels;
  } catch (err) {
    container.innerHTML = '<div class="channel-browser-empty" style="padding:4px 20px;text-align:left">Failed to load channels</div>';
  }
}

async function toggleChannel(channelId, subscribe, itemEl) {
  try {
    const chName = channelNameMap[channelId] || '';
    const gName = channelGuildMap[channelId] || '';
    const endpoint = '/api/channels/' + channelId + (subscribe ? '/subscribe' : '/unsubscribe') + '?name=' + encodeURIComponent(chName) + '&guild=' + encodeURIComponent(gName);
    const resp = await fetch(endpoint, { method: 'POST' });
    if (!resp.ok) throw new Error('HTTP ' + resp.status);
    if (subscribe) {
      subscribedIds.add(channelId);
      itemEl.classList.add('subscribed');
    } else {
      subscribedIds.delete(channelId);
      itemEl.classList.remove('subscribed');
    }
    updateSubCount();
  } catch (err) {
    const cb = itemEl.querySelector('input[type="checkbox"]');
    if (cb) cb.checked = !subscribe;
  }
}

function updateSubCount() {
  document.getElementById('subCount').textContent = subscribedIds.size;
  renderActiveSubs();
}

function renderActiveSubs() {
  var el = document.getElementById('activeSubs');
  el.innerHTML = '';
  for (var id of subscribedIds) {
    var name = channelNameMap[id] || id;
    var row = document.createElement('div');
    row.className = 'active-sub';
    var span = document.createElement('span');
    span.className = 'sub-name';
    span.innerHTML = '<span class="hash">#</span>' + escapeHtml(name);
    var btn = document.createElement('button');
    btn.className = 'sub-x';
    btn.textContent = '\u00d7';
    btn.title = 'Unsubscribe';
    (function(chId) {
      btn.onclick = function() { unsubFromActive(chId); };
    })(id);
    row.appendChild(span);
    row.appendChild(btn);
    el.appendChild(row);
  }
}

async function unsubFromActive(channelId) {
  try {
    var chName = channelNameMap[channelId] || '';
    var gName = channelGuildMap[channelId] || '';
    var resp = await fetch('/api/channels/' + channelId + '/unsubscribe?name=' + encodeURIComponent(chName) + '&guild=' + encodeURIComponent(gName), { method: 'POST' });
    if (!resp.ok) throw new Error('HTTP ' + resp.status);
    subscribedIds.delete(channelId);
    updateSubCount();
    document.querySelectorAll('.channel-item input[type="checkbox"]').forEach(function(cb) {
      var item = cb.closest('.channel-item');
      var chName = item?.querySelector('.channel-name')?.textContent;
      if (chName === channelNameMap[channelId]) {
        cb.checked = false;
        item?.classList.remove('subscribed');
      }
    });
  } catch(e) { addLog('[dashboard] Unsub error: ' + e); }
}

let preFilterState = null;

function applyFilters() {
  const gq = (document.getElementById('guildSearch').value || '').toLowerCase().trim();
  const cq = (document.getElementById('channelSearch').value || '').toLowerCase().trim();
  const list = document.getElementById('channelList');
  const headers = list.querySelectorAll('.guild-header');
  const containers = list.querySelectorAll('.guild-channels');
  const active = gq || cq;

  if (!active) {
    headers.forEach(h => h.style.display = '');
    containers.forEach((c, i) => {
      c.style.display = '';
      for (const ch of c.querySelectorAll('.channel-item')) ch.style.display = '';
      if (preFilterState && preFilterState[i]) {
        c.classList.add('collapsed');
        if (headers[i]) headers[i].classList.add('collapsed');
      }
    });
    preFilterState = null;
    return;
  }

  if (preFilterState === null) {
    preFilterState = Array.from(containers).map(c => c.classList.contains('collapsed'));
  }

  headers.forEach((header, idx) => {
    const guildName = (header.querySelector('.guild-name')?.textContent || '').toLowerCase();
    const container = containers[idx];
    if (!container) return;

    const guildVisible = !gq || guildName.includes(gq);
    if (!guildVisible) {
      header.style.display = 'none';
      container.style.display = 'none';
      return;
    }

    const items = container.querySelectorAll('.channel-item');
    let hasVisibleChannel = items.length === 0;

    items.forEach(ch => {
      const chName = (ch.querySelector('.channel-name')?.textContent || '').toLowerCase();
      if (!cq || chName.includes(cq)) { ch.style.display = ''; hasVisibleChannel = true; }
      else ch.style.display = 'none';
    });

    if (hasVisibleChannel) {
      header.style.display = '';
      container.style.display = '';
      header.classList.remove('collapsed');
      container.classList.remove('collapsed');
    } else {
      header.style.display = 'none';
      container.style.display = 'none';
    }
  });
}

function escapeHtml(text) {
  var d = document.createElement('div');
  d.textContent = text;
  return d.innerHTML;
}

/* ===== Chat message display ===== */

function addMessage(data) {
  var msg = data.message || data;
  var channelId = msg.channel_id || data.channel_id || '';
  var channelName = channelNameMap[channelId] || channelId;
  var author = msg.author || {};
  var authorName = author.username || 'unknown';
  var content = msg.content || '';
  var edited = msg.edited_timestamp ? true : false;

  var el = document.getElementById('console');
  var div = document.createElement('div');
  div.className = 'msg';
  div.setAttribute('data-msg-id', msg.id || '');

  div.innerHTML =
    '<span class="msg-channel">#' + escapeHtml(channelName) + '</span> ' +
    '<span class="msg-author">' + escapeHtml(authorName) + '</span>: ' +
    '<span class="msg-content">' + escapeHtml(content) + '</span>' +
    (edited ? '<span class="msg-edited">(edited)</span>' : '');

  el.appendChild(div);
  el.scrollTop = el.scrollHeight;
}

function updateMessage(data) {
  var msg = data.message || data;
  var id = msg.id || '';
  if (!id) return;

  var existing = document.querySelector('.msg[data-msg-id="' + id + '"]');
  if (!existing) return;

  var content = msg.content || '';
  var contentSpan = existing.querySelector('.msg-content');
  if (contentSpan) contentSpan.textContent = content;

  if (!existing.querySelector('.msg-edited')) {
    var edited = document.createElement('span');
    edited.className = 'msg-edited';
    edited.textContent = '(edited)';
    existing.appendChild(edited);
  }
}

function deleteMessage(data) {
  var msg = data.message || data;
  var id = msg.id || '';
  if (!id) return;

  var existing = document.querySelector('.msg[data-msg-id="' + id + '"]');
  if (existing) existing.classList.add('deleted');
}

async function saveDiscordApp() {
  var clientId = document.getElementById('setupClientId').value.trim();
  var clientSecret = document.getElementById('setupClientSecret').value.trim();
  var errEl = document.getElementById('setupError');

  if (!clientId || !clientSecret) {
    errEl.textContent = 'Both fields are required.';
    errEl.style.display = 'block';
    return;
  }
  if (!/^\d{17,20}$/.test(clientId)) {
    errEl.textContent = 'Client ID should be a number (17-20 digits).';
    errEl.style.display = 'block';
    return;
  }

  errEl.style.display = 'none';
  await fetch('/api/settings', {
    method: 'POST',
    headers: {'Content-Type':'application/json'},
    body: JSON.stringify({ DiscordClientId: clientId, DiscordClientSecret: clientSecret })
  });
  addLog('Discord app credentials saved! The app will connect automatically.');
  document.getElementById('setupWizard').style.display = 'none';
  document.getElementById('console').style.display = '';
}

async function checkSetup() {
  var r = await fetch('/api/settings');
  var s = await r.json();
  if (!s.DiscordClientId || !s.DiscordClientSecret) {
    document.getElementById('setupWizard').style.display = 'flex';
    document.getElementById('console').style.display = 'none';
  }
}

loadSettings();
connect();
checkSetup();
setTimeout(loadGuilds, 1500);
</script>
</body>
</html>
""";
}
