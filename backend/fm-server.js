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

  console.log('\n[Server] Ready and listening for MQTT messages\n');
}

main();
