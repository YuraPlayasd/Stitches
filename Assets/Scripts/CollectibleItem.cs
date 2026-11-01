using System.Collections;
using UnityEngine;
using TMPro;

public enum CollectibleType
{
    FrankHead,
    FrankArm,
    FrankTorso,
    FrankLeg,
    Health,
    Arrows,
    Custom
}

[RequireComponent(typeof(Collider2D))]
public class CollectibleItem : MonoBehaviour
{
    [Header("Collectible Type & Value")]
    public CollectibleType itemType = CollectibleType.FrankHead;
    [Tooltip("Display name used in pickup prompt (e.g., Frank's Head, Healing Potion, Arrows)")]
    public string itemName = "Frank's Head";
    [Tooltip("Amount restored/given (e.g. 25 for +25 HP, 5 for +5 Arrows, 1 for Body Part)")]
    public int amount = 1;

    [Header("Interaction Settings")]
    [Tooltip("If true, player must press a key. If false, collected instantly on touch.")]
    public bool requireKeyPress = true;
    public KeyCode pickupKey = KeyCode.E;

    [Header("UI Prompt")]
    [Tooltip("UI GameObject for the prompt (e.g. Prompt text in Canvas)")]
    public GameObject promptUI;
    public TextMeshProUGUI promptText;
    [Tooltip("Custom prompt message. If empty, automatically generated from itemName.")]
    public string customPromptMessage = "";

    [Header("Dialogue / Story (Optional)")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI dialogueText;
    public string speakerName = "Frank's Head";
    [TextArea(2, 4)]
    public string pickupSpeech = "Careful, Doctor! Don't drop me! Now let's explore the castle and find all my body parts!";
    public float speechDuration = 3.5f;

    [Header("Visual Effects")]
    [Tooltip("Gentle floating bobbing effect")]
    public bool enableBobbing = true;
    public float bobbingSpeed = 2.5f;
    public float bobbingHeight = 0.08f;

    [Header("Audio (Optional)")]
    public AudioSource audioSource;
    public AudioClip pickupSound;

    [Header("Status")]
    public bool isCollected = false;

    private bool isPlayerNearby = false;
    private Player nearbyPlayer;
    private Vector3 initialPosition;
    [Header("Detection Range")]
    [Tooltip("Radius within which the player is considered nearby")]
    public float interactionDistance = 2.5f;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
            if (col is CircleCollider2D circle)
            {
                circle.radius = Mathf.Max(circle.radius, 2.5f);
            }
        }

        initialPosition = transform.position;

        // Auto-find prompt UI if not assigned
        if (promptUI == null)
        {
            GameObject dt = GameObject.Find("DoorText");
            if (dt != null)
            {
                promptUI = dt;
                if (promptText == null) promptText = dt.GetComponent<TextMeshProUGUI>();
            }
        }

        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }

    void OnEnable()
    {
        initialPosition = transform.position;
    }

    public void SetInitialPosition(Vector3 pos)
    {
        initialPosition = pos;
        transform.position = pos;
    }

    void Update()
    {
        if (isCollected) return;

        // Gentle bobbing effect while waiting to be picked up
        if (enableBobbing)
        {
            float newY = initialPosition.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
            transform.position = new Vector3(initialPosition.x, newY, initialPosition.z);
        }

        // Proximity check via Distance
        if (nearbyPlayer == null)
        {
            nearbyPlayer = Object.FindAnyObjectByType<Player>();
        }

        if (nearbyPlayer != null)
        {
            float dist = Vector2.Distance(transform.position, nearbyPlayer.transform.position);
            bool inRange = dist <= interactionDistance;
            if (inRange != isPlayerNearby)
            {
                isPlayerNearby = inRange;
                ShowPrompt(inRange);
            }

            if (inRange)
            {
                // Auto-collect on walk-by / proximity or on any interaction key
                if (!requireKeyPress || dist <= 2.0f || Input.GetKeyDown(pickupKey) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
                {
                    Collect();
                    return;
                }
            }
        }
    }

    private bool IsPlayer(Collider2D other, out Player player)
    {
        player = null;
        if (other == null) return false;

        player = other.GetComponent<Player>() ?? other.GetComponentInParent<Player>();
        if (player != null) return true;

        if (other.CompareTag("Player") || other.name == "Player") return true;

        if (other.attachedRigidbody != null)
        {
            player = other.attachedRigidbody.GetComponent<Player>();
            if (player != null || other.attachedRigidbody.CompareTag("Player") || other.attachedRigidbody.name == "Player")
                return true;
        }

        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        if (IsPlayer(other, out Player player))
        {
            isPlayerNearby = true;
            nearbyPlayer = player;
            Collect();
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (isCollected) return;

        if (IsPlayer(other, out Player player))
        {
            isPlayerNearby = true;
            nearbyPlayer = player;
            Collect();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other, out _))
        {
            isPlayerNearby = false;
            ShowPrompt(false);
        }
    }

    public string GetFormattedPrompt()
    {
        if (!string.IsNullOrEmpty(customPromptMessage))
        {
            return customPromptMessage;
        }

        switch (itemType)
        {
            case CollectibleType.FrankHead:
                return $"[ {pickupKey} ] Pick up Frank's Head";
            case CollectibleType.FrankArm:
                return $"[ {pickupKey} ] Pick up Frank's Arm";
            case CollectibleType.FrankTorso:
                return $"[ {pickupKey} ] Pick up Frank's Torso";
            case CollectibleType.FrankLeg:
                return $"[ {pickupKey} ] Pick up Frank's Leg";
            case CollectibleType.Health:
                return $"[ {pickupKey} ] Pick up Health (+{amount} HP)";
            case CollectibleType.Arrows:
                return $"[ {pickupKey} ] Pick up Arrows (+{amount})";
            default:
                return $"[ {pickupKey} ] Pick up {itemName}";
        }
    }

    private void ShowPrompt(bool show)
    {
        if (promptUI != null)
        {
            if (promptText != null && show)
            {
                promptText.text = GetFormattedPrompt();
            }
            promptUI.SetActive(show);
        }
    }

    public void Collect()
    {
        if (isCollected) return;
        isCollected = true;
        ShowPrompt(false);

        // Register in hidden body parts counter
        if (itemType == CollectibleType.FrankHead ||
            itemType == CollectibleType.FrankArm ||
            itemType == CollectibleType.FrankTorso ||
            itemType == CollectibleType.FrankLeg)
        {
            BodyPartsTracker.RegisterPart(itemType);
        }

        // Apply health or resource effect
        if (nearbyPlayer != null && itemType == CollectibleType.Health)
        {
            nearbyPlayer.Heal(amount);
        }

        if (audioSource != null && pickupSound != null)
        {
            audioSource.PlayOneShot(pickupSound);
        }

        // Show dialogue message if configured (for Frank body parts)
        if (itemType == CollectibleType.FrankHead && FrankVision.Instance != null && !string.IsNullOrEmpty(pickupSpeech))
        {
            FrankVision.Instance.Speak(pickupSpeech, FrankDialoguePriority.Story, speechDuration);
            gameObject.SetActive(false);
            return;
        }

        if (dialoguePanel != null && dialogueText != null && !string.IsNullOrEmpty(pickupSpeech))
        {
            StartCoroutine(ShowPickupSpeechRoutine());
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private IEnumerator ShowPickupSpeechRoutine()
    {
        dialoguePanel.SetActive(true);
        if (speakerNameText != null) speakerNameText.text = speakerName;
        dialogueText.text = pickupSpeech;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        float timer = 0f;
        while (timer < speechDuration)
        {
            timer += Time.deltaTime;
            if (timer > 0.35f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0)))
            {
                break;
            }
            yield return null;
        }

        dialoguePanel.SetActive(false);
        gameObject.SetActive(false);
    }
}
