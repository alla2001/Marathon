# MQTT Connection Troubleshooting Guide

## Error: "Failed to connect to MQTT broker"

This means the MQTT broker (Mosquitto) is not running or not accessible.

### Quick Fix Steps:

### 1. Install Mosquitto (if not installed)

**Option A: Using Chocolatey (Recommended)**
```bash
choco install mosquitto
```

**Option B: Direct Download**
- Go to: https://mosquitto.org/download/
- Download Windows installer
- Install to default location
- Add to PATH during installation

**Option C: Use Online Broker (for testing)**
- Broker: `test.mosquitto.org`
- Port: `1883`
- Change in Debug Menu (Shift+D)

### 2. Start Mosquitto Broker

**Method 1: Use the batch file**
```bash
# Double-click this file:
START_MQTT_BROKER.bat
```

**Method 2: Command line**
```bash
# Open Command Prompt and run:
mosquitto -v
```

**Method 3: Windows Service**
```bash
# Start as service
net start mosquitto

# Stop service
net stop mosquitto
```

### 3. Verify Broker is Running

Open a new Command Prompt:
```bash
# Test connection
mosquitto_sub -h localhost -t test/topic -v
```

If you see "Connecting to localhost:1883" and no errors, the broker is running!

### 4. Test in Unity

1. Run your Unity project
2. Open Debug Menu (`Shift + D`)
3. Check connection status
4. Should show "Connected" in green

---

## Common Issues

### Issue: "mosquitto: command not found"

**Fix:** Mosquitto is not in PATH

1. Find installation folder (usually `C:\Program Files\mosquitto`)
2. Add to PATH:
   - Windows Search → "Environment Variables"
   - Edit "Path" variable
   - Add: `C:\Program Files\mosquitto`
   - Restart Command Prompt

### Issue: "Address already in use"

**Fix:** Another program is using port 1883

1. Find what's using the port:
   ```bash
   netstat -ano | findstr :1883
   ```

2. Kill that process or use different port:
   - Open Debug Menu
   - Change port to `1884`
   - Start mosquitto: `mosquitto -p 1884 -v`

### Issue: Connection works then drops

**Fix:** Firewall blocking MQTT

1. Allow Mosquitto in Windows Firewall
2. Or disable firewall temporarily for testing

### Issue: "Cannot subscribe: Not connected"

**Fix:** Subscription happens before connection

- This is now fixed in the code
- Subscriptions happen automatically after connection
- Wait a few seconds after starting

---

## Testing MQTT Communication

### Test 1: Echo Test

**Terminal 1: Subscribe**
```bash
mosquitto_sub -h localhost -t "test/echo" -v
```

**Terminal 2: Publish**
```bash
mosquitto_pub -h localhost -t "test/echo" -m "Hello MQTT!"
```

You should see "Hello MQTT!" in Terminal 1.

### Test 2: Marathon Topics

**Subscribe to all Marathon messages:**
```bash
mosquitto_sub -h localhost -t "marathon/#" -v
```

**Send test game start:**
```bash
mosquitto_pub -h localhost -t "marathon/tablet/command" -m "{\"messageType\":\"START_GAME\",\"timestamp\":1234567890,\"playerName\":\"Test\",\"gameMode\":\"rowing\"}"
```

### Test 3: Unity Debug Menu Tests

1. Open Debug Menu (`Shift + D`)
2. Connect to broker
3. Click "Test Start Game"
4. Check Console for sent message

---

## Alternative: Use Online MQTT Broker

If you can't get Mosquitto working locally:

### HiveMQ Public Broker
- Broker: `broker.hivemq.com`
- Port: `1883`
- No authentication required

### Eclipse Mosquitto Test Broker
- Broker: `test.mosquitto.org`
- Port: `1883`
- Public (not secure)

### Update Unity Settings:

1. Open Debug Menu (`Shift + D`)
2. Broker Address: `test.mosquitto.org`
3. Port: `1883`
4. Click "Connect"

**⚠️ Warning:** Public brokers are not secure. Use only for testing!

---

## Still Having Issues?

### Check Unity Console for exact error:

1. `"Exception connecting to the broker"` → Broker not running
2. `"Not connected to MQTT broker"` → Wait for connection
3. `"Connection timeout"` → Check firewall/network
4. `"Authentication failed"` → Check username/password

### Enable Debug Logs:

In MQTTManager Inspector:
- ✓ Show Debug Logs

Watch Unity Console for detailed connection info.

---

## Quick Checklist:

- [ ] Mosquitto installed
- [ ] Mosquitto running (`mosquitto -v`)
- [ ] Port 1883 not blocked
- [ ] Firewall allows mosquitto.exe
- [ ] Unity Debug Menu shows "Connected"
- [ ] Test message sends successfully

---

## Get Help:

If still stuck, provide:
1. Unity Console error messages
2. Mosquitto command output
3. Operating system version
4. `netstat -ano | findstr :1883` output

**Most common fix:** Just run `mosquitto -v` in Command Prompt!
