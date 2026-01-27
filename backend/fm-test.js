const mqtt = require('mqtt');
const fs = require('fs');
const path = require('path');

// ============================================
// Load MQTT Settings
// ============================================
const MQTT_SETTINGS_FILE = path.join(__dirname, 'mqtt-settings.json');
let mqttSettings = {
  brokerUrl: 'mqtt://localhost:1883',
  clientId: 'fm-test-client',
  username: '',
  password: '',
  reconnectPeriod: 5000,
  clean: true
};

try {
  if (fs.existsSync(MQTT_SETTINGS_FILE)) {
    const data = fs.readFileSync(MQTT_SETTINGS_FILE, 'utf8');
    mqttSettings = { ...mqttSettings, ...JSON.parse(data), clientId: 'fm-test-client' };
  }
} catch (err) {
  console.error('Error loading mqtt-settings.json:', err.message);
}

// ============================================
// Config
// ============================================
const GAME_DURATION = 10; // seconds of fake gameplay
const FAKE_DISTANCE = 800 + Math.random() * 800; // random 800-1600m

// ============================================
// Connect
// ============================================
const options = {
  clientId: mqttSettings.clientId,
  clean: true,
  reconnectPeriod: 5000
};
if (mqttSettings.username) {
  options.username = mqttSettings.username;
  options.password = mqttSettings.password;
}

const client = mqtt.connect(mqttSettings.brokerUrl || 'mqtt://localhost:1883', options);

// ============================================
// Track active games per side
// ============================================
const activeGames = {}; // { left: timeout, right: timeout }

function publish(topic, data) {
  const msg = typeof data === 'string' ? data : JSON.stringify(data);
  client.publish(topic, msg, { qos: 1 });
  console.log(`[SENT] ${topic}: ${msg}`);
}

function simulateGame(side, playerName) {
  if (activeGames[side]) {
    console.log(`[GAME] ${side} already has an active game, ignoring`);
    return;
  }

  console.log(`\n[GAME] === ${side.toUpperCase()} === Player "${playerName}" starting game ===`);

  // Send "Game Active" status
  publish(`MarathonFM/${side}/status`, 'Game Active');

  // After GAME_DURATION seconds, end the game
  activeGames[side] = setTimeout(() => {
    const distance = Math.round(800 + Math.random() * 800); // 800-1600m
    const time = GAME_DURATION;

    console.log(`\n[GAME] === ${side.toUpperCase()} === Game over for "${playerName}" ===`);
    console.log(`[GAME] Distance: ${distance}m, Time: ${time}s`);

    // Write leaderboard entry
    publish('MarathonFM/leaderboard/write', {
      username: playerName,
      distance: distance,
      time: time
    });

    // Send "Game Idle" status
    publish(`MarathonFM/${side}/status`, 'Game Idle');

    delete activeGames[side];
  }, GAME_DURATION * 1000);
}

// ============================================
// Subscribe and auto-respond
// ============================================
client.on('connect', () => {
  console.log('========================================');
  console.log('  FM Test Client - Auto Game Simulator');
  console.log('========================================\n');
  console.log(`Game duration: ${GAME_DURATION}s`);
  console.log('Listening for name topics to start games...\n');

  const topics = [
    'MarathonFM/left/name',
    'MarathonFM/right/name',
    'MarathonFM/leaderboard/top10'
  ];

  client.subscribe(topics, (err) => {
    if (err) {
      console.error('Subscribe error:', err);
    } else {
      console.log('Subscribed to:', topics.join('\n  '));
      console.log('\nWaiting for players...\n');
    }
  });
});

// ============================================
// Handle incoming messages
// ============================================
client.on('message', (topic, message) => {
  const str = message.toString();

  if (topic === 'MarathonFM/left/name') {
    console.log(`\n[RECEIVED] Left tablet player: "${str}"`);
    simulateGame('left', str);
  } else if (topic === 'MarathonFM/right/name') {
    console.log(`\n[RECEIVED] Right tablet player: "${str}"`);
    simulateGame('right', str);
  } else if (topic === 'MarathonFM/leaderboard/top10') {
    try {
      const parsed = JSON.parse(str);
      const count = parsed.leaderboard ? parsed.leaderboard.length : 0;
      console.log(`[TOP10] ${count} entries, total distance: ${parsed.totalDistances || 0}m`);
    } catch {
      // ignore parse errors on top10
    }
  }
});

client.on('error', (err) => {
  console.error('MQTT Error:', err.message);
});
