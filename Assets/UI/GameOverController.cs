using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;

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

    // Win/Lose UI elements
    private VisualElement winContainer;
    private VisualElement loseContainer;
    private Label winTimeResult;
    private Label winDistanceResult;
    private Label loseTimeResult;
    private Label loseDistanceResult;

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

        // Get Win/Lose containers
        winContainer = root.Q<VisualElement>("WinContainer") ?? root.Q<VisualElement>("WinScreen") ?? root.Q<VisualElement>("Victory");
        loseContainer = root.Q<VisualElement>("LoseContainer") ?? root.Q<VisualElement>("LoseScreen") ?? root.Q<VisualElement>("GameOver");

        // Get result labels from win container
        if (winContainer != null)
        {
            winTimeResult = winContainer.Q<Label>("TimeResult") ?? winContainer.Q<Label>("WinTimeResult");
            winDistanceResult = winContainer.Q<Label>("DistanceResult") ?? winContainer.Q<Label>("WinDistanceResult");
        }

        // Get result labels from lose container
        if (loseContainer != null)
        {
            loseTimeResult = loseContainer.Q<Label>("TimeResult") ?? loseContainer.Q<Label>("LoseTimeResult");
            loseDistanceResult = loseContainer.Q<Label>("DistanceResult") ?? loseContainer.Q<Label>("LoseDistanceResult");
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

        // Hide containers initially
        if (winContainer != null) winContainer.style.display = DisplayStyle.None;
        if (loseContainer != null) loseContainer.style.display = DisplayStyle.None;

        // Hide the game over screen initially
        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;

        Debug.Log($"[GameOverController] Win container: {(winContainer != null ? "found" : "NOT FOUND")}, Lose container: {(loseContainer != null ? "found" : "NOT FOUND")}");
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

        // Show appropriate win/lose container
        if (winContainer != null) winContainer.style.display = won ? DisplayStyle.Flex : DisplayStyle.None;
        if (loseContainer != null) loseContainer.style.display = won ? DisplayStyle.None : DisplayStyle.Flex;

        // Set which content container to animate
        contentContainer = won ? (winContainer ?? contentContainer) : (loseContainer ?? contentContainer);

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

        // Clear result text initially (for the active container)
        Label activeTimeLabel = didWin ? (winTimeResult ?? timeResult) : (loseTimeResult ?? timeResult);
        Label activeDistanceLabel = didWin ? (winDistanceResult ?? distanceResult) : (loseDistanceResult ?? distanceResult);
        if (activeTimeLabel != null) activeTimeLabel.text = "00:00";
        if (activeDistanceLabel != null) activeDistanceLabel.text = "0 m";

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

        // Get the appropriate labels based on win/lose state
        Label activeTimeLabel = didWin ? (winTimeResult ?? timeResult) : (loseTimeResult ?? timeResult);
        Label activeDistanceLabel = didWin ? (winDistanceResult ?? distanceResult) : (loseDistanceResult ?? distanceResult);

        while (elapsed < resultCountUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / resultCountUpDuration);

            // Ease out cubic for smooth deceleration
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            // Update time display (count up)
            float currentTime = Mathf.Lerp(0f, finalTime, easedT);
            if (activeTimeLabel != null)
            {
                int minutes = Mathf.FloorToInt(currentTime / 60f);
                int seconds = Mathf.FloorToInt(currentTime % 60f);
                activeTimeLabel.text = $"{minutes:00}:{seconds:00}";
            }

            // Update distance display (count up)
            float currentDistance = Mathf.Lerp(0f, finalDistance, easedT);
            if (activeDistanceLabel != null)
            {
                activeDistanceLabel.text = $"{Mathf.RoundToInt(currentDistance)} m";
            }

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

        // Update appropriate label based on win/lose
        Label activeLabel = didWin ? (winTimeResult ?? timeResult) : (loseTimeResult ?? timeResult);
        if (activeLabel != null)
        {
            activeLabel.text = timeText;
        }
    }

    private void UpdateDistanceDisplay()
    {
        string distanceText = $"{Mathf.RoundToInt(finalDistance)} m";

        // Update appropriate label based on win/lose
        Label activeLabel = didWin ? (winDistanceResult ?? distanceResult) : (loseDistanceResult ?? distanceResult);
        if (activeLabel != null)
        {
            activeLabel.text = distanceText;
        }
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
}
