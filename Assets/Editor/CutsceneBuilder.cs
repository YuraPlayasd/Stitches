using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;

public static class CutsceneBuilder
{
    [MenuItem("Tools/Setup Intro Cutscene (Франк & Блискавка)", false, 1)]
    public static void ManualBuild()
    {
        BuildCutscene(false);
    }

    public static void BuildCutscene(bool forceRecreate)
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.isLoaded || !activeScene.path.Contains("GameScene"))
        {
            activeScene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        }

        Debug.Log("<color=cyan><b>[CutsceneBuilder]</b></color> Configuring Intro Cutscene for GameScene...");

        // 1. Find or create CutsceneManager
        GameObject managerObj = GameObject.Find("CutsceneManager");
        if (managerObj == null)
        {
            managerObj = new GameObject("CutsceneManager");
            Undo.RegisterCreatedObjectUndo(managerObj, "Create CutsceneManager");
        }

        IntroCutscene cutscene = managerObj.GetComponent<IntroCutscene>();
        if (cutscene == null)
        {
            cutscene = Undo.AddComponent<IntroCutscene>(managerObj);
        }

        // 2. Base position (Frank's operating table resting on ground at level with scientist)
        Vector3 basePosition = new Vector3(73.81f, 2.40f, 0f);

        // Ensure solid ground floor under the lab
        GameObject floorObj = GameObject.Find("CutsceneFloor");
        if (floorObj == null)
        {
            floorObj = new GameObject("CutsceneFloor");
            floorObj.transform.position = new Vector3(73.0f, -0.5f, 0f);
            BoxCollider2D bc = floorObj.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(8f, 1f);
            floorObj.layer = 6;
            Undo.RegisterCreatedObjectUndo(floorObj, "Create CutsceneFloor");
        }

        // 3. Frank's Operating Table (FrankDesk)
        GameObject deskObj = GameObject.Find("FrankDesk");
        if (deskObj == null)
        {
            deskObj = new GameObject("FrankDesk");
            Undo.RegisterCreatedObjectUndo(deskObj, "Create FrankDesk");
        }
        deskObj.transform.position = basePosition;
        deskObj.transform.localScale = Vector3.one;

        SpriteRenderer deskSr = deskObj.GetComponent<SpriteRenderer>();
        if (deskSr == null) deskSr = Undo.AddComponent<SpriteRenderer>(deskObj);
        deskSr.sortingLayerName = "Player";
        deskSr.sortingOrder = 1;

        Sprite deadSprite = LoadSprite("Assets/Art/Frank/Desk_Dead.png");
        Sprite aliveSprite = LoadSprite("Assets/Art/Frank/Desk_Alive.png");
        if (deadSprite != null) deskSr.sprite = deadSprite;

        cutscene.deskRenderer = deskSr;
        cutscene.deskDeadSprite = deadSprite;
        cutscene.deskAliveSprite = aliveSprite;

        // 4. Dissipation Smoke (DeathSmoke) with all 7 death animation frames
        GameObject smokeObj = GameObject.Find("DeathSmoke");
        if (smokeObj == null)
        {
            smokeObj = new GameObject("DeathSmoke");
            Undo.RegisterCreatedObjectUndo(smokeObj, "Create DeathSmoke");
        }
        smokeObj.transform.position = basePosition + new Vector3(0f, 0.4f, 0f);
        smokeObj.transform.localScale = Vector3.one;

        SpriteRenderer smokeSr = smokeObj.GetComponent<SpriteRenderer>();
        if (smokeSr == null) smokeSr = Undo.AddComponent<SpriteRenderer>(smokeObj);
        smokeSr.sortingLayerName = "Player";
        smokeSr.sortingOrder = 2;

        List<Sprite> smokeList = new List<Sprite>();
        for (int i = 0; i <= 6; i++)
        {
            Sprite s = LoadSprite($"Assets/Art/Death_Anim/sprite_{i}.png");
            if (s != null) smokeList.Add(s);
        }
        cutscene.smokeRenderer = smokeSr;
        cutscene.smokeFrames = smokeList.ToArray();
        cutscene.smokeFps = 6.5f;
        smokeObj.SetActive(false);

        // 5. Frank's Head (Head) + Collectible Setup
        GameObject headObj = GameObject.Find("Head");
        if (headObj == null)
        {
            headObj = new GameObject("Head");
            Undo.RegisterCreatedObjectUndo(headObj, "Create Head");
        }
        headObj.transform.position = new Vector3(basePosition.x, 0.85f, 0f);
        headObj.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
        cutscene.headScale = new Vector3(0.85f, 0.85f, 1f);

        SpriteRenderer headSr = headObj.GetComponent<SpriteRenderer>();
        if (headSr == null) headSr = Undo.AddComponent<SpriteRenderer>(headObj);
        headSr.sortingLayerName = "Player";
        headSr.sortingOrder = 3;

        Sprite headNormal = LoadSprite("Assets/Art/Frank/Head.png");
        Sprite headTalk = LoadSprite("Assets/Art/Frank/Head_talk.png");
        if (headNormal != null) headSr.sprite = headNormal;

        cutscene.headRenderer = headSr;
        cutscene.headNormalSprite = headNormal;
        cutscene.headTalkSprite = headTalk;
        headObj.SetActive(false);

        // Configure CollectibleItem on the Head
        CircleCollider2D headCol = headObj.GetComponent<CircleCollider2D>();
        if (headCol == null) headCol = Undo.AddComponent<CircleCollider2D>(headObj);
        headCol.isTrigger = true;
        headCol.radius = 2.5f;

        CollectibleItem headCollectible = headObj.GetComponent<CollectibleItem>();
        if (headCollectible == null) headCollectible = Undo.AddComponent<CollectibleItem>(headObj);
        headCollectible.interactionDistance = 2.5f;
        headCollectible.itemType = CollectibleType.FrankHead;
        headCollectible.itemName = "Голова Франка";
        headCollectible.requireKeyPress = false;
        headCollectible.pickupKey = KeyCode.E;
        headCollectible.speakerName = "Голова Франка";
        headCollectible.pickupSpeech = "Обережно, Лікарю! Не впустіть мене! А тепер ходімо досліджувати замок і знайдемо всі мої частини тіла!";
        headCollectible.enableBobbing = true;
        headCollectible.SetInitialPosition(new Vector3(basePosition.x, 0.85f, 0f));

        // 6. Lightning Strike
        GameObject lightningObj = GameObject.Find("Lightning");
        if (lightningObj == null)
        {
            lightningObj = new GameObject("Lightning", typeof(AudioSource), typeof(LightningStrike));
            Undo.RegisterCreatedObjectUndo(lightningObj, "Create Lightning");
        }
        if (lightningObj.GetComponent<AudioSource>() == null)
        {
            Undo.AddComponent<AudioSource>(lightningObj);
        }
        LightningStrike lightningStrike = lightningObj.GetComponent<LightningStrike>();
        if (lightningStrike == null) lightningStrike = Undo.AddComponent<LightningStrike>(lightningObj);

        AudioClip thunderClip = Resources.Load<AudioClip>("thunder") ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/soundreality-thunder-city-377703.mp3");
        AudioSource lightningAudio = lightningObj.GetComponent<AudioSource>();
        if (lightningAudio != null)
        {
            lightningAudio.playOnAwake = false;
            lightningAudio.spatialBlend = 0f; // 2D sound
            lightningAudio.volume = 1f;
        }
        lightningStrike.audioSource = lightningAudio;
        lightningStrike.thunderSound = thunderClip;

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.localScale = Vector3.one;
            lightningStrike.targetCamera = mainCam;
        }
        cutscene.lightningStrike = lightningStrike;

        GameObject skyPoint = GameObject.Find("LightningSkyPoint");
        if (skyPoint == null)
        {
            skyPoint = new GameObject("LightningSkyPoint");
            Undo.RegisterCreatedObjectUndo(skyPoint, "Create LightningSkyPoint");
        }
        skyPoint.transform.position = basePosition + new Vector3(-1.5f, 13f, 0f);
        cutscene.lightningStartPoint = skyPoint.transform;

        // 7. Doctor (Player character standing beside the table)
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null)
        {
            cutscene.doctorPlayer = playerObj;
            cutscene.doctorOffset = new Vector3(-4.5f, 0f, 0f);
            playerObj.transform.position = basePosition + new Vector3(-4.5f, 0f, 0f);
            playerObj.SetActive(true);
        }

        // 8. Cinemachine Camera Zoom
        CinemachineCamera[] cinemachines = Object.FindObjectsByType<CinemachineCamera>();
        if (cinemachines.Length > 0)
        {
            cutscene.cinemachineCamera = cinemachines[0];
            cutscene.cutsceneZoom = 5.0f;
            cutscene.normalZoom = 10.0f;
            cutscene.zoomOutDuration = 2.0f;

            // Align secondary cinemachine camera if any exists
            foreach (var cm in cinemachines)
            {
                cm.Lens.OrthographicSize = 10.0f;
            }
        }

        // 9. Canvas UI setup
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
        }

        // Screen Flash Image
        Transform flashTransform = canvas.transform.Find("ScreenFlash");
        GameObject flashObj = null;
        if (flashTransform != null)
        {
            if (!(flashTransform is RectTransform))
            {
                Object.DestroyImmediate(flashTransform.gameObject);
            }
            else
            {
                flashObj = flashTransform.gameObject;
            }
        }

        if (flashObj == null)
        {
            flashObj = new GameObject("ScreenFlash", typeof(RectTransform));
            flashObj.transform.SetParent(canvas.transform, false);
            Undo.RegisterCreatedObjectUndo(flashObj, "Create ScreenFlash");
        }

        RectTransform flashRt = flashObj.transform as RectTransform;
        if (flashRt != null)
        {
            flashRt.anchorMin = Vector2.zero;
            flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = Vector2.zero;
            flashRt.offsetMax = Vector2.zero;
        }

        Image flashImg = flashObj.GetComponent<Image>();
        if (flashImg == null) flashImg = Undo.AddComponent<Image>(flashObj);
        flashImg.color = new Color(1f, 1f, 1f, 0f);
        flashImg.raycastTarget = false;
        flashObj.transform.SetAsLastSibling();
        flashObj.SetActive(false);
        lightningStrike.flashScreenImage = flashImg;

        // Dialogue Panel
        Transform dialogueTransform = canvas.transform.Find("DialoguePanel");
        GameObject dialogueObj = null;
        if (dialogueTransform != null)
        {
            if (!(dialogueTransform is RectTransform))
            {
                Object.DestroyImmediate(dialogueTransform.gameObject);
            }
            else
            {
                dialogueObj = dialogueTransform.gameObject;
            }
        }

        if (dialogueObj == null)
        {
            dialogueObj = new GameObject("DialoguePanel", typeof(RectTransform));
            dialogueObj.transform.SetParent(canvas.transform, false);
            Undo.RegisterCreatedObjectUndo(dialogueObj, "Create DialoguePanel");
        }

        RectTransform diagRt = dialogueObj.transform as RectTransform;
        if (diagRt != null)
        {
            diagRt.anchorMin = new Vector2(0.08f, 0.04f);
            diagRt.anchorMax = new Vector2(0.92f, 0.38f);
            diagRt.offsetMin = Vector2.zero;
            diagRt.offsetMax = Vector2.zero;
        }

        Image diagBg = dialogueObj.GetComponent<Image>();
        if (diagBg == null) diagBg = Undo.AddComponent<Image>(dialogueObj);
        diagBg.color = new Color(0.06f, 0.07f, 0.12f, 0.94f);
        diagBg.raycastTarget = false;

        // Speaker Name (Large bold text: 40)
        TextMeshProUGUI speakerText = CreateOrGetTMP(dialogueObj.transform, "SpeakerName", "Франк", 40, FontStyles.Bold, new Color(0.35f, 0.95f, 0.72f));
        RectTransform speakerRt = speakerText.transform as RectTransform;
        if (speakerRt != null)
        {
            speakerRt.anchorMin = new Vector2(0.04f, 0.76f);
            speakerRt.anchorMax = new Vector2(0.96f, 0.96f);
            speakerRt.offsetMin = Vector2.zero;
            speakerRt.offsetMax = Vector2.zero;
        }
        speakerText.alignment = TextAlignmentOptions.TopLeft;

        // Dialogue Text (Size 50 with auto-sizing)
        TextMeshProUGUI dialogueText = CreateOrGetTMP(dialogueObj.transform, "DialogueText", "...", 50, FontStyles.Normal, Color.white);
        RectTransform diagTextRt = dialogueText.transform as RectTransform;
        if (diagTextRt != null)
        {
            diagTextRt.anchorMin = new Vector2(0.04f, 0.22f);
            diagTextRt.anchorMax = new Vector2(0.96f, 0.76f);
            diagTextRt.offsetMin = Vector2.zero;
            diagTextRt.offsetMax = Vector2.zero;
        }
        dialogueText.alignment = TextAlignmentOptions.TopLeft;
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        dialogueText.enableAutoSizing = true;
        dialogueText.fontSizeMin = 28;
        dialogueText.fontSizeMax = 54;

        // Continue Prompt (Size 30, centered)
        TextMeshProUGUI promptText = CreateOrGetTMP(dialogueObj.transform, "ContinuePrompt", "[ Натисніть ПРОБІЛ або E щоб продовжити ]", 30, FontStyles.Italic, new Color(1f, 0.88f, 0.40f));
        RectTransform promptRt = promptText.transform as RectTransform;
        if (promptRt != null)
        {
            promptRt.anchorMin = new Vector2(0.04f, 0.02f);
            promptRt.anchorMax = new Vector2(0.96f, 0.20f);
            promptRt.offsetMin = Vector2.zero;
            promptRt.offsetMax = Vector2.zero;
        }
        promptText.alignment = TextAlignmentOptions.Center;

        cutscene.dialoguePanel = dialogueObj;
        cutscene.speakerNameText = speakerText;
        cutscene.dialogueText = dialogueText;
        cutscene.continuePrompt = promptText.gameObject;
        cutscene.continuePromptText = promptText;
        cutscene.promptMessage = "[ Натисніть ПРОБІЛ або E щоб продовжити ]";
        cutscene.continueKey = KeyCode.Space;
        cutscene.alternateKey = KeyCode.E;
        cutscene.blinkPrompt = true;

        // Link Dialogue to Collectible Head
        headCollectible.dialoguePanel = dialogueObj;
        headCollectible.speakerNameText = speakerText;
        headCollectible.dialogueText = dialogueText;

        // Prompt UI for Collectibles (DoorText)
        GameObject promptUIObj = GameObject.Find("DoorText");
        if (promptUIObj != null)
        {
            headCollectible.promptUI = promptUIObj;
            headCollectible.promptText = promptUIObj.GetComponent<TextMeshProUGUI>();
        }

        // Skip Cutscene Button
        Transform skipT = canvas.transform.Find("SkipButton");
        GameObject skipObj = null;
        if (skipT != null)
        {
            skipObj = skipT.gameObject;
        }
        else
        {
            skipObj = new GameObject("SkipButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            skipObj.transform.SetParent(canvas.transform, false);
            Undo.RegisterCreatedObjectUndo(skipObj, "Create SkipButton");
        }

        RectTransform srt = skipObj.GetComponent<RectTransform>();
        if (srt != null)
        {
            srt.anchorMin = new Vector2(1f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 1f);
            srt.anchoredPosition = new Vector2(-25f, -25f);
            srt.sizeDelta = new Vector2(210f, 48f);
        }

        Image sImg = skipObj.GetComponent<Image>();
        if (sImg == null) sImg = skipObj.AddComponent<Image>();
        sImg.color = new Color(0.10f, 0.12f, 0.20f, 0.88f);

        Button sBtn = skipObj.GetComponent<Button>();
        if (sBtn == null) sBtn = skipObj.AddComponent<Button>();

        TextMeshProUGUI skipTxt = CreateOrGetTMP(skipObj.transform, "SkipText", "Пропустити >> [Esc]", 22, FontStyles.Bold, new Color(0.9f, 0.95f, 1f));
        RectTransform txtRt = skipTxt.transform as RectTransform;
        if (txtRt != null)
        {
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
        }
        skipTxt.alignment = TextAlignmentOptions.Center;

        cutscene.skipButton = sBtn;
        skipObj.SetActive(false);

        dialogueObj.SetActive(false);

        // 10. Gameplay UI
        GameObject healthBar = GameObject.Find("HealthBar") ?? GameObject.Find("Health");
        if (healthBar != null) cutscene.gameplayUI = healthBar;

        // Cutscene Dialogue Texts & Timings
        cutscene.frankSpeakerName = "Франк";
        cutscene.headSpeakerName = "Голова Франка";
        cutscene.screamText = "А-А-А-А-А-А-А-А-А-А-А-А-А-А!!!";
        cutscene.monologueText = "Лікарю! Я настільки злякався, що кожна частина мого тіла розлетілася й телепортувалася в різні частини замку! Ми мусимо знайти їх усі!";

        cutscene.delayBeforeStrike = 1.8f;
        cutscene.delayAfterStrike = 0.25f;
        cutscene.screamDuration = 1.3f;
        cutscene.delayAfterSmoke = 0.6f;
        cutscene.typingSpeed = 0.04f;
        cutscene.delayBeforeContinuePrompt = 0.5f;

        // Save active scene immediately to persist changes
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = managerObj;
        Debug.Log("<color=green><b>[CutsceneBuilder] Successfully configured and saved Intro Cutscene to GameScene!</b></color>");
    }

    private static void DestroyIfExists(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
        {
            Object.DestroyImmediate(go);
        }
    }

    private static Sprite LoadSprite(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var obj in assets)
        {
            if (obj is Sprite s) return s;
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static TextMeshProUGUI CreateOrGetTMP(Transform parent, string name, string defaultText, float size, FontStyles style, Color color)
    {
        Transform t = parent.Find(name);
        GameObject go = null;
        if (t != null && !(t is RectTransform))
        {
            Object.DestroyImmediate(t.gameObject);
            t = null;
        }

        if (t == null)
        {
            go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        }
        else
        {
            go = t.gameObject;
        }

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = Undo.AddComponent<TextMeshProUGUI>(go);
        tmp.text = defaultText;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }
}
