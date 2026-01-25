using UnityEngine;
using Dreamteck.Splines;

public class ForceObjectControllerRebuild : MonoBehaviour
{
    [SerializeField] private float rebuildDelay = 0.2f;
    [SerializeField] private int forceSpawnCount = 100;

    private ObjectController objectController;

    void Start()
    {
        objectController = GetComponent<ObjectController>();
        if (objectController != null)
        {
            // Force spawn count immediately
            objectController.spawnCount = forceSpawnCount;

            // Rebuild after delay to ensure spline is ready
            Invoke(nameof(DoRebuild), rebuildDelay);
        }
        else
        {
            Debug.LogError($"[ForceObjectControllerRebuild] No ObjectController found on {gameObject.name}");
        }
    }

    void DoRebuild()
    {
        if (objectController != null)
        {
            // Set count again in case it was overwritten
            objectController.spawnCount = forceSpawnCount;

            // Force rebuild
            objectController.RebuildImmediate();

            Debug.Log($"[ForceObjectControllerRebuild] Rebuilt {gameObject.name} with {forceSpawnCount} instances");
        }
    }

    // Also rebuild on enable in case object is re-enabled
    void OnEnable()
    {
        if (objectController != null && Application.isPlaying)
        {
            objectController.spawnCount = forceSpawnCount;
            Invoke(nameof(DoRebuild), rebuildDelay);
        }
    }
}
