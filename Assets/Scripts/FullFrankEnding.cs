using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Attached directly to the Full_Frank GameObject in Level 3.
/// Starts hidden, and is activated when the player collects the Torso.
/// Displays Frank's final speech, fades the screen to black, and enables exit via Escape.
/// </summary>
public class FullFrankEnding : MonoBehaviour
{
    [Header("Speaker & Dialogue Settings")]
    [Tooltip("Speaker name displayed in dialogue box (Frank, not Frank's Head)")]
    public string speakerName = "Frank";

    [TextArea(3, 6)]
    public string finalSpeech = "Thank you for putting me back together, Doctor... But after seeing what you do here in this laboratory, I cannot stay with you in this castle. Farewell.";

    public float speechDuration = 7.0f;

    [Header("Fade & Exit UI")]
    public Canvas canvas;
    public UnityEngine.UI.Image fadeImage;
    public TextMeshProUGUI exitPromptText;
    public TextMeshProUGUI titleText;

    private bool sequenceStarted = false;
    private bool isFadedToBlack = false;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        // Keep Full_Frank hidden until Torso is picked up!
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }
    }

    void Start()
    {
        // Double check sprite is hidden at start
        if (!sequenceStarted && spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }
        BuildFadeUI();
    }

    void Update()
    {
        if (isFadedToBlack)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
    }

    /// <summary>
    /// Triggered by TorsoCollectible when the player collects Frank's torso.
    /// </summary>
    public void TriggerEndingSequence()
    {
        if (sequenceStarted) return;
        sequenceStarted = true;

        gameObject.SetActive(true);
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        StartCoroutine(EndingSequenceRoutine());
    }

    private IEnumerator EndingSequenceRoutine()
    {
        // 1. Freeze player movement during the ending dialogue
        Player player = Object.FindAnyObjectByType<Player>();
        Rigidbody2D playerRb = player != null ? player.GetComponent<Rigidbody2D>() : null;
        if (player != null)
        {
            player.enabled = false;
        }
        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector2.zero;
        }

        yield return new WaitForSeconds(0.6f);

        // 2. Play Frank's final dialogue using FrankVision dialogue UI with speakerName = "Frank"
        if (FrankVision.Instance != null)
        {
            FrankVision.Instance.Speak(finalSpeech, FrankDialoguePriority.Story, speechDuration, customSpeakerName: speakerName);
        }

        // Wait for dialogue typewriter + display time
        float timer = 0f;
        while (timer < speechDuration)
        {
            timer += Time.deltaTime;
            if (timer > 1.0f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)))
            {
                break;
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.8f);

        // 3. Screen fade to black
        yield return StartCoroutine(FadeToBlackRoutine(2.0f));

        // 4. Mark faded to black and display final exit instructions
        isFadedToBlack = true;
        if (titleText != null)
        {
            titleText.gameObject.SetActive(true);
        }
        if (exitPromptText != null)
        {
            exitPromptText.gameObject.SetActive(true);
        }
    }

    private IEnumerator FadeToBlackRoutine(float duration)
    {
        if (fadeImage == null) BuildFadeUI();
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            float elapsed = 0f;
            Color c = fadeImage.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / duration);
                fadeImage.color = c;
                yield return null;
            }
            c.a = 1f;
            fadeImage.color = c;
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }
    }

    private void BuildFadeUI()
    {
        if (fadeImage != null) return;

        Canvas c = Object.FindAnyObjectByType<Canvas>();
        if (c == null)
        {
            GameObject canvasGo = new GameObject("EndingCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            c = canvasGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 999;
        }
        canvas = c;

        // Fullscreen black image
        GameObject fadeGo = new GameObject("ScreenFadeOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        fadeGo.transform.SetParent(c.transform, false);

        RectTransform fadeRt = fadeGo.GetComponent<RectTransform>();
        fadeRt.anchorMin = Vector2.zero;
        fadeRt.anchorMax = Vector2.one;
        fadeRt.offsetMin = Vector2.zero;
        fadeRt.offsetMax = Vector2.zero;

        fadeImage = fadeGo.GetComponent<UnityEngine.UI.Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = true;
        fadeGo.SetActive(false);

        // Title text "STITCHES"
        GameObject titleGo = new GameObject("EndingTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(fadeGo.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.1f, 0.55f);
        titleRt.anchorMax = new Vector2(0.9f, 0.75f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        titleText = titleGo.GetComponent<TextMeshProUGUI>();
        titleText.text = "STITCHES";
        titleText.fontSize = 64;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.35f, 0.95f, 0.72f);
        titleText.alignment = TextAlignmentOptions.Center;

        // Exit prompt text
        GameObject exitGo = new GameObject("ExitPromptText", typeof(RectTransform), typeof(TextMeshProUGUI));
        exitGo.transform.SetParent(fadeGo.transform, false);
        RectTransform exitRt = exitGo.GetComponent<RectTransform>();
        exitRt.anchorMin = new Vector2(0.1f, 0.35f);
        exitRt.anchorMax = new Vector2(0.9f, 0.45f);
        exitRt.offsetMin = Vector2.zero;
        exitRt.offsetMax = Vector2.zero;

        exitPromptText = exitGo.GetComponent<TextMeshProUGUI>();
        exitPromptText.text = "PRESS [ ESCAPE ] TO EXIT";
        exitPromptText.fontSize = 36;
        exitPromptText.fontStyle = FontStyles.Normal;
        exitPromptText.color = Color.white;
        exitPromptText.alignment = TextAlignmentOptions.Center;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF");
        if (font != null)
        {
            titleText.font = font;
            exitPromptText.font = font;
        }

        titleGo.SetActive(false);
        exitGo.SetActive(false);
    }
}
