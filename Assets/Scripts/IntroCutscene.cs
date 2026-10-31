using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;

public class IntroCutscene : MonoBehaviour
{
    public static IntroCutscene Instance { get; private set; }

    [Header("Cutscene Timing & Pacing (Theatrical Pacing)")]
    public bool playOnStart = true;
    [Tooltip("Initial delay before lightning strikes (atmospheric lab buildup)")]
    public float delayBeforeStrike = 1.8f;
    [Tooltip("Delay after lightning strike before screaming")]
    public float delayAfterStrike = 0.25f;
    [Tooltip("How long the scream lasts")]
    public float screamDuration = 1.3f;
    [Tooltip("Pause after smoke vanishes before the head speaks")]
    public float delayAfterSmoke = 0.6f;
    [Tooltip("Typewriter speed (higher value = slower, clearer speech)")]
    public float typingSpeed = 0.045f;
    [Tooltip("Delay before continue prompt appears")]
    public float delayBeforeContinuePrompt = 0.6f;

    [Header("Doctor (Player Character)")]
    [Tooltip("The main character Doctor (Player) standing beside the table")]
    public GameObject doctorPlayer;
    [Tooltip("Offset of Doctor relative to the table (standing at a comfortable viewing distance)")]
    public Vector3 doctorOffset = new Vector3(-4.5f, 0f, 0f);

    [Header("Camera Zoom Settings")]
    public CinemachineCamera cinemachineCamera;
    public Camera fallbackCamera;
    [Tooltip("Zoom level during cutscene (closer view to see actors clearly)")]
    public float cutsceneZoom = 5.0f;
    [Tooltip("Normal zoom level during regular gameplay")]
    public float normalZoom = 10.0f;
    [Tooltip("How smoothly camera zooms out to normal view after cutscene")]
    public float zoomOutDuration = 2.0f;

    [Header("Lightning")]
    public LightningStrike lightningStrike;
    [Tooltip("Point where lightning originates from the sky")]
    public Transform lightningStartPoint;

    [Header("Frank Operating Table")]
    public SpriteRenderer deskRenderer;
    public Sprite deskDeadSprite;
    public Sprite deskAliveSprite;

    [Header("Death / Dissipation Smoke Animation")]
    public SpriteRenderer smokeRenderer;
    public Sprite[] smokeFrames;
    public float smokeFps = 4.5f;

    [Header("Frank Head")]
    public SpriteRenderer headRenderer;
    public Sprite headNormalSprite;
    public Sprite headTalkSprite;
    [Tooltip("Scale of Frank's head so it stays proportional and not gigantic")]
    public Vector3 headScale = new Vector3(0.85f, 0.85f, 1f);

    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI dialogueText;

    [Header("Continue Prompt Settings")]
    public GameObject continuePrompt;
    public TextMeshProUGUI continuePromptText;
    public string promptMessage = "[ Натисніть ПРОБІЛ або E щоб продовжити ]";
    public KeyCode continueKey = KeyCode.Space;
    public KeyCode alternateKey = KeyCode.E;
    public bool blinkPrompt = true;
    public float blinkSpeed = 2.5f;
    public float autoContinueSeconds = 0f;

    [Header("Dialogue Texts (Ukrainian)")]
    public string frankSpeakerName = "Франк";
    public string headSpeakerName = "Голова Франка";
    [TextArea(2, 4)]
    public string screamText = "А-А-А-А-А-А-А-А-А-А-А-А-А-А!!!";
    [TextArea(3, 6)]
    public string monologueText = "Лікарю! Я настільки злякався, що кожна частина мого тіла розлетілася й телепортувалася в різні частини замку! Ми мусимо знайти їх усі!";

    [Header("Gameplay Transition")]
    public GameObject gameplayUI;

    [Header("Audio (Optional)")]
    public AudioSource audioSource;
    public AudioClip screamSound;
    public AudioClip voiceBlipSound;

    [Header("Skip Cutscene Settings")]
    public Button skipButton;
    public KeyCode skipKey = KeyCode.Escape;
    public KeyCode alternateSkipKey = KeyCode.Tab;

    [HideInInspector]
    public bool hasStarted = false;
    [HideInInspector]
    public bool isCutsceneFinished = false;

    private bool skipTyping = false;
    private Coroutine cutsceneCoroutine;
    private Coroutine mouthCoroutine;
    private Coroutine blinkCoroutine;

    void Awake()
    {
        Instance = this;
        BuildAndWireAllRuntimeObjects();
    }

    void Start()
    {
        if (playOnStart && !hasStarted)
        {
            StartCutscene();
        }
    }

    void Update()
    {
        if (!isCutsceneFinished)
        {
            if (Input.GetKeyDown(skipKey) || Input.GetKeyDown(alternateSkipKey) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SkipCutscene();
            }
        }
    }

    /// <summary>
    /// Safe static bootstrap called from Player.Start() or scene loaders
    /// to guarantee the cutscene plays even with Domain Reload disabled.
    /// </summary>
    public static void EnsureCutsceneExists()
    {
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (activeScene.name != "GameScene" && !string.IsNullOrEmpty(activeScene.name))
        {
            return;
        }

        GameObject managerObj = GameObject.Find("CutsceneManager");
        IntroCutscene cutscene = null;

        if (managerObj == null)
        {
            managerObj = new GameObject("CutsceneManager");
            cutscene = managerObj.AddComponent<IntroCutscene>();
        }
        else
        {
            cutscene = managerObj.GetComponent<IntroCutscene>();
            if (cutscene == null) cutscene = managerObj.AddComponent<IntroCutscene>();
        }

        cutscene.BuildAndWireAllRuntimeObjects();
        if (!cutscene.hasStarted)
        {
            cutscene.StartCutscene();
        }
    }

    [ContextMenu("Auto Wire & Build Objects")]
    public void BuildAndWireAllRuntimeObjects()
    {
        Vector3 basePosition = new Vector3(73.81f, 2.40f, 0f);

        // Ensure solid ground floor under the lab
        GameObject floorObj = GameObject.Find("CutsceneFloor");
        if (floorObj == null)
        {
            floorObj = new GameObject("CutsceneFloor");
            floorObj.transform.position = new Vector3(73.0f, -0.5f, 0f);
            BoxCollider2D bc = floorObj.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(8f, 1f);
            floorObj.layer = 6; // Layer 6 is Ground
        }

        // 1. Doctor (Player)
        if (doctorPlayer == null) doctorPlayer = GameObject.Find("Player");

        // 2. Head
        if (headRenderer == null)
        {
            GameObject headObj = GameObject.Find("Head");
            if (headObj == null)
            {
                headObj = new GameObject("Head");
                headObj.transform.position = new Vector3(basePosition.x, 0.85f, 0f);
            }
            headRenderer = headObj.GetComponent<SpriteRenderer>() ?? headObj.AddComponent<SpriteRenderer>();
        }
        headRenderer.sortingLayerName = "Player";
        headRenderer.sortingOrder = 3;
        headRenderer.transform.localScale = headScale;
        headRenderer.transform.position = new Vector3(basePosition.x, 0.85f, 0f);

        // 3. Frank Desk
        if (deskRenderer == null)
        {
            GameObject deskObj = GameObject.Find("FrankDesk");
            if (deskObj == null)
            {
                deskObj = new GameObject("FrankDesk");
                deskObj.transform.position = basePosition;
            }
            deskRenderer = deskObj.GetComponent<SpriteRenderer>() ?? deskObj.AddComponent<SpriteRenderer>();
        }
        deskRenderer.sortingLayerName = "Player";
        deskRenderer.sortingOrder = 1;
        deskRenderer.transform.position = basePosition;
        deskRenderer.transform.localScale = Vector3.one;

        // 4. Death Smoke
        if (smokeRenderer == null)
        {
            GameObject smokeObj = GameObject.Find("DeathSmoke");
            if (smokeObj == null)
            {
                smokeObj = new GameObject("DeathSmoke");
                smokeObj.transform.position = basePosition + new Vector3(0f, 0.4f, 0f);
            }
            smokeRenderer = smokeObj.GetComponent<SpriteRenderer>() ?? smokeObj.AddComponent<SpriteRenderer>();
        }
        smokeRenderer.sortingLayerName = "Player";
        smokeRenderer.sortingOrder = 2;
        smokeRenderer.transform.position = basePosition + new Vector3(0f, 0.4f, 0f);
        smokeRenderer.gameObject.SetActive(false);

        // 5. Lightning & SkyPoint
        if (lightningStrike == null)
        {
            GameObject lightningObj = GameObject.Find("Lightning");
            if (lightningObj == null)
            {
                lightningObj = new GameObject("Lightning", typeof(AudioSource), typeof(LightningStrike));
            }
            AudioSource aSrc = lightningObj.GetComponent<AudioSource>();
            if (aSrc == null) aSrc = lightningObj.AddComponent<AudioSource>();
            if (aSrc != null)
            {
                aSrc.playOnAwake = false;
                aSrc.spatialBlend = 0f;
            }

            lightningStrike = lightningObj.GetComponent<LightningStrike>();
            if (lightningStrike == null)
            {
                lightningStrike = lightningObj.AddComponent<LightningStrike>();
            }
            if (lightningStrike != null)
            {
                lightningStrike.audioSource = aSrc;
            }
        }

        if (lightningStartPoint == null)
        {
            GameObject skyPoint = GameObject.Find("LightningSkyPoint");
            if (skyPoint == null)
            {
                skyPoint = new GameObject("LightningSkyPoint");
            }
            skyPoint.transform.position = basePosition + new Vector3(-1.5f, 13f, 0f);
            lightningStartPoint = skyPoint.transform;
        }

        // 6. Cameras
        if (cinemachineCamera == null) cinemachineCamera = Object.FindAnyObjectByType<CinemachineCamera>();
        if (fallbackCamera == null) fallbackCamera = Camera.main;

        // 7. Canvas & Dialogue UI
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            // Screen Flash
            Transform flashT = canvas.transform.Find("ScreenFlash");
            GameObject flashObj = null;
            if (flashT != null)
            {
                if (!(flashT is RectTransform))
                {
                    Object.DestroyImmediate(flashT.gameObject);
                }
                else
                {
                    flashObj = flashT.gameObject;
                }
            }

            if (flashObj == null)
            {
                flashObj = new GameObject("ScreenFlash", typeof(RectTransform));
                flashObj.transform.SetParent(canvas.transform, false);
            }

            RectTransform rt = flashObj.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            Image img = flashObj.GetComponent<Image>();
            if (img == null) img = flashObj.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = false;
            flashObj.transform.SetAsLastSibling();
            flashObj.SetActive(false);

            if (lightningStrike != null)
            {
                lightningStrike.flashScreenImage = img;
            }

            // Dialogue Panel
            Transform diagT = canvas.transform.Find("DialoguePanel");
            GameObject diagObj = null;
            if (diagT != null)
            {
                if (!(diagT is RectTransform))
                {
                    Object.DestroyImmediate(diagT.gameObject);
                }
                else
                {
                    diagObj = diagT.gameObject;
                }
            }

            if (diagObj == null)
            {
                diagObj = new GameObject("DialoguePanel", typeof(RectTransform));
                diagObj.transform.SetParent(canvas.transform, false);
            }

            dialoguePanel = diagObj;
            RectTransform diagRt = diagObj.transform as RectTransform;
            if (diagRt != null)
            {
                diagRt.anchorMin = new Vector2(0.08f, 0.04f);
                diagRt.anchorMax = new Vector2(0.92f, 0.38f);
                diagRt.offsetMin = Vector2.zero;
                diagRt.offsetMax = Vector2.zero;
            }

            Image diagBg = diagObj.GetComponent<Image>();
            if (diagBg == null) diagBg = diagObj.AddComponent<Image>();
            diagBg.color = new Color(0.06f, 0.07f, 0.12f, 0.94f);
            diagBg.raycastTarget = false;

            if (speakerNameText == null) speakerNameText = dialoguePanel.transform.Find("SpeakerName")?.GetComponent<TextMeshProUGUI>();
            if (speakerNameText == null)
            {
                speakerNameText = CreateTMP(dialoguePanel.transform, "SpeakerName", frankSpeakerName, 40, FontStyles.Bold, new Color(0.35f, 0.95f, 0.72f));
                RectTransform sRt = speakerNameText.transform as RectTransform;
                if (sRt != null)
                {
                    sRt.anchorMin = new Vector2(0.04f, 0.76f);
                    sRt.anchorMax = new Vector2(0.96f, 0.96f);
                    sRt.offsetMin = Vector2.zero;
                    sRt.offsetMax = Vector2.zero;
                }
                speakerNameText.alignment = TextAlignmentOptions.TopLeft;
            }

            if (dialogueText == null) dialogueText = dialoguePanel.transform.Find("DialogueText")?.GetComponent<TextMeshProUGUI>();
            if (dialogueText == null)
            {
                dialogueText = CreateTMP(dialoguePanel.transform, "DialogueText", "...", 50, FontStyles.Normal, Color.white);
                RectTransform dRt = dialogueText.transform as RectTransform;
                if (dRt != null)
                {
                    dRt.anchorMin = new Vector2(0.04f, 0.22f);
                    dRt.anchorMax = new Vector2(0.96f, 0.76f);
                    dRt.offsetMin = Vector2.zero;
                    dRt.offsetMax = Vector2.zero;
                }
                dialogueText.alignment = TextAlignmentOptions.TopLeft;
                dialogueText.textWrappingMode = TextWrappingModes.Normal;
                dialogueText.enableAutoSizing = true;
                dialogueText.fontSizeMin = 28;
                dialogueText.fontSizeMax = 54;
            }

            if (continuePrompt == null)
            {
                Transform cpT = dialoguePanel.transform.Find("ContinuePrompt");
                if (cpT != null)
                {
                    continuePrompt = cpT.gameObject;
                    continuePromptText = cpT.GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    continuePromptText = CreateTMP(dialoguePanel.transform, "ContinuePrompt", promptMessage, 30, FontStyles.Italic, new Color(1f, 0.88f, 0.40f));
                    RectTransform pRt = continuePromptText.transform as RectTransform;
                    if (pRt != null)
                    {
                        pRt.anchorMin = new Vector2(0.04f, 0.02f);
                        pRt.anchorMax = new Vector2(0.96f, 0.20f);
                        pRt.offsetMin = Vector2.zero;
                        pRt.offsetMax = Vector2.zero;
                    }
                    continuePromptText.alignment = TextAlignmentOptions.Center;
                    continuePrompt = continuePromptText.gameObject;
                }
            }

            // Skip Cutscene Button
            if (skipButton == null)
            {
                Transform skipT = canvas.transform.Find("SkipButton");
                if (skipT != null)
                {
                    skipButton = skipT.GetComponent<Button>();
                }
            }

            if (skipButton == null)
            {
                GameObject skipObj = new GameObject("SkipButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                skipObj.transform.SetParent(canvas.transform, false);

                RectTransform srt = skipObj.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(1f, 1f);
                srt.anchorMax = new Vector2(1f, 1f);
                srt.pivot = new Vector2(1f, 1f);
                srt.anchoredPosition = new Vector2(-25f, -25f);
                srt.sizeDelta = new Vector2(230f, 48f);

                Image sImg = skipObj.GetComponent<Image>();
                sImg.color = new Color(0.10f, 0.12f, 0.20f, 0.88f);

                Button btn = skipObj.GetComponent<Button>();
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(0.35f, 0.85f, 1f, 1f);
                cb.pressedColor = new Color(0.2f, 0.6f, 0.9f, 1f);
                btn.colors = cb;

                TextMeshProUGUI skipTxt = CreateTMP(skipObj.transform, "SkipText", "Пропустити ▸▸ [Esc]", 22, FontStyles.Bold, new Color(0.9f, 0.95f, 1f));
                RectTransform txtRt = skipTxt.transform as RectTransform;
                if (txtRt != null)
                {
                    txtRt.anchorMin = Vector2.zero;
                    txtRt.anchorMax = Vector2.one;
                    txtRt.offsetMin = Vector2.zero;
                    txtRt.offsetMax = Vector2.zero;
                }
                skipTxt.alignment = TextAlignmentOptions.Center;
                skipButton = btn;
            }

            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(SkipCutscene);
                skipButton.gameObject.SetActive(playOnStart && !isCutsceneFinished);
            }

            dialoguePanel.SetActive(false);
        }

        if (gameplayUI == null) gameplayUI = GameObject.Find("HealthBar") ?? GameObject.Find("Health");

#if UNITY_EDITOR
        // Load art & audio in Editor via AssetDatabase if missing
        if (deskDeadSprite == null) deskDeadSprite = LoadSpriteAsset("Assets/Art/Frank/Desk_Dead.png");
        if (deskAliveSprite == null) deskAliveSprite = LoadSpriteAsset("Assets/Art/Frank/Desk_Alive.png");
        if (headNormalSprite == null) headNormalSprite = LoadSpriteAsset("Assets/Art/Frank/Head.png");
        if (headTalkSprite == null) headTalkSprite = LoadSpriteAsset("Assets/Art/Frank/Head_talk.png");

        if (smokeFrames == null || smokeFrames.Length == 0)
        {
            var frames = new List<Sprite>();
            for (int i = 0; i <= 6; i++)
            {
                var s = LoadSpriteAsset($"Assets/Art/Death_Anim/sprite_{i}.png");
                if (s != null) frames.Add(s);
            }
            smokeFrames = frames.ToArray();
        }

        if (lightningStrike != null)
        {
            lightningStrike.LoadThunderAudio();
        }
#endif
    }

#if UNITY_EDITOR
    private static Sprite LoadSpriteAsset(string path)
    {
        Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var obj in assets)
        {
            if (obj is Sprite s) return s;
        }
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
#endif

    private static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float size, FontStyles style, Color color)
    {
        Transform existing = parent.Find(name);
        if (existing != null && !(existing is RectTransform))
        {
            Object.DestroyImmediate(existing.gameObject);
            existing = null;
        }

        GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    public void StartCutscene()
    {
        if (hasStarted) return;
        hasStarted = true;
        isCutsceneFinished = false;
        if (skipButton != null) skipButton.gameObject.SetActive(true);
        cutsceneCoroutine = StartCoroutine(CutsceneRoutine());
    }

    private IEnumerator CutsceneRoutine()
    {
        // 1. Initial State:
        if (skipButton != null) skipButton.gameObject.SetActive(true);
        // Set camera zoom in for the cutscene
        SetCameraZoom(cutsceneZoom);

        // Position Doctor beside the table and freeze controls
        if (doctorPlayer != null)
        {
            doctorPlayer.SetActive(true);
            if (deskRenderer != null)
            {
                doctorPlayer.transform.position = deskRenderer.transform.position + doctorOffset;
            }

            Player playerScript = doctorPlayer.GetComponent<Player>();
            if (playerScript != null) playerScript.enabled = false;

            Rigidbody2D rb = doctorPlayer.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        if (gameplayUI != null) gameplayUI.SetActive(false);
        if (smokeRenderer != null) smokeRenderer.gameObject.SetActive(false);
        if (headRenderer != null) headRenderer.gameObject.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        SetPromptVisible(false);

        // Frank lies dead on the table
        if (deskRenderer != null)
        {
            if (deskDeadSprite != null) deskRenderer.sprite = deskDeadSprite;
            deskRenderer.gameObject.SetActive(true);
        }

        // Wait before strike (let player take in the eerie lab atmosphere)
        yield return new WaitForSeconds(delayBeforeStrike);

        // 2. Lightning Strike
        Vector3 hitTarget = deskRenderer != null ? deskRenderer.transform.position + new Vector3(0, 0.4f, 0) : transform.position;
        Vector3 strikeOrigin = lightningStartPoint != null ? lightningStartPoint.position : hitTarget + new Vector3(-1.5f, 13f, 0);

        bool strikeFinished = false;
        if (lightningStrike != null)
        {
            lightningStrike.TriggerStrike(strikeOrigin, hitTarget, () => strikeFinished = true);
        }
        else
        {
            strikeFinished = true;
        }

        // Frank opens his eyes on the electrical flash!
        yield return new WaitForSeconds(0.08f);
        if (deskRenderer != null && deskAliveSprite != null)
        {
            deskRenderer.sprite = deskAliveSprite;
        }

        while (!strikeFinished)
        {
            yield return null;
        }

        yield return new WaitForSeconds(delayAfterStrike);

        // 3. Scream Dialogue
        SetPromptVisible(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        if (speakerNameText != null) speakerNameText.text = frankSpeakerName;
        if (audioSource != null && screamSound != null)
        {
            audioSource.PlayOneShot(screamSound);
        }

        if (dialogueText != null)
        {
            dialogueText.text = screamText;
        }
        yield return new WaitForSeconds(screamDuration);

        // 4. Body Dissolves / Teleports with Death Animation Smoke
        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        if (deskRenderer != null)
        {
            deskRenderer.gameObject.SetActive(false);
        }

        // Play all 7 death dissipation frames
        if (smokeRenderer != null && smokeFrames != null && smokeFrames.Length > 0)
        {
            smokeRenderer.transform.position = hitTarget;
            smokeRenderer.gameObject.SetActive(true);

            float frameTime = 1f / smokeFps;
            for (int i = 0; i < smokeFrames.Length; i++)
            {
                smokeRenderer.sprite = smokeFrames[i];
                yield return new WaitForSeconds(frameTime);
            }

            smokeRenderer.gameObject.SetActive(false);
        }

        // 5. Only Frank's Head Remains on the Ground
        if (headRenderer != null)
        {
            Vector3 headGroundPos = deskRenderer != null ? new Vector3(deskRenderer.transform.position.x, 0.85f, 0f) : new Vector3(hitTarget.x, 0.85f, 0f);
            headRenderer.transform.position = headGroundPos;
            headRenderer.transform.localScale = headScale;
            if (headNormalSprite != null) headRenderer.sprite = headNormalSprite;
            headRenderer.gameObject.SetActive(true);

            CollectibleItem item = headRenderer.GetComponent<CollectibleItem>();
            if (item != null)
            {
                item.enabled = false;
                item.SetInitialPosition(headGroundPos);
            }
        }

        yield return new WaitForSeconds(delayAfterSmoke);

        // 6. Head Monologue with Animated Mouth
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        if (speakerNameText != null) speakerNameText.text = headSpeakerName;

        yield return mouthCoroutine = StartCoroutine(TypeTextWithMouth(monologueText));

        // Delay to prevent accidental key skip from closing dialogue instantly
        yield return new WaitForSeconds(delayBeforeContinuePrompt);

        // Show Continue Prompt
        SetPromptVisible(true);

        // Wait for player confirmation (Space, E, or Click)
        float timer = 0f;
        bool proceed = false;

        while (!proceed)
        {
            if (Input.GetKeyDown(continueKey) || Input.GetKeyDown(alternateKey) || Input.GetMouseButtonDown(0))
            {
                proceed = true;
            }

            if (autoContinueSeconds > 0f)
            {
                timer += Time.deltaTime;
                if (timer >= autoContinueSeconds)
                {
                    proceed = true;
                }
            }

            yield return null;
        }

        // 7. Cutscene Completion & Gameplay Handoff
        SetPromptVisible(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        // Make Frank's head remain on the ground as an interactive Collectible!
        if (headRenderer != null)
        {
            headRenderer.gameObject.SetActive(true);
            headRenderer.transform.localScale = headScale;

            // Ensure collider
            CircleCollider2D headCol = headRenderer.GetComponent<CircleCollider2D>();
            if (headCol == null) headCol = headRenderer.gameObject.AddComponent<CircleCollider2D>();
            headCol.isTrigger = true;
            headCol.radius = 2.5f;

            // Ensure CollectibleItem component
            CollectibleItem collectible = headRenderer.GetComponent<CollectibleItem>();
            if (collectible == null) collectible = headRenderer.gameObject.AddComponent<CollectibleItem>();
            collectible.enabled = true;
            collectible.interactionDistance = 2.5f;
            collectible.itemType = CollectibleType.FrankHead;
            collectible.itemName = "Голова Франка";
            collectible.requireKeyPress = false;
            collectible.pickupKey = KeyCode.E;
            collectible.dialoguePanel = dialoguePanel;
            collectible.dialogueText = dialogueText;
            collectible.speakerNameText = speakerNameText;
            collectible.speakerName = headSpeakerName;
            collectible.pickupSpeech = "Обережно, Лікарю! Не впустіть мене! А тепер ходімо досліджувати замок і знайдемо всі мої частини тіла!";
            collectible.enableBobbing = true;
            collectible.SetInitialPosition(headRenderer.transform.position);

            GameObject doorTextObj = GameObject.Find("DoorText");
            if (doorTextObj != null)
            {
                collectible.promptUI = doorTextObj;
                collectible.promptText = doorTextObj.GetComponent<TextMeshProUGUI>();
            }
        }

        // Re-enable Doctor (Player) controls
        if (doctorPlayer != null)
        {
            Player playerScript = doctorPlayer.GetComponent<Player>();
            if (playerScript != null) playerScript.enabled = true;
        }

        if (gameplayUI != null)
        {
            gameplayUI.SetActive(true);
        }

        isCutsceneFinished = true;
        if (skipButton != null) skipButton.gameObject.SetActive(false);

        // Smoothly zoom camera back out to normal gameplay view
        StartCoroutine(ZoomCameraRoutine(cutsceneZoom, normalZoom, zoomOutDuration));
    }

    public void SkipCutscene()
    {
        if (isCutsceneFinished) return;
        hasStarted = true;
        isCutsceneFinished = true;

        if (cutsceneCoroutine != null)
        {
            StopCoroutine(cutsceneCoroutine);
            cutsceneCoroutine = null;
        }

        if (mouthCoroutine != null)
        {
            StopCoroutine(mouthCoroutine);
            mouthCoroutine = null;
        }

        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        // Cancel screen flash / lightning
        if (lightningStrike != null)
        {
            lightningStrike.CancelStrike();
        }

        // Stop cutscene audio
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        // Hide operating table and smoke
        if (deskRenderer != null)
        {
            deskRenderer.gameObject.SetActive(false);
        }
        if (smokeRenderer != null)
        {
            smokeRenderer.gameObject.SetActive(false);
        }

        // Hide dialogue UI and skip button
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        SetPromptVisible(false);
        if (skipButton != null) skipButton.gameObject.SetActive(false);

        // Position Frank's head on the ground as an interactive Collectible
        Vector3 hitTarget = deskRenderer != null ? deskRenderer.transform.position + new Vector3(0, 0.4f, 0) : transform.position;
        Vector3 headGroundPos = deskRenderer != null ? new Vector3(deskRenderer.transform.position.x, 0.85f, 0f) : new Vector3(hitTarget.x, 0.85f, 0f);

        if (headRenderer != null)
        {
            headRenderer.transform.position = headGroundPos;
            headRenderer.transform.localScale = headScale;
            if (headNormalSprite != null) headRenderer.sprite = headNormalSprite;
            headRenderer.gameObject.SetActive(true);

            CircleCollider2D headCol = headRenderer.GetComponent<CircleCollider2D>();
            if (headCol == null) headCol = headRenderer.gameObject.AddComponent<CircleCollider2D>();
            headCol.isTrigger = true;
            headCol.radius = 2.5f;

            CollectibleItem collectible = headRenderer.GetComponent<CollectibleItem>();
            if (collectible == null) collectible = headRenderer.gameObject.AddComponent<CollectibleItem>();
            collectible.enabled = true;
            collectible.interactionDistance = 2.5f;
            collectible.itemType = CollectibleType.FrankHead;
            collectible.itemName = "Голова Франка";
            collectible.requireKeyPress = false;
            collectible.pickupKey = KeyCode.E;
            collectible.dialoguePanel = dialoguePanel;
            collectible.dialogueText = dialogueText;
            collectible.speakerNameText = speakerNameText;
            collectible.speakerName = headSpeakerName;
            collectible.pickupSpeech = "Обережно, Лікарю! Не впустіть мене! А тепер ходімо досліджувати замок і знайдемо всі мої частини тіла!";
            collectible.enableBobbing = true;
            collectible.SetInitialPosition(headGroundPos);

            GameObject doorTextObj = GameObject.Find("DoorText");
            if (doorTextObj != null)
            {
                collectible.promptUI = doorTextObj;
                collectible.promptText = doorTextObj.GetComponent<TextMeshProUGUI>();
            }
        }

        // Restore Doctor (Player) controls and position
        if (doctorPlayer != null)
        {
            doctorPlayer.SetActive(true);
            if (deskRenderer != null)
            {
                doctorPlayer.transform.position = deskRenderer.transform.position + doctorOffset;
            }
            Player playerScript = doctorPlayer.GetComponent<Player>();
            if (playerScript != null) playerScript.enabled = true;
            Rigidbody2D rb = doctorPlayer.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        // Enable gameplay UI (HealthBar)
        if (gameplayUI != null)
        {
            gameplayUI.SetActive(true);
        }

        // Smoothly zoom camera back out
        StartCoroutine(ZoomCameraRoutine(cutsceneZoom, normalZoom, 0.4f));
    }

    private void SetCameraZoom(float zoom)
    {
        CinemachineCamera[] cms = Object.FindObjectsByType<CinemachineCamera>();
        if (cms != null && cms.Length > 0)
        {
            foreach (var cm in cms)
            {
                cm.Lens.OrthographicSize = zoom;
            }
        }
        else if (cinemachineCamera != null)
        {
            cinemachineCamera.Lens.OrthographicSize = zoom;
        }
        else if (fallbackCamera != null)
        {
            fallbackCamera.orthographicSize = zoom;
        }
    }

    private IEnumerator ZoomCameraRoutine(float startZoom, float targetZoom, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float current = Mathf.Lerp(startZoom, targetZoom, t);
            SetCameraZoom(current);
            yield return null;
        }
        SetCameraZoom(targetZoom);
    }

    private void SetPromptVisible(bool visible)
    {
        GameObject promptObj = continuePrompt;
        if (promptObj == null && continuePromptText != null)
        {
            promptObj = continuePromptText.gameObject;
        }

        if (promptObj != null)
        {
            if (continuePromptText != null && !string.IsNullOrEmpty(promptMessage))
            {
                continuePromptText.text = promptMessage;
            }

            promptObj.SetActive(visible);

            if (visible && blinkPrompt)
            {
                if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
                blinkCoroutine = StartCoroutine(BlinkPromptRoutine(promptObj));
            }
            else
            {
                if (blinkCoroutine != null)
                {
                    StopCoroutine(blinkCoroutine);
                    blinkCoroutine = null;
                }
            }
        }
    }

    private IEnumerator BlinkPromptRoutine(GameObject targetObj)
    {
        CanvasGroup cg = targetObj.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = targetObj.AddComponent<CanvasGroup>();
        }

        while (true)
        {
            float alpha = Mathf.Lerp(0.25f, 1f, (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f);
            cg.alpha = alpha;
            yield return null;
        }
    }

    private IEnumerator TypeTextWithMouth(string fullText)
    {
        skipTyping = false;
        dialogueText.text = "";

        int charCount = 0;

        for (int i = 0; i < fullText.Length; i++)
        {
            if (Input.GetKeyDown(continueKey) || Input.GetKeyDown(alternateKey) || Input.GetMouseButtonDown(0))
            {
                skipTyping = true;
            }

            if (skipTyping)
            {
                dialogueText.text = fullText;
                break;
            }

            dialogueText.text += fullText[i];
            charCount++;

            // Alternate mouth sprite every few characters for natural speech animation
            if (headRenderer != null && headTalkSprite != null && headNormalSprite != null)
            {
                if (!char.IsWhiteSpace(fullText[i]))
                {
                    headRenderer.sprite = (charCount % 4 < 2) ? headTalkSprite : headNormalSprite;
                    if (audioSource != null && voiceBlipSound != null && charCount % 3 == 0)
                    {
                        audioSource.PlayOneShot(voiceBlipSound, 0.35f);
                    }
                }
            }

            yield return new WaitForSeconds(typingSpeed);
        }

        // Return mouth to closed idle sprite
        if (headRenderer != null && headNormalSprite != null)
        {
            headRenderer.sprite = headNormalSprite;
        }
    }
}
