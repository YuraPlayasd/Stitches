using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public enum FrankDialoguePriority
{
    Warning = 1,   // Low: Spikes and minor hazards
    BodyPart = 2,  // Medium: Body parts
    Story = 3      // High: Location announcements, story explanations, head pickup
}

/// <summary>
/// Frank's Vision & Reaction System.
/// When Frank's head has been collected, Frank watches the world around the player.
/// When specific objects (body parts, spikes/damage, new locations) enter his field of view
/// for the first time, Frank's head speaks up with appropriate dialogue lines.
/// Warnings are low-priority and wait in queue if story dialogue is actively speaking.
/// </summary>
public class FrankVision : MonoBehaviour
{
    public static FrankVision Instance { get; private set; }

    [Header("Vision Settings")]
    [Tooltip("Radius around the player in which Frank spots objects (constrained to camera reach)")]
    public float visionRadius = 5.5f;

    [Tooltip("Check frequency in seconds (optimizes performance)")]
    public float scanInterval = 0.2f;

    [Tooltip("If enabled, only triggers when object is actually visible within the camera's screen bounds")]
    public bool requireInCameraView = true;

    [Header("Speech / Dialogue UI")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI dialogueText;
    public string speakerName = "Frank's Head";
    public float defaultSpeechDuration = 4.0f;
    public float typingSpeed = 0.035f;

    [Header("Spike Warning Settings")]
    [Tooltip("Distance from spikes/Damage tilemap to trigger Frank's warning (constrained to camera reach)")]
    public float spikeWarningDistance = 4.5f;

    private readonly HashSet<string> seenObjects = new HashSet<string>();
    private readonly HashSet<string> announcedScenes = new HashSet<string>();

    private Coroutine speechCoroutine;
    private Coroutine scanCoroutine;
    private bool isSpeaking = false;
    private FrankDialoguePriority currentPriority = FrankDialoguePriority.Warning;

    // Pending lower-priority queue (e.g. warnings delayed by story dialogue)
    private string pendingWarningMessage = null;
    private float pendingWarningDuration = -1f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        FindOrBuildDialogueUI();
        scanCoroutine = StartCoroutine(VisionScanRoutine());
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Check if starting directly in a new scene with Frank's head already collected
        CheckLocationAnnouncement(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FindOrBuildDialogueUI();
        CheckLocationAnnouncement(scene.name);
    }

    /// <summary>
    /// Ensures FrankVision is attached to the Player in the active scene.
    /// </summary>
    public static FrankVision EnsureExists(GameObject playerObj = null)
    {
        if (Instance != null) return Instance;

        if (playerObj == null)
        {
            Player p = Object.FindAnyObjectByType<Player>();
            if (p != null) playerObj = p.gameObject;
        }

        if (playerObj == null)
        {
            playerObj = GameObject.FindWithTag("Player");
        }

        if (playerObj != null)
        {
            FrankVision fv = playerObj.GetComponent<FrankVision>();
            if (fv == null)
            {
                fv = playerObj.AddComponent<FrankVision>();
            }
            Instance = fv;
            return fv;
        }

        return null;
    }

    /// <summary>
    /// Routine that scans nearby colliders and tilemaps for notable objects.
    /// Only triggers if Frank's head has been collected!
    /// </summary>
    private IEnumerator VisionScanRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(scanInterval);

        while (true)
        {
            yield return wait;

            // Frank cannot see or talk until the player has picked up his head!
            if (!BodyPartsTracker.HasHead)
            {
                continue;
            }

            // Scan for body parts
            ScanForBodyParts();

            // Scan for spikes and hazards
            ScanForDamageAndSpikes();
        }
    }

    /// <summary>
    /// Checks whether a given world position is currently within the camera's viewport reach.
    /// </summary>
    public bool IsInCameraView(Vector3 worldPos, float margin = 0.02f)
    {
        if (!requireInCameraView) return true;

        Camera cam = Camera.main;
        if (cam == null) return true;

        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        return vp.z > 0f &&
               vp.x >= margin && vp.x <= (1f - margin) &&
               vp.y >= margin && vp.y <= (1f - margin);
    }

    /// <summary>
    /// Scans for Frank's body parts (Arm, Torso, Leg) in field of view.
    /// </summary>
    private void ScanForBodyParts()
    {
        if (!BodyPartsTracker.HasHead) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, visionRadius);
        foreach (var col in hits)
        {
            if (col == null || col.gameObject == gameObject) continue;

            CollectibleItem item = col.GetComponent<CollectibleItem>();
            if (item == null)
            {
                item = col.GetComponentInParent<CollectibleItem>();
            }

            if (item != null && !item.isCollected)
            {
                // Must be within camera view reach
                if (!IsInCameraView(item.transform.position)) continue;

                string id = "BodyPart_" + item.itemType + "_" + item.name + "_" + item.transform.position;
                if (seenObjects.Contains(id)) continue;

                // React to body parts
                string line = GetBodyPartPhrase(item.itemType, item.itemName);
                if (!string.IsNullOrEmpty(line))
                {
                    seenObjects.Add(id);
                    Speak(line, FrankDialoguePriority.BodyPart);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Scans for Damage grid / spikes in proximity.
    /// Warnings are low-priority: if Frank is speaking story lines, warning queues for later.
    /// </summary>
    private void ScanForDamageAndSpikes()
    {
        if (!BodyPartsTracker.HasHead) return;

        if (seenObjects.Contains("Spikes_First_Seen"))
        {
            return;
        }

        // 1. Check colliders tagged "Damage" or named "Spikes"
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, spikeWarningDistance);
        foreach (var col in hits)
        {
            if (col == null) continue;

            bool isSpike = col.CompareTag("Damage") ||
                           col.gameObject.name.IndexOf("Damage", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                           col.gameObject.name.IndexOf("Spike", System.StringComparison.OrdinalIgnoreCase) >= 0;

            // Exclude enemies from spike warning (they have their own behaviour)
            if (col.GetComponent<Enemy>() != null || (col.transform.parent != null && col.transform.parent.GetComponent<Enemy>() != null))
            {
                continue;
            }

            if (isSpike)
            {
                Vector2 closest = col.ClosestPoint(transform.position);
                if (!IsInCameraView(closest)) continue;

                seenObjects.Add("Spikes_First_Seen");
                Speak("Doctor, watch out! Sharp spikes ahead! Don't step on them, they will hurt us!", FrankDialoguePriority.Warning, 4.0f);
                return;
            }
        }

        // 2. Direct search for Damage Tilemap object within distance
        GameObject damageObj = GameObject.Find("Damage");
        if (damageObj != null)
        {
            Collider2D dmgCol = damageObj.GetComponent<Collider2D>();
            if (dmgCol != null)
            {
                Vector2 closest = dmgCol.ClosestPoint(transform.position);
                float dist = Vector2.Distance(transform.position, closest);
                if (dist <= spikeWarningDistance && IsInCameraView(closest))
                {
                    seenObjects.Add("Spikes_First_Seen");
                    Speak("Doctor, watch out! Sharp spikes ahead! Don't step on them, they will hurt us!", FrankDialoguePriority.Warning, 4.0f);
                }
            }
        }
    }

    /// <summary>
    /// Checks and announces new locations when entered (LevelHub / Lab).
    /// Story explanations have highest priority!
    /// </summary>
    public void CheckLocationAnnouncement(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (announcedScenes.Contains(sceneName)) return;

        // If Frank's head is not yet collected in this scene, we wait until it is picked up
        if (!BodyPartsTracker.HasHead) return;

        string phrase = GetLocationPhrase(sceneName);
        if (!string.IsNullOrEmpty(phrase))
        {
            announcedScenes.Add(sceneName);
            StartCoroutine(DelayedLocationSpeak(phrase, 1.0f));
        }
    }

    private IEnumerator DelayedLocationSpeak(string phrase, float delay)
    {
        yield return new WaitForSeconds(delay);
        Speak(phrase, FrankDialoguePriority.Story, 5.0f);
    }

    private string GetBodyPartPhrase(CollectibleType type, string itemName)
    {
        switch (type)
        {
            case CollectibleType.FrankArm:
                return "Look, Doctor! That's my arm over there! Quick, let's pick it up!";
            case CollectibleType.FrankLeg:
                return "Aha! There's my leg lying right there! Let's go grab it!";
            case CollectibleType.FrankTorso:
                return "Doctor! My torso is right ahead! We have to get it back!";
            default:
                if (itemName != null && itemName.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return null; // Head itself doesn't need to be recognized by head before pickup
                }
                if (type == CollectibleType.Health || type == CollectibleType.Arrows)
                {
                    return null;
                }
                return "Doctor, look over there! That part belongs to me! Let's pick it up!";
        }
    }

    private string GetLocationPhrase(string sceneName)
    {
        string lower = sceneName.ToLowerInvariant();
        if (lower.Contains("hub") || lower.Contains("castle") || lower.Contains("level2"))
        {
            return "This is the castle hub! It is cold and dangerous here, but I can clearly sense one of my parts nearby!";
        }
        else if (lower.Contains("lab"))
        {
            return "The laboratory! My creator's sanctuary! I can feel the energy of my missing part in this room!";
        }
        return null;
    }

    /// <summary>
    /// Displays a speech line in the dialogue panel with priority control and typewriter effect.
    /// Low-priority warnings will wait in queue if higher-priority story or body part lines are active.
    /// </summary>
    public void Speak(string message, FrankDialoguePriority priority = FrankDialoguePriority.Story, float duration = -1f)
    {
        if (string.IsNullOrEmpty(message)) return;

        // Never speak before Frank's head is picked up!
        if (!BodyPartsTracker.HasHead) return;

        FindOrBuildDialogueUI();

        // If currently speaking a higher-priority message, queue this lower-priority message
        if (isSpeaking && priority < currentPriority)
        {
            if (priority == FrankDialoguePriority.Warning)
            {
                pendingWarningMessage = message;
                pendingWarningDuration = duration > 0 ? duration : defaultSpeechDuration;
            }
            return;
        }

        // Higher or equal priority interrupts current speech
        if (speechCoroutine != null)
        {
            StopCoroutine(speechCoroutine);
        }

        currentPriority = priority;
        speechCoroutine = StartCoroutine(ShowSpeechRoutine(message, duration > 0 ? duration : defaultSpeechDuration));
    }

    private IEnumerator ShowSpeechRoutine(string message, float duration)
    {
        isSpeaking = true;

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        if (speakerNameText != null)
        {
            speakerNameText.text = speakerName;
        }

        if (dialogueText != null)
        {
            dialogueText.text = "";
            for (int i = 0; i < message.Length; i++)
            {
                dialogueText.text += message[i];
                yield return new WaitForSeconds(typingSpeed);
            }
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            // Allow skip with Space or E after brief initial display
            if (timer > 0.4f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E)))
            {
                break;
            }
            yield return null;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        isSpeaking = false;
        speechCoroutine = null;

        // If a lower-priority warning was waiting behind a story dialogue, play it now!
        if (!string.IsNullOrEmpty(pendingWarningMessage))
        {
            string nextMessage = pendingWarningMessage;
            float nextDuration = pendingWarningDuration;
            pendingWarningMessage = null;

            yield return new WaitForSeconds(0.4f);
            Speak(nextMessage, FrankDialoguePriority.Warning, nextDuration);
        }
    }

    /// <summary>
    /// Finds existing DialoguePanel in scene or builds one inside Canvas.
    /// </summary>
    public void FindOrBuildDialogueUI()
    {
        if (dialoguePanel != null && dialogueText != null) return;

        // 1. Search existing DialoguePanel
        GameObject dp = GameObject.Find("DialoguePanel");
        if (dp == null)
        {
            IntroCutscene ic = Object.FindAnyObjectByType<IntroCutscene>();
            if (ic != null && ic.dialoguePanel != null)
            {
                dp = ic.dialoguePanel;
                if (speakerNameText == null) speakerNameText = ic.speakerNameText;
                if (dialogueText == null) dialogueText = ic.dialogueText;
            }
        }

        if (dp != null)
        {
            dialoguePanel = dp;
            if (speakerNameText == null)
            {
                speakerNameText = dp.transform.Find("SpeakerName")?.GetComponent<TextMeshProUGUI>();
            }
            if (dialogueText == null)
            {
                dialogueText = dp.transform.Find("DialogueText")?.GetComponent<TextMeshProUGUI>();
            }
            return;
        }

        // 2. Build DialoguePanel inside Canvas if missing (e.g. in LevelHub)
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject newPanel = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        newPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRt = newPanel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.08f, 0.04f);
        panelRt.anchorMax = new Vector2(0.92f, 0.38f);
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        panelRt.pivot = new Vector2(0.5f, 0.5f);

        UnityEngine.UI.Image panelImg = newPanel.GetComponent<UnityEngine.UI.Image>();
        panelImg.color = new Color(0.06f, 0.07f, 0.12f, 0.94f);
        panelImg.raycastTarget = false;

        // Speaker Name text
        GameObject nameGo = new GameObject("SpeakerName", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(newPanel.transform, false);
        RectTransform nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0.04f, 0.76f);
        nameRt.anchorMax = new Vector2(0.96f, 0.96f);
        nameRt.offsetMin = Vector2.zero;
        nameRt.offsetMax = Vector2.zero;

        TextMeshProUGUI nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
        nameTmp.text = speakerName;
        nameTmp.fontSize = 40;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.color = new Color(0.35f, 0.95f, 0.72f);
        nameTmp.alignment = TextAlignmentOptions.TopLeft;
        nameTmp.raycastTarget = false;

        // Dialogue text
        GameObject textGo = new GameObject("DialogueText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(newPanel.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.04f, 0.12f);
        textRt.anchorMax = new Vector2(0.96f, 0.74f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI textTmp = textGo.GetComponent<TextMeshProUGUI>();
        textTmp.text = "...";
        textTmp.fontSize = 42;
        textTmp.fontStyle = FontStyles.Normal;
        textTmp.color = Color.white;
        textTmp.alignment = TextAlignmentOptions.TopLeft;
        textTmp.raycastTarget = false;

        // Apply Electronic Highway Sign SDF font if available
        TMP_FontAsset electronicFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF");
        if (electronicFont != null)
        {
            nameTmp.font = electronicFont;
            textTmp.font = electronicFont;
        }

        dialoguePanel = newPanel;
        speakerNameText = nameTmp;
        dialogueText = textTmp;
        dialoguePanel.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        // Draw Frank's vision circle in editor (camera reach)
        Gizmos.color = new Color(0.35f, 0.95f, 0.72f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, visionRadius);

        // Draw spike warning circle
        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, spikeWarningDistance);
    }
}
