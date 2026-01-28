const mqtt = require('mqtt');
const fs = require('fs');
const path = require('path');
const { checkProfanity } = require('./profanity-filter');

// ============================================
// Load MQTT Settings
// ============================================
const MQTT_SETTINGS_FILE = path.join(__dirname, 'mqtt-settings.json');
let mqttSettings = {
  brokerUrl: 'mqtt://localhost:1883',
  clientId: 'leaderboard-server',
  username: '',
  password: '',
  reconnectPeriod: 5000,
  clean: true
};

function loadMqttSettings() {
  try {
    if (fs.existsSync(MQTT_SETTINGS_FILE)) {
      const data = fs.readFileSync(MQTT_SETTINGS_FILE, 'utf8');
      mqttSettings = { ...mqttSettings, ...JSON.parse(data) };
      console.log(`[MQTT Settings] Loaded from mqtt-settings.json`);
    } else {
      // Create default settings file
      fs.writeFileSync(MQTT_SETTINGS_FILE, JSON.stringify(mqttSettings, null, 2), 'utf8');
      console.log('[MQTT Settings] Created default mqtt-settings.json');
    }
  } catch (err) {
    console.error('[MQTT Settings] Error loading:', err.message);
  }
  return mqttSettings;
}

// ============================================
// JSON Database Setup (Per Game Mode)
// ============================================
const DB_FILE = path.join(__dirname, 'leaderboard.json');
const GAME_MODES = ['rowing', 'running', 'cycling'];

// Initialize database structure with separate leaderboards per game mode
let database = {
  rowing: [],
  running: [],
  cycling: []
};

// Load database from file
function loadDatabase() {
  try {
    if (fs.existsSync(DB_FILE)) {
      const data = fs.readFileSync(DB_FILE, 'utf8');
      const loaded = JSON.parse(data);

      // Handle migration from old format (single entries array)
      if (loaded.entries && !loaded.rowing) {
        console.log('[DB] Migrating from old database format...');
        // Move all old entries to 'rowing' by default or based on their data
        database = {
          rowing: [],
          running: [],
          cycling: []
        };
        loaded.entries.forEach(entry => {
          const mode = entry.gameMode || 'rowing';
          if (database[mode]) {
            database[mode].push(entry);
          } else {
            database.rowing.push(entry);
          }
        });
        saveDatabase();
        console.log('[DB] Migration complete');
      } else {
        // Ensure all game modes exist
        database = {
          rowing: loaded.rowing || [],
          running: loaded.running || [],
          cycling: loaded.cycling || []
        };
      }

      const totalEntries = database.rowing.length + database.running.length + database.cycling.length;
      console.log(`[DB] Loaded ${totalEntries} total entries (rowing: ${database.rowing.length}, running: ${database.running.length}, cycling: ${database.cycling.length})`);
    } else {
      // Create new database file
      saveDatabase();
      console.log('[DB] Created new database file');
    }
  } catch (err) {
    console.error('[DB] Error loading database:', err.message);
    database = { rowing: [], running: [], cycling: [] };
    saveDatabase();
  }
}

// Save database to file
function saveDatabase() {
  try {
    fs.writeFileSync(DB_FILE, JSON.stringify(database, null, 2), 'utf8');
  } catch (err) {
    console.error('[DB] Error saving database:', err.message);
  }
}

// Database operations (per game mode)
function findEntryByUsername(gameMode, username) {
  const entries = database[gameMode] || [];
  return entries.find(e => e.username.toLowerCase() === username.toLowerCase());
}

function addEntry(gameMode, entry) {
  if (!database[gameMode]) {
    database[gameMode] = [];
  }
  entry.gameMode = gameMode;
  entry.createdAt = new Date().toISOString();
  entry.updatedAt = new Date().toISOString();
  database[gameMode].push(entry);
  saveDatabase();
  return entry;
}

function updateEntry(gameMode, username, updates) {
  const entries = database[gameMode] || [];
  const index = entries.findIndex(e => e.username.toLowerCase() === username.toLowerCase());
  if (index !== -1) {
    entries[index] = { ...entries[index], ...updates, updatedAt: new Date().toISOString() };
    saveDatabase();
    return entries[index];
  }
  return null;
}

function getTop10(gameMode) {
  const entries = database[gameMode] || [];
  return [...entries]
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

function getAllTop10() {
  return {
    rowing: getTop10('rowing'),
    running: getTop10('running'),
    cycling: getTop10('cycling')
  };
}

// Calculate total distance for a game mode (sum of all entries' distances)
function getTotalDistance(gameMode) {
  const entries = database[gameMode] || [];
  return entries.reduce((sum, entry) => sum + (entry.distance || 0), 0);
}

// Get total distances for all game modes
function getAllTotalDistances() {
  const rowing = getTotalDistance('rowing');
  const running = getTotalDistance('running');
  const cycling = getTotalDistance('cycling');
  const total = rowing + running + cycling;

  return {
    rowing: rowing,
    running: running,
    cycling: cycling,
    total: total,
    // Also provide in km
    rowingKm: (rowing / 1000).toFixed(2),
    runningKm: (running / 1000).toFixed(2),
    cyclingKm: (cycling / 1000).toFixed(2),
    totalKm: (total / 1000).toFixed(2)
  };
}

// ============================================
// Load Game Configuration
// ============================================
let gameConfig = null;
const CONFIG_PATH = path.join(__dirname, 'config.json');

function loadGameConfig() {
  try {
    const configData = fs.readFileSync(CONFIG_PATH, 'utf8');
    gameConfig = JSON.parse(configData);
    console.log('[Config] Loaded game configuration:', Object.keys(gameConfig.gameModes).join(', '));
    return gameConfig;
  } catch (err) {
    console.error('[Config] Error loading config.json:', err.message);
    // Use default config
    gameConfig = {
      gameModes: {
        rowing: { routeDistance: 1600, timeLimit: 300, countdownSeconds: 3, resultsDisplaySeconds: 8, idleTimeoutSeconds: 10 },
        running: { routeDistance: 1600, timeLimit: 300, countdownSeconds: 3, resultsDisplaySeconds: 8, idleTimeoutSeconds: 10 },
        cycling: { routeDistance: 2000, timeLimit: 240, countdownSeconds: 3, resultsDisplaySeconds: 8, idleTimeoutSeconds: 10 }
      },
      broadcast: { onConnect: true, configIntervalSeconds: 5, leaderboardIntervalSeconds: 6 }
    };
    return gameConfig;
  }
}

// ============================================
// Hot-Reload Config Watcher
// ============================================
let configWatchDebounce = null;

function setupConfigWatcher() {
  console.log('[Config] Setting up hot-reload watcher for config.json');

  fs.watch(CONFIG_PATH, (eventType, filename) => {
    if (eventType === 'change') {
      // Debounce to avoid multiple reloads from rapid saves
      if (configWatchDebounce) {
        clearTimeout(configWatchDebounce);
      }

      configWatchDebounce = setTimeout(() => {
        console.log('[Config] config.json changed, reloading...');
        const oldConfig = gameConfig;
        loadGameConfig();

        // Broadcast new config immediately if connected
        if (mqttClient && mqttClient.connected) {
          broadcastGameConfig();
          console.log('[Config] Hot-reloaded and broadcasted new configuration!');
        }

        configWatchDebounce = null;
      }, 500); // Wait 500ms before reloading to handle multiple rapid writes
    }
  });
}

// ============================================
// MQTT Topics
// ============================================
const TOPICS = {
  // Requests (server subscribes)
  SUBMIT: 'leaderboard/submit',
  TOP10_REQUEST: 'leaderboard/top10/request',
  CHECK_USERNAME: 'leaderboard/check-username',
  POSITION_REQUEST: 'leaderboard/position/request',
  CONFIG_REQUEST: 'marathon/config/request',

  // Responses (server publishes to station-specific topics)
  SUBMIT_RESPONSE: 'leaderboard/submit/response',
  TOP10_RESPONSE: 'leaderboard/top10/response',
  CHECK_USERNAME_RESPONSE: 'leaderboard/check-username/response',
  POSITION_RESPONSE: 'leaderboard/position/response',

  // Broadcast topics
  CONFIG_BROADCAST: 'marathon/config/broadcast',
  LEADERBOARD_BROADCAST: 'leaderboard/broadcast'
};

// Helper to get station-specific response topic
function getResponseTopic(baseTopic, stationId) {
  if (stationId !== undefined && stationId !== null) {
    return `${baseTopic}/${stationId}`;
  }
  return baseTopic;
}

// ============================================
// MQTT Client Setup
// ============================================
let mqttClient = null;

function connectMQTT() {
  const options = {
    clientId: mqttSettings.clientId || 'leaderboard-server-' + Math.random().toString(16).substr(2, 8),
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

    // Subscribe to topics
    const topicsToSubscribe = [
      TOPICS.SUBMIT,
      TOPICS.TOP10_REQUEST,
      TOPICS.CHECK_USERNAME,
      TOPICS.POSITION_REQUEST,
      TOPICS.CONFIG_REQUEST
    ];

    mqttClient.subscribe(topicsToSubscribe, (err) => {
      if (err) {
        console.error('[MQTT] Subscribe error:', err);
      } else {
        console.log('[MQTT] Subscribed to:', topicsToSubscribe.join(', '));
      }
    });

    // Broadcast on connect if enabled
    if (gameConfig && gameConfig.broadcast && gameConfig.broadcast.onConnect) {
      setTimeout(() => {
        broadcastGameConfig();
        broadcastAllLeaderboards();
      }, 1000);
    }

    // Set up periodic config broadcast
    const configInterval = gameConfig?.broadcast?.configIntervalSeconds || gameConfig?.broadcast?.intervalSeconds || 5;
    if (configInterval > 0) {
      setInterval(() => {
        broadcastGameConfig();
      }, configInterval * 1000);
      console.log(`[Broadcast] Config broadcast every ${configInterval}s`);
    }

    // Set up periodic leaderboard broadcast
    const leaderboardInterval = gameConfig?.broadcast?.leaderboardIntervalSeconds || 6;
    if (leaderboardInterval > 0) {
      setInterval(() => {
        broadcastAllLeaderboards();
      }, leaderboardInterval * 1000);
      console.log(`[Broadcast] Leaderboard broadcast every ${leaderboardInterval}s`);
    }
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
      case TOPICS.SUBMIT:
        handleSubmit(payload);
        break;
      case TOPICS.TOP10_REQUEST:
        handleTop10Request(payload);
        break;
      case TOPICS.CHECK_USERNAME:
        handleCheckUsername(payload);
        break;
      case TOPICS.CONFIG_REQUEST:
        handleConfigRequest(payload);
        break;
      case TOPICS.POSITION_REQUEST:
        handlePositionRequest(payload);
        break;
      default:
        console.log(`[MQTT] Unknown topic: ${topic}`);
    }
  } catch (err) {
    console.error('[MQTT] Error processing message:', err.message);
  }
}

// ============================================
// Submit Score Handler
// ============================================
function handleSubmit(payload) {
  const { username, score, distance, time, stationId, gameMode } = payload;
  const responseTopic = getResponseTopic(TOPICS.SUBMIT_RESPONSE, stationId);

  // Default to 'rowing' if no game mode specified
  const mode = (gameMode || 'rowing').toLowerCase();

  if (!username || score === undefined) {
    publishResponse(responseTopic, {
      success: false,
      error: 'Missing required fields: username and score',
      stationId
    });
    return;
  }

  if (!GAME_MODES.includes(mode)) {
    publishResponse(responseTopic, {
      success: false,
      error: `Invalid game mode: ${mode}. Must be one of: ${GAME_MODES.join(', ')}`,
      stationId
    });
    return;
  }

  try {
    // Check if user exists in this game mode's leaderboard
    let entry = findEntryByUsername(mode, username);

    if (entry) {
      // Update only if new score is higher
      if (score > entry.score) {
        updateEntry(mode, username, {
          score,
          distance: distance || entry.distance,
          time: time || entry.time,
          stationId: stationId || entry.stationId
        });

        console.log(`[DB] Updated ${mode} score for ${username}: ${score}`);
        publishResponse(responseTopic, {
          success: true,
          message: 'Score updated (new high score)',
          username,
          score,
          gameMode: mode,
          stationId
        });
      } else {
        console.log(`[DB] Score not updated for ${username} in ${mode} (not a high score)`);
        publishResponse(responseTopic, {
          success: true,
          message: 'Score not updated (not higher than current)',
          username,
          currentScore: entry.score,
          submittedScore: score,
          gameMode: mode,
          stationId
        });
      }
    } else {
      // Create new entry
      addEntry(mode, {
        username,
        score,
        distance: distance || 0,
        time: time || 0,
        stationId: stationId || 0
      });

      console.log(`[DB] New ${mode} entry created for ${username}: ${score}`);
      publishResponse(responseTopic, {
        success: true,
        message: 'New entry created',
        username,
        score,
        gameMode: mode,
        stationId
      });
    }
  } catch (err) {
    console.error('[DB] Submit error:', err.message);
    publishResponse(responseTopic, {
      success: false,
      error: err.message,
      stationId
    });
  }
}

// ============================================
// Top 10 Request Handler
// ============================================
function handleTop10Request(payload) {
  const { stationId, gameMode } = payload || {};
  const responseTopic = getResponseTopic(TOPICS.TOP10_RESPONSE, stationId);

  try {
    let response;

    if (gameMode) {
      // Return top 10 for specific game mode
      const mode = gameMode.toLowerCase();
      if (!GAME_MODES.includes(mode)) {
        publishResponse(responseTopic, {
          success: false,
          error: `Invalid game mode: ${mode}`,
          stationId
        });
        return;
      }

      const rankedEntries = getTop10(mode);
      console.log(`[DB] Returning top 10 for ${mode} (${rankedEntries.length} entries)`);
      response = {
        success: true,
        gameMode: mode,
        entries: rankedEntries,
        stationId
      };
    } else {
      // Return top 10 for all game modes
      const allTop10 = getAllTop10();
      console.log(`[DB] Returning top 10 for all game modes`);
      response = {
        success: true,
        leaderboards: allTop10,
        stationId
      };
    }

    publishResponse(responseTopic, response);
  } catch (err) {
    console.error('[DB] Top 10 error:', err.message);
    publishResponse(responseTopic, {
      success: false,
      error: err.message,
      entries: [],
      stationId
    });
  }
}

// ============================================
// Check Username Handler
// ============================================
function handleCheckUsername(payload) {
  const { username, stationId, gameMode } = payload;
  const responseTopic = getResponseTopic(TOPICS.CHECK_USERNAME_RESPONSE, stationId);

  if (!username) {
    publishResponse(responseTopic, {
      success: false,
      error: 'Missing username',
      stationId
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
        exists: true,
        existsIn: [],
        stationId
      });
      return;
    }

    // Check across all game modes or specific mode
    let exists = false;
    let existsIn = [];

    if (gameMode) {
      const mode = gameMode.toLowerCase();
      exists = !!findEntryByUsername(mode, username);
      if (exists) existsIn.push(mode);
    } else {
      // Check all game modes
      GAME_MODES.forEach(mode => {
        if (findEntryByUsername(mode, username)) {
          exists = true;
          existsIn.push(mode);
        }
      });
    }

    console.log(`[DB] Username "${username}" exists: ${exists} (in: ${existsIn.join(', ') || 'none'})`);
    publishResponse(responseTopic, {
      success: true,
      username,
      isUnique: !exists,
      exists: exists,
      existsIn: existsIn,
      stationId
    });
  } catch (err) {
    console.error('[DB] Check username error:', err.message);
    publishResponse(responseTopic, {
      success: false,
      error: err.message,
      stationId
    });
  }
}

// ============================================
// Config Request Handler
// ============================================
function handleConfigRequest(payload) {
  const { stationId } = payload || {};
  console.log(`[Config] Config requested by station ${stationId || 'unknown'}`);
  broadcastGameConfig(stationId);
}

// ============================================
// Broadcast Game Config
// ============================================
function broadcastGameConfig(stationId = null) {
  if (!gameConfig) {
    console.error('[Config] No config loaded');
    return;
  }

  const configMessage = {
    messageType: 'GAME_CONFIG',
    timestamp: Date.now(),
    gameModes: gameConfig.gameModes
  };

  const topic = stationId
    ? `marathon/station${stationId}/config`
    : TOPICS.CONFIG_BROADCAST;

  publishResponse(topic, configMessage);
  console.log(`[Config] Broadcasted game config to ${stationId ? `station ${stationId}` : 'all stations'}`);
}

// ============================================
// Position Request Handler
// ============================================
function getPositionForDistance(gameMode, distance) {
  const entries = database[gameMode] || [];
  // Count how many entries have a higher distance (they rank above this player)
  const entriesAbove = entries.filter(e => (e.distance || 0) > distance).length;
  // Position is 1-based (1 = first place)
  return entriesAbove + 1;
}

function handlePositionRequest(payload) {
  const { distance, gameMode, stationId } = payload;
  const mode = (gameMode || 'rowing').toLowerCase();
  const responseTopic = getResponseTopic(TOPICS.POSITION_RESPONSE, stationId);

  if (distance === undefined) {
    publishResponse(responseTopic, {
      success: false,
      error: 'Missing required field: distance',
      stationId
    });
    return;
  }

  const position = getPositionForDistance(mode, distance);
  const totalEntries = (database[mode] || []).length;

  publishResponse(responseTopic, {
    success: true,
    position,
    totalEntries,
    distance,
    gameMode: mode,
    stationId
  });
}

// ============================================
// Broadcast All Leaderboards
// ============================================
function broadcastAllLeaderboards() {
  const allTop10 = getAllTop10();
  const totalDistances = getAllTotalDistances();

  const leaderboardMessage = {
    messageType: 'LEADERBOARD_UPDATE',
    timestamp: Date.now(),
    leaderboards: allTop10,
    totalDistances: totalDistances
  };

  // Broadcast combined leaderboard
  publishResponse(TOPICS.LEADERBOARD_BROADCAST, leaderboardMessage);

  // Also broadcast each game mode separately
  GAME_MODES.forEach(mode => {
    const modeMessage = {
      messageType: 'LEADERBOARD_UPDATE',
      timestamp: Date.now(),
      gameMode: mode,
      entries: allTop10[mode],
      totalDistance: totalDistances[mode],
      totalDistanceKm: totalDistances[`${mode}Km`]
    };
    publishResponse(`${TOPICS.LEADERBOARD_BROADCAST}/${mode}`, modeMessage);
  });

  console.log(`[Leaderboard] Broadcasted - Total distance: ${totalDistances.totalKm}km (rowing: ${totalDistances.rowingKm}km, running: ${totalDistances.runningKm}km, cycling: ${totalDistances.cyclingKm}km)`);
}

// ============================================
// Publish Response Helper
// ============================================
function publishResponse(topic, payload) {
  if (mqttClient && mqttClient.connected) {
    const message = JSON.stringify(payload);
    mqttClient.publish(topic, message, { qos: 1 });
    // Only log non-broadcast messages in detail
    if (!topic.includes('broadcast')) {
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

    // Save database before exit
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
const DASHBOARD_PORT = 3000;

let sseClients = [];

function getDashboardHTML() {
  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Marathon Game Dashboard</title>
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
  .tabs { display: flex; gap: 0; }
  .tab { padding: 8px 20px; background: #1a1a2e; border: 1px solid #2a2a4a; color: #888; cursor: pointer; font-size: 13px; font-weight: 500; transition: all 0.2s; }
  .tab:first-child { border-radius: 8px 0 0 8px; }
  .tab:last-child { border-radius: 0 8px 8px 0; }
  .tab.active { background: #e67e22; color: #fff; border-color: #e67e22; }
  .tab:hover:not(.active) { background: #252545; color: #ccc; }
  .spacer { flex: 1; }
  .btn { padding: 8px 18px; border: none; border-radius: 8px; cursor: pointer; font-size: 13px; font-weight: 500; transition: all 0.2s; }
  .btn-danger { background: rgba(231,76,60,0.15); color: #e74c3c; border: 1px solid rgba(231,76,60,0.3); }
  .btn-danger:hover { background: rgba(231,76,60,0.3); }
  .btn-warn { background: rgba(241,196,15,0.15); color: #f1c40f; border: 1px solid rgba(241,196,15,0.3); }
  .btn-warn:hover { background: rgba(241,196,15,0.3); }
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
  .all-entries-label { font-size: 12px; color: #666; }
</style>
</head>
<body>
<div class="header">
  <h1>Marathon <span>Game Dashboard</span></h1>
  <div class="mqtt-badge disconnected" id="mqttStatus">MQTT Disconnected</div>
</div>
<div class="stats-bar">
  <div class="stat-card"><div class="label">Total Entries</div><div class="value" id="totalEntries">0</div></div>
  <div class="stat-card"><div class="label">Rowing Entries</div><div class="value" id="rowingEntries">0</div></div>
  <div class="stat-card"><div class="label">Running Entries</div><div class="value" id="runningEntries">0</div></div>
  <div class="stat-card"><div class="label">Cycling Entries</div><div class="value" id="cyclingEntries">0</div></div>
  <div class="stat-card"><div class="label">Total Distance</div><div class="value" id="totalDistance">0 <small>km</small></div></div>
</div>
<div class="controls">
  <div class="tabs">
    <div class="tab active" data-mode="rowing" onclick="switchTab('rowing')">Rowing</div>
    <div class="tab" data-mode="running" onclick="switchTab('running')">Running</div>
    <div class="tab" data-mode="cycling" onclick="switchTab('cycling')">Cycling</div>
  </div>
  <label style="display:flex;align-items:center;gap:6px;margin-left:12px;cursor:pointer;">
    <input type="checkbox" id="showAll" onchange="toggleShowAll()" style="accent-color:#e67e22;">
    <span class="all-entries-label">Show all entries (not just top 10)</span>
  </label>
  <div class="spacer"></div>
  <button class="btn btn-warn" onclick="confirmAction('clear-mode')">Clear Current Mode</button>
  <button class="btn btn-danger" onclick="confirmAction('clear-all')">Clear All Modes</button>
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
let currentMode = 'rowing';
let leaderboardData = { rowing: [], running: [], cycling: [] };
let allData = { rowing: [], running: [], cycling: [] };
let stats = {};
let showAll = false;

function switchTab(mode) {
  currentMode = mode;
  document.querySelectorAll('.tab').forEach(t => t.classList.toggle('active', t.dataset.mode === mode));
  renderTable();
}

function toggleShowAll() {
  showAll = document.getElementById('showAll').checked;
  renderTable();
}

function renderTable() {
  const data = showAll ? allData[currentMode] : leaderboardData[currentMode];
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
  if (action === 'clear-all') { title = 'Clear All Modes'; desc = 'This will permanently delete ALL entries across rowing, running, and cycling.'; }
  else if (action === 'clear-mode') { title = 'Clear ' + currentMode.charAt(0).toUpperCase() + currentMode.slice(1); desc = 'This will delete all entries in the ' + currentMode + ' leaderboard.'; }
  else if (action === 'delete') { title = 'Delete Entry'; desc = 'Delete entry for "' + username + '" from ' + currentMode + '?'; }
  const overlay = document.createElement('div');
  overlay.className = 'confirm-overlay';
  overlay.innerHTML = '<div class="confirm-box"><h3>' + title + '</h3><p>' + desc + '</p><div class="btns"><button class="btn btn-cancel" onclick="this.closest(\\'.confirm-overlay\\').remove()">Cancel</button><button class="btn btn-confirm" id="cfmBtn">Confirm</button></div></div>';
  document.body.appendChild(overlay);
  overlay.addEventListener('click', (e) => { if (e.target === overlay) overlay.remove(); });
  document.getElementById('cfmBtn').onclick = () => {
    overlay.remove();
    if (action === 'clear-all') clearAll();
    else if (action === 'clear-mode') clearMode(currentMode);
    else if (action === 'delete') deleteEntry(currentMode, username);
  };
}

async function clearAll() {
  try { const r = await fetch('/api/clear', { method: 'POST' }); const j = await r.json(); showToast(j.message || 'Cleared', 'success'); } catch(e) { showToast('Error: ' + e.message, 'error'); }
}
async function clearMode(mode) {
  try { const r = await fetch('/api/clear/' + mode, { method: 'POST' }); const j = await r.json(); showToast(j.message || 'Cleared', 'success'); } catch(e) { showToast('Error: ' + e.message, 'error'); }
}
async function deleteEntry(mode, username) {
  try { const r = await fetch('/api/delete/' + mode + '/' + encodeURIComponent(username), { method: 'POST' }); const j = await r.json(); showToast(j.message || 'Deleted', 'success'); } catch(e) { showToast('Error: ' + e.message, 'error'); }
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
  if (data.leaderboards) leaderboardData = data.leaderboards;
  if (data.allEntries) allData = data.allEntries;
  if (data.stats) {
    stats = data.stats;
    document.getElementById('totalEntries').textContent = stats.totalEntries || 0;
    document.getElementById('rowingEntries').textContent = stats.rowingEntries || 0;
    document.getElementById('runningEntries').textContent = stats.runningEntries || 0;
    document.getElementById('cyclingEntries').textContent = stats.cyclingEntries || 0;
    document.getElementById('totalDistance').innerHTML = (stats.totalDistanceKm || '0') + ' <small>km</small>';
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

function getAllEntriesSorted(mode) {
  const entries = database[mode] || [];
  return [...entries]
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
  return JSON.stringify({
    mqtt: mqttClient && mqttClient.connected,
    leaderboards: getAllTop10(),
    allEntries: {
      rowing: getAllEntriesSorted('rowing'),
      running: getAllEntriesSorted('running'),
      cycling: getAllEntriesSorted('cycling')
    },
    stats: {
      totalEntries: (database.rowing || []).length + (database.running || []).length + (database.cycling || []).length,
      rowingEntries: (database.rowing || []).length,
      runningEntries: (database.running || []).length,
      cyclingEntries: (database.cycling || []).length,
      totalDistanceKm: getAllTotalDistances().totalKm
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
    // CORS headers
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
      database.rowing = [];
      database.running = [];
      database.cycling = [];
      saveDatabase();
      console.log('[Dashboard] Cleared ALL leaderboards');
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ success: true, message: 'All leaderboards cleared' }));
      sendSSEUpdate();
    }
    else if (req.method === 'POST' && req.url.startsWith('/api/clear/')) {
      const mode = req.url.split('/api/clear/')[1];
      if (GAME_MODES.includes(mode)) {
        database[mode] = [];
        saveDatabase();
        console.log('[Dashboard] Cleared ' + mode + ' leaderboard');
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: true, message: mode + ' leaderboard cleared' }));
        sendSSEUpdate();
      } else {
        res.writeHead(400, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: false, error: 'Invalid mode: ' + mode }));
      }
    }
    else if (req.method === 'POST' && req.url.startsWith('/api/delete/')) {
      const parts = req.url.split('/api/delete/')[1].split('/');
      const mode = parts[0];
      const username = decodeURIComponent(parts.slice(1).join('/'));
      if (!GAME_MODES.includes(mode)) {
        res.writeHead(400, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: false, error: 'Invalid mode' }));
        return;
      }
      const idx = (database[mode] || []).findIndex(e => e.username.toLowerCase() === username.toLowerCase());
      if (idx !== -1) {
        database[mode].splice(idx, 1);
        saveDatabase();
        console.log('[Dashboard] Deleted entry "' + username + '" from ' + mode);
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ success: true, message: 'Deleted "' + username + '" from ' + mode }));
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

  // Send SSE updates every 5s
  setInterval(sendSSEUpdate, 5000);
}

// ============================================
// Main Entry Point
// ============================================
function main() {
  console.log('========================================');
  console.log('  Marathon Leaderboard Server');
  console.log('========================================\n');

  // Load MQTT settings
  loadMqttSettings();

  // Load game configuration
  loadGameConfig();

  // Setup hot-reload watcher for config
  setupConfigWatcher();

  // Load JSON database
  loadDatabase();

  setupGracefulShutdown();
  connectMQTT();
  startDashboardServer();

  console.log('\n[Server] Ready and listening for MQTT messages\n');
}

main();
