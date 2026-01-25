using UnityEngine;

/// <summary>
/// Adds subtle camera animations based on game mode and movement speed.
/// Attach to the camera or a parent object that the camera follows.
/// </summary>
public class CameraAnimationController : MonoBehaviour
{
    public enum GameMode
    {
        Rowing,
        Running,
        Cycling
    }

    [Header("References")]
    [SerializeField] private MainGameManager gameManager;
    [SerializeField] private Transform cameraTransform;

    [Header("General Settings")]
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private bool animatePosition = true;
    [SerializeField] private bool animateRotation = true;

    [Header("Rowing Animation")]
    [Tooltip("Forward/back sway like rowing stroke")]
    [SerializeField] private float rowingBobAmount = 0.05f;
    [SerializeField] private float rowingBobSpeed = 1.5f;
    [SerializeField] private float rowingPitchAmount = 1f;

    [Header("Running Animation")]
    [Tooltip("Up/down bob like footsteps")]
    [SerializeField] private float runningBobAmount = 0.03f;
    [SerializeField] private float runningBobSpeed = 8f;
    [SerializeField] private float runningRollAmount = 0.5f;

    [Header("Cycling Animation")]
    [Tooltip("Side-to-side sway like pedaling")]
    [SerializeField] private float cyclingSwayAmount = 0.02f;
    [SerializeField] private float cyclingSwaySpeed = 3f;
    [SerializeField] private float cyclingRollAmount = 0.8f;

    // Internal state
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private float animationTime = 0f;
    private float currentIntensity = 0f;
    private GameMode currentMode = GameMode.Rowing;

    private void Start()
    {
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main?.transform;
        }

        if (cameraTransform != null)
        {
            originalLocalPosition = cameraTransform.localPosition;
            originalLocalRotation = cameraTransform.localRotation;
        }

        if (gameManager == null)
        {
            gameManager = FindObjectOfType<MainGameManager>();
        }
    }

    private void Update()
    {
        if (cameraTransform == null || gameManager == null)
            return;

        // Only animate during gameplay
        if (gameManager.CurrentState != MainGameManager.GameState.PLAYING)
        {
            // Smoothly return to original position
            currentIntensity = Mathf.Lerp(currentIntensity, 0f, Time.deltaTime * smoothSpeed);
            ApplyAnimation();
            return;
        }

        // Update game mode
        UpdateGameMode();

        // Calculate intensity based on speed
        float speedRatio = Mathf.Clamp01(gameManager.CurrentSpeed / maxSpeed);
        currentIntensity = Mathf.Lerp(currentIntensity, speedRatio, Time.deltaTime * smoothSpeed);

        // Only animate if there's movement
        if (currentIntensity > 0.01f)
        {
            // Advance animation time based on speed
            float speedMultiplier = 0.5f + (currentIntensity * 0.5f);
            animationTime += Time.deltaTime * speedMultiplier;

            ApplyAnimation();
        }
    }

    private void UpdateGameMode()
    {
        string mode = gameManager.CurrentGameMode?.ToLower() ?? "rowing";
        switch (mode)
        {
            case "rowing":
                currentMode = GameMode.Rowing;
                break;
            case "running":
                currentMode = GameMode.Running;
                break;
            case "cycling":
                currentMode = GameMode.Cycling;
                break;
        }
    }

    private void ApplyAnimation()
    {
        Vector3 positionOffset = Vector3.zero;
        Vector3 rotationOffset = Vector3.zero;

        switch (currentMode)
        {
            case GameMode.Rowing:
                ApplyRowingAnimation(ref positionOffset, ref rotationOffset);
                break;
            case GameMode.Running:
                ApplyRunningAnimation(ref positionOffset, ref rotationOffset);
                break;
            case GameMode.Cycling:
                ApplyCyclingAnimation(ref positionOffset, ref rotationOffset);
                break;
        }

        // Apply with intensity
        positionOffset *= currentIntensity;
        rotationOffset *= currentIntensity;

        // Set position and rotation
        if (animatePosition)
        {
            cameraTransform.localPosition = originalLocalPosition + positionOffset;
        }

        if (animateRotation)
        {
            cameraTransform.localRotation = originalLocalRotation * Quaternion.Euler(rotationOffset);
        }
    }

    private void ApplyRowingAnimation(ref Vector3 posOffset, ref Vector3 rotOffset)
    {
        // Rowing: Forward/back bob with slight pitch
        // Simulates the pulling motion of rowing
        float t = animationTime * rowingBobSpeed;

        // Forward/back movement (Z axis) - smooth sine wave
        float forwardBack = Mathf.Sin(t) * rowingBobAmount;
        posOffset.z = forwardBack;

        // Slight up/down at the peak of the stroke
        float upDown = Mathf.Sin(t * 2f) * rowingBobAmount * 0.3f;
        posOffset.y = upDown;

        // Pitch forward/back with the stroke
        rotOffset.x = Mathf.Sin(t) * rowingPitchAmount;
    }

    private void ApplyRunningAnimation(ref Vector3 posOffset, ref Vector3 rotOffset)
    {
        // Running: Quick up/down bob with slight roll
        // Simulates footstep impact
        float t = animationTime * runningBobSpeed;

        // Vertical bob - double frequency for left/right foot
        float verticalBob = Mathf.Abs(Mathf.Sin(t)) * runningBobAmount;
        posOffset.y = verticalBob;

        // Slight horizontal sway
        float horizontalSway = Mathf.Sin(t * 0.5f) * runningBobAmount * 0.3f;
        posOffset.x = horizontalSway;

        // Roll with each step
        rotOffset.z = Mathf.Sin(t * 0.5f) * runningRollAmount;

        // Slight pitch on impact
        rotOffset.x = Mathf.Abs(Mathf.Sin(t)) * runningRollAmount * 0.3f;
    }

    private void ApplyCyclingAnimation(ref Vector3 posOffset, ref Vector3 rotOffset)
    {
        // Cycling: Side-to-side sway with roll
        // Simulates the pedaling motion
        float t = animationTime * cyclingSwaySpeed;

        // Side to side sway
        float sway = Mathf.Sin(t) * cyclingSwayAmount;
        posOffset.x = sway;

        // Slight vertical movement synced with pedaling
        float verticalBob = Mathf.Sin(t * 2f) * cyclingSwayAmount * 0.5f;
        posOffset.y = verticalBob;

        // Roll with the sway (leaning into pedal push)
        rotOffset.z = Mathf.Sin(t) * cyclingRollAmount;
    }

    /// <summary>
    /// Manually set the game mode (can be called from MainGameManager)
    /// </summary>
    public void SetGameMode(string mode)
    {
        switch (mode.ToLower())
        {
            case "rowing":
                currentMode = GameMode.Rowing;
                break;
            case "running":
                currentMode = GameMode.Running;
                break;
            case "cycling":
                currentMode = GameMode.Cycling;
                break;
        }
    }

    /// <summary>
    /// Reset the camera to its original position
    /// </summary>
    public void ResetCamera()
    {
        currentIntensity = 0f;
        animationTime = 0f;

        if (cameraTransform != null)
        {
            cameraTransform.localPosition = originalLocalPosition;
            cameraTransform.localRotation = originalLocalRotation;
        }
    }

    private void OnDisable()
    {
        // Reset camera when disabled
        if (cameraTransform != null)
        {
            cameraTransform.localPosition = originalLocalPosition;
            cameraTransform.localRotation = originalLocalRotation;
        }
    }
}
