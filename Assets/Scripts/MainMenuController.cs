using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Controller for the Main Menu scene displayed before Level 1.
/// Features a black background, top logo area for "Stitches",
/// and a prompt/button "PRESS ENTER TO CONTINUE".
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Scene Loading")]
    [Tooltip("Name of the scene to load upon continuing")]
    public string firstLevelSceneName = "Level 1";

    [Header("Logo Settings (Stitches)")]
    [Tooltip("Top logo text displayed until a custom sprite is provided")]
    public TextMeshProUGUI logoText;
    [Tooltip("Image component reserved for attaching the custom Stitches logo sprite later")]
    public Image logoImage;

    [Header("Prompt / Button Settings")]
    [Tooltip("Prompt text using Electronic Highway Sign SDF font")]
    public TextMeshProUGUI promptText;
    [Tooltip("Clickable button component")]
    public Button continueButton;

    [Header("Prompt Message")]
    [TextArea(1, 3)]
    public string promptMessage = "PRESS ENTER TO CONTINUE";

    private bool isTransitioning = false;

    void Start()
    {
        // Setup font and visual text
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF");
        if (font == null)
        {
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        if (logoText != null)
        {
            logoText.text = "STITCHES";
            if (font != null) logoText.font = font;
        }

        if (promptText != null)
        {
            promptText.text = promptMessage;
            if (font != null) promptText.font = font;
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(StartGame);
        }
    }

    void Update()
    {
        if (isTransitioning) return;

        // Enter or Return or Space to start the game
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
        {
            StartGame();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    public void StartGame()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        if (Application.CanStreamedLevelBeLoaded(firstLevelSceneName))
        {
            SceneManager.LoadScene(firstLevelSceneName);
        }
        else
        {
            // Fallback to scene index 1
            SceneManager.LoadScene(1);
        }
    }
}
