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

    [Header("Shooting & Ammo")] 
    public GameObject arrowPrefab;
    public Transform arrowSpawnPoint;
    public float arrowCooldown = 0.5f;
    [Tooltip("Number of arrows remaining")]
    public int arrowsCount = 0;
    public TMPro.TextMeshProUGUI arrowCountText;

    [Header("Jump / Fall Feel")]
    [Tooltip("Multiplier applied to gravity when falling to increase fall speed")]
    public float fallMultiplier = 2.5f;

    private float nextArrowTime;

    [Header("Hitbox / Collider Settings")]
    [Tooltip("Player's BoxCollider2D (automatically found if not assigned)")]
    public BoxCollider2D playerCollider;
    [Tooltip("Whether to resize the collider during the fall animation")]
    public bool adjustHitboxOnFall = true;
    [Tooltip("Collider size during fall (smaller bottom to match sprite during fall animation)")]
    public Vector2 fallColliderSize = new Vector2(1.8f, 4.14f);
    [Tooltip("Collider offset during fall (raised from the bottom)")]
    public Vector2 fallColliderOffset = new Vector2(0f, 0.275f);

    [Header("Slope Settings")]
    [Tooltip("Maximum slope angle in degrees that the player can walk on")]
    public float maxSlopeAngle = 55f;
    [Tooltip("Minimum slope angle in degrees before slope mechanics kick in (prevents false slope detection on flat tile seams)")]
    public float minSlopeAngle = 5f;
    [Tooltip("Distance below player collider to check for ground and slopes")]
    public float slopeCheckDistance = 0.5f;

    private bool isOnSlope = false;
    private Vector2 slopeNormal = Vector2.up;
    private float slopeAngle = 0f;
    private bool isJumping = false;
    private float jumpCooldownTimer = 0f;
    private float moveInput = 0f;

    [Header("One-Way Platform")]
    private Collider2D currentOneWayPlatform;
    private float dropThroughTimer = 0f;

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
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
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

        string sceneName = SceneManager.GetActiveScene().name;
        if (BodyPartsTracker.IsHubOrLevel2(sceneName))
        {
            BodyPartsTracker.SetHasHead(true);
        }

        EnsureArrowPrefab();
        AutoFindArrowCountUI();
        UpdateArrowUI();

        IntroCutscene.EnsureCutsceneExists();
        FrankVision.EnsureExists(gameObject);
    }

    void Update()
    {
        moveInput = Input.GetAxis("Horizontal");
        if (Input.GetKeyDown(KeyCode.F))
        {
            Shoot();
        }

        if (jumpCooldownTimer > 0f)
        {
            jumpCooldownTimer -= Time.deltaTime;
        }

        if (dropThroughTimer > 0f)
        {
            dropThroughTimer -= Time.deltaTime;
            if (dropThroughTimer <= 0f)
            {
                currentOneWayPlatform = null;
            }
        }

        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            TryDropThroughPlatform();
        }

        if (isGrounded || (isOnSlope && !isJumping))
        {
            extraJumps = extraJumpesValue;
        }

        jumpBufferCounter -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isGrounded || (isOnSlope && !isJumping))
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

        if ((isGrounded || (isOnSlope && !isJumping)) && jumpBufferCounter > 0f)
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
            isJumping = true;
            jumpCooldownTimer = 0.2f;
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

    private void TryDropThroughPlatform()
    {
        if (playerCollider == null)
        {
            playerCollider = GetComponent<BoxCollider2D>();
            if (playerCollider == null) return;
        }

        Bounds bounds = playerCollider.bounds;
        Vector2 boxOrigin = bounds.center;
        Vector2 boxSize = new Vector2(bounds.size.x * 0.85f, 0.25f);
        float castDistance = bounds.extents.y + 0.35f;

        RaycastHit2D[] hits = Physics2D.BoxCastAll(boxOrigin, boxSize, 0f, Vector2.down, castDistance, groundLayer);
        foreach (var h in hits)
        {
            if (h.collider == null || h.collider == playerCollider) continue;

            OneWayPlatform platform = h.collider.GetComponent<OneWayPlatform>();
            if (platform == null)
            {
                platform = h.collider.GetComponentInParent<OneWayPlatform>();
            }

            if (platform != null)
            {
                platform.DropPlayer(playerCollider);
                currentOneWayPlatform = h.collider;
                dropThroughTimer = platform.dropDuration;
                isGrounded = false;
                isOnSlope = false;
                if (rb != null)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, -2f);
                }
                break;
            }
        }
    }

    private void Jump()
    {
        isJumping = true;
        jumpCooldownTimer = 0.2f;
        isGrounded = false;
        isOnSlope = false;
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, jumpforce);
        }
    }

    private void FixedUpdate()
    {
        CheckGrounded();
        ApplyMovement();

        // Increase fall speed for snappy, responsive platformer controls
        if (rb != null && rb.linearVelocity.y < 0f && !isGrounded)
        {
            rb.linearVelocity += Vector2.up * (Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime);
        }
    }

    private void CheckGrounded()
    {
        if (isJumping && jumpCooldownTimer > 0f)
        {
            isGrounded = false;
            isOnSlope = false;
            return;
        }

        if (playerCollider == null)
        {
            playerCollider = GetComponent<BoxCollider2D>();
        }

        bool grounded = false;
        isOnSlope = false;
        slopeNormal = Vector2.up;
        slopeAngle = 0f;

        // 1. Traditional OverlapCircle check at GroundChek position if assigned
        if (GroundChek != null)
        {
            Collider2D[] circleHits = Physics2D.OverlapCircleAll(GroundChek.position, GroundChekRadius, groundLayer);
            foreach (var c in circleHits)
            {
                if (c != null && c != playerCollider && (dropThroughTimer <= 0f || c != currentOneWayPlatform))
                {
                    grounded = true;
                    break;
                }
            }
        }

        // 2. BoxCast check across the width of the player collider
        if (playerCollider != null)
        {
            Bounds bounds = playerCollider.bounds;
            Vector2 boxOrigin = bounds.center;
            Vector2 boxSize = new Vector2(bounds.size.x * 0.8f, 0.1f);
            float castDistance = bounds.extents.y + slopeCheckDistance;

            RaycastHit2D[] hits = Physics2D.BoxCastAll(boxOrigin, boxSize, 0f, Vector2.down, castDistance, groundLayer);
            RaycastHit2D validHit = default;
            bool foundHit = false;

            foreach (var h in hits)
            {
                if (h.collider != null && h.collider != playerCollider)
                {
                    if (dropThroughTimer > 0f && h.collider == currentOneWayPlatform)
                        continue;
                    validHit = h;
                    foundHit = true;
                    break;
                }
            }

            if (foundHit)
            {
                // To determine ground normal without catching on tile seams, cast downward from player center
                RaycastHit2D centerHit = Physics2D.Raycast(new Vector2(bounds.center.x, bounds.min.y + 0.15f), Vector2.down, slopeCheckDistance + 0.2f, groundLayer);
                Vector2 normal = Vector2.up;
                if (centerHit.collider != null && centerHit.collider != playerCollider && (dropThroughTimer <= 0f || centerHit.collider != currentOneWayPlatform))
                {
                    normal = centerHit.normal;
                }
                else
                {
                    normal = validHit.normal;
                }

                float hitAngle = Vector2.Angle(normal, Vector2.up);

                if (hitAngle <= maxSlopeAngle)
                {
                    float distanceToGround = Mathf.Max(0f, validHit.distance - (bounds.extents.y - 0.05f));
                    float allowedDistance = hitAngle >= minSlopeAngle ? slopeCheckDistance : 0.2f;

                    if (distanceToGround <= allowedDistance)
                    {
                        grounded = true;

                        // Only activate slope physics if the surface angle is significantly sloped
                        // Surfaces below minSlopeAngle (0-5 deg) are treated as pure flat ground to prevent seam stutter
                        if (hitAngle >= minSlopeAngle)
                        {
                            isOnSlope = true;
                            slopeNormal = normal;
                            slopeAngle = hitAngle;

                            // Downward slope snapping ONLY on actual slopes during descent
                            if (!isJumping && distanceToGround > 0.02f && distanceToGround <= 0.35f && rb != null && rb.linearVelocity.y <= 0.1f)
                            {
                                rb.position += Vector2.down * (distanceToGround - 0.005f);
                            }
                        }
                    }
                }
            }
        }

        isGrounded = grounded;

        if (isGrounded && jumpCooldownTimer <= 0f)
        {
            isJumping = false;
        }
    }

    private void ApplyMovement()
    {
        if (rb == null) return;

        if (isJumping && jumpCooldownTimer > 0f)
        {
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
        }
        else if (isOnSlope && isGrounded)
        {
            // Perpendicular vector pointing rightward along the slope surface
            Vector2 slopeDir = new Vector2(slopeNormal.y, -slopeNormal.x).normalized;

            if (Mathf.Abs(moveInput) > 0.01f)
            {
                // Move smoothly along the slope surface
                rb.linearVelocity = slopeDir * (moveInput * moveSpeed);
            }
            else
            {
                // Stand still on slope without sliding down
                rb.linearVelocity = Vector2.zero;
            }
        }
        else
        {
            // Flat ground or airborne
            float yVel = rb.linearVelocity.y;
            if (isGrounded && yVel < 0f && yVel > -1.5f)
            {
                // Zero out micro-downward velocity on flat ground so player doesn't push into floor seams
                yVel = 0f;
            }
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, yVel);
        }
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

        if (isGrounded || (isOnSlope && !isJumping))
        {
            if (Mathf.Abs(moveInput) < 0.01f)
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
            if (rb != null && rb.linearVelocityY > 0.1f)
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
            // If currently falling and settings change during play mode, update hitbox
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

        if (playerCollider != null)
        {
            // Give BoxCollider2D rounded corners to prevent internal edge snagging on tile seams
            if (playerCollider.edgeRadius < 0.05f)
            {
                playerCollider.edgeRadius = 0.05f;
            }

            // Ensure zero friction material is active on the collider
            if (playerCollider.sharedMaterial == null)
            {
                PhysicsMaterial2D mat = Resources.Load<PhysicsMaterial2D>("MovementMaterial");
                if (mat == null)
                {
                    mat = new PhysicsMaterial2D("MovementMaterial") { friction = 0f, bounciness = 0f };
                }
                playerCollider.sharedMaterial = mat;
            }

            if (!isDefaultColliderCached)
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
        if (adjustHitboxOnFall)
        {
            // Draw fall hitbox outline in cyan when player is selected in editor
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.75f);
            float flipSign = transform.localScale.x >= 0 ? 1f : -1f;
            Vector3 center = transform.position + new Vector3(fallColliderOffset.x * flipSign, fallColliderOffset.y, 0f);
            Gizmos.DrawWireCube(center, new Vector3(fallColliderSize.x, fallColliderSize.y, 0.1f));
        }

        if (playerCollider != null)
        {
            Gizmos.color = isOnSlope ? Color.yellow : (isGrounded ? Color.green : Color.red);
            Bounds b = playerCollider.bounds;
            Vector3 boxCenter = b.center + Vector3.down * (slopeCheckDistance * 0.5f);
            Gizmos.DrawWireCube(boxCenter, new Vector3(b.size.x * 0.85f, b.size.y + slopeCheckDistance, 0.1f));
        }
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

    public void AddArrows(int amount)
    {
        arrowsCount += amount;
        UpdateArrowUI();

        if (FrankVision.Instance != null)
        {
            FrankVision.Instance.OnArrowCollected();
        }
    }

    public void EnsureArrowPrefab()
    {
        if (arrowPrefab == null)
        {
            arrowPrefab = Resources.Load<GameObject>("Arrow");
            if (arrowPrefab == null)
            {
                arrowPrefab = Resources.Load<GameObject>("Prefabs/Arrow");
            }
        }
    }

    public void AutoFindArrowCountUI()
    {
        if (arrowCountText != null) return;

        GameObject counterObj = GameObject.Find("ArrowCountText");
        if (counterObj == null) counterObj = GameObject.Find("ArrowCounter");

        if (counterObj != null)
        {
            arrowCountText = counterObj.GetComponent<TMPro.TextMeshProUGUI>();
            if (arrowCountText == null) arrowCountText = counterObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        }

        if (arrowCountText == null)
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                GameObject newObj = new GameObject("ArrowCounter");
                newObj.transform.SetParent(canvas.transform, false);

                RectTransform rt = newObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-40f, -40f);
                rt.sizeDelta = new Vector2(320f, 90f);

                TMPro.TextMeshProUGUI tmp = newObj.AddComponent<TMPro.TextMeshProUGUI>();
                tmp.fontSize = 56f;
                tmp.fontStyle = TMPro.FontStyles.Bold;
                tmp.alignment = TMPro.TextAlignmentOptions.TopRight;
                tmp.color = Color.white;
                tmp.outlineColor = Color.black;
                tmp.outlineWidth = 0.2f;

                TMPro.TMP_FontAsset font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF");
                if (font == null) font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font != null) tmp.font = font;

                arrowCountText = tmp;
            }
        }
    }

    public void UpdateArrowUI()
    {
        if (arrowCountText == null)
        {
            AutoFindArrowCountUI();
        }

        if (arrowCountText != null)
        {
            arrowCountText.text = $"x {arrowsCount}";
        }
    }

    public void Die()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(currentScene))
        {
            SceneManager.LoadScene("Level 1");
        }
        else
        {
            SceneManager.LoadScene(currentScene);
        }
    }

    private void Shoot()
    {
        if (arrowsCount <= 0)
        {
            return;
        }

        if (Time.time < nextArrowTime)
        {
            return;
        }

        EnsureArrowPrefab();
        if (arrowPrefab == null)
        {
            return;
        }

        nextArrowTime = Time.time + arrowCooldown;
        arrowsCount--;
        UpdateArrowUI();

        float shootDir = transform.localScale.x >= 0 ? 1f : -1f;
        Vector2 rayOrigin = (Vector2)transform.position + new Vector2(0f, 0.2f);
        Vector2 shootVec = new Vector2(shootDir, 0f);
        float spawnDist = 0.65f;

        // Ensure we never spawn an arrow inside or past a solid wall if the player is hugging a wall
        RaycastHit2D[] wallHits = Physics2D.RaycastAll(rayOrigin, shootVec, 0.85f);
        foreach (var wh in wallHits)
        {
            if (wh.collider == null || wh.collider.gameObject == gameObject) continue;
            if (wh.collider.transform.IsChildOf(transform)) continue;
            if (wh.collider.isTrigger) continue;
            spawnDist = Mathf.Min(spawnDist, Mathf.Max(0.15f, wh.distance - 0.2f));
        }

        Vector3 spawnPos = arrowSpawnPoint != null
            ? arrowSpawnPoint.position
            : transform.position + new Vector3(shootDir * spawnDist, 0.2f, 0f);

        // Arrow sprite tip is on the left:
        // shootDir > 0 (flying right) -> rotate 180 deg to point right
        // shootDir < 0 (flying left) -> rotate 0 deg (identity) to point left
        Quaternion rot = shootDir > 0 ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
        GameObject arrow = Instantiate(arrowPrefab, spawnPos, rot);

        // Ignore collisions between all player colliders and the arrow collider
        Collider2D arrowCol = arrow.GetComponent<Collider2D>();
        if (arrowCol != null)
        {
            foreach (var pCol in GetComponentsInChildren<Collider2D>())
            {
                if (pCol != null)
                {
                    Physics2D.IgnoreCollision(arrowCol, pCol, true);
                }
            }
        }

        Arrow arrowComp = arrow.GetComponent<Arrow>();
        if (arrowComp != null)
        {
            arrowComp.Init(shootDir);
        }
        else
        {
            Rigidbody2D arrowRb = arrow.GetComponent<Rigidbody2D>();
            if (arrowRb != null)
            {
                arrowRb.linearVelocity = new Vector2(shootDir * 12f, 0f);
            }
        }
    }
}