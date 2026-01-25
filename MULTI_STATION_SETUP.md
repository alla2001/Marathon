# Multi-Station Setup Guide
## For Venues with Multiple Game Stations

---

## Architecture Overview

### Centralized MQTT Broker

```
                 Central Server (Router/PC)
                 ┌─────────────────────┐
                 │   Mosquitto Broker  │
                 │   192.168.1.100     │
                 │   Port: 1883        │
                 └──────────┬──────────┘
                            │
          ┌─────────────────┼─────────────────┐
          │                 │                 │
     Station 1          Station 2        Station 3
    ┌──────────┐      ┌──────────┐    ┌──────────┐
    │ Game PC  │      │ Game PC  │    │ Game PC  │
    │ Tablet   │      │ Tablet   │    │ Tablet   │
    │ Rowing   │      │ Running  │    │ Cycling  │
    └──────────┘      └──────────┘    └──────────┘
```

### Key Points:
- **ONE** centralized MQTT broker (on a server or dedicated PC)
- **Each station** has a unique Station ID (1, 2, 3, ...)
- **Topics** are station-specific to prevent cross-talk
- **All devices** connect to the same broker IP

---

## Station Topics

Each station communicates on its own topics:

### Station 1:
- Tablet publishes: `marathon/station1/tablet/command`
- Game publishes: `marathon/station1/game/data`
- Game state: `marathon/station1/game/state`

### Station 2:
- Tablet publishes: `marathon/station2/tablet/command`
- Game publishes: `marathon/station2/game/data`
- Game state: `marathon/station2/game/state`

### Station 3:
- Tablet publishes: `marathon/station3/tablet/command`
- Game publishes: `marathon/station3/game/data`
- Game state: `marathon/station3/game/state`

**Result:** No interference between stations!

---

## Setup Instructions

### Step 1: Setup Central MQTT Broker

**Option A: Dedicated Server (Recommended)**
1. Use a dedicated PC/server always running
2. Install Mosquitto
3. Set static IP (e.g., `192.168.1.100`)
4. Configure firewall to allow port 1883
5. Start Mosquitto as a service

```bash
# Windows
net start mosquitto

# Linux
sudo systemctl enable mosquitto
sudo systemctl start mosquitto
```

**Option B: Router with MQTT Support**
- Some routers support MQTT directly
- Check router documentation

**Option C: Cloud MQTT (HiveMQ, AWS IoT)**
- For internet-based installations
- Requires internet connection

### Step 2: Configure Network

**Network Requirements:**
- All devices on same network/VLAN
- Firewall allows port 1883
- Static IP for broker recommended

**Find Broker IP:**
```bash
# On broker machine
ipconfig  # Windows
ifconfig  # Linux/Mac
```

Example: `192.168.1.100`

### Step 3: Configure Each Station

For EACH station (Game PC + Tablet pair):

**1. Assign Station ID:**
- Station 1: Rowing #1
- Station 2: Rowing #2
- Station 3: Running #1
- Station 4: Cycling #1
- etc...

**2. Configure Game PC:**
   - Open Unity project
   - Select `MQTTManager` GameObject
   - Set these Inspector values:
     ```
     Broker Address: 192.168.1.100  (your broker IP)
     Broker Port: 1883
     Station ID: 1  (unique for this station!)
     Use Station Topics: ✓ (checked)
     Auto Connect: ✓
     ```

**3. Configure Tablet:**
   - Open Unity project
   - Select `MQTTManager` GameObject
   - Set **SAME** values as Game PC:
     ```
     Broker Address: 192.168.1.100
     Broker Port: 1883
     Station ID: 1  (MUST match Game PC!)
     Use Station Topics: ✓
     Auto Connect: ✓
     ```

**4. Physical Labels:**
   - Put a label on each station: "STATION 1", "STATION 2", etc.
   - Helps staff match tablet to correct game PC

### Step 4: Test Each Station

**For Station 1:**
1. Start Game PC project
2. Start Tablet project
3. Both should connect to broker
4. Enter name on tablet
5. Click "Start Rowing"
6. Game should start countdown
7. Press Space on Game PC
8. Tablet HUD should update

**Verify no cross-talk:**
- Start Station 1 game
- Station 2 should NOT react
- Each station independent

---

## Debug Menu Configuration

On Tablet, open Debug Menu (`Shift+D`):

```
MQTT SETTINGS:
  Broker Address: 192.168.1.100
  Port: 1883
  Station ID: 1  (← SET THIS!)

  [Connect]
```

After connecting, test with "Test Start Game" button.

---

## Troubleshooting Multi-Station Setup

### Problem: Tablet 1 controls Game 2 (wrong station)

**Cause:** Station IDs don't match

**Fix:**
1. Check MQTTManager on both Tablet and Game
2. Verify Station ID is the same
3. Verify "Use Station Topics" is checked
4. Restart both projects

### Problem: All stations respond to one tablet

**Cause:** "Use Station Topics" is unchecked

**Fix:**
1. Enable "Use Station Topics" on ALL devices
2. Restart all projects

### Problem: Messages not received

**Cause:** Wrong broker IP or broker down

**Fix:**
1. Ping broker: `ping 192.168.1.100`
2. Check broker is running: `netstat -an | findstr 1883`
3. Check firewall allows 1883
4. Verify all devices on same network

### Problem: Connection drops randomly

**Cause:** Network congestion or broker overload

**Fix:**
1. Use wired ethernet instead of WiFi
2. Increase broker message limit in config
3. Reduce game data update frequency (in MainGameManager)

---

## Monitoring All Stations

Subscribe to all marathon topics on broker machine:

```bash
mosquitto_sub -h localhost -t "marathon/#" -v
```

You'll see all messages from all stations:
```
marathon/station1/tablet/command {"messageType":"START_GAME"...}
marathon/station2/game/data {"currentDistance":500...}
marathon/station3/game/state {"state":"PLAYING"}
```

---

## Station ID Assignment Strategy

### Option 1: By Activity Type
```
Stations 1-5:   Rowing
Stations 6-10:  Running
Stations 11-15: Cycling
```

### Option 2: By Physical Location
```
Station 1: Room A, Lane 1
Station 2: Room A, Lane 2
Station 3: Room B, Lane 1
```

### Option 3: Sequential
```
Station 1, 2, 3, 4, 5...
```

**Document your assignment!** Keep a spreadsheet:
```
Station ID | Location | Activity | Game PC Name | Tablet Name
-----------|----------|----------|--------------|-------------
     1     | Room A-1 | Rowing   | GAMEPC-01    | TABLET-01
     2     | Room A-2 | Rowing   | GAMEPC-02    | TABLET-02
     3     | Room B-1 | Running  | GAMEPC-03    | TABLET-03
```

---

## Physical Setup Checklist

For each station:

- [ ] Game PC connected to network
- [ ] Tablet connected to network
- [ ] Both have same Station ID configured
- [ ] Both connect to broker successfully
- [ ] Tested start/game/finish flow
- [ ] Station labeled physically
- [ ] Documented in spreadsheet

---

## Alternative: Dynamic Pairing

If you don't want to manually configure Station IDs, you can implement pairing:

1. **QR Code Pairing:**
   - Game PC shows QR code with its Station ID
   - Tablet scans QR code
   - Automatically sets Station ID

2. **NFC Pairing:**
   - Tap tablet on NFC tag at station
   - Tag contains Station ID

3. **Button Pairing:**
   - Both devices have "Pair" button
   - Press simultaneously
   - Exchange Station IDs via discovery

(I can implement any of these if needed!)

---

## Scalability

This architecture supports:
- ✓ **10-20 stations:** Works perfectly
- ✓ **20-50 stations:** May need message rate limiting
- ✓ **50+ stations:** Consider multiple brokers or enterprise MQTT

---

## Network Diagram Example

```
Internet Router (192.168.1.1)
    │
    ├── Switch/Hub
    │
    ├── MQTT Broker Server (192.168.1.100)
    │
    ├── Station 1 Game PC (192.168.1.101)
    ├── Station 1 Tablet (192.168.1.111)
    │
    ├── Station 2 Game PC (192.168.1.102)
    ├── Station 2 Tablet (192.168.1.112)
    │
    ├── Station 3 Game PC (192.168.1.103)
    ├── Station 3 Tablet (192.168.1.113)
    │
    └── Admin Dashboard (192.168.1.200)
```

---

## Summary

✅ **ONE broker** for all stations
✅ **Unique Station ID** per station
✅ **Same Station ID** on paired Tablet + Game
✅ **Enable "Use Station Topics"** on all devices
✅ **Label stations physically**
✅ **Test each station independently**

This setup ensures clean, reliable communication with no cross-talk between stations!
