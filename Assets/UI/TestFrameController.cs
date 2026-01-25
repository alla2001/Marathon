using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Simple controller to display the Test Frame from Figma export
/// This is a test of the Figma-to-Unity workflow
/// </summary>
public class TestFrameController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement root;

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        root = uiDocument.rootVisualElement;

        Debug.Log("[TestFrame] Test frame loaded from Figma export");
    }
}
