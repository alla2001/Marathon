require('dotenv').config();
const mqtt = require('mqtt');
const fs = require('fs');
const path = require('path');

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

// ============================================
// Load Game Configuration
// ============================================
let gameConfig = null;

function loadGameConfig() {
  const configPath = path.join(__dirname, 'config.json');
  try {
    const configData = fs.readFileSync(configPath, 'utf8');
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
// MQTT Topics
// ============================================
const TOPICS = {
  // Requests (server subscribes)
  SUBMIT: 'leaderboard/submit',
  TOP10_REQUEST: 'leaderboard/top10/request',
  CHECK_USERNAME: 'leaderboard/check-username',
  CONFIG_REQUEST: 'marathon/config/request',

  // Responses (server publishes to station-specific topics)
  SUBMIT_RESPONSE: 'leaderboard/submit/response',
  TOP10_RESPONSE: 'leaderboard/top10/response',
  CHECK_USERNAME_RESPONSE: 'leaderboard/check-username/response',

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
    clientId: process.env.MQTT_CLIENT_ID || 'leaderboard-server-' + Math.random().toString(16).substr(2, 8),
    clean: true,
    reconnectPeriod: 5000
  };

  if (process.env.MQTT_USERNAME) {
    options.username = process.env.MQTT_USERNAME;
    options.password = process.env.MQTT_PASSWORD;
  }

  const brokerUrl = process.env.MQTT_BROKER_URL || 'mqtt://localhost:1883';
  console.log(`[MQTT] Connecting to ${brokerUrl}...`);

  mqttClient = mqtt.connect(brokerUrl, options);

  mqttClient.on('connect', () => {
    console.log('[MQTT] Connected to broker');

    // Subscribe to topics
    const topicsToSubscribe = [
      TOPICS.SUBMIT,
      TOPICS.TOP10_REQUEST,
      TOPICS.CHECK_USERNAME,
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
// Broadcast All Leaderboards
// ============================================
function broadcastAllLeaderboards() {
  const allTop10 = getAllTop10();

  const leaderboardMessage = {
    messageType: 'LEADERBOARD_UPDATE',
    timestamp: Date.now(),
    leaderboards: allTop10
  };

  // Broadcast combined leaderboard
  publishResponse(TOPICS.LEADERBOARD_BROADCAST, leaderboardMessage);

  // Also broadcast each game mode separately
  GAME_MODES.forEach(mode => {
    const modeMessage = {
      messageType: 'LEADERBOARD_UPDATE',
      timestamp: Date.now(),
      gameMode: mode,
      entries: allTop10[mode]
    };
    publishResponse(`${TOPICS.LEADERBOARD_BROADCAST}/${mode}`, modeMessage);
  });

  console.log(`[Leaderboard] Broadcasted top 10 for all game modes (rowing: ${allTop10.rowing.length}, running: ${allTop10.running.length}, cycling: ${allTop10.cycling.length})`);
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
// Main Entry Point
// ============================================
function main() {
  console.log('========================================');
  console.log('  Marathon Leaderboard Server');
  console.log('========================================\n');

  // Load game configuration
  loadGameConfig();

  // Load JSON database
  loadDatabase();

  setupGracefulShutdown();
  connectMQTT();

  console.log('\n[Server] Ready and listening for MQTT messages\n');
}

main();
