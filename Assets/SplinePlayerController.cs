using UnityEngine;
using Dreamteck.Splines;
using System.Collections.Generic;

/// <summary>
/// Controls player movement along a spline based on speed from machine
/// Handles checkpoint detection and timer countdown
/// </summary>
public class SplinePlayerController : MonoBehaviour
{
    [Header("Spline Settings")]
    [SerializeField] private SplineFollower splineFollower;
    [SerializeField] private float maxSpeed = 20f; // Max speed in m/s
    [SerializeField] private float speedDecayRate = 0.5f; // How fast speed decreases without input
    [SerializeField] private float speedMultiplier = 1f; // Multiplier for speed

    [Header("Checkpoint Settings")]
    [SerializeField] private List<Checkpoint> checkpoints = new List<Checkpoint>();
    [SerializeField] private int currentCheckpointIndex = 0;

    [Header("Timer Settings")]
    [SerializeField] private float timeLimit = 300f; // 5 minutes in seconds
    [SerializeField] private bool useTimer = true;

    [Header("References")]
    [SerializeField] private MainGameManager gameManager;
    [SerializeField] private GameHUDController hudController;

    // Cached values
    private float splineLength = 0f;

    // State
    private float currentSpeed = 0f;
    private float currentTimeRemaining = 0f;
    private bool isPlaying = false;
    private int totalCheckpoints = 0;

    // Machine input (placeholder for now)
    private float machineSpeed = 0f;

    private void Start()
    {
        if (splineFollower == null)
        {
            splineFollower = GetComponent<SplineFollower>();
        }

        if (splineFollower == null)
        {
            Debug.LogError("[SplinePlayerController] SplineFollower component not found!");
            return;
        }

        // Initialize
        splineFollower.follow = false;
        totalCheckpoints = checkpoints.Count;

        // Cache spline length
        if (splineFollower.spline != null)
        {
            splineLength = (float)splineFollower.spline.CalculateLength();
            Debug.Log($"[SplinePlayerController] Spline length: {splineLength}m");

            // Set total distance on HUD and GameManager
            if (hudController != null)
            {
                hudController.SetTotalDistance(splineLength);
            }
            if (gameManager != null)
            {
                gameManager.SetTotalDistance(splineLength);
            }
        }

        // Register checkpoints
        for (int i = 0; i < checkpoints.Count; i++)
        {
            int index = i; // Capture for closure
            checkpoints[i].OnCheckpointReached += () => OnCheckpointReached(index);
        }

        Debug.Log($"[SplinePlayerController] Initialized with {totalCheckpoints} checkpoints");
    }

    private void Update()
    {
        if (!isPlaying)
            return;

        // Update timer
        if (useTimer)
        {
            UpdateTimer();
        }

        // Update speed from machine (placeholder - will be replaced with MQTT data)
        UpdateSpeedFromMachine();

        // Apply speed decay
        ApplySpeedDecay();

        // Move along spline
        MoveAlongSpline();

        // Update HUD if available
        UpdateHUD();
    }

    public void StartGameplay()
    {
        isPlaying = true;
        currentSpeed = 0f;
        currentTimeRemaining = timeLimit; // Start with full time
        currentCheckpointIndex = 0;

        // Reset spline position and refresh follower
        if (splineFollower != null)
        {
            // Disable and re-enable to force refresh (fixes follower not updating issue)
            splineFollower.enabled = false;
            splineFollower.SetPercent(0);
            splineFollower.follow = true;
            splineFollower.enabled = true;

            // Force rebuild the follower
            splineFollower.RebuildImmediate();
        }

        // Reset checkpoints
        foreach (var checkpoint in checkpoints)
        {
            checkpoint.ResetCheckpoint();
        }

        Debug.Log($"[SplinePlayerController] Gameplay started! Time limit: {timeLimit}s");
    }

    public void StopGameplay()
    {
        isPlaying = false;
        if (splineFollower != null)
        {
            splineFollower.follow = false;
        }

        Debug.Log("[SplinePlayerController] Gameplay stopped!");
    }

    public void ResetPosition()
    {
        currentSpeed = 0f;
        machineSpeed = 0f;
        currentCheckpointIndex = 0;

        // Reset spline position to start and refresh follower
        if (splineFollower != null)
        {
            splineFollower.enabled = false;
            splineFollower.SetPercent(0);
            splineFollower.follow = false;
            splineFollower.enabled = true;
            splineFollower.RebuildImmediate();
        }

        // Reset checkpoints
        foreach (var checkpoint in checkpoints)
        {
            checkpoint.ResetCheckpoint();
        }

        Debug.Log("[SplinePlayerController] Position reset to start");
    }

    private void UpdateTimer()
    {
        currentTimeRemaining -= Time.deltaTime;

        if (currentTimeRemaining <= 0f)
        {
            currentTimeRemaining = 0f;
            OnTimerExpired();
        }
    }

    private void UpdateSpeedFromMachine()
    {
        // PLACEHOLDER: This will be replaced with actual machine data from MQTT
        // For now, use keyboard input for testing
        if (Input.GetKeyDown(KeyCode.Space))
        {
            machineSpeed += 2f; // Simulate machine stroke/step
        }

        // Gradually apply machine speed
        currentSpeed = Mathf.Lerp(currentSpeed, machineSpeed, Time.deltaTime * 5f);
        currentSpeed = Mathf.Clamp(currentSpeed, 0f, maxSpeed);
    }

    private void ApplySpeedDecay()
    {
        // Gradually decrease speed (simulates friction/resistance)
        machineSpeed = Mathf.Max(0f, machineSpeed - Time.deltaTime * speedDecayRate);
    }

    private void MoveAlongSpline()
    {
        if (splineFollower == null)
            return;

        // Convert speed to spline movement
        // Speed is in m/s, we need to convert to percent per second
        float splineLength = (float)splineFollower.spline.CalculateLength();
        float adjustedSpeed = currentSpeed * speedMultiplier;
        float percentPerSecond = (adjustedSpeed / splineLength);

        splineFollower.followSpeed = percentPerSecond;

        // Check if reached end of spline
        if (splineFollower.result.percent >= 1f)
        {
            OnReachedEnd();
        }
    }

    private void OnCheckpointReached(int checkpointIndex)
    {
        if (checkpointIndex != currentCheckpointIndex)
        {
            Debug.LogWarning($"[SplinePlayerController] Checkpoint {checkpointIndex} reached out of order!");
            return;
        }

        currentCheckpointIndex++;
        Debug.Log($"[SplinePlayerController] Checkpoint {checkpointIndex + 1}/{totalCheckpoints} reached!");

        // Check if all checkpoints completed
        if (currentCheckpointIndex >= totalCheckpoints)
        {
            OnAllCheckpointsCompleted();
        }
    }

    private void OnTimerExpired()
    {
        if (!isPlaying)
            return;

        Debug.Log("[SplinePlayerController] Timer expired! Game Over.");
        TriggerGameOver(false);
    }

    private void OnAllCheckpointsCompleted()
    {
        Debug.Log("[SplinePlayerController] All checkpoints completed! Victory!");
        TriggerGameOver(true);
    }

    private void OnReachedEnd()
    {
        // Reached end of spline (same as completing all checkpoints typically)
        if (isPlaying && currentCheckpointIndex >= totalCheckpoints)
        {
            OnAllCheckpointsCompleted();
        }
    }

    private void TriggerGameOver(bool victory)
    {
        StopGameplay();

        // Show game over UI
        // This will be handled by GameOverController
        Debug.Log($"[SplinePlayerController] Game Over - Victory: {victory}");
    }

    private void UpdateHUD()
    {
        if (splineFollower == null || splineFollower.spline == null)
            return;

        // Calculate distance traveled from spline position
        float distanceTraveled = (float)splineFollower.result.percent * splineLength;

        // Update HUD
        if (hudController != null)
        {
            hudController.UpdateDistance(distanceTraveled);
            hudController.SetTime(timeLimit - currentTimeRemaining);
        }

        // Sync distance and speed with MainGameManager so MQTT sends correct values
        if (gameManager != null)
        {
            gameManager.SetDistance(distanceTraveled);
            gameManager.SetSpeed(currentSpeed);
        }
    }

    // Public method to set speed from machine (will be called from MQTT handler)
    public void SetMachineSpeed(float speed)
    {
        machineSpeed = Mathf.Clamp(speed, 0f, maxSpeed);
    }

    // Public method to set distance from machine (uses treadmill's distance directly)
    public void SetMachineDistance(float distanceMeters)
    {
        if (splineFollower == null || splineLength <= 0f)
            return;

        // Convert distance to spline percent
        float targetPercent = Mathf.Clamp01(distanceMeters / splineLength);

        // Set spline position directly
        splineFollower.SetPercent(targetPercent);

        // Sync to MainGameManager
        if (gameManager != null)
        {
            gameManager.SetDistance(distanceMeters);
        }

        Debug.Log($"[SplinePlayerController] Set distance from treadmill: {distanceMeters:F2}m ({targetPercent * 100:F1}%)");
    }

    // Public method to add speed impulse (for stroke/step detection)
    public void AddSpeedImpulse(float impulse)
    {
        machineSpeed = Mathf.Clamp(machineSpeed + impulse, 0f, maxSpeed);
    }

    // Public getters
    public float CurrentSpeed => currentSpeed;
    public float TimeRemaining => currentTimeRemaining;
    public int CurrentCheckpointIndex => currentCheckpointIndex;
    public int TotalCheckpoints => totalCheckpoints;
    public bool IsPlaying => isPlaying;

    private void OnDestroy()
    {
        // Unregister checkpoint events
        foreach (var checkpoint in checkpoints)
        {
            checkpoint.OnCheckpointReached -= () => OnCheckpointReached(0);
        }
    }
}
