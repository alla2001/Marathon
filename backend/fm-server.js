const mqtt = require('mqtt');
const fs = require('fs');
const path = require('path');
const { checkProfanity } = require('./profanity-filter');

// ============================================
// Load MQTT Settings (shared with main server)
// ============================================
const MQTT_SETTINGS_FILE = path.join(__dirname, 'mqtt-settings.json');
let mqttSettings = {
  brokerUrl: 'mqtt://localhost:1883',
  clientId: 'marathon-fm-leaderboard-server',
  username: '',
  password: '',
  reconnectPeriod: 5000,
  clean: true
};

function loadMqttSettings() {
  try {
    if (fs.existsSync(MQTT_SETTINGS_FILE)) {
      const data = fs.readFileSync(MQTT_SETTINGS_FILE, 'utf8');
      const loaded = JSON.parse(data);
      // Use shared settings but override clientId
      mqttSettings = { ...mqttSettings, ...loaded, clientId: 'marathon-fm-leaderboard-server' };
      console.log('[MQTT Settings] Loaded from mqtt-settings.json');
    }
  } catch (err) {
    console.error('[MQTT Settings] Error loading:', err.message);
  }
  return mqttSettings;
}

// ============================================
// JSON Database (single flat array, no game modes)
// ============================================
const DB_FILE = path.join(__dirname, 'fm-leaderboard.json');

let database = [];

function loadDatabase() {
  try {
    if (fs.existsSync(DB_FILE)) {
      const data = fs.readFileSync(DB_FILE, 'utf8');
      database = JSON.parse(data);
      if (!Array.isArray(database)) {
        database = [];
      }
      console.log(`[DB] Loaded ${database.length} entries`);
    } else {
      saveDatabase();
      console.log('[DB] Created new fm-leaderboard.json');
    }
  } catch (err) {
    console.error('[DB] Error loading database:', err.message);
    database = [];
    saveDatabase();
  }
}

function saveDatabase() {
  try {
    fs.writeFileSync(DB_FILE, JSON.stringify(database, null, 2), 'utf8');
  } catch (err) {
    console.error('[DB] Error saving database:', err.message);
  }
}

function findEntryByUsername(username) {
  return database.find(e => e.username.toLowerCase() === username.toLowerCase());
}

function addEntry(entry) {
  entry.createdAt = new Date().toISOString();
  entry.updatedAt = new Date().toISOString();
  database.push(entry);
  saveDatabase();
  return entry;
}

function updateEntry(username, updates) {
  const index = database.findIndex(e => e.username.toLowerCase() === username.toLowerCase());
  if (index !== -1) {
    database[index] = { ...database[index], ...updates, updatedAt: new Date().toISOString() };
    saveDatabase();
    return database[index];
  }
  return null;
}

function getTop10() {
  return [...database]
    .sort((a, b) => b.score - a.score)
    .slice(0, 10)
    .map((entry, index) => ({
      rank: index + 1,
      username: entry.username,
      score: entry.score,
      distance: entry.distance,
      time: entry.time
    }));
}

function getTotalDistances() {
  return database.reduce((sum, entry) => sum + (entry.distance || 0), 0);
}

// ============================================
// MQTT Topics
// ============================================
const TOPICS = {
  // Requests (server subscribes)
  WRITE: 'MarathonFM/leaderboard/write',
  CHECK_LEFT: 'MarathonFM/leaderboard/left/checkname',
  CHECK_RIGHT: 'MarathonFM/leaderboard/right/checkname',

  // Responses (server publishes)
  CHECK_LEFT_RESPONSE: 'MarathonFM/leaderboard/left/checkname/response',
  CHECK_RIGHT_RESPONSE: 'MarathonFM/leaderboard/right/checkname/response',

  // Broadcast
  TOP10: 'MarathonFM/leaderboard/top10'
};

// ============================================
// MQTT Client
// ============================================
let mqttClient = null;

function connectMQTT() {
  const options = {
    clientId: mqttSettings.clientId,
    clean: mqttSettings.clean !== false,
    reconnectPeriod: mqttSettings.reconnectPeriod || 5000
  };

  if (mqttSettings.username) {
    options.username = mqttSettings.username;
    options.password = mqttSettings.password;
  }

  const brokerUrl = mqttSettings.brokerUrl || 'mqtt://localhost:1883';
  console.log(`[MQTT] Connecting to ${brokerUrl}...`);

  mqttClient = mqtt.connect(brokerUrl, options);

  mqttClient.on('connect', () => {
    console.log('[MQTT] Connected to broker');

    const topicsToSubscribe = [
      TOPICS.WRITE,
      TOPICS.CHECK_LEFT,
      TOPICS.CHECK_RIGHT
    ];

    mqttClient.subscribe(topicsToSubscribe, (err) => {
      if (err) {
        console.error('[MQTT] Subscribe error:', err);
      } else {
        console.log('[MQTT] Subscribed to:', topicsToSubscribe.join(', '));
      }
    });

    // Broadcast top 10 immediately on connect
    setTimeout(() => {
      broadcastTop10();
    }, 1000);

    // Broadcast top 10 every 5 seconds
    setInterval(() => {
      broadcastTop10();
    }, 5000);
    console.log('[Broadcast] Top 10 broadcast every 5s');
  });

  mqttClient.on('message', handleMessage);

  mqttClient.on('error', (err) => {
    console.error('[MQTT] Error:', err.message);
  });

  mqttClient.on('close', () => {
    console.log('[MQTT] Connection closed');
  });

  mqttClient.on('reconnect', () => {
    console.log('[MQTT] Reconnecting...');
  });
}

// ============================================
// Message Handler
// ============================================
function handleMessage(topic, message) {
  try {
    const payload = JSON.parse(message.toString());
    console.log(`[MQTT] Received on ${topic}:`, payload);

    switch (topic) {
      case TOPICS.WRITE:
        handleWrite(payload);
        break;
      case TOPICS.CHECK_LEFT:
        handleCheckUsername(payload, TOPICS.CHECK_LEFT_RESPONSE);
        break;
      case TOPICS.CHECK_RIGHT:
        handleCheckUsername(payload, TOPICS.CHECK_RIGHT_RESPONSE);
        break;
      default:
        console.log(`[MQTT] Unknown topic: ${topic}`);
    }
  } catch (err) {
    console.error('[MQTT] Error processing message:', err.message);
  }
}

// ============================================
// Write Entry Handler
// ============================================
function handleWrite(payload) {
  const { username, distance, time } = payload;

  if (!username) {
    console.error('[DB] Write rejected - missing username');
    return;
  }

  // Score is derived from distance (rounded to int)
  const score = Math.round(distance || 0);

  try {
    let entry = findEntryByUsername(username);

    if (entry) {
      if (score > entry.score) {
        updateEntry(username, {
          score,
          distance: distance || entry.distance,
          time: time || entry.time
        });
        console.log(`[DB] Updated ${username}: score=${score}, distance=${distance}, time=${time}`);
      } else {
        console.log(`[DB] Score not updated for ${username} (${score} <= ${entry.score})`);
      }
    } else {
      addEntry({
        username,
        score,
        distance: distance || 0,
        time: time || 0
      });
      console.log(`[DB] New entry: ${username}, score=${score}, distance=${distance}, time=${time}`);
    }
  } catch (err) {
    console.error('[DB] Write error:', err.message);
  }
}

// ============================================
// Check Username Handler
// ============================================
function handleCheckUsername(payload, responseTopic) {
  const { username } = payload;

  if (!username) {
    publishResponse(responseTopic, {
      success: false,
      error: 'Missing username'
    });
    return;
  }

  try {
    // Check profanity first — treat as "not unique" if profane
    const profanityResult = checkProfanity(username);
    if (profanityResult.isProfane) {
      console.log(`[Filter] Username "${username}" blocked by profanity filter`);
      publishResponse(responseTopic, {
        success: true,
        username,
        isUnique: false,
        exists: true
      });
      return;
    }

    const exists = !!findEntryByUsername(username);

    console.log(`[DB] Username "${username}" ${exists ? 'EXISTS' : 'is UNIQUE'}`);
    publishResponse(responseTopic, {
      success: true,
      username,
      isUnique: !exists,
      exists: exists
    });
  } catch (err) {
    console.error('[DB] Check username error:', err.message);
    publishResponse(responseTopic, {
      success: false,
      error: err.message
    });
  }
}

// ============================================
// Broadcast Top 10
// ============================================
function broadcastTop10() {
  const top10 = getTop10();

  const message = {
    messageType: 'LEADERBOARD_UPDATE',
    timestamp: Date.now(),
    leaderboard: top10,
    totalDistances: getTotalDistances()
  };

  publishResponse(TOPICS.TOP10, message);
}

// ============================================
// Publish Helper
// ============================================
function publishResponse(topic, payload) {
  if (mqttClient && mqttClient.connected) {
    const message = JSON.stringify(payload);
    mqttClient.publish(topic, message, { qos: 1 });
    if (!topic.includes('top10')) {
      console.log(`[MQTT] Published to ${topic}:`, payload);
    }
  } else {
    console.error('[MQTT] Cannot publish - not connected');
  }
}

// ============================================
// Graceful Shutdown
// ============================================
function setupGracefulShutdown() {
  const shutdown = () => {
    console.log('\n[Server] Shutting down...');
    saveDatabase();
    console.log('[DB] Database saved');

    if (mqttClient) {
      mqttClient.end();
      console.log('[MQTT] Disconnected');
    }

    process.exit(0);
  };

  process.on('SIGINT', shutdown);
  process.on('SIGTERM', shutdown);
}

// ============================================
// Web Dashboard
// ============================================
const http = require('http');
const DASHBOARD_PORT = 3001;

let sseClients = [];

function getDashboardHTML() {
  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Marathon FM Dashboard</title>
<style>
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body { background: #0f0f0f; color: #e0e0e0; font-family: 'Segoe UI', system-ui, -apple-system, sans-serif; min-height: 100vh; }
  .header { background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%); padding: 20px 32px; border-bottom: 2px solid #e67e22; display: flex; align-items: center; justify-content: space-between; }
  .header h1 { font-size: 22px; font-weight: 600; color: #fff; }
  .header h1 span { color: #e67e22; }
  .mqtt-badge { padding: 6px 14px; border-radius: 20px; font-size: 12px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; }
  .mqtt-badge.connected { background: rgba(46,204,113,0.15); color: #2ecc71; border: 1px solid rgba(46,204,113,0.3); }
  .mqtt-badge.disconnected { background: rgba(231,76,60,0.15); color: #e74c3c; border: 1px solid rgba(231,76,60,0.3); }
  .stats-bar { display: flex; gap: 16px; padding: 16px 32px; background: #141422; border-bottom: 1px solid #222; flex-wrap: wrap; }
  .stat-card { background: #1a1a2e; border-radius: 10px; padding: 14px 20px; flex: 1; min-width: 160px; border: 1px solid #2a2a4a; }
  .stat-card .label { font-size: 11px; text-transform: uppercase; color: #888; letter-spacing: 0.8px; margin-bottom: 4px; }
  .stat-card .value { font-size: 22px; font-weight: 700; color: #e67e22; }
  .stat-card .value small { font-size: 13px; color: #888; font-weight: 400; }
  .controls { padding: 16px 32px; display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }
  .spacer { flex: 1; }
  .btn { padding: 8px 18px; border: none; border-radius: 8px; cursor: pointer; font-size: 13px; font-weight: 500; transition: all 0.2s; }
  .btn-danger { background: rgba(231,76,60,0.15); color: #e74c3c; border: 1px solid rgba(231,76,60,0.3); }
  .btn-danger:hover { background: rgba(231,76,60,0.3); }
  .all-entries-label { font-size: 12px; color: #666; }
  .table-wrap { padding: 0 32px 32px; }
  table { width: 100%; border-collapse: collapse; margin-top: 8px; }
  thead th { text-align: left; padding: 10px 14px; font-size: 11px; text-transform: uppercase; letter-spacing: 0.8px; color: #666; border-bottom: 2px solid #222; background: #141422; position: sticky; top: 0; }
  tbody tr { border-bottom: 1px solid #1a1a2e; transition: background 0.15s; }
  tbody tr:hover { background: #1a1a2e; }
  tbody td { padding: 10px 14px; font-size: 14px; }
  .rank { font-weight: 700; color: #e67e22; width: 50px; }
  .rank-1 { color: #f1c40f; }
  .rank-2 { color: #bdc3c7; }
  .rank-3 { color: #cd7f32; }
  .username { font-weight: 500; color: #fff; }
  .score { font-weight: 600; color: #2ecc71; }
  .distance { color: #3498db; }
  .time-col { color: #9b59b6; }
  .date-col { color: #666; font-size: 12px; }
  .del-btn { background: none; border: 1px solid rgba(231,76,60,0.3); color: #e74c3c; padding: 4px 10px; border-radius: 6px; cursor: pointer; font-size: 11px; transition: all 0.2s; }
  .del-btn:hover { background: rgba(231,76,60,0.3); }
  .empty { text-align: center; padding: 48px; color: #555; font-size: 15px; }
  .toast { position: fixed; bottom: 24px; right: 24px; padding: 12px 20px; border-radius: 10px; font-size: 13px; font-weight: 500; opacity: 0; transition: opacity 0.3s; pointer-events: none; z-index: 100; }
  .toast.show { opacity: 1; }
  .toast.success { background: rgba(46,204,113,0.9); color: #fff; }
  .toast.error { background: rgba(231,76,60,0.9); color: #fff; }
  .confirm-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.7); display: flex; align-items: center; justify-content: center; z-index: 200; }
  .confirm-box { background: #1a1a2e; border: 1px solid #2a2a4a; border-radius: 14px; padding: 28px; max-width: 400px; width: 90%; text-align: center; }
  .confirm-box h3 { color: #fff; margin-bottom: 10px; font-size: 17px; }
  .confirm-box p { color: #888; margin-bottom: 20px; font-size: 14px; }
  .confirm-box .btns { display: flex; gap: 10px; justify-content: center; }
  .confirm-box .btn-cancel { background: #2a2a4a; color: #ccc; }
  .confirm-box .btn-cancel:hover { background: #3a3a5a; }
  .confirm-box .btn-confirm { background: #e74c3c; color: #fff; }
  .confirm-box .btn-confirm:hover { background: #c0392b; }
</style>
</head>
<body>
<div class="header">
  <h1>Marathon <span>FM Dashboard</span></h1>
  <div class="mqtt-badge disconnected" id="mqttStatus">MQTT Disconnected</div>
</div>
<div class="stats-bar">
  <div class="stat-card"><div class="label">Total Entries</div><div class="value" id="totalEntries">0</div></div>
  <div class="stat-card"><div class="label">Total Distance</div><div class="value" id="totalDistance">0 <small>km</small></div></div>
  <div class="stat-card"><div class="label">Top Score</div><div class="value" id="topScore">-</div></div>
</div>
<div class="controls">
  <label style="display:flex;align-items:center;gap:6px;cursor:pointer;">
    <input type="checkbox" id="showAll" onchange="toggleShowAll()" style="accent-color:#e67e22;">
    <span class="all-entries-label">Show all entries (not just top 10)</span>
  </label>
  <div class="spacer"></div>
  <button class="btn btn-danger" onclick="confirmAction('clear-all')">Clear All Entries</button>
</div>
<div class="table-wrap">
  <table>
    <thead>
      <tr>
        <th>Rank</th>
        <th>Username</th>
        <th>Score</th>
        <th>Distance</th>
        <th>Time</th>
        <th>Date</th>
        <th></th>
      </tr>
    </thead>
    <tbody id="tableBody">
      <tr><td colspan="7" class="empty">Connecting...</td></tr>
    </tbody>
  </table>
</div>
<div class="toast" id="toast"></div>
<script>
let top10Data = [];
let allEntriesData = [];
let showAll = false;

function toggleShowAll() {
  showAll = document.getElementById('showAll').checked;
  renderTable();
}

function renderTable() {
  const data = showAll ? allEntriesData : top10Data;
  const tbody = document.getElementById('tableBody');
  if (!data || data.length === 0) {
    tbody.innerHTML = '<tr><td colspan="7" class="empty">No entries yet</td></tr>';
    return;
  }
  tbody.innerHTML = data.map((e, i) => {
    const rank = e.rank || i + 1;
    const rankClass = rank <= 3 ? ' rank-' + rank : '';
    const dist = typeof e.distance === 'number' ? (e.distance / 1000).toFixed(2) + ' km' : '-';
    const time = typeof e.time === 'number' ? formatTime(e.time) : '-';
    const date = e.createdAt ? new Date(e.createdAt).toLocaleDateString() : '-';
    return '<tr>' +
      '<td class="rank' + rankClass + '">#' + rank + '</td>' +
      '<td class="username">' + escHtml(e.username) + '</td>' +
      '<td class="score">' + (e.score || 0) + '</td>' +
      '<td class="distance">' + dist + '</td>' +
      '<td class="time-col">' + time + '</td>' +
      '<td class="date-col">' + date + '</td>' +
      '<td><button class="del-btn" onclick="confirmAction(\'delete\',\'' + escAttr(e.username) + '\')">Delete</button></td>' +
      '</tr>';
  }).join('');
}

function formatTime(s) {
  const m = Math.floor(s / 60);
  const sec = Math.floor(s % 60);
  return m + ':' + (sec < 10 ? '0' : '') + sec;
}

function escHtml(s) { const d = document.createElement('div'); d.textContent = s; return d.innerHTML; }
function escAttr(s) { return s.replace(/'/g, "\\\\'").replace(/"/g, '&quot;'); }

function showToast(msg, type) {
  const t = document.getElementById('toast');
  t.textContent = msg;
  t.className = 'toast show ' + type;
  setTimeout(() => t.className = 'toast', 2500);
}

function confirmAction(action, username) {
  let title, desc;
  if (action === 'clear-all') { title = 'Clear All Entries'; desc = 'This will permanently delete ALL FM leaderboard entries.'; }
  else if (action === 'delete') { title = 'Delete Entry'; desc = 'Delete entry for "' + username + '"?'; }
  const overlay = document.createElement('div');
  overlay.className = 'confirm-overlay';
  overlay.innerHTML = '<div class="confirm-box"><h3>' + title + '</h3><p>' + desc + '</p><div class="btns"><button class="btn btn-cancel" onclick="this.closest(\\'.confirm-overlay\\').remove()">Cancel</button><button class="btn btn-confirm" id="cfmBtn">Confirm</button></div></div>';
  document.body.appendChild(overlay);
  overlay.addEventListener('click', (e) => { if (e.target === overlay) overlay.remove(); });
  document.getElementById('cfmBtn').onclick = () => {
    overlay.remove();
    if (action === 'clear-all') clearAll();
    else if (action === 'delete') deleteEntry(username);
  };
}

async function clearAll() {
  try { const r = await fetch('/api/clear', { method: 'POST' }); const j = await r.json(); showToast(j.message || 'Cleared', 'success'); } catch(e) { showToast('Error: ' + e.message, 'error'); }
}
async function deleteEntry(username) {
  try { const r = await fetch('/api/delete/' + encodeURIComponent(username), { method: 'POST' }); const j = await r.json(); showToast(j.message || 'Deleted', 'success'); } catch(e) { showToast('Error: ' + e.message, 'error'); }
}

// SSE
const evtSource = new EventSource('/events');
evtSource.onmessage = function(event) {
  const data = JSON.parse(event.data);
  if (data.mqtt !== undefined) {
    const badge = document.getElementById('mqttStatus');
    badge.textContent = data.mqtt ? 'MQTT Connected' : 'MQTT Disconnected';
    badge.className = 'mqtt-badge ' + (data.mqtt ? 'connected' : 'disconnected');
  }
  if (data.top10) top10Data = data.top10;
  if (data.allEntries) allEntriesData = data.allEntries;
  if (data.stats) {
    document.getElementById('totalEntries').textContent = data.stats.totalEntries || 0;
    document.getElementById('totalDistance').innerHTML = (data.stats.totalDistanceKm || '0') + ' <small>km</small>';
    document.getElementById('topScore').textContent = data.stats.topScore || '-';
  }
  renderTable();
};
evtSource.onerror = function() {
  document.getElementById('mqttStatus').textContent = 'Dashboard Disconnected';
  document.getElementById('mqttStatus').className = 'mqtt-badge disconnected';
};
</script>
</body>
</html>`;
}

function getAllEntriesSorted() {
  return [...database]
    .sort((a, b) => b.score - a.score)
    .map((entry, index) => ({
      rank: index + 1,
      username: entry.username,
      score: entry.score,
      distance: entry.distance,
      time: entry.time,
      createdAt: entry.createdAt
    }));
}

function getSSEData() {
  const totalDist = getTotalDistances();
  const sorted = getAllEntriesSorted();
  const topScore = sorted.length > 0 ? sorted[0].score : 0;
  return JSON.stringify({
    mqtt: mqttClient && mqttClient.connected,
    top10: sorted.slice(0, 10),
    allEntries: sorted,
    stats: {
      totalEntries: database.length,
      totalDistanceKm: (totalDist / 1000).toFixed(2),
      topScore: topScore
    }
  });
}

function sendSSEUpdate() {
  const data = getSSEData();
  sseClients = sseClients.filter(res => {
    try { res.write('data: ' + data + '\n\n'); return true; } catch(e) { return false; }
  });
}

function startDashboardServer() {
  const server = http.createServer((req, res) => {
    res.setHeader('Access-Control-Allow-Origin', '*');

    if (req.method === 'GET' && req.url === '/') {
      res.writeHead(200, { 'Content-Type': 'text/html' });
      res.end(getDashboardHTML());
    }
    else if (req.method === 'GET' && req.url === '/events') {
      res.writeHead(200, {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
        'Connection': 'keep-alive',
        'X-Accel-Buffering': 'no'
      });
      res.flushHeaders();
      req.socket.setNoDelay(true);
      // Send initial data directly to this client
      const initData = getSSEData();
      res.write('data: ' + initData + '\n\n');
      sseClients.push(res);
      req.on('close', () => { sseClients = sseClients.filter(c => c !== res); });
    }
    else if (req.method === 'POST' && req.url === '/api/clear') {
      database = [];
      saveDatabase();
      console.log('[Dashboard] Cleared FM leaderboard');
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ success: true, message: 'FM leaderboard cleared' }));
      sendSSEUpdate();
    }
    else if (req.method === 'POST' && req.url.startsWith('/api/delete/')) {
      const username = decodeURIComponent(req.url.split('/api/delete/')[1]);
      const idx = database.findIndex(e => e.username.toLowerCase() === username.toLowerCase());
      if (idx !== -1) {
        database.splice(idx, 1);
        saveDatabase();
        console.log('[Dashboard] Deleted entry "' + username + '"');
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: true, message: 'Deleted "' + username + '"' }));
        sendSSEUpdate();
      } else {
        res.writeHead(404, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: false, error: 'Entry not found' }));
      }
    }
    else {
      res.writeHead(404, { 'Content-Type': 'text/plain' });
      res.end('Not Found');
    }
  });

  server.listen(DASHBOARD_PORT, () => {
    console.log('[Dashboard] Running at http://localhost:' + DASHBOARD_PORT);
  });

  setInterval(sendSSEUpdate, 5000);
}

// ============================================
// Main
// ============================================
function main() {
  console.log('========================================');
  console.log('  Marathon FM Leaderboard Server');
  console.log('========================================\n');

  loadMqttSettings();
  loadDatabase();
  setupGracefulShutdown();
  connectMQTT();
  startDashboardServer();

  console.log('\n[Server] Ready and listening for MQTT messages\n');
}

main();
