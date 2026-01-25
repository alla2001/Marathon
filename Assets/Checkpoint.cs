using UnityEngine;
using System;

/// <summary>
/// Checkpoint trigger component
/// Place this on checkpoint GameObjects along the spline
/// </summary>
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] private int checkpointIndex = 0;
    [SerializeField] private bool isFinishLine = false;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject activeVisual;
    [SerializeField] private GameObject completedVisual;
    [SerializeField] private Color activeColor = Color.yellow;
    [SerializeField] private Color completedColor = Color.green;

    // Events
    public event Action OnCheckpointReached;

    private bool hasBeenReached = false;
    private Renderer visualRenderer;

    private void Start()
    {
        // Ensure collider is trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // Setup visuals
        if (activeVisual != null)
        {
            visualRenderer = activeVisual.GetComponent<Renderer>();
            SetVisualState(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if it's the player
        if (other.CompareTag("Player") && !hasBeenReached)
        {
            ReachCheckpoint();
        }
    }

    private void ReachCheckpoint()
    {
        if (hasBeenReached)
            return;

        hasBeenReached = true;
        Debug.Log($"[Checkpoint {checkpointIndex}] Reached!");

        // Update visuals
        SetVisualState(true);

        // Trigger event
        OnCheckpointReached?.Invoke();

        // Play sound/particle effect here if needed
    }

    public void ResetCheckpoint()
    {
        hasBeenReached = false;
        SetVisualState(false);
    }

    private void SetVisualState(bool completed)
    {
        if (activeVisual != null)
        {
            activeVisual.SetActive(!completed);
        }

        if (completedVisual != null)
        {
            completedVisual.SetActive(completed);
        }

        if (visualRenderer != null)
        {
            visualRenderer.material.color = completed ? completedColor : activeColor;
        }
    }

    // Public getters
    public int CheckpointIndex => checkpointIndex;
    public bool IsFinishLine => isFinishLine;
    public bool HasBeenReached => hasBeenReached;
}
