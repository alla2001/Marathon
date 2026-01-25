# Installing M2Mqtt for Unity

## Method 1: Download Pre-built DLL (Easiest)

1. **Download M2Mqtt DLL:**
   - Go to: https://github.com/eclipse/paho.mqtt.m2mqtt/releases
   - Download the latest release ZIP
   - Or direct link: https://github.com/gpvigano/M2MqttUnity/releases

2. **Extract and Copy:**
   - Extract the ZIP file
   - Find `M2Mqtt.Net.dll` (or `M2Mqtt.dll`)
   - Create folder: `Assets/Plugins/M2MqttUnity/`
   - Copy the DLL into that folder

3. **Configure DLL in Unity:**
   - Select the DLL in Unity
   - In Inspector, check "Any Platform" is selected
   - Apply changes

## Method 2: Use M2MqttUnity Package (Recommended)

1. **Download M2MqttUnity:**
   ```
   https://github.com/gpvigano/M2MqttUnity
   ```

2. **Install via Unity:**
   - Download the repository as ZIP
   - Extract it
   - Copy the `M2MqttUnity` folder to your `Assets/` folder
   - Unity will automatically import it

3. **Verify Installation:**
   - Check for `M2MqttUnity/Scripts/M2MqttUnityClient.cs`
   - No errors should appear in Console

## Method 3: NuGet Package (Advanced)

If you have NuGet for Unity installed:

1. Open NuGet Package Manager
2. Search for "M2Mqtt"
3. Install `M2MqttDotnetCore`

## Troubleshooting

### Error: "The type or namespace name 'uPLibrary' could not be found"
- DLL is not imported correctly
- Check DLL is in `Assets/Plugins/` folder
- Restart Unity Editor

### Error: "MqttClient could not be found"
- Missing using directive
- Check DLL platform settings (should be "Any Platform")
- Try reimporting the DLL

### If Nothing Works:
- Use the alternative MQTT client I'll provide below
- Or use Unity WebSockets as alternative communication

---

## Quick Test After Installation

Add this test script to verify M2Mqtt works:

```csharp
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;

public class MQTTTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("M2Mqtt library loaded successfully!");

        // Try creating a client (don't connect yet)
        try
        {
            var client = new MqttClient("test.mosquitto.org");
            Debug.Log("MqttClient created successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create MqttClient: {e.Message}");
        }
    }
}
```

If you see "M2Mqtt library loaded successfully!" in console, you're good to go!
