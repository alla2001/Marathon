using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class GameHUDController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    [Header("Game Settings")]
    [SerializeField] private float totalDistance = 1600f; // Total distance in meters
    [SerializeField] private float timeLimit = 300f; // Time limit in seconds (5 minutes)

    [Header("Checkpoint Settings")]
    [SerializeField] private float motivationDisplayDuration = 5f; // How long to show motivation text
    [SerializeField] private Sprite[] motivationSprites; // Different motivation images for each checkpoint

    private VisualElement hudRoot;
    private Label distanceValue;
    private Label timeValue;
    private VisualElement progressFill;
    private VisualElement distanceMarkers;
    private VisualElement motivationTextImage;

    private float currentDistance = 0f;
    private float currentTime = 0f;
    private float timeRemaining = 300f;
    private bool isGameRunning = false;

    // Automatic checkpoint tracking (at 1/3, 2/3 of distance)
    private bool[] checkpointsReached = new bool[2]; // 2 checkpoints (33% and 66%)
    private Coroutine hideMotivationCoroutine;

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError("UIDocument component not found!");
            return;
        }

        var root = uiDocument.rootVisualElement;
        hudRoot = root.Q<VisualElement>("HUDRoot");

        // Get UI elements
        distanceValue = root.Q<Label>("DistanceValue");
        timeValue = root.Q<Label>("TimeValue");
        progressFill = root.Q<VisualElement>("ProgressFill");
        distanceMarkers = root.Q<VisualElement>("DistanceMarkers");
        motivationTextImage = root.Q<VisualElement>("MotivationTextImage");

        if (distanceValue == null || timeValue == null || progressFill == null)
        {
            Debug.LogError("HUD UI elements not found!");
        }

        // Initialize motivation image as hidden
        if (motivationTextImage != null)
        {
            motivationTextImage.style.display = DisplayStyle.None;
            motivationTextImage.style.opacity = 0f;
        }

        // Update distance markers based on initial totalDistance
        UpdateDistanceMarkers();

        // Initialize display
        UpdateDistanceDisplay();
        UpdateTimeDisplay();
        UpdateProgressBar();

        // Hide HUD initially
        HideHUD();
    }

    private void Update()
    {
        if (isGameRunning)
        {
            currentTime += Time.deltaTime;
            timeRemaining = Mathf.Max(0, timeLimit - currentTime);
            UpdateTimeDisplay();

            // Check if time expired
            if (timeRemaining <= 0)
            {
                OnGameFinished();
            }
        }
    }

    public void StartGame()
    {
        isGameRunning = true;
        currentDistance = 0f;
        currentTime = 0f;
        timeRemaining = timeLimit;

        // Reset checkpoints
        for (int i = 0; i < checkpointsReached.Length; i++)
        {
            checkpointsReached[i] = false;
        }

        // Hide motivation image if visible
        HideMotivationImage();

        UpdateDistanceDisplay();
        UpdateTimeDisplay();
        UpdateProgressBar();
        ShowHUD();

        Debug.Log($"[Game HUD] Game started - Time limit: {timeLimit}s, Checkpoints at {totalDistance / 3f:F0}m and {totalDistance * 2f / 3f:F0}m");
    }

    public void StopGame()
    {
        isGameRunning = false;
        HideMotivationImage();
    }

    public void ShowHUD()
    {
        if (hudRoot != null)
        {
            hudRoot.style.display = DisplayStyle.Flex;
        }
    }

    public void HideHUD()
    {
        if (hudRoot != null)
        {
            hudRoot.style.display = DisplayStyle.None;
        }
    }

    public void UpdateDistance(float distance)
    {
        currentDistance = Mathf.Clamp(distance, 0f, totalDistance);
        UpdateDistanceDisplay();
        UpdateProgressBar();

        // Check automatic checkpoints (at 1/3 and 2/3 of total distance)
        CheckAutomaticCheckpoints();

        // Check if finished
        if (currentDistance >= totalDistance)
        {
            OnGameFinished();
        }
    }

    /// <summary>
    /// Checks if player has reached any automatic checkpoint (1/3, 2/3 of distance)
    /// </summary>
    private void CheckAutomaticCheckpoints()
    {
        float checkpoint1Distance = totalDistance / 3f;      // 33%
        float checkpoint2Distance = totalDistance * 2f / 3f; // 66%

        // Check first checkpoint (1/3)
        if (!checkpointsReached[0] && currentDistance >= checkpoint1Distance)
        {
            checkpointsReached[0] = true;
            OnCheckpointReached(0);
            Debug.Log($"[GameHUD] Checkpoint 1 reached at {checkpoint1Distance:F0}m!");
        }

        // Check second checkpoint (2/3)
        if (!checkpointsReached[1] && currentDistance >= checkpoint2Distance)
        {
            checkpointsReached[1] = true;
            OnCheckpointReached(1);
            Debug.Log($"[GameHUD] Checkpoint 2 reached at {checkpoint2Distance:F0}m!");
        }
    }

    /// <summary>
    /// Called when a checkpoint is reached - shows motivation image with animation
    /// </summary>
    private void OnCheckpointReached(int checkpointIndex)
    {
        ShowMotivationImage(checkpointIndex);
    }

    public void AddDistance(float deltaDistance)
    {
        UpdateDistance(currentDistance + deltaDistance);
    }

    private void UpdateDistanceDisplay()
    {
        if (distanceValue != null)
        {
            distanceValue.text = $"{Mathf.RoundToInt(currentDistance)} m";
        }
    }

    private void UpdateTimeDisplay()
    {
        if (timeValue != null)
        {
            // Show TIME REMAINING (countdown)
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            timeValue.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void UpdateProgressBar()
    {
        if (progressFill != null)
        {
            float progressPercent = (currentDistance / totalDistance) * 100f;
            progressFill.style.width = Length.Percent(progressPercent);
        }
    }

    private void OnGameFinished()
    {
        isGameRunning = false;
        Debug.Log($"Game Finished! Distance: {currentDistance}m, Time: {currentTime:F2}s");

        // Show game over screen with results
        GameOverController.ShowResults(currentTime, currentDistance);
    }

    // Public getters
    public float CurrentDistance => currentDistance;
    public float CurrentTime => currentTime;
    public bool IsGameRunning => isGameRunning;

    // Public method to set distance directly (useful for testing)
    public void SetDistance(float distance)
    {
        UpdateDistance(distance);
    }

    // Public method to set time directly (useful for testing)
    public void SetTime(float time)
    {
        currentTime = time;
        UpdateTimeDisplay();
    }

    // Public method to set total distance (spline length)
    public void SetTotalDistance(float distance)
    {
        totalDistance = distance;
        UpdateProgressBar();
        UpdateDistanceMarkers();
    }

    private void UpdateDistanceMarkers()
    {
        if (distanceMarkers == null) return;

        var labels = distanceMarkers.Query<Label>().ToList();
        if (labels.Count == 0) return;

        int markerCount = labels.Count;
        for (int i = 0; i < markerCount; i++)
        {
            // Calculate distance for this marker (evenly spaced from 0 to totalDistance)
            float markerDistance = (totalDistance / (markerCount - 1)) * i;
            labels[i].text = $"{Mathf.RoundToInt(markerDistance)}m";
        }

        Debug.Log($"[GameHUD] Updated {markerCount} distance markers for {totalDistance}m total");
    }

    // ========================================
    // Motivation Image Animation
    // ========================================

    /// <summary>
    /// Shows the motivation image with a scale + fade animation
    /// </summary>
    private void ShowMotivationImage(int checkpointIndex)
    {
        if (motivationTextImage == null)
        {
            Debug.LogWarning("[GameHUD] MotivationTextImage not found!");
            return;
        }

        // Stop any existing hide coroutine
        if (hideMotivationCoroutine != null)
        {
            StopCoroutine(hideMotivationCoroutine);
        }

        // Set sprite if available
        if (motivationSprites != null && checkpointIndex < motivationSprites.Length && motivationSprites[checkpointIndex] != null)
        {
            motivationTextImage.style.backgroundImage = new StyleBackground(motivationSprites[checkpointIndex]);
        }

        // Show and animate
        motivationTextImage.style.display = DisplayStyle.Flex;

        // Start animation: scale from small to normal + fade in
        motivationTextImage.style.opacity = 0f;
        motivationTextImage.style.scale = new Scale(new Vector3(0.5f, 0.5f, 1f));

        // Use USS transitions for smooth animation
        motivationTextImage.style.transitionProperty = new StyleList<StylePropertyName>(
            new System.Collections.Generic.List<StylePropertyName> {
                new StylePropertyName("opacity"),
                new StylePropertyName("scale")
            }
        );
        motivationTextImage.style.transitionDuration = new StyleList<TimeValue>(
            new System.Collections.Generic.List<TimeValue> {
                new TimeValue(0.5f, TimeUnit.Second),
                new TimeValue(0.5f, TimeUnit.Second)
            }
        );
        motivationTextImage.style.transitionTimingFunction = new StyleList<EasingFunction>(
            new System.Collections.Generic.List<EasingFunction> {
                new EasingFunction(EasingMode.EaseOutBack),
                new EasingFunction(EasingMode.EaseOutBack)
            }
        );

        // Trigger animation on next frame
        motivationTextImage.schedule.Execute(() =>
        {
            motivationTextImage.style.opacity = 1f;
            motivationTextImage.style.scale = new Scale(Vector3.one);
        }).ExecuteLater(10);

        // Schedule hide after duration
        hideMotivationCoroutine = StartCoroutine(HideMotivationAfterDelay());

        Debug.Log($"[GameHUD] Showing motivation image for checkpoint {checkpointIndex + 1}");
    }

    /// <summary>
    /// Coroutine to hide motivation image after delay
    /// </summary>
    private IEnumerator HideMotivationAfterDelay()
    {
        yield return new WaitForSeconds(motivationDisplayDuration);
        HideMotivationImage();
    }

    /// <summary>
    /// Hides the motivation image with fade out animation
    /// </summary>
    private void HideMotivationImage()
    {
        if (motivationTextImage == null) return;

        // Stop any pending hide coroutine
        if (hideMotivationCoroutine != null)
        {
            StopCoroutine(hideMotivationCoroutine);
            hideMotivationCoroutine = null;
        }

        // Animate out: fade and scale down
        motivationTextImage.style.transitionDuration = new StyleList<TimeValue>(
            new System.Collections.Generic.List<TimeValue> {
                new TimeValue(0.3f, TimeUnit.Second),
                new TimeValue(0.3f, TimeUnit.Second)
            }
        );
        motivationTextImage.style.transitionTimingFunction = new StyleList<EasingFunction>(
            new System.Collections.Generic.List<EasingFunction> {
                new EasingFunction(EasingMode.EaseIn),
                new EasingFunction(EasingMode.EaseIn)
            }
        );

        motivationTextImage.style.opacity = 0f;
        motivationTextImage.style.scale = new Scale(new Vector3(0.8f, 0.8f, 1f));

        // Hide after animation completes
        motivationTextImage.schedule.Execute(() =>
        {
            motivationTextImage.style.display = DisplayStyle.None;
        }).ExecuteLater(300);
    }
}
