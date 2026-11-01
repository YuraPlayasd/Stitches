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

    [Header("Speech Audio (Yaping)")]
    public AudioClip yapingAudioClip;
    private AudioSource yapingAudioSource;

    [Header("Spike Warning Settings")]
    [Tooltip("Distance from spikes/Damage tilemap to trigger Frank's warning (close proximity, e.g. 2.8)")]
    public float spikeWarningDistance = 2.8f;

    [Header("Level 2 Arrow Tutorial")]
    [Tooltip("Distance walked to the right from start in Level 2 before warning if player has 0 arrows")]
    public float level2WalkDistanceForWarning = 2.0f;
    [TextArea(2, 4)]
    public string noArrowsWarningText = "Doctor, wait! It looks dangerous ahead! We should check to the left for a way to defend ourselves!";
    [TextArea(2, 4)]
    public string firstArrowPickupText = "Aha! You can throw these arrows by pressing [F]! I'll count how many we have in the top right corner!";

    [Header("Level 3 Story Dialogues")]
    [TextArea(2, 4)]
    public string level3OfficeStartText = "This is the office... I sense my final body part nearby! Doctor, save your arrows — I feel a terrible danger lurking ahead!";
    [TextArea(2, 4)]
    public string level3LabTubesReactionText = "Doctor... what are these horrors?! Look at those test tubes! Humanoids floating inside... gruesome, unnatural experiments! How could you do something so monstrous?!";

    private static bool hasWarnedNoArrowsInLevel2 = false;
    private static bool hasExplainedArrows = false;
    private static bool hasSpokenLevel3TubesReaction = false;
    private float level2PlayerStartX = float.NaN;
    private bool isLevel2Scene = false;
    private bool isLevel3Scene = false;
    private bool hasSpawnedLevel2Arrows = false;

    private static bool hasWarnedAboutSpikes = false;
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

        string activeScene = SceneManager.GetActiveScene().name;
        InitSceneContext(activeScene);
    }

    private void Update()
    {
        if (BodyPartsTracker.HasHead && isLevel2Scene && !hasWarnedNoArrowsInLevel2)
        {
            CheckLevel2NoArrowsWarning();
        }
    }

    void Start()
    {
        FindOrBuildDialogueUI();
        scanCoroutine = StartCoroutine(VisionScanRoutine());
        SceneManager.sceneLoaded += OnSceneLoaded;

        string activeScene = SceneManager.GetActiveScene().name;
        // Check if starting directly in a new scene with Frank's head already collected
        CheckLocationAnnouncement(activeScene);
        InitSceneContext(activeScene);
    }

    void OnDisable()
    {
        StopYapingAudio();
    }

    void OnDestroy()
    {
        StopYapingAudio();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FindOrBuildDialogueUI();
        CheckLocationAnnouncement(scene.name);
        InitSceneContext(scene.name);
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

            // Check Level 2 arrow tutorial warning
            CheckLevel2NoArrowsWarning();

            // Check Level 3 tubes reaction near left patrol point
            CheckLevel3LeftPatrolPointReaction();
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

                // Use persistent InstanceID so bobbing does not change the ID every frame
                string id = "BodyPart_" + item.itemType + "_" + item.gameObject.name;
                if (seenObjects.Contains(id)) continue;

                string typeKey = "SeenType_" + item.itemType;
                if (seenObjects.Contains(typeKey)) continue;

                // React to body parts once per scene
                string line = GetBodyPartPhrase(item.itemType, item.itemName);
                if (!string.IsNullOrEmpty(line))
                {
                    seenObjects.Add(id);
                    seenObjects.Add(typeKey);
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
        if (hasWarnedAboutSpikes) return;

        // User requested: Remove spike warning from Level 2 and Level 3! Only active in Level 1.
        string activeSceneName = SceneManager.GetActiveScene().name.ToLowerInvariant();
        if (activeSceneName.Contains("level 2") || activeSceneName.Contains("level2") ||
            activeSceneName.Contains("level 3") || activeSceneName.Contains("level3") ||
            activeSceneName.Contains("hub"))
        {
            return;
        }

        // 1. Check direct nearby colliders tagged or named Damage / Spike
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, spikeWarningDistance);
        foreach (var col in hits)
        {
            if (col == null) continue;

            // Exclude player, enemies, arrows
            if (col.CompareTag("Player") || col.GetComponent<Enemy>() != null || (col.transform.parent != null && col.transform.parent.GetComponent<Enemy>() != null) || col.GetComponent<Arrow>() != null)
            {
                continue;
            }

            bool isSpike = col.CompareTag("Damage") ||
                           (col.transform.parent != null && col.transform.parent.CompareTag("Damage")) ||
                           col.gameObject.name.IndexOf("Damage", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                           col.gameObject.name.IndexOf("Spike", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isSpike)
            {
                TriggerSpikeWarning();
                return;
            }
        }

        // 2. Direct Tilemap check for Damage tilemap tiles near the player
        GameObject damageObj = GameObject.Find("Damage");
        if (damageObj != null)
        {
            UnityEngine.Tilemaps.Tilemap tm = damageObj.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            if (tm != null)
            {
                Vector3Int centerCell = tm.WorldToCell(transform.position);
                int checkRadius = Mathf.CeilToInt(spikeWarningDistance);
                for (int x = -checkRadius; x <= checkRadius; x++)
                {
                    for (int y = -checkRadius; y <= checkRadius; y++)
                    {
                        Vector3Int cell = new Vector3Int(centerCell.x + x, centerCell.y + y, 0);
                        if (tm.HasTile(cell))
                        {
                            Vector3 tilePos = tm.GetCellCenterWorld(cell);
                            float dist = Vector2.Distance(transform.position, tilePos);
                            if (dist <= spikeWarningDistance)
                            {
                                TriggerSpikeWarning();
                                return;
                            }
                        }
                    }
                }
            }
        }
    }

    private void TriggerSpikeWarning()
    {
        hasWarnedAboutSpikes = true;
        seenObjects.Add("Spikes_First_Seen");
        Speak("Doctor, watch out! Sharp spikes ahead! Don't step on them, they will hurt us!", FrankDialoguePriority.Warning, 4.0f);
    }

    /// <summary>
    /// Checks and announces new locations when entered (LevelHub / Lab).
    /// Story explanations have highest priority!
    /// </summary>
    public void CheckLocationAnnouncement(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (announcedScenes.Contains(sceneName)) return;

        if (BodyPartsTracker.IsHubOrLevel2(sceneName))
        {
            BodyPartsTracker.SetHasHead(true);
        }

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
                return "My legs! Doctor, look, those are my legs! Finally, I can stand on my own two feet again! Let's pick them up!";
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
        if (lower.Contains("hub") || lower.Contains("castle") || lower.Contains("level2") || lower.Contains("level 2"))
        {
            return "This is the castle hub! It is cold and dangerous here, but I can clearly sense one of my parts nearby!";
        }
        else if (lower.Contains("level 3") || lower.Contains("level3") || lower.Contains("lab"))
        {
            return level3OfficeStartText;
        }
        return null;
    }

    /// <summary>
    /// Displays a speech line in the dialogue panel with priority control and typewriter effect.
    /// Low-priority warnings will wait in queue if higher-priority story or body part lines are active.
    /// </summary>
    public void Speak(string message, FrankDialoguePriority priority = FrankDialoguePriority.Story, float duration = -1f, string customSpeakerName = null)
    {
        if (string.IsNullOrEmpty(message)) return;

        string activeScene = SceneManager.GetActiveScene().name;
        if (BodyPartsTracker.IsHubOrLevel2(activeScene))
        {
            BodyPartsTracker.SetHasHead(true);
        }

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
            StopYapingAudio();
        }

        currentPriority = priority;
        speechCoroutine = StartCoroutine(ShowSpeechRoutine(message, duration > 0 ? duration : defaultSpeechDuration, customSpeakerName));
    }

    private void EnsureYapingAudio()
    {
        if (yapingAudioSource == null)
        {
            Transform existing = transform.Find("FrankSpeechAudio");
            GameObject speechObj = existing != null ? existing.gameObject : new GameObject("FrankSpeechAudio");
            speechObj.transform.SetParent(transform);
            speechObj.transform.localPosition = Vector3.zero;

            yapingAudioSource = speechObj.GetComponent<AudioSource>();
            if (yapingAudioSource == null)
            {
                yapingAudioSource = speechObj.AddComponent<AudioSource>();
            }
            yapingAudioSource.playOnAwake = false;
            yapingAudioSource.spatialBlend = 0f; // 2D UI sound
            yapingAudioSource.loop = true;
        }

        if (yapingAudioClip == null)
        {
            yapingAudioClip = Resources.Load<AudioClip>("Audio/yaping");
            if (yapingAudioClip == null)
            {
                yapingAudioClip = Resources.Load<AudioClip>("yaping");
            }
        }

        if (yapingAudioSource != null && yapingAudioClip != null)
        {
            yapingAudioSource.clip = yapingAudioClip;
        }
    }

    private void PlayYapingAudio()
    {
        EnsureYapingAudio();
        if (yapingAudioSource != null && yapingAudioClip != null)
        {
            yapingAudioSource.clip = yapingAudioClip;
            yapingAudioSource.loop = true;
            if (!yapingAudioSource.isPlaying)
            {
                yapingAudioSource.Play();
            }
        }
    }

    private void StopYapingAudio()
    {
        if (yapingAudioSource != null && yapingAudioSource.isPlaying)
        {
            yapingAudioSource.Stop();
        }
    }

    private IEnumerator ShowSpeechRoutine(string message, float duration, string customSpeakerName = null)
    {
        isSpeaking = true;

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        if (speakerNameText != null)
        {
            speakerNameText.text = !string.IsNullOrEmpty(customSpeakerName) ? customSpeakerName : speakerName;
        }

        if (dialogueText != null)
        {
            dialogueText.text = "";
            PlayYapingAudio();

            bool skipTyping = false;
            for (int i = 0; i < message.Length; i++)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
                {
                    skipTyping = true;
                }

                if (skipTyping)
                {
                    dialogueText.text = message;
                    break;
                }

                dialogueText.text += message[i];
                yield return new WaitForSeconds(typingSpeed);
            }

            // Immediately stop yaping as soon as typewriter text generation finishes
            StopYapingAudio();
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

        StopYapingAudio();

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

    private void InitSceneContext(string sceneName)
    {
        string lower = sceneName.ToLowerInvariant();
        // Warning about danger ahead and needing to go left is ONLY for Level 2!
        isLevel2Scene = (lower.Contains("level 2") || lower.Contains("level2") || lower.Contains("hub")) &&
                        !lower.Contains("level 3") && !lower.Contains("level3") &&
                        !lower.Contains("level 1") && !lower.Contains("level1");
        isLevel3Scene = lower.Contains("level 3") || lower.Contains("level3");

        if (isLevel2Scene)
        {
            BodyPartsTracker.SetHasHead(true);
            hasWarnedNoArrowsInLevel2 = false;
            hasExplainedArrows = false;

            Player player = GetComponent<Player>();
            if (player == null) player = Object.FindAnyObjectByType<Player>();
            if (player != null)
            {
                level2PlayerStartX = player.transform.position.x;
            }
            else
            {
                level2PlayerStartX = transform.position.x;
            }

            SpawnLevel2TutorialArrows();
        }
        else if (isLevel3Scene)
        {
            BodyPartsTracker.SetHasHead(true);
            hasSpokenLevel3TubesReaction = false;
        }
    }

    private void CheckLevel3LeftPatrolPointReaction()
    {
        if (!isLevel3Scene || hasSpokenLevel3TubesReaction) return;
        if (!BodyPartsTracker.HasHead) return;

        Player player = GetComponent<Player>();
        if (player == null) player = Object.FindAnyObjectByType<Player>();
        if (player == null) return;

        // In Level 3, the left mob patrol point is Point1_Patrol around (111.0, 25.39)
        GameObject leftPointGo = GameObject.Find("Point1_Patrol");
        Vector3 checkPos = leftPointGo != null ? leftPointGo.transform.position : new Vector3(111.0f, 25.39f, 0f);

        float dist = Vector2.Distance(player.transform.position, checkPos);
        if (dist <= 6.5f || (player.transform.position.x >= 106.0f && player.transform.position.x <= 118.0f && player.transform.position.y > 18.0f))
        {
            hasSpokenLevel3TubesReaction = true;
            Speak(level3LabTubesReactionText, FrankDialoguePriority.Story, 6.0f);
        }
    }

    private void SpawnLevel2TutorialArrows()
    {
        if (hasSpawnedLevel2Arrows) return;

        // Check if arrows already exist in the scene (placed manually by user in Scene view)
        ArrowPickup[] existingPickups = Object.FindObjectsByType<ArrowPickup>();
        if (existingPickups != null && existingPickups.Length >= 3)
        {
            hasSpawnedLevel2Arrows = true;
            return;
        }

        GameObject existingTutorial = GameObject.Find("Level2_TutorialArrows");
        if (existingTutorial != null)
        {
            hasSpawnedLevel2Arrows = true;
            return;
        }

        hasSpawnedLevel2Arrows = true;
        GameObject parentObj = new GameObject("Level2_TutorialArrows");

        // Target coordinates specified by user: Vector3(25.7900009, 1.97000003, 0)
        Vector3 basePos = new Vector3(25.79f, 1.97f, 0f);
        Vector3[] spawnPositions = new Vector3[]
        {
            basePos + new Vector3(-0.8f, 0f, 0f),
            basePos,
            basePos + new Vector3(0.8f, 0f, 0f)
        };

        foreach (var pos in spawnPositions)
        {
            ArrowPickup.SpawnSingleArrowPickup(pos, parentObj.transform, isFalling: false);
        }
    }

    private void CheckLevel2NoArrowsWarning()
    {
        if (!isLevel2Scene || hasWarnedNoArrowsInLevel2) return;
        if (!BodyPartsTracker.HasHead) return;

        Player player = GetComponent<Player>();
        if (player == null) player = Object.FindAnyObjectByType<Player>();
        if (player == null) return;

        if (float.IsNaN(level2PlayerStartX))
        {
            level2PlayerStartX = player.transform.position.x;
            return;
        }

        if (player.arrowsCount == 0)
        {
            if (player.transform.position.x > level2PlayerStartX + level2WalkDistanceForWarning)
            {
                hasWarnedNoArrowsInLevel2 = true;
                Speak(noArrowsWarningText, FrankDialoguePriority.Story, 5.5f);
            }
        }
    }

    /// <summary>
    /// Called when player picks up any arrow pickup in any level.
    /// Frank's head explains how to shoot [F] and that ammo will be tracked in the top right corner.
    /// </summary>
    public void OnArrowCollected()
    {
        if (!hasExplainedArrows)
        {
            hasExplainedArrows = true;
            string sceneName = SceneManager.GetActiveScene().name;
            if (BodyPartsTracker.IsHubOrLevel2(sceneName))
            {
                BodyPartsTracker.SetHasHead(true);
            }
            Speak(firstArrowPickupText, FrankDialoguePriority.Story, 5.5f);
        }
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
