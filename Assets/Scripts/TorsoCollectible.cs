using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Attached directly to the Torso GameObject in Level 3.
/// Allows the player to collect Frank's torso once the two guarding spirits are defeated,
/// which triggers the Full_Frank ending sequence.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TorsoCollectible : MonoBehaviour
{
    [Header("Guarding Spirits")]
    [Tooltip("First guarding spirit that must be defeated before torso can be collected")]
    public Enemy spirit1;
    [Tooltip("Second guarding spirit that must be defeated before torso can be collected")]
    public Enemy spirit2;

    [Header("Full Frank Reference")]
    [Tooltip("Reference to the Full_Frank GameObject / script that appears after pickup")]
    public FullFrankEnding fullFrank;

    [Header("Interaction Settings")]
    public KeyCode pickupKey = KeyCode.E;
    public float interactionDistance = 2.8f;
    public bool requireKeyPress = true;

    [Header("Bobbing Animation")]
    public bool enableBobbing = true;
    public float bobbingSpeed = 2.5f;
    public float bobbingHeight = 0.08f;

    [Header("UI Prompt")]
    public GameObject promptUI;
    public TextMeshProUGUI promptText;

    private Vector3 initialPosition;
    private bool isPlayerNear = false;
    private bool isCollected = false;
    private Transform playerTransform;

    void Awake()
    {
        initialPosition = transform.position;
        AutoFindReferences();
    }

    void Start()
    {
        initialPosition = transform.position;
        AutoFindReferences();
        EnsureTriggerCollider();
        FindOrBuildPromptUI();
        if (promptUI != null) promptUI.SetActive(false);
    }

    private void AutoFindReferences()
    {
        if (fullFrank == null)
        {
            fullFrank = Object.FindAnyObjectByType<FullFrankEnding>(FindObjectsInactive.Include);
            if (fullFrank == null)
            {
                GameObject ffGo = GameObject.Find("Full_Frank");
                if (ffGo != null)
                {
                    fullFrank = ffGo.GetComponent<FullFrankEnding>() ?? ffGo.AddComponent<FullFrankEnding>();
                }
            }
        }

        if (spirit1 == null || spirit2 == null)
        {
            GameObject s1Obj = GameObject.Find("Enemy_Spirit");
            if (s1Obj != null) spirit1 = s1Obj.GetComponent<Enemy>();

            GameObject s2Obj = GameObject.Find("Enemy_Spirit (1)");
            if (s2Obj != null) spirit2 = s2Obj.GetComponent<Enemy>();

            if (spirit1 == null || spirit2 == null)
            {
                Enemy[] allEnemies = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var e in allEnemies)
                {
                    if (e.enemyType == EnemyType.Spirit)
                    {
                        if (spirit1 == null) spirit1 = e;
                        else if (spirit2 == null && e != spirit1) spirit2 = e;
                    }
                }
            }
        }

        if (playerTransform == null)
        {
            Player p = Object.FindAnyObjectByType<Player>();
            if (p != null) playerTransform = p.transform;
            else
            {
                GameObject pObj = GameObject.FindWithTag("Player");
                if (pObj != null) playerTransform = pObj.transform;
            }
        }
    }

    private void EnsureTriggerCollider()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 1.0f;
        }
        else
        {
            col.isTrigger = true;
        }
    }

    void Update()
    {
        if (isCollected) return;

        // Visual bobbing effect
        if (enableBobbing)
        {
            float newY = initialPosition.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }

        // Distance check fallback if player didn't trigger via physics
        if (playerTransform != null)
        {
            float dist = Vector2.Distance(transform.position, playerTransform.position);
            bool near = dist <= interactionDistance;
            if (near != isPlayerNear)
            {
                isPlayerNear = near;
                UpdatePromptVisibility();
            }
        }

        if (isPlayerNear)
        {
            bool spiritsDefeated = AreSpiritsDefeated();
            UpdatePromptText(spiritsDefeated);

            if (spiritsDefeated)
            {
                if (!requireKeyPress || Input.GetKeyDown(pickupKey))
                {
                    CollectTorso();
                }
            }
        }
    }

    private bool AreSpiritsDefeated()
    {
        bool s1Dead = spirit1 == null || spirit1.IsDead;
        bool s2Dead = spirit2 == null || spirit2.IsDead;
        return s1Dead && s2Dead;
    }

    private void UpdatePromptVisibility()
    {
        if (promptUI != null)
        {
            promptUI.SetActive(isPlayerNear);
        }
    }

    private void UpdatePromptText(bool spiritsDefeated)
    {
        if (promptText == null) return;

        if (!spiritsDefeated)
        {
            promptText.text = "Defeat the spirits to claim the Torso!";
            promptText.color = new Color(1f, 0.4f, 0.4f);
        }
        else
        {
            promptText.text = $"[ {pickupKey} ] Pick up Frank's Torso";
            promptText.color = new Color(0.35f, 0.95f, 0.72f);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") || collision.GetComponent<Player>() != null)
        {
            isPlayerNear = true;
            UpdatePromptVisibility();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") || collision.GetComponent<Player>() != null)
        {
            isPlayerNear = false;
            UpdatePromptVisibility();
        }
    }

    private void CollectTorso()
    {
        if (isCollected) return;
        isCollected = true;

        if (promptUI != null) promptUI.SetActive(false);

        // Register torso in BodyPartsTracker
        BodyPartsTracker.RegisterPart(CollectibleType.FrankTorso);

        // Hide Torso object
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Trigger Full Frank appearance and monologue
        if (fullFrank == null)
        {
            AutoFindReferences();
        }

        if (fullFrank != null)
        {
            fullFrank.TriggerEndingSequence();
        }
        else
        {
            Debug.LogWarning("[TorsoCollectible] FullFrank reference not found to trigger ending!");
        }

        gameObject.SetActive(false);
    }

    private void FindOrBuildPromptUI()
    {
        if (promptUI != null) return;

        GameObject existing = GameObject.Find("PickupPrompt");
        if (existing != null)
        {
            promptUI = existing;
            promptText = existing.GetComponentInChildren<TextMeshProUGUI>();
            return;
        }

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject promptObj = new GameObject("TorsoPickupPrompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        promptObj.transform.SetParent(canvas.transform, false);

        RectTransform rt = promptObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.2f, 0.15f);
        rt.anchorMax = new Vector2(0.8f, 0.25f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        promptText = promptObj.GetComponent<TextMeshProUGUI>();
        promptText.text = $"[ {pickupKey} ] Pick up Frank's Torso";
        promptText.fontSize = 32;
        promptText.fontStyle = FontStyles.Bold;
        promptText.color = new Color(0.35f, 0.95f, 0.72f);
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.raycastTarget = false;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF");
        if (font != null) promptText.font = font;

        promptUI = promptObj;
        promptUI.SetActive(false);
    }
}
