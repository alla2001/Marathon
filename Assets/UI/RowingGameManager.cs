using UnityEngine;

public class RowingGameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameHUDController hudController;

    [Header("Rowing Settings")]
    [SerializeField] private float distancePerStroke = 5f; // Distance gained per rowing stroke
    [SerializeField] private float speedMultiplier = 1f; // Speed multiplier based on rowing intensity

    [Header("Input (for testing)")]
    [SerializeField] private KeyCode rowKey = KeyCode.Space; // Test key for rowing

    private float currentSpeed = 0f;
    private float lastStrokeTime = 0f;

    private void Start()
    {
        // Find HUD controller if not assigned
        if (hudController == null)
        {
            hudController = FindObjectOfType<GameHUDController>();
        }

        if (hudController == null)
        {
            Debug.LogError("GameHUDController not found!");
            return;
        }

        // Start the game
        hudController.StartGame();
    }

    private void Update()
    {
        if (hudController == null || !hudController.IsGameRunning)
            return;

        // Handle rowing input
        HandleRowingInput();

        // Update distance based on current speed
        if (currentSpeed > 0f)
        {
            float distanceThisFrame = currentSpeed * Time.deltaTime;
            hudController.AddDistance(distanceThisFrame);

            // Gradually decrease speed (friction/water resistance)
            currentSpeed = Mathf.Max(0f, currentSpeed - Time.deltaTime * 2f);
        }
    }

    private void HandleRowingInput()
    {
        // Example using keyboard (replace with your actual rowing input)
        if (Input.GetKeyDown(rowKey))
        {
            PerformRowStroke();
        }

        // You can also add continuous rowing based on rowing machine input
        // Example: if rowing machine is moving, call PerformRowStroke() at appropriate intervals
    }

    public void PerformRowStroke()
    {
        float currentTime = Time.time;
        float timeSinceLastStroke = currentTime - lastStrokeTime;

        // Calculate stroke power based on timing (faster strokes = more power)
        float strokePower = 1f;
        if (timeSinceLastStroke < 1f && timeSinceLastStroke > 0.2f)
        {
            strokePower = 1.5f; // Bonus for good rhythm
        }

        // Add speed based on stroke
        currentSpeed += distancePerStroke * strokePower * speedMultiplier;
        currentSpeed = Mathf.Min(currentSpeed, 20f); // Cap maximum speed

        lastStrokeTime = currentTime;

        Debug.Log($"Row stroke! Speed: {currentSpeed:F2} m/s");
    }

    // Call this method from your rowing hardware/input system
    public void OnRowingMachineStroke(float intensity)
    {
        // intensity should be between 0 and 1
        intensity = Mathf.Clamp01(intensity);

        float distance = distancePerStroke * intensity * speedMultiplier;
        currentSpeed += distance;
        currentSpeed = Mathf.Min(currentSpeed, 20f);

        lastStrokeTime = Time.time;
    }

    // Example method for continuous rowing machine input
    public void UpdateRowingMachineSpeed(float speed)
    {
        // speed in m/s from rowing machine
        currentSpeed = speed * speedMultiplier;
    }

    public void PauseGame()
    {
        if (hudController != null)
        {
            hudController.StopGame();
        }
    }

    public void ResumeGame()
    {
        if (hudController != null)
        {
            hudController.StartGame();
        }
    }
}
