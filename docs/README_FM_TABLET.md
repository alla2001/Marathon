# Marathon FM Tablet - Setup & Usage Guide

## Overview
The FM Tablet is a standalone registration tablet. It allows users to:
- Enter their name
- Validate against FM leaderboard
- Register and see success confirmation

**No game metrics or gameplay** - just registration flow.

---

## Building

### Unity Build Settings
1. Open Unity project
2. Go to **File > Build Settings**
3. Select scene: `Scenes/Tablet FM`
4. Set platform to **Android** or **iOS**
5. Click **Build**

---

## Backend Requirements

### MQTT Topics (Different from Game)
The FM tablet uses separate leaderboard topics:
- `fm-leaderboard/check-username` - Validate username
- `fm-leaderboard/register` - Register user

These are **separate** from the game leaderboard, so the same username can exist in both systems.

### Backend Setup
If using the existing backend, add FM leaderboard handling to `server.js`, or run a separate FM backend.

---

## User Flow

### 1. Start Screen
![English Version]
- **Logo**: Marathon FM branding
- **Image**: Landing artwork
- **Description**: Instructions text
- **Name Input**: Enter name/nickname
- **Validation**: Shows if username available/taken
- **Terms Toggle**: Must accept to proceed
- **Start Button**: Enabled when valid

### 2. Username Validation
| Status | Color | Message |
|--------|-------|---------|
| Checking | Yellow | "Checking..." / "جاري التحقق..." |
| Available | Green | "Username available" / "اسم المستخدم متاح" |
| Taken | Red | "Username already registered" / "اسم المستخدم مسجل مسبقاً" |

### 3. Success Popup
When **Start** is pressed:
- Popup appears with success message
- **English**: "Success! User can go into the wheel and start the next steps"
- **Arabic**: "تم بنجاح! يمكن للمشارك الدخول إلى العجلة والبدء باللعب."
- Auto-closes after 3 seconds (or tap X)
- Form resets for next user

---

## Language Toggle

Press **العربية / English** button to switch:

### English Mode
- All labels in English
- Text aligned left
- Close button on right

### Arabic Mode (RTL)
- All labels in Arabic
- Text aligned right
- Close button on left

---

## Configuration

### In Unity Inspector (TabletFMController)

#### Popup Settings
- `Popup Display Duration`: How long popup shows (default: 3s)
- `Success Message English/Arabic`: Popup title text
- `Popup Description English/Arabic`: Popup body text

#### Localization - English
- `Description English`: Main description text
- `Input Label English`: "Enter your name or nickname"
- `Name Placeholder English`: "Name"
- `Validation Default English`: Validation hint text
- `Start Button English`: "Start"
- `Terms English`: Terms and conditions text

#### Localization - Arabic
- Same fields with Arabic translations

---

## Station Configuration

Like the game tablet, FM tablet uses station IDs:
1. Find `MQTTManager` in scene
2. Set `Station ID`

This allows multiple FM tablets to operate independently.

---

## Troubleshooting

### Start button always disabled
1. Check username is not empty
2. Check terms toggle is checked
3. Check username validation completed (not "Checking...")
4. If username taken, try different name

### Language not switching properly
- All labels should update on button press
- If some stay English, check element names in UXML match controller

### Popup not showing
- Check `Popup` element exists in UXML
- Check `PopupTitle` and `PopupDescription` labels exist
- Check popup `display` is set to `none` initially

### Username always shows as "taken"
- Backend may not be responding
- After timeout (3s), assumes unique
- Check MQTT connection

---

## Files

### Scene
- `Scenes/Tablet FM.unity`

### UI
- `UI/Tablet FM.uxml` - UI layout
- `UI/TabletFMController.cs` - Main controller
- `UI/FMLeaderboardManager.cs` - FM-specific leaderboard

### Images
- `UI/Images/pop up.png` - Popup background
- `UI/Images/Group 2147203289.png` - FM Logo
- `UI/Images/Frame 2147203202.png` - Landing image
