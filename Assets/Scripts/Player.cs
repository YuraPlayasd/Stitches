using UnityEngine;
using System.Collections; 
using UnityEngine.SceneManagement; 
using UnityEngine.UI;
using UnityEngine.Tilemaps;

public class Player : MonoBehaviour
{
    [Header("Health Settings")]
    public int health = 100;
    public int maxHealth = 100;
    public Image healthImage;
    public float invulnerabilityTime = 0.5f;
    private bool isInvulnerable = false;

    [Header("Movement")]
    public float moveSpeed = 6.2f;
    public float jumpforce = 11.5f;

    [Header("Footstep / Walk Audio")]
    public AudioClip walkLevel1Clip;
    public AudioClip walkLevel2Clip;
    [Range(0f, 1f)]
    [Tooltip("Footstep and landing audio volume - louder than ambient music")]
    public float walkAudioVolume = 1.0f;

    private AudioSource landingAudioSource;
    private Coroutine stopLandingRoutine;
    private float airTime = 0f;
    private AudioSource walkAudioSource;
    private bool isWalkAudioPaused = false;
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

    public static int CarriedArrowsCount { get; set; } = -1;

    public static void SaveArrowsCount(int count)
    {
        CarriedArrowsCount = count;
    }
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
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene.IndexOf("Level 3", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            currentScene.IndexOf("Level3", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (CarriedArrowsCount >= 0)
            {
                arrowsCount = CarriedArrowsCount;
            }
        }
        else
        {
            arrowsCount = 0;
        }
        CacheDefaultCollider();
        EnsureTilemapCompositeColliders();
        AutoFindHealthImage();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
        animator = GetComponent<Animator>();
        extraJumps = extraJumpesValue;
        spriteRender = GetComponent<SpriteRenderer>();
        if (spriteRender == null)
        {
            spriteRender = GetComponentInChildren<SpriteRenderer>();
        }

        CacheDefaultCollider();
        EnsureTilemapCompositeColliders();

        AutoFindHealthImage();
        UpdateHealthUI();

        string sceneName = SceneManager.GetActiveScene().name;
        if (BodyPartsTracker.IsHubOrLevel2(sceneName))
        {
            BodyPartsTracker.SetHasHead(true);
        }

        // Enforce 0 arrows on Level 1 & 2; restore carried arrows on Level 3
        string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeSceneName.IndexOf("Level 3", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            activeSceneName.IndexOf("Level3", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (CarriedArrowsCount >= 0)
            {
                arrowsCount = CarriedArrowsCount;
            }
        }
        else if (activeSceneName.IndexOf("Level 1", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 activeSceneName.IndexOf("Level1", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 activeSceneName.IndexOf("Level 2", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 activeSceneName.IndexOf("Level2", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            arrowsCount = 0;
        }

        EnsureArrowPrefab();
        AutoFindArrowCountUI();
        UpdateArrowUI();

        IntroCutscene.EnsureCutsceneExists();
        FrankVision.EnsureExists(gameObject);

        SetupWalkAudio();
        SceneManager.sceneLoaded += OnSceneLoadedForWalkAudio;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedForWalkAudio;
        if (walkAudioSource != null && walkAudioSource.isPlaying)
        {
            walkAudioSource.Stop();
        }
    }

    void Update()
    {
        moveInput = Input.GetAxis("Horizontal");
        UpdateWalkAudio();
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
                float distanceToGround = Mathf.Max(0f, validHit.distance - (bounds.extents.y - 0.05f));

                // Sample multiple raycasts downward across player width to accurately determine ground normal
                // (prevents seam artifacts where a BoxCast hitting a tile corner returns a diagonal normal)
                float rayY = bounds.min.y + 0.2f;
                float rayDist = slopeCheckDistance + 0.3f;
                float halfWidth = bounds.extents.x * 0.45f;

                RaycastHit2D centerHit = Physics2D.Raycast(new Vector2(bounds.center.x, rayY), Vector2.down, rayDist, groundLayer);
                RaycastHit2D leftHit = Physics2D.Raycast(new Vector2(bounds.center.x - halfWidth, rayY), Vector2.down, rayDist, groundLayer);
                RaycastHit2D rightHit = Physics2D.Raycast(new Vector2(bounds.center.x + halfWidth, rayY), Vector2.down, rayDist, groundLayer);

                Vector2 normal = Vector2.up;

                // If any downward ray detects a flat surface, prioritize flat ground to prevent seam stutter
                if (IsFlatSurface(centerHit) || IsFlatSurface(leftHit) || IsFlatSurface(rightHit))
                {
                    normal = Vector2.up;
                }
                else if (IsValidGroundHit(centerHit))
                {
                    normal = centerHit.normal;
                }
                else if (IsValidGroundHit(leftHit))
                {
                    normal = leftHit.normal;
                }
                else if (IsValidGroundHit(rightHit))
                {
                    normal = rightHit.normal;
                }
                else if (validHit.normal.y > 0.4f && Vector2.Angle(validHit.normal, Vector2.up) < minSlopeAngle)
                {
                    normal = Vector2.up;
                }

                float hitAngle = Vector2.Angle(normal, Vector2.up);

                if (hitAngle <= maxSlopeAngle && normal.y > 0.3f)
                {
                    float allowedDistance = hitAngle >= minSlopeAngle ? slopeCheckDistance : 0.2f;

                    if (distanceToGround <= allowedDistance)
                    {
                        grounded = true;

                        // Only activate slope physics if the surface is genuinely sloped
                        if (hitAngle >= minSlopeAngle)
                        {
                            isOnSlope = true;
                            slopeNormal = normal;
                            slopeAngle = hitAngle;

                            // Downward slope snapping ONLY when actively descending on a confirmed slope
                            if (!isJumping && Mathf.Abs(moveInput) > 0.01f && distanceToGround > 0.04f && distanceToGround <= 0.3f && rb != null && rb.linearVelocity.y <= 0.1f)
                            {
                                rb.position += Vector2.down * (distanceToGround - 0.01f);
                            }
                        }
                    }
                }
            }
        }

        isGrounded = grounded;

        if (!isGrounded)
        {
            airTime += Time.fixedDeltaTime;
        }
        else
        {
            // Landing after being in the air from a jump or drop
            if (airTime >= 0.12f && jumpCooldownTimer <= 0f)
            {
                PlayLandingSound();
            }
            airTime = 0f;
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
            if (isGrounded && yVel < 0.05f && yVel > -2f)
            {
                // Zero out micro-downward velocity on flat ground so player doesn't push into floor seams or jitter
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
            // Coyote time / micro-separation grace while running horizontally
            if (!isJumping && airTime < 0.12f && Mathf.Abs(moveInput) > 0.01f && rb != null && rb.linearVelocity.y >= -1.5f)
            {
                newAnimation = "Player_run";
            }
            else if (rb != null && rb.linearVelocityY > 0.1f)
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
            // Align bottom with default collider so bottom never dips below or shifts into the floor!
            float defaultBottom = defaultColliderOffset.y - defaultColliderSize.y * 0.5f;
            Vector2 alignedOffset = new Vector2(fallColliderOffset.x, defaultBottom + fallColliderSize.y * 0.5f);
            playerCollider.size = fallColliderSize;
            playerCollider.offset = alignedOffset;
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

        FrankVision fv = FrankVision.Instance ?? FrankVision.EnsureExists(gameObject);
        if (fv != null)
        {
            fv.OnArrowCollected();
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
        if (walkAudioSource != null && walkAudioSource.isPlaying)
        {
            walkAudioSource.Stop();
        }
        isWalkAudioPaused = false;
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

    // ==========================================
    // FOOTSTEP & WALKING AUDIO SYSTEM
    // ==========================================
    private void SetupWalkAudio()
    {
        if (walkLevel1Clip == null)
        {
            walkLevel1Clip = Resources.Load<AudioClip>("Audio/walk_level1");
            if (walkLevel1Clip == null) walkLevel1Clip = Resources.Load<AudioClip>("walk_level1");
        }

        if (walkLevel2Clip == null)
        {
            walkLevel2Clip = Resources.Load<AudioClip>("Audio/walk_level2");
            if (walkLevel2Clip == null) walkLevel2Clip = Resources.Load<AudioClip>("walk_level2");
        }

        if (walkAudioSource == null)
        {
            Transform existing = transform.Find("PlayerWalkAudio");
            GameObject walkObj = existing != null ? existing.gameObject : new GameObject("PlayerWalkAudio");
            walkObj.transform.SetParent(transform);
            walkObj.transform.localPosition = Vector3.zero;

            walkAudioSource = walkObj.GetComponent<AudioSource>();
            if (walkAudioSource == null)
            {
                walkAudioSource = walkObj.AddComponent<AudioSource>();
            }
            walkAudioSource.playOnAwake = false;
            walkAudioSource.loop = true;
            walkAudioSource.spatialBlend = 0f; // clean 2D audio
            walkAudioSource.volume = walkAudioVolume;
        }

        string currentScene = SceneManager.GetActiveScene().name;
        AudioClip targetClip = IsLevel1Scene(currentScene) ? walkLevel1Clip : walkLevel2Clip;

        if (walkAudioSource.clip != targetClip)
        {
            bool wasPlaying = walkAudioSource.isPlaying;
            float prevTime = (walkAudioSource != null && walkAudioSource.clip != null) ? walkAudioSource.time : 0f;
            walkAudioSource.clip = targetClip;
            if (wasPlaying && targetClip != null)
            {
                walkAudioSource.time = Mathf.Clamp(prevTime, 0f, targetClip.length - 0.01f);
                walkAudioSource.Play();
            }
        }
    }

    private void OnSceneLoadedForWalkAudio(Scene scene, LoadSceneMode mode)
    {
        EnsureTilemapCompositeColliders();
        SetupWalkAudio();
    }

    private bool IsLevel1Scene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return true;
        string lower = sceneName.ToLowerInvariant();
        return lower.Contains("level 1") || lower.Contains("level1") || lower.Contains("gamescene");
    }

    private void UpdateWalkAudio()
    {
        if (walkAudioSource == null) return;

        string currentScene = SceneManager.GetActiveScene().name;
        AudioClip targetClip = IsLevel1Scene(currentScene) ? walkLevel1Clip : walkLevel2Clip;
        if (walkAudioSource.clip != targetClip && targetClip != null)
        {
            float prevTime = (walkAudioSource != null && walkAudioSource.clip != null) ? walkAudioSource.time : 0f;
            bool wasPlaying = walkAudioSource.isPlaying;
            walkAudioSource.clip = targetClip;
            walkAudioSource.time = Mathf.Clamp(prevTime, 0f, targetClip.length - 0.01f);
            if (wasPlaying) walkAudioSource.Play();
        }

        if (walkAudioSource.clip == null) return;

        if (health <= 0)
        {
            if (walkAudioSource.isPlaying) walkAudioSource.Stop();
            isWalkAudioPaused = false;
            return;
        }

        // Active walking on ground or slope
        bool isWalking = (isGrounded || (isOnSlope && !isJumping)) && Mathf.Abs(moveInput) > 0.05f && (rb == null || Mathf.Abs(rb.linearVelocity.x) > 0.1f);

        if (isWalking)
        {
            if (!walkAudioSource.isPlaying)
            {
                if (isWalkAudioPaused)
                {
                    walkAudioSource.UnPause();
                }
                else
                {
                    walkAudioSource.Play();
                }
                isWalkAudioPaused = false;
            }
        }
        else
        {
            if (walkAudioSource.isPlaying)
            {
                walkAudioSource.Pause();
                isWalkAudioPaused = true;
            }
        }
    }

    private void EnsureLandingAudio()
    {
        if (landingAudioSource != null) return;

        Transform child = transform.Find("PlayerLandingAudio");
        if (child == null)
        {
            GameObject obj = new GameObject("PlayerLandingAudio");
            obj.transform.SetParent(transform, false);
            child = obj.transform;
        }

        landingAudioSource = child.GetComponent<AudioSource>();
        if (landingAudioSource == null)
        {
            landingAudioSource = child.gameObject.AddComponent<AudioSource>();
        }

        landingAudioSource.loop = false;
        landingAudioSource.playOnAwake = false;
        landingAudioSource.spatialBlend = 0f;
    }

    private void PlayLandingSound()
    {
        if (health <= 0) return;

        string currentScene = SceneManager.GetActiveScene().name;
        AudioClip clip = IsLevel1Scene(currentScene) ? walkLevel1Clip : walkLevel2Clip;
        if (clip == null) return;

        EnsureLandingAudio();
        if (landingAudioSource != null)
        {
            landingAudioSource.clip = clip;
            // Play from roughly the middle of the walking audio file for the current location
            landingAudioSource.time = Mathf.Clamp(clip.length * 0.48f, 0f, clip.length - 0.1f);
            landingAudioSource.volume = walkAudioVolume;
            landingAudioSource.Play();

            if (stopLandingRoutine != null)
            {
                StopCoroutine(stopLandingRoutine);
            }
            stopLandingRoutine = StartCoroutine(StopLandingSoundAfter(0.32f));
        }
    }

    private IEnumerator StopLandingSoundAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (landingAudioSource != null && landingAudioSource.isPlaying)
        {
            landingAudioSource.Stop();
        }
        stopLandingRoutine = null;
    }

    public static void EnsureTilemapCompositeColliders()
    {
        TilemapCollider2D[] tilemapColliders = Object.FindObjectsByType<TilemapCollider2D>();
        foreach (var tc in tilemapColliders)
        {
            CompositeCollider2D comp = tc.GetComponent<CompositeCollider2D>();
            if (comp != null)
            {
                if (tc.compositeOperation != Collider2D.CompositeOperation.Merge)
                {
                    tc.compositeOperation = Collider2D.CompositeOperation.Merge;
                    comp.GenerateGeometry();
                }
            }
        }
    }

    private bool IsValidGroundHit(RaycastHit2D hit)
    {
        return hit.collider != null && hit.collider != playerCollider && hit.normal.y > 0.3f && (dropThroughTimer <= 0f || hit.collider != currentOneWayPlatform);
    }

    private bool IsFlatSurface(RaycastHit2D hit)
    {
        return IsValidGroundHit(hit) && Vector2.Angle(hit.normal, Vector2.up) < minSlopeAngle;
    }
}