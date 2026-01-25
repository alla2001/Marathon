require('dotenv').config();
const mqtt = require('mqtt');
const fs = require('fs');
const path = require('path');

// ============================================
// JSON Database Setup
// ============================================
const DB_FILE = path.join(__dirname, 'leaderboard.json');

// Initialize database structure
let database = {
  entries: []
};

// Load database from file
function loadDatabase() {
  try {
    if (fs.existsSync(DB_FILE)) {
      const data = fs.readFileSync(DB_FILE, 'utf8');
      database = JSON.parse(data);
      console.log(`[DB] Loaded ${database.entries.length} entries from database`);
    } else {
      // Create new database file
      saveDatabase();
      console.log('[DB] Created new database file');
    }
  } catch (err) {
    console.error('[DB] Error loading database:', err.message);
    database = { entries: [] };
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

// Database operations
function findEntryByUsername(username) {
  return database.entries.find(e => e.username.toLowerCase() === username.toLowerCase());
}

function addEntry(entry) {
  entry.createdAt = new Date().toISOString();
  entry.updatedAt = new Date().toISOString();
  database.entries.push(entry);
  saveDatabase();
  return entry;
}

function updateEntry(username, updates) {
  const index = database.entries.findIndex(e => e.username.toLowerCase() === username.toLowerCase());
  if (index !== -1) {
    database.entries[index] = { ...database.entries[index], ...updates, updatedAt: new Date().toISOString() };
    saveDatabase();
    return database.entries[index];
  }
  return null;
}

function getTop10() {
  return [...database.entries]
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
      broadcast: { onConnect: true, intervalSeconds: 60 }
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
  // Format: leaderboard/{action}/response/{stationId}
  SUBMIT_RESPONSE: 'leaderboard/submit/response',
  TOP10_RESPONSE: 'leaderboard/top10/response',
  CHECK_USERNAME_RESPONSE: 'leaderboard/check-username/response',

  // Game config broadcast (sent to all stations)
  CONFIG_BROADCAST: 'marathon/config/broadcast'
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

    // Broadcast config on connect if enabled
    if (gameConfig && gameConfig.broadcast && gameConfig.broadcast.onConnect) {
      setTimeout(() => {
        broadcastGameConfig();
      }, 1000); // Small delay to ensure subscriptions are ready
    }

    // Set up periodic config broadcast if configured
    if (gameConfig && gameConfig.broadcast && gameConfig.broadcast.intervalSeconds > 0) {
      setInterval(() => {
        broadcastGameConfig();
      }, gameConfig.broadcast.intervalSeconds * 1000);
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
  const { username, score, distance, time, stationId } = payload;
  const responseTopic = getResponseTopic(TOPICS.SUBMIT_RESPONSE, stationId);

  if (!username || score === undefined) {
    publishResponse(responseTopic, {
      success: false,
      error: 'Missing required fields: username and score',
      stationId
    });
    return;
  }

  try {
    // Check if user exists
    let entry = findEntryByUsername(username);

    if (entry) {
      // Update only if new score is higher
      if (score > entry.score) {
        updateEntry(username, {
          score,
          distance: distance || entry.distance,
          time: time || entry.time,
          stationId: stationId || entry.stationId
        });

        console.log(`[DB] Updated score for ${username}: ${score}`);
        publishResponse(responseTopic, {
          success: true,
          message: 'Score updated (new high score)',
          username,
          score,
          stationId
        });
      } else {
        console.log(`[DB] Score not updated for ${username} (not a high score)`);
        publishResponse(responseTopic, {
          success: true,
          message: 'Score not updated (not higher than current)',
          username,
          currentScore: entry.score,
          submittedScore: score,
          stationId
        });
      }
    } else {
      // Create new entry
      addEntry({
        username,
        score,
        distance: distance || 0,
        time: time || 0,
        stationId: stationId || 0
      });

      console.log(`[DB] New entry created for ${username}: ${score}`);
      publishResponse(responseTopic, {
        success: true,
        message: 'New entry created',
        username,
        score,
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
  const { stationId } = payload || {};
  const responseTopic = getResponseTopic(TOPICS.TOP10_RESPONSE, stationId);

  try {
    const rankedEntries = getTop10();

    console.log(`[DB] Returning top 10 (${rankedEntries.length} entries)`);
    publishResponse(responseTopic, {
      success: true,
      entries: rankedEntries,
      stationId
    });
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
  const { username, stationId } = payload;
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
    const exists = findEntryByUsername(username);

    console.log(`[DB] Username "${username}" exists: ${!!exists}`);
    publishResponse(responseTopic, {
      success: true,
      username,
      isUnique: !exists,
      exists: !!exists,
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

  // If stationId provided, send to specific station, otherwise broadcast to all
  const topic = stationId
    ? `marathon/station${stationId}/config`
    : TOPICS.CONFIG_BROADCAST;

  publishResponse(topic, configMessage);
  console.log(`[Config] Broadcasted game config to ${stationId ? `station ${stationId}` : 'all stations'}`);
}

// ============================================
// Publish Response Helper
// ============================================
function publishResponse(topic, payload) {
  if (mqttClient && mqttClient.connected) {
    const message = JSON.stringify(payload);
    mqttClient.publish(topic, message, { qos: 1 });
    console.log(`[MQTT] Published to ${topic}:`, payload);
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
