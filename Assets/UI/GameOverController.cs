using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class GameOverController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private Label timeResult;
    private Label distanceResult;
    // No buttons - this is display only on Game PC

    private float finalTime;
    private float finalDistance;

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

        // Get UI elements
        timeResult = root.Q<Label>("TimeResult");
        distanceResult = root.Q<Label>("DistanceResult");

        // Hide the game over screen initially
        root.style.display = DisplayStyle.None;
    }

    private void OnDisable()
    {
        // No cleanup needed - display only
    }

    public void ShowGameOver(float time, float distance)
    {
        if (uiDocument == null)
        {
            Debug.LogError("[GameOverController] Cannot show - uiDocument is NULL!");
            return;
        }

        finalTime = time;
        finalDistance = distance;

        // Update UI with results
        UpdateTimeDisplay();
        UpdateDistanceDisplay();

        // Show the game over screen
        var root = uiDocument.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[GameOverController] Cannot show - rootVisualElement is NULL!");
            return;
        }

        root.style.display = DisplayStyle.Flex;
        Debug.Log($"[GameOverController] Game Over screen SHOWN - Time: {time:F2}s, Distance: {distance:F1}m");

        // Optionally save the score
        SaveScore(time, distance);
    }

    public void HideGameOver()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[GameOverController] Cannot hide - uiDocument is NULL!");
            return;
        }

        var root = uiDocument.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[GameOverController] Cannot hide - rootVisualElement is NULL!");
            return;
        }

        root.style.display = DisplayStyle.None;
        Debug.Log("[GameOverController] Game Over screen HIDDEN");
    }

    private void UpdateTimeDisplay()
    {
        if (timeResult != null)
        {
            int minutes = Mathf.FloorToInt(finalTime / 60f);
            int seconds = Mathf.FloorToInt(finalTime % 60f);
            timeResult.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void UpdateDistanceDisplay()
    {
        if (distanceResult != null)
        {
            distanceResult.text = $"{Mathf.RoundToInt(finalDistance)} m";
        }
    }

    // No button handlers - this is display only on Game PC
    // Tablet handles "Play Again" interaction

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
    public static void ShowResults(float time, float distance)
    {
        GameOverController controller = FindObjectOfType<GameOverController>();
        if (controller != null)
        {
            controller.ShowGameOver(time, distance);
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

    // Public getters
    public float FinalTime => finalTime;
    public float FinalDistance => finalDistance;
}
