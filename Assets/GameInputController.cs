using UnityEngine;

// Simple input controller for testing the main game
// Replace this with actual hardware input (rowing machine, treadmill, bike)
public class GameInputController : MonoBehaviour
{
    [SerializeField] private MainGameManager gameManager;

    [Header("Test Input Keys")]
    [SerializeField] private KeyCode actionKey = KeyCode.Space;
    [SerializeField] private KeyCode pauseKey = KeyCode.P;
    [SerializeField] private KeyCode resetKey = KeyCode.R;

    [Header("Input Settings")]
    [SerializeField] private float actionIntensity = 1f;

    private void Start()
    {
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<MainGameManager>();
        }

        if (gameManager == null)
        {
            Debug.LogError("MainGameManager not found!");
        }
    }

    private void Update()
    {
        if (gameManager == null)
            return;

        // Test action (rowing stroke, running step, cycling pedal)
        if (Input.GetKeyDown(actionKey))
        {
            gameManager.PerformAction(actionIntensity);
        }

        // Pause/Resume toggle
        if (Input.GetKeyDown(pauseKey))
        {
            if (gameManager.CurrentState == MainGameManager.GameState.PLAYING)
            {
                gameManager.PauseGame();
            }
            else if (gameManager.CurrentState == MainGameManager.GameState.PAUSED)
            {
                gameManager.ResumeGame();
            }
        }

        // Reset game
        if (Input.GetKeyDown(resetKey))
        {
            gameManager.ResetGame();
        }
    }

    // Call these methods from your hardware input system
    public void OnHardwareAction(float intensity)
    {
        if (gameManager != null)
        {
            gameManager.PerformAction(intensity);
        }
    }

    public void OnHardwarePause()
    {
        if (gameManager != null)
        {
            gameManager.PauseGame();
        }
    }

    public void OnHardwareResume()
    {
        if (gameManager != null)
        {
            gameManager.ResumeGame();
        }
    }
}
