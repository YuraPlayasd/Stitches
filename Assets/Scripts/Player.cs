using UnityEngine;
using System.Collections; 
using UnityEngine.SceneManagement; 
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    [Header("Health Settings")]
    public int health = 100;
    public int maxHealth = 100;
    public Image healthImage;
    public float invulnerabilityTime = 0.5f;
    private bool isInvulnerable = false;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpforce = 10f;
    public Transform GroundChek;
    public float GroundChekRadius = 0.2f;
    public LayerMask groundLayer;

    public float jumpBufferTime = 0.15f; 
    private float jumpBufferCounter;

    private Rigidbody2D rb;
    private bool isGrounded;

    private Animator animator;
    private string currentAnimation;

    public int extraJumpesValue = 1;
    private int extraJumps;

    private SpriteRenderer spriteRender;

    [Header("Shooting")] 
    public GameObject arrowPrefab;
    public Transform arrowSpawnPoint;
    public float arrowCooldown = 0.5f;

    private float nextArrowTime;

    [Header("Hitbox / Collider Settings")]
    [Tooltip("BoxCollider2D гравця (якщо не вказано, знайдеться автоматично)")]
    public BoxCollider2D playerCollider;
    [Tooltip("Чи змінювати розмір колайдера під час анімації падіння")]
    public bool adjustHitboxOnFall = true;
    [Tooltip("Розмір колайдера під час падіння (менший знизу відповідно до модельки під час падіння)")]
    public Vector2 fallColliderSize = new Vector2(1.8f, 4.14f);
    [Tooltip("Зсув колайдера під час падіння (піднятий знизу)")]
    public Vector2 fallColliderOffset = new Vector2(0f, 0.275f);

    private Vector2 defaultColliderSize;
    private Vector2 defaultColliderOffset;
    private bool isDefaultColliderCached = false;

    void Awake()
    {
        CacheDefaultCollider();
        AutoFindHealthImage();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        extraJumps = extraJumpesValue;
        spriteRender = GetComponent<SpriteRenderer>();
        if (spriteRender == null)
        {
            spriteRender = GetComponentInChildren<SpriteRenderer>();
        }

        CacheDefaultCollider();

        AutoFindHealthImage();
        UpdateHealthUI();

        IntroCutscene.EnsureCutsceneExists();
    }

    void Update()
    {
        float moveInput = Input.GetAxis("Horizontal");
        if (Input.GetKeyDown(KeyCode.F))
        {
            Shoot();
        }
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        if (isGrounded)
        {
            extraJumps = extraJumpesValue;
        }

        jumpBufferCounter -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isGrounded)
            {
                Jump();
            }
            else if (extraJumps > 0)
            {
                Jump();
                extraJumps--;
            }
            else
            {
                jumpBufferCounter = jumpBufferTime;
            }
        }

        if (isGrounded && jumpBufferCounter > 0f)
        {
            Jump();
            jumpBufferCounter = 0f;
        }

        UpdateHealthUI();

        SetAnimation(moveInput);
    }

    public void AutoFindHealthImage()
    {
        if (healthImage != null)
        {
            EnsureHealthImageSettings();
            return;
        }

        // Direct search if active
        GameObject healthObj = GameObject.Find("Health");
        if (healthObj != null)
        {
            healthImage = healthObj.GetComponent<Image>();
        }

        // Search including inactive (e.g. while cutscene disables gameplayUI)
        if (healthImage == null)
        {
            Image[] allImages = Object.FindObjectsByType<Image>(FindObjectsInactive.Include);
            foreach (var img in allImages)
            {
                if (img.gameObject.name.Equals("Health", System.StringComparison.OrdinalIgnoreCase))
                {
                    healthImage = img;
                    break;
                }
            }

            if (healthImage == null)
            {
                foreach (var img in allImages)
                {
                    if (img.transform.parent != null && img.transform.parent.name.Equals("HealthBar", System.StringComparison.OrdinalIgnoreCase))
                    {
                        healthImage = img;
                        break;
                    }
                }
            }
        }

        EnsureHealthImageSettings();
    }

    private void EnsureHealthImageSettings()
    {
        if (healthImage == null) return;

        if (healthImage.type != Image.Type.Filled)
        {
            healthImage.type = Image.Type.Filled;
            healthImage.fillMethod = Image.FillMethod.Horizontal;
            healthImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    public void UpdateHealthUI()
    {
        if (healthImage == null)
        {
            AutoFindHealthImage();
        }

        if (healthImage != null)
        {
            EnsureHealthImageSettings();
            float max = maxHealth > 0 ? maxHealth : 100f;
            healthImage.fillAmount = Mathf.Clamp01((float)health / max);
        }
    }

    public void TakeDamage(int damage)
    {
        if (isInvulnerable || health <= 0) return;

        health -= damage;
        health = Mathf.Max(0, health);
        UpdateHealthUI();

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 1.5f * jumpforce);
        }

        StartCoroutine(BlinkRed());

        if (health <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        health = Mathf.Min(health + amount, maxHealth);
        UpdateHealthUI();
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpforce);
    }

    private void FixedUpdate()
    {
        isGrounded = Physics2D.OverlapCircle(GroundChek.position, GroundChekRadius, groundLayer);
    }

    private void SetAnimation(float moveInput)
    {
        if (moveInput > 0)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        else if (moveInput < 0)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }

        string newAnimation;

        if (isGrounded)
        {
            if (moveInput == 0)
            {
                newAnimation = "Player_idle";
            }
            else
            {
                newAnimation = "Player_run";
            }
        }
        else
        {
            if (rb.linearVelocityY > 0.1f)
            {
                newAnimation = "Player_Jump";
            }
            else
            {
                newAnimation = "Player_fall";
            }
        }

        ChangeAnimation(newAnimation);
    }

    private void ChangeAnimation(string newAnimation)
    {
        if (currentAnimation == newAnimation)
        {
            // Якщо зараз триває падіння і налаштування змінюються під час гри, оновлюємо хітбокс
            if (currentAnimation == "Player_fall" && adjustHitboxOnFall && playerCollider != null)
            {
                if (playerCollider.size != fallColliderSize || playerCollider.offset != fallColliderOffset)
                {
                    playerCollider.size = fallColliderSize;
                    playerCollider.offset = fallColliderOffset;
                }
            }
            return;
        }

        animator.Play(newAnimation);
        currentAnimation = newAnimation;
        UpdateColliderForAnimation(newAnimation);
    }

    public void CacheDefaultCollider()
    {
        if (playerCollider == null)
        {
            playerCollider = GetComponent<BoxCollider2D>();
        }

        if (playerCollider != null && !isDefaultColliderCached)
        {
            defaultColliderSize = playerCollider.size;
            defaultColliderOffset = playerCollider.offset;
            isDefaultColliderCached = true;

            if (fallColliderSize == Vector2.zero)
            {
                float bottomReduction = 0.55f;
                fallColliderSize = new Vector2(defaultColliderSize.x * 0.9f, Mathf.Max(0.1f, defaultColliderSize.y - bottomReduction));
                fallColliderOffset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y + bottomReduction * 0.5f);
            }
        }
    }

    private void UpdateColliderForAnimation(string animName)
    {
        if (playerCollider == null)
        {
            playerCollider = GetComponent<BoxCollider2D>();
            if (playerCollider == null) return;
        }

        if (!isDefaultColliderCached)
        {
            CacheDefaultCollider();
        }

        if (adjustHitboxOnFall && animName == "Player_fall")
        {
            playerCollider.size = fallColliderSize;
            playerCollider.offset = fallColliderOffset;
        }
        else
        {
            playerCollider.size = defaultColliderSize;
            playerCollider.offset = defaultColliderOffset;
        }
    }

    private void OnValidate()
    {
        if (playerCollider == null)
        {
            playerCollider = GetComponent<BoxCollider2D>();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!adjustHitboxOnFall) return;

        // Показуємо контур хітбокса падіння блакитним кольором при виборі гравця в редакторі
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.75f);
        float flipSign = transform.localScale.x >= 0 ? 1f : -1f;
        Vector3 center = transform.position + new Vector3(fallColliderOffset.x * flipSign, fallColliderOffset.y, 0f);
        Gizmos.DrawWireCube(center, new Vector3(fallColliderSize.x, fallColliderSize.y, 0.1f));
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckDamage(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckDamage(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        CheckDamage(collider.gameObject);
    }

    private void OnTriggerStay2D(Collider2D collider)
    {
        CheckDamage(collider.gameObject);
    }

    private void CheckDamage(GameObject target)
    {
        if (target == null) return;

        bool isDamage = target.CompareTag("Damage") ||
                        (target.transform.parent != null && target.transform.parent.CompareTag("Damage")) ||
                        target.GetComponent<Enemy>() != null ||
                        (target.transform.parent != null && target.transform.parent.GetComponent<Enemy>() != null);

        if (isDamage)
        {
            TakeDamage(25);
        }
    }

    private IEnumerator BlinkRed()
    {
        isInvulnerable = true;
        if (spriteRender != null)
        {
            spriteRender.color = Color.red;
        }

        yield return new WaitForSeconds(0.15f);

        if (spriteRender != null)
        {
            spriteRender.color = Color.white;
        }

        if (invulnerabilityTime > 0.15f)
        {
            yield return new WaitForSeconds(invulnerabilityTime - 0.15f);
        }

        isInvulnerable = false;
    }

    public void Die()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(currentScene))
        {
            SceneManager.LoadScene("GameScene");
        }
        else
        {
            SceneManager.LoadScene(currentScene);
        }
    }

    private void Shoot()
    {
        if (arrowPrefab == null)
        {
            return;
        }

        if (Time.time < nextArrowTime)
        {
            return;
        }
        
        nextArrowTime = Time.time + arrowCooldown;
        
        Vector3 spawnPos = arrowSpawnPoint != null ? arrowSpawnPoint.position : transform.position;
        GameObject arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);

        float shootDir = transform.localScale.x > 0 ? 1f : -1f;

        if (transform.localScale.x > 0)
        {
            arrow.transform.rotation = Quaternion.Euler(0, 0, 180);
        }
        else
        {
            arrow.transform.rotation = Quaternion.identity;
        }

        Rigidbody2D arrowRb = arrow.GetComponent<Rigidbody2D>();
        if (arrowRb != null)
        {
            float arrowSpeed = 10f;
            arrowRb.linearVelocity = new Vector2(shootDir * arrowSpeed, 0f);
        }
    }
}