using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class GameOverController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    [Header("Transition Settings")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private float contentDelayAfterFade = 0.3f;
    [SerializeField] private float resultCountUpDuration = 1.5f;

    private VisualElement root;
    private VisualElement contentContainer;
    private VisualElement backgroundOverlay;
    private Label timeResult;
    private Label distanceResult;

    // Win/Lose UI elements (lists to support multiple elements with same name)
    private List<VisualElement> winContainers = new List<VisualElement>();
    private List<VisualElement> loseContainers = new List<VisualElement>();
    private List<Label> winTimeResults = new List<Label>();
    private List<Label> winDistanceResults = new List<Label>();
    private List<Label> loseTimeResults = new List<Label>();
    private List<Label> loseDistanceResults = new List<Label>();

    private float finalTime;
    private float finalDistance;
    private bool didWin;
    private Coroutine transitionCoroutine;
    private Coroutine countUpCoroutine;

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

        root = uiDocument.rootVisualElement;

        // Get ALL Win/Lose containers with any of these names
        winContainers = FindAllElementsWithNames(root, "WinContainer", "WinScreen", "Victory", "Win", "WinPanel");
        loseContainers = FindAllElementsWithNames(root, "LoseContainer", "LoseScreen", "GameOver", "Lose", "LosePanel", "Defeat");

        Debug.Log($"[GameOverController] Found {winContainers.Count} win containers, {loseContainers.Count} lose containers");

        // Get result labels from all win containers
        foreach (var container in winContainers)
        {
            var timeLabel = container.Q<Label>("TimeResult") ?? container.Q<Label>("WinTimeResult");
            var distLabel = container.Q<Label>("DistanceResult") ?? container.Q<Label>("WinDistanceResult");
            if (timeLabel != null) winTimeResults.Add(timeLabel);
            if (distLabel != null) winDistanceResults.Add(distLabel);
        }

        // Get result labels from all lose containers
        foreach (var container in loseContainers)
        {
            var timeLabel = container.Q<Label>("TimeResult") ?? container.Q<Label>("LoseTimeResult");
            var distLabel = container.Q<Label>("DistanceResult") ?? container.Q<Label>("LoseDistanceResult");
            if (timeLabel != null) loseTimeResults.Add(timeLabel);
            if (distLabel != null) loseDistanceResults.Add(distLabel);
        }

        // Fallback to generic labels if win/lose specific ones not found
        timeResult = root.Q<Label>("TimeResult");
        distanceResult = root.Q<Label>("DistanceResult");
        contentContainer = root.Q<VisualElement>("ContentContainer") ?? root.Q<VisualElement>("GameOverContent") ?? root;
        backgroundOverlay = root.Q<VisualElement>("BackgroundOverlay");

        // Create background overlay if it doesn't exist
        if (backgroundOverlay == null)
        {
            CreateBackgroundOverlay();
        }

        // Hide all containers initially
        foreach (var container in winContainers) container.style.display = DisplayStyle.None;
        foreach (var container in loseContainers) container.style.display = DisplayStyle.None;

        // Hide the game over screen initially
        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;

        Debug.Log($"[GameOverController] Initialized - {winContainers.Count} win containers, {loseContainers.Count} lose containers");
    }

    private void OnDisable()
    {
        // No cleanup needed - display only
    }

    public void ShowGameOver(float time, float distance, bool won = false)
    {
        if (uiDocument == null)
        {
            Debug.LogError("[GameOverController] Cannot show - uiDocument is NULL!");
            return;
        }

        finalTime = time;
        finalDistance = distance;
        didWin = won;

        if (root == null)
        {
            Debug.LogError("[GameOverController] Cannot show - rootVisualElement is NULL!");
            return;
        }

        // Stop any existing transition
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }
        if (countUpCoroutine != null)
        {
            StopCoroutine(countUpCoroutine);
        }

        // Show appropriate win/lose containers (all of them)
        foreach (var container in winContainers) container.style.display = won ? DisplayStyle.Flex : DisplayStyle.None;
        foreach (var container in loseContainers) container.style.display = won ? DisplayStyle.None : DisplayStyle.Flex;

        // Set which content container to animate (use first container if available)
        var firstWinContainer = winContainers.Count > 0 ? winContainers[0] : null;
        var firstLoseContainer = loseContainers.Count > 0 ? loseContainers[0] : null;
        contentContainer = won ? (firstWinContainer ?? contentContainer) : (firstLoseContainer ?? contentContainer);

        // Start smooth transition
        transitionCoroutine = StartCoroutine(ShowTransitionCoroutine());

        Debug.Log($"[GameOverController] Game Over screen transition started - Won: {won}, Time: {time:F2}s, Distance: {distance:F1}m");

        // Optionally save the score
        SaveScore(time, distance);
    }

    // Overload for backwards compatibility
    public void ShowGameOver(float time, float distance)
    {
        // Determine win/lose based on distance (completed = won)
        float targetDistance = PlayerPrefs.GetFloat("TargetDistance", 1600f);
        bool won = distance >= targetDistance;
        ShowGameOver(time, distance, won);
    }

    private IEnumerator ShowTransitionCoroutine()
    {
        // Prepare initial state
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 0f;

        // Set initial state for content (scale down and transparent)
        if (contentContainer != null && contentContainer != root)
        {
            contentContainer.style.opacity = 0f;
            contentContainer.style.scale = new Scale(new Vector3(0.8f, 0.8f, 1f));
        }

        // Clear result text initially (for all active containers)
        var activeTimeLabels = didWin ? winTimeResults : loseTimeResults;
        var activeDistanceLabels = didWin ? winDistanceResults : loseDistanceResults;
        foreach (var label in activeTimeLabels) label.text = "00:00";
        foreach (var label in activeDistanceLabels) label.text = "0 m";
        if (timeResult != null) timeResult.text = "00:00";
        if (distanceResult != null) distanceResult.text = "0 m";

        // Setup background fade transition
        SetupFadeTransition(root, fadeInDuration);

        // Trigger background fade in
        yield return null; // Wait a frame for styles to apply
        root.style.opacity = 1f;

        // Wait for background fade
        yield return new WaitForSeconds(fadeInDuration * 0.5f);

        // Animate content in
        if (contentContainer != null && contentContainer != root)
        {
            SetupContentTransition(contentContainer);
            yield return null;
            contentContainer.style.opacity = 1f;
            contentContainer.style.scale = new Scale(Vector3.one);
        }

        // Wait a bit then start counting up results
        yield return new WaitForSeconds(contentDelayAfterFade);

        // Animate results counting up
        countUpCoroutine = StartCoroutine(CountUpResultsCoroutine());

        Debug.Log("[GameOverController] Game Over screen SHOWN with transition");
    }

    private IEnumerator CountUpResultsCoroutine()
    {
        float elapsed = 0f;

        // Get the appropriate labels based on win/lose state (all of them)
        var activeTimeLabels = didWin ? winTimeResults : loseTimeResults;
        var activeDistanceLabels = didWin ? winDistanceResults : loseDistanceResults;

        while (elapsed < resultCountUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / resultCountUpDuration);

            // Ease out cubic for smooth deceleration
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            // Update time display (count up) for all labels
            float currentTime = Mathf.Lerp(0f, finalTime, easedT);
            int minutes = Mathf.FloorToInt(currentTime / 60f);
            int seconds = Mathf.FloorToInt(currentTime % 60f);
            string timeText = $"{minutes:00}:{seconds:00}";

            foreach (var label in activeTimeLabels) label.text = timeText;
            if (timeResult != null) timeResult.text = timeText;

            // Update distance display (count up) for all labels
            float currentDistance = Mathf.Lerp(0f, finalDistance, easedT);
            string distText = $"{Mathf.RoundToInt(currentDistance)} m";

            foreach (var label in activeDistanceLabels) label.text = distText;
            if (distanceResult != null) distanceResult.text = distText;

            yield return null;
        }

        // Ensure final values are exact
        UpdateTimeDisplay();
        UpdateDistanceDisplay();
    }

    public void HideGameOver()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[GameOverController] Cannot hide - uiDocument is NULL!");
            return;
        }

        if (root == null)
        {
            Debug.LogError("[GameOverController] Cannot hide - rootVisualElement is NULL!");
            return;
        }

        // Stop any existing transitions
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }
        if (countUpCoroutine != null)
        {
            StopCoroutine(countUpCoroutine);
        }

        // Start hide transition
        transitionCoroutine = StartCoroutine(HideTransitionCoroutine());
    }

    private IEnumerator HideTransitionCoroutine()
    {
        // Setup fade out transition
        SetupFadeTransition(root, fadeOutDuration);

        // Fade out content first
        if (contentContainer != null && contentContainer != root)
        {
            SetupContentTransition(contentContainer, fadeOutDuration * 0.5f);
            contentContainer.style.opacity = 0f;
            contentContainer.style.scale = new Scale(new Vector3(0.9f, 0.9f, 1f));
        }

        yield return new WaitForSeconds(fadeOutDuration * 0.3f);

        // Fade out background
        root.style.opacity = 0f;

        // Wait for fade to complete
        yield return new WaitForSeconds(fadeOutDuration);

        // Hide completely
        root.style.display = DisplayStyle.None;

        Debug.Log("[GameOverController] Game Over screen HIDDEN with transition");
    }

    private void UpdateTimeDisplay()
    {
        int minutes = Mathf.FloorToInt(finalTime / 60f);
        int seconds = Mathf.FloorToInt(finalTime % 60f);
        string timeText = $"{minutes:00}:{seconds:00}";

        // Update all appropriate labels based on win/lose
        var activeLabels = didWin ? winTimeResults : loseTimeResults;
        foreach (var label in activeLabels) label.text = timeText;
        if (timeResult != null) timeResult.text = timeText;
    }

    private void UpdateDistanceDisplay()
    {
        string distanceText = $"{Mathf.RoundToInt(finalDistance)} m";

        // Update all appropriate labels based on win/lose
        var activeLabels = didWin ? winDistanceResults : loseDistanceResults;
        foreach (var label in activeLabels) label.text = distanceText;
        if (distanceResult != null) distanceResult.text = distanceText;
    }

    // No button handlers - this is display only on Game PC
    // Tablet handles "Play Again" interaction

    private void SetupFadeTransition(VisualElement element, float duration)
    {
        element.style.transitionProperty = new StyleList<StylePropertyName>(
            new System.Collections.Generic.List<StylePropertyName> {
                new StylePropertyName("opacity")
            }
        );
        element.style.transitionDuration = new StyleList<TimeValue>(
            new System.Collections.Generic.List<TimeValue> {
                new TimeValue(duration, TimeUnit.Second)
            }
        );
        element.style.transitionTimingFunction = new StyleList<EasingFunction>(
            new System.Collections.Generic.List<EasingFunction> {
                new EasingFunction(EasingMode.EaseInOut)
            }
        );
    }

    private void SetupContentTransition(VisualElement element, float duration = -1f)
    {
        if (duration < 0) duration = fadeInDuration;

        element.style.transitionProperty = new StyleList<StylePropertyName>(
            new System.Collections.Generic.List<StylePropertyName> {
                new StylePropertyName("opacity"),
                new StylePropertyName("scale")
            }
        );
        element.style.transitionDuration = new StyleList<TimeValue>(
            new System.Collections.Generic.List<TimeValue> {
                new TimeValue(duration, TimeUnit.Second),
                new TimeValue(duration, TimeUnit.Second)
            }
        );
        element.style.transitionTimingFunction = new StyleList<EasingFunction>(
            new System.Collections.Generic.List<EasingFunction> {
                new EasingFunction(EasingMode.EaseOutCubic),
                new EasingFunction(EasingMode.EaseOutBack)
            }
        );
    }

    private void CreateBackgroundOverlay()
    {
        // Create a semi-transparent background overlay for smooth fade effect
        backgroundOverlay = new VisualElement();
        backgroundOverlay.name = "BackgroundOverlay";
        backgroundOverlay.style.position = Position.Absolute;
        backgroundOverlay.style.left = 0;
        backgroundOverlay.style.right = 0;
        backgroundOverlay.style.top = 0;
        backgroundOverlay.style.bottom = 0;
        backgroundOverlay.style.backgroundColor = new Color(0, 0, 0, 0.85f);

        // Insert at the beginning so it's behind content
        if (root.childCount > 0)
        {
            root.Insert(0, backgroundOverlay);
        }
        else
        {
            root.Add(backgroundOverlay);
        }
    }

    private void SaveScore(float time, float distance)
    {
        // Save the score to PlayerPrefs or a database
        PlayerPrefs.SetFloat("LastTime", time);
        PlayerPrefs.SetFloat("LastDistance", distance);

        // Check if it's a personal best
        float bestTime = PlayerPrefs.GetFloat("BestTime", float.MaxValue);
        if (distance >= 1600f && time < bestTime) // Completed full distance
        {
            PlayerPrefs.SetFloat("BestTime", time);
            Debug.Log($"New personal best time: {time:F2}s!");
        }

        PlayerPrefs.Save();

        // Send score to online leaderboard
        // SubmitToOnlineLeaderboard(time, distance);
    }

    // Call this method from GameHUDController when game is complete
    public static void ShowResults(float time, float distance, bool won = false)
    {
        GameOverController controller = FindObjectOfType<GameOverController>();
        if (controller != null)
        {
            controller.ShowGameOver(time, distance, won);
        }
        else
        {
            Debug.LogError("GameOverController not found in scene!");
        }

        // Hide the HUD when showing game over
        GameHUDController hudController = FindObjectOfType<GameHUDController>();
        if (hudController != null)
        {
            hudController.HideHUD();
        }
    }

    // Overload for backwards compatibility
    public static void ShowResults(float time, float distance)
    {
        float targetDistance = PlayerPrefs.GetFloat("TargetDistance", 1600f);
        bool won = distance >= targetDistance;
        ShowResults(time, distance, won);
    }

    // Public getters
    public float FinalTime => finalTime;
    public float FinalDistance => finalDistance;

    /// <summary>
    /// Finds the first existing VisualElement from a list of possible names
    /// </summary>
    private VisualElement FindFirstExistingElement(VisualElement parent, params string[] names)
    {
        foreach (string name in names)
        {
            var element = parent.Q<VisualElement>(name);
            if (element != null)
            {
                Debug.Log($"[GameOverController] Found element with name: {name}");
                return element;
            }
        }
        return null;
    }

    /// <summary>
    /// Finds ALL VisualElements that match any of the given names
    /// </summary>
    private List<VisualElement> FindAllElementsWithNames(VisualElement parent, params string[] names)
    {
        var results = new List<VisualElement>();
        foreach (string name in names)
        {
            var elements = parent.Query<VisualElement>(name).ToList();
            foreach (var element in elements)
            {
                if (!results.Contains(element))
                {
                    results.Add(element);
                    Debug.Log($"[GameOverController] Found element: {name}");
                }
            }
        }
        return results;
    }
}
