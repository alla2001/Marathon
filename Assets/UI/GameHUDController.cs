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
    [SerializeField] private int numberOfCheckpoints = 4; // How many checkpoints (e.g., 4 = every 20%)

    [Header("Final Countdown Settings")]
    [SerializeField] private Font countdownFont; // Assign piepie.ttf in inspector

    [Header("Position Settings")]
    [SerializeField] private float positionUpdateInterval = 2f; // How often to request position from backend

    [Header("References")]
    [SerializeField] private MainGameManager gameManager;

    private VisualElement hudRoot;
    private Label distanceValue;
    private Label timeValue;
    private VisualElement progressFill;
    private VisualElement distanceMarkers;
    private VisualElement motivationTextImage;
    private Label finalCountdownLabel;
    private VisualElement finalCountdownContainer;

    // Position section
    private VisualElement positionSection; // The container with background image
    private Label positionLabel; // The "Position" label showing the number

    private float currentDistance = 0f;
    private float currentTime = 0f;
    private float timeRemaining = 300f;
    private bool isGameRunning = false;

    // Automatic checkpoint tracking
    private bool[] checkpointsReached;
    private Coroutine hideMotivationCoroutine;

    // Final countdown tracking
    private int lastDisplayedCountdown = -1;
    private const int COUNTDOWN_START_SECONDS = 10;

    // Track last motivation sprite to avoid repetition
    private int lastMotivationSpriteIndex = -1;

    // Position tracking
    private int currentPosition = -1; // -1 = unknown/hidden
    private Coroutine positionRequestCoroutine;

    // Tier thresholds - shows "Top X" when position is within this tier
    private static readonly int[] positionTiers = { 1000, 500, 300, 200, 100, 50, 40, 30, 20, 10 };

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

        // Position section - the first child of Motivation is the position card
        var motivation = root.Q<VisualElement>("Motivation");
        if (motivation != null && motivation.childCount > 0)
        {
            positionSection = motivation[0]; // The card with background image
        }
        positionLabel = root.Q<Label>("Position");

        Debug.Log($"[GameHUD] Position refs - section={positionSection != null}, label={positionLabel != null}, motivation={motivation != null}");

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

        // Get or create final countdown elements
        finalCountdownContainer = root.Q<VisualElement>("FinalCountdownContainer");
        finalCountdownLabel = root.Q<Label>("FinalCountdownLabel");

        // If container doesn't exist, create it dynamically
        if (finalCountdownContainer == null)
        {
            CreateFinalCountdownUI(root);
        }
        else if (finalCountdownLabel == null)
        {
            finalCountdownLabel = finalCountdownContainer.Q<Label>();
        }

        // Hide countdown initially
        if (finalCountdownContainer != null)
        {
            finalCountdownContainer.style.display = DisplayStyle.None;
        }

        // Hide position section initially
        if (positionSection != null)
        {
            positionSection.style.display = DisplayStyle.None;
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

            // Check for final countdown (last 10 seconds)
            CheckFinalCountdown();

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

        // Initialize and reset checkpoints array
        checkpointsReached = new bool[numberOfCheckpoints];
        for (int i = 0; i < checkpointsReached.Length; i++)
        {
            checkpointsReached[i] = false;
        }

        // Hide motivation image if visible and reset tracking
        HideMotivationImage();
        lastMotivationSpriteIndex = -1;

        // Reset final countdown
        ResetFinalCountdown();

        // Reset position section
        currentPosition = -1;
        if (positionSection != null)
        {
            positionSection.style.display = DisplayStyle.None;
        }

        // Start requesting position updates
        StartPositionRequests();

        UpdateDistanceDisplay();
        UpdateTimeDisplay();
        UpdateProgressBar();
        ShowHUD();

        // Log checkpoint distances
        float checkpointInterval = totalDistance / (numberOfCheckpoints + 1);
        Debug.Log($"[Game HUD] Game started - Time limit: {timeLimit}s, {numberOfCheckpoints} checkpoints every {checkpointInterval:F0}m");
    }

    public void StopGame()
    {
        isGameRunning = false;
        HideMotivationImage();
        HideFinalCountdown();
        StopPositionRequests();
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
    /// Checks if player has reached any automatic checkpoint (evenly distributed along the route)
    /// </summary>
    private void CheckAutomaticCheckpoints()
    {
        if (checkpointsReached == null) return;

        // Checkpoints are evenly distributed (e.g., 4 checkpoints = 20%, 40%, 60%, 80%)
        float checkpointInterval = totalDistance / (numberOfCheckpoints + 1);

        for (int i = 0; i < numberOfCheckpoints; i++)
        {
            if (!checkpointsReached[i])
            {
                float checkpointDistance = checkpointInterval * (i + 1);
                if (currentDistance >= checkpointDistance)
                {
                    checkpointsReached[i] = true;
                    OnCheckpointReached(i);
                    int percent = Mathf.RoundToInt((checkpointDistance / totalDistance) * 100f);
                    Debug.Log($"[GameHUD] Checkpoint {i + 1}/{numberOfCheckpoints} reached at {checkpointDistance:F0}m ({percent}%)!");
                }
            }
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

        // Notify MainGameManager to handle game over (sends MQTT to tablet)
        if (gameManager != null)
        {
            bool completed = currentDistance >= totalDistance;
            gameManager.TriggerGameOver(completed);
        }
        else
        {
            // Fallback: just show local game over screen
            Debug.LogWarning("[GameHUD] No MainGameManager reference - tablet won't be notified!");
            GameOverController.ShowResults(currentTime, currentDistance);
        }
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

        // Set sprite randomly from available sprites (avoid repeating the same one)
        if (motivationSprites != null && motivationSprites.Length > 0)
        {
            int spriteIndex;
            if (motivationSprites.Length > 1)
            {
                // Pick a random index that's different from the last one
                do
                {
                    spriteIndex = Random.Range(0, motivationSprites.Length);
                } while (spriteIndex == lastMotivationSpriteIndex);
            }
            else
            {
                spriteIndex = 0;
            }
            lastMotivationSpriteIndex = spriteIndex;

            if (motivationSprites[spriteIndex] != null)
            {
                motivationTextImage.style.backgroundImage = new StyleBackground(motivationSprites[spriteIndex]);
                // Preserve aspect ratio - don't stretch
                motivationTextImage.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
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

    // ========================================
    // Final Countdown (Last 10 Seconds)
    // ========================================

    /// <summary>
    /// Creates the final countdown UI dynamically if not present in UXML
    /// </summary>
    private void CreateFinalCountdownUI(VisualElement root)
    {
        finalCountdownContainer = new VisualElement();
        finalCountdownContainer.name = "FinalCountdownContainer";
        finalCountdownContainer.style.position = Position.Absolute;
        finalCountdownContainer.style.left = 0;
        finalCountdownContainer.style.right = 0;
        finalCountdownContainer.style.top = 0;
        finalCountdownContainer.style.bottom = 0;
        finalCountdownContainer.style.alignItems = Align.Center;
        finalCountdownContainer.style.justifyContent = Justify.Center;
        finalCountdownContainer.style.display = DisplayStyle.None;

        finalCountdownLabel = new Label();
        finalCountdownLabel.name = "FinalCountdownLabel";
        finalCountdownLabel.style.fontSize = 240; // 20% bigger than original 200
        finalCountdownLabel.style.color = Color.white; // Starts white, transitions to red
        finalCountdownLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        finalCountdownLabel.style.unityTextOutlineWidth = 4;
        finalCountdownLabel.style.unityTextOutlineColor = new Color(0.8f, 0f, 0f, 1f); // Dark red outline
        finalCountdownLabel.style.textShadow = new TextShadow
        {
            offset = new Vector2(8, 8), // Sharp offset for stacked look
            blurRadius = 0, // No blur = sharp shadow
            color = new Color(0.9f, 0.1f, 0.1f, 1f) // Red shadow
        };

        // Apply custom font if assigned
        if (countdownFont != null)
        {
            finalCountdownLabel.style.unityFont = countdownFont;
        }

        finalCountdownContainer.Add(finalCountdownLabel);
        root.Add(finalCountdownContainer);

        Debug.Log("[GameHUD] Created final countdown UI dynamically");
    }

    /// <summary>
    /// Checks if we should show the final countdown and updates the display
    /// </summary>
    private void CheckFinalCountdown()
    {
        if (finalCountdownContainer == null || finalCountdownLabel == null)
            return;

        int secondsLeft = Mathf.CeilToInt(timeRemaining);

        // Show countdown for last 10 seconds
        if (secondsLeft <= COUNTDOWN_START_SECONDS && secondsLeft > 0)
        {
            // Only update when the second changes
            if (secondsLeft != lastDisplayedCountdown)
            {
                lastDisplayedCountdown = secondsLeft;
                ShowCountdownNumber(secondsLeft);
            }
        }
        else if (secondsLeft <= 0 && lastDisplayedCountdown != 0)
        {
            // Hide countdown when time is up
            lastDisplayedCountdown = 0;
            HideFinalCountdown();
        }
        else if (secondsLeft > COUNTDOWN_START_SECONDS && finalCountdownContainer.style.display == DisplayStyle.Flex)
        {
            // Hide if somehow visible but not in countdown range
            HideFinalCountdown();
        }
    }

    /// <summary>
    /// Shows a countdown number with animation
    /// </summary>
    private void ShowCountdownNumber(int number)
    {
        if (finalCountdownContainer == null || finalCountdownLabel == null)
            return;

        // Update the number
        finalCountdownLabel.text = number.ToString();

        // Make visible
        finalCountdownContainer.style.display = DisplayStyle.Flex;

        // Color transition: white to red as time runs out
        float urgency = 1f - (number / (float)COUNTDOWN_START_SECONDS);
        Color countdownColor = Color.Lerp(Color.white, new Color(1f, 0.1f, 0.1f), urgency);
        finalCountdownLabel.style.color = countdownColor;

        // Shadow gets more intense red as urgency increases
        finalCountdownLabel.style.textShadow = new TextShadow
        {
            offset = new Vector2(8, 8),
            blurRadius = 0,
            color = Color.Lerp(new Color(0.7f, 0.1f, 0.1f, 0.8f), new Color(1f, 0f, 0f, 1f), urgency)
        };

        // Scale animation: start big, shrink to normal
        finalCountdownLabel.style.scale = new Scale(new Vector3(1.5f, 1.5f, 1f));
        finalCountdownLabel.style.opacity = 1f;

        // Set up transition
        finalCountdownLabel.style.transitionProperty = new StyleList<StylePropertyName>(
            new System.Collections.Generic.List<StylePropertyName> {
                new StylePropertyName("scale"),
                new StylePropertyName("opacity")
            }
        );
        finalCountdownLabel.style.transitionDuration = new StyleList<TimeValue>(
            new System.Collections.Generic.List<TimeValue> {
                new TimeValue(0.3f, TimeUnit.Second),
                new TimeValue(0.7f, TimeUnit.Second)
            }
        );
        finalCountdownLabel.style.transitionTimingFunction = new StyleList<EasingFunction>(
            new System.Collections.Generic.List<EasingFunction> {
                new EasingFunction(EasingMode.EaseOutBack),
                new EasingFunction(EasingMode.EaseIn)
            }
        );

        // Animate to normal scale and fade slightly
        finalCountdownLabel.schedule.Execute(() =>
        {
            finalCountdownLabel.style.scale = new Scale(Vector3.one);
            finalCountdownLabel.style.opacity = 0.8f;
        }).ExecuteLater(10);

        Debug.Log($"[GameHUD] Final countdown: {number}");
    }

    /// <summary>
    /// Hides the final countdown display
    /// </summary>
    private void HideFinalCountdown()
    {
        if (finalCountdownContainer == null)
            return;

        finalCountdownContainer.style.display = DisplayStyle.None;
        lastDisplayedCountdown = -1;
    }

    /// <summary>
    /// Resets the final countdown state (call when starting a new game)
    /// </summary>
    private void ResetFinalCountdown()
    {
        lastDisplayedCountdown = -1;
        HideFinalCountdown();
    }

    // ========================================
    // Position Section
    // ========================================

    /// <summary>
    /// Starts periodic position requests from the leaderboard backend
    /// </summary>
    private void StartPositionRequests()
    {
        StopPositionRequests();
        positionRequestCoroutine = StartCoroutine(PositionRequestLoop());
        Debug.Log("[GameHUD] Position requests started");
    }

    /// <summary>
    /// Stops position request coroutine and unsubscribes from events
    /// </summary>
    private void StopPositionRequests()
    {
        if (positionRequestCoroutine != null)
        {
            StopCoroutine(positionRequestCoroutine);
            positionRequestCoroutine = null;
        }

        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.OnPositionReceived -= OnPositionReceived;
        }
    }

    /// <summary>
    /// Periodically requests the player's leaderboard position
    /// </summary>
    private IEnumerator PositionRequestLoop()
    {
        // Wait a bit before the first request so we have some distance
        yield return new WaitForSeconds(positionUpdateInterval);

        while (isGameRunning)
        {
            // Re-subscribe each loop iteration to be robust against late initialization
            if (LeaderboardManager.Instance != null)
            {
                LeaderboardManager.Instance.OnPositionReceived -= OnPositionReceived;
                LeaderboardManager.Instance.OnPositionReceived += OnPositionReceived;

                if (gameManager != null)
                {
                    LeaderboardManager.Instance.RequestPosition(currentDistance, gameManager.CurrentGameMode);
                    Debug.Log($"[GameHUD] Requesting position for distance={currentDistance:F1}m, mode={gameManager.CurrentGameMode}");
                }
                else
                {
                    Debug.LogWarning("[GameHUD] Cannot request position - gameManager is null");
                }
            }
            else
            {
                Debug.LogWarning("[GameHUD] Cannot request position - LeaderboardManager.Instance is null");
            }

            yield return new WaitForSeconds(positionUpdateInterval);
        }
    }

    /// <summary>
    /// Called when we receive a position response from the backend
    /// </summary>
    private void OnPositionReceived(int position)
    {
        UpdatePosition(position);
    }

    /// <summary>
    /// Updates the position display based on the player's leaderboard position.
    /// Shows tiered display: Top 1000, 500, 300, 200, 100, 50, 40, 30, 20, 10, then exact 9-1
    /// </summary>
    private void UpdatePosition(int position)
    {
        currentPosition = position;

        if (positionSection == null || positionLabel == null)
            return;

        // Hide if position is unknown or beyond top 1000
        if (position <= 0 || position > 1000)
        {
            positionSection.style.display = DisplayStyle.None;
            return;
        }

        // Show the section
        positionSection.style.display = DisplayStyle.Flex;

        // For positions 1-10, show exact number
        if (position <= 10)
        {
            positionLabel.text = position.ToString();
        }
        else
        {
            // Find the appropriate tier
            // Tiers: 1000, 500, 300, 200, 100, 50, 40, 30, 20, 10
            // If position <= tier, show that tier value
            int displayValue = 1000; // default
            for (int i = 0; i < positionTiers.Length; i++)
            {
                if (position <= positionTiers[i])
                {
                    displayValue = positionTiers[i];
                }
                else
                {
                    break;
                }
            }

            positionLabel.text = displayValue.ToString();
        }

        Debug.Log($"[GameHUD] Position updated: {position} (displaying: {positionLabel.text})");
    }

    private void OnDisable()
    {
        StopPositionRequests();
    }
}
