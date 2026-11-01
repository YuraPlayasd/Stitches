using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyType
{
    Zombie,
    Skeleton,
    Spirit,
    Custom
}

public class Enemy : MonoBehaviour
{
    [Header("Enemy Type & Health")]
    public EnemyType enemyType = EnemyType.Zombie;
    [Tooltip("Health points: Zombie=1 (1 arrow), Skeleton=2 (2 arrows), Spirit=3 (3 arrows)")]
    public int health = 1;
    public int maxHealth = 1;

    [Header("Movement Speeds")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 3.5f;

    [Header("Patrol Waypoints")]
    public Transform[] waypoints;
    private int currentPointIndex;

    [Header("Target Tracking")]
    public Transform targetPlayer;
    [Tooltip("Horizontal sight range to detect the player")]
    public float detectionRadius = 5f;
    [Tooltip("Maximum vertical height difference to detect the player (sight is constrained horizontally)")]
    public float maxVerticalDistance = 4f;

    [Header("Obstacle & Ledge Detection")]
    [Tooltip("Layer considered solid ground / obstacles (defaults to Ground)")]
    public LayerMask groundLayer;
    [Tooltip("Prevents ground enemies (Skeleton/Zombie) from walking off ledges / cliffs during patrol")]
    public bool stopAtLedges = true;
    [Tooltip("If true, enemies also stop at ledges when chasing the player. If false (default), player can bait them to fall off cliffs!")]
    public bool stopAtLedgesWhenChasing = false;
    [Tooltip("Requires clear line-of-sight to player (blocked by walls/floors)")]
    public bool requireLineOfSight = true;
    [Tooltip("When chasing, stop at patrol waypoint boundaries instead of running off")]
    public bool stayWithinWaypoints = false;

    [Header("Death Animation")]
    public float deathDuration = 0.65f;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Collider2D enemyCollider;
    private Collider2D[] myColliders;
    private Rigidbody2D rb;
    private bool isDead = false;

    // Static registry so enemies can pass through each other without collision
    private static readonly HashSet<Collider2D> allEnemyColliders = new HashSet<Collider2D>();

    void OnEnable()
    {
        RegisterEnemyColliders();
    }

    void OnDisable()
    {
        UnregisterEnemyColliders();
    }

    void OnDestroy()
    {
        UnregisterEnemyColliders();
    }

    private void RegisterEnemyColliders()
    {
        if (enemyCollider == null)
        {
            enemyCollider = GetComponent<Collider2D>();
        }

        myColliders = GetComponentsInChildren<Collider2D>(true);
        if (myColliders == null || myColliders.Length == 0) return;

        // Clean up stale destroyed colliders
        allEnemyColliders.RemoveWhere(col => col == null);

        // Ignore collisions between this enemy and all other active enemies
        foreach (var otherCol in allEnemyColliders)
        {
            if (otherCol == null) continue;
            foreach (var myCol in myColliders)
            {
                if (myCol != null && myCol != otherCol)
                {
                    Physics2D.IgnoreCollision(myCol, otherCol, true);
                }
            }
        }

        // Add this enemy's colliders to the static set
        foreach (var myCol in myColliders)
        {
            if (myCol != null)
            {
                allEnemyColliders.Add(myCol);
            }
        }
    }

    private void UnregisterEnemyColliders()
    {
        if (myColliders != null)
        {
            foreach (var myCol in myColliders)
            {
                if (myCol != null)
                {
                    allEnemyColliders.Remove(myCol);
                }
            }
        }
    }

    void Awake()
    {
        InitializeHealth();
        InitGroundLayer();
    }

    private void InitGroundLayer()
    {
        if (groundLayer.value == 0)
        {
            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0)
            {
                groundLayer = 1 << ground;
            }
            else
            {
                groundLayer = 1 << 6;
            }
        }
    }

    void Start()
    {
        InitGroundLayer();

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }

        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (animator == null)
        {
            animator = gameObject.AddComponent<Animator>();
        }

        if (animator.runtimeAnimatorController == null)
        {
            string ctrlName = enemyType == EnemyType.Zombie ? "Zombie" :
                              enemyType == EnemyType.Skeleton ? "Sceleton" : "Spirit";
            RuntimeAnimatorController ctrl = Resources.Load<RuntimeAnimatorController>("EnemyControllers/" + ctrlName);
            if (ctrl != null)
            {
                animator.runtimeAnimatorController = ctrl;
            }
        }

        enemyCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();

        // Spirit can float through obstacles and is immune to gravity traps
        if (enemyType == EnemyType.Spirit)
        {
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            if (enemyCollider != null)
            {
                enemyCollider.isTrigger = true;
            }
        }

        if (targetPlayer == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) targetPlayer = p.transform;
        }

        RegisterEnemyColliders();
    }

    private void OnValidate()
    {
        InitializeHealth();
    }

    private void InitializeHealth()
    {
        if (enemyType == EnemyType.Zombie)
        {
            maxHealth = 1;
            patrolSpeed = 0.9f;
            chaseSpeed = 1.6f;
        }
        else if (enemyType == EnemyType.Skeleton)
        {
            maxHealth = 2;
            patrolSpeed = 2.0f;
            chaseSpeed = 3.2f;
        }
        else if (enemyType == EnemyType.Spirit)
        {
            maxHealth = 3;
            patrolSpeed = 2.5f;
            chaseSpeed = 4.0f;
        }

        if (health <= 0 || health > maxHealth)
        {
            health = maxHealth;
        }
    }

    void Update()
    {
        if (isDead) return;

        // Clean up enemies falling far below the level
        if (transform.position.y < -35f)
        {
            Die();
            return;
        }

        if (CanSeePlayer())
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }
    }

    private bool CanSeePlayer()
    {
        if (targetPlayer == null) return false;

        float dx = Mathf.Abs(transform.position.x - targetPlayer.position.x);
        float dy = Mathf.Abs(transform.position.y - targetPlayer.position.y);

        // 1. Distance thresholds
        if (dx > detectionRadius || (maxVerticalDistance > 0f && dy > maxVerticalDistance))
        {
            return false;
        }

        // 2. Spirits ignore physical sight blocking
        if (enemyType == EnemyType.Spirit || !requireLineOfSight)
        {
            return true;
        }

        // 3. Line-of-sight raycast from eye level to player center (blocked by floors / walls)
        Vector2 eyeOrigin = (Vector2)transform.position + Vector2.up * 0.4f;
        Vector2 targetCenter = (Vector2)targetPlayer.position + Vector2.up * 0.4f;
        Vector2 toPlayer = targetCenter - eyeOrigin;
        float dist = toPlayer.magnitude;

        if (dist > 0.1f)
        {
            RaycastHit2D hit = Physics2D.Raycast(eyeOrigin, toPlayer.normalized, dist, groundLayer);
            if (hit.collider != null && hit.collider.gameObject != targetPlayer.gameObject && !hit.collider.transform.IsChildOf(targetPlayer))
            {
                // Obstructed by floor or wall! Enemy cannot see through it
                return false;
            }
        }

        return true;
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        health -= damage;
        StartCoroutine(BlinkRed());

        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        UnregisterEnemyColliders();

        if (enemyCollider != null)
        {
            enemyCollider.enabled = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (animator != null)
        {
            animator.SetTrigger("Die");
            animator.Play("Death");
        }

        Destroy(gameObject, deathDuration);
    }

    private IEnumerator BlinkRed()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.12f);
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
        }
    }

    public bool IsLedgeAhead(float moveDirX)
    {
        if (!stopAtLedges || enemyType == EnemyType.Spirit) return false;
        if (Mathf.Abs(moveDirX) < 0.01f) return false;

        float dirSign = moveDirX > 0 ? 1f : -1f;
        Bounds bounds = enemyCollider != null ? enemyCollider.bounds : new Bounds(transform.position, Vector3.one);

        // Check slightly ahead of the front foot
        float frontX = bounds.center.x + dirSign * (bounds.extents.x + 0.15f);
        float footY = bounds.min.y + 0.15f;
        Vector2 checkOrigin = new Vector2(frontX, footY);
        float ledgeCheckDist = 0.8f;

        RaycastHit2D groundHit = Physics2D.Raycast(checkOrigin, Vector2.down, ledgeCheckDist, groundLayer);
        return groundHit.collider == null;
    }

    public bool IsWallAhead(float moveDirX)
    {
        if (enemyType == EnemyType.Spirit) return false;
        if (Mathf.Abs(moveDirX) < 0.01f) return false;

        float dirSign = moveDirX > 0 ? 1f : -1f;
        Bounds bounds = enemyCollider != null ? enemyCollider.bounds : new Bounds(transform.position, Vector3.one);

        float footY = bounds.min.y + 0.3f;
        Vector2 checkOrigin = new Vector2(bounds.center.x, footY);
        float checkDist = bounds.extents.x + 0.15f;

        RaycastHit2D wallHit = Physics2D.Raycast(checkOrigin, Vector2.right * dirSign, checkDist, groundLayer);
        return wallHit.collider != null;
    }

    private void Patrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Transform targetPoint = waypoints[currentPointIndex];
        if (targetPoint == null) return;

        float dirX = targetPoint.position.x - transform.position.x;
        float distanceX = Mathf.Abs(dirX);

        // If reached waypoint, or a cliff / wall is directly ahead, reverse patrol direction
        if (distanceX < 0.25f || IsLedgeAhead(dirX) || IsWallAhead(dirX))
        {
            currentPointIndex++;
            if (currentPointIndex >= waypoints.Length)
            {
                currentPointIndex = 0;
            }
            targetPoint = waypoints[currentPointIndex];
            if (targetPoint == null) return;
            dirX = targetPoint.position.x - transform.position.x;
        }

        // Only advance if path is clear of ledges and walls
        if (!IsLedgeAhead(dirX) && !IsWallAhead(dirX))
        {
            Vector2 targetPosition = new Vector2(targetPoint.position.x, transform.position.y);
            transform.position = Vector2.MoveTowards(transform.position, targetPosition, patrolSpeed * Time.deltaTime);
        }

        FlipSprite(targetPoint.position.x);
    }

    private void ChasePlayer()
    {
        if (targetPlayer == null) return;

        float dirX = targetPlayer.position.x - transform.position.x;

        // If configured to stop at ledges during chase, stop; otherwise charge forward (bait mechanic)
        if ((stopAtLedgesWhenChasing && IsLedgeAhead(dirX)) || IsWallAhead(dirX))
        {
            FlipSprite(targetPlayer.position.x);
            return;
        }

        // Optional waypoint clamping: don't chase far beyond patrol zone
        if (stayWithinWaypoints && waypoints != null && waypoints.Length >= 2)
        {
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            foreach (var wp in waypoints)
            {
                if (wp != null)
                {
                    minX = Mathf.Min(minX, wp.position.x);
                    maxX = Mathf.Max(maxX, wp.position.x);
                }
            }
            if (minX <= maxX)
            {
                if ((dirX < 0 && transform.position.x <= minX) ||
                    (dirX > 0 && transform.position.x >= maxX))
                {
                    FlipSprite(targetPlayer.position.x);
                    return;
                }
            }
        }

        Vector2 targetPosition = new Vector2(targetPlayer.position.x, transform.position.y);
        transform.position = Vector2.MoveTowards(transform.position, targetPosition, chaseSpeed * Time.deltaTime);
        FlipSprite(targetPlayer.position.x);
    }

    private void FlipSprite(float targetX)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = (transform.position.x - targetX) < 0;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider != null && (collision.collider.GetComponent<Enemy>() != null || collision.collider.GetComponentInParent<Enemy>() != null))
        {
            if (myColliders != null)
            {
                foreach (var myCol in myColliders)
                {
                    if (myCol != null)
                    {
                        Physics2D.IgnoreCollision(myCol, collision.collider, true);
                    }
                }
            }
            return;
        }

        CheckHazard(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.collider != null && (collision.collider.GetComponent<Enemy>() != null || collision.collider.GetComponentInParent<Enemy>() != null))
        {
            if (myColliders != null)
            {
                foreach (var myCol in myColliders)
                {
                    if (myCol != null)
                    {
                        Physics2D.IgnoreCollision(myCol, collision.collider, true);
                    }
                }
            }
            return;
        }

        CheckHazard(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        CheckHazard(collider.gameObject);
    }

    private void OnTriggerStay2D(Collider2D collider)
    {
        CheckHazard(collider.gameObject);
    }

    private void CheckHazard(GameObject hitObj)
    {
        if (isDead || enemyType == EnemyType.Spirit || hitObj == null) return;
        if (hitObj.CompareTag("Player") || hitObj.GetComponent<Enemy>() != null || hitObj.GetComponent<Arrow>() != null) return;

        bool isHazard = hitObj.CompareTag("Damage") ||
                        (hitObj.transform.parent != null && hitObj.transform.parent.CompareTag("Damage")) ||
                        hitObj.name.IndexOf("Damage", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        hitObj.name.IndexOf("Spike", System.StringComparison.OrdinalIgnoreCase) >= 0;

        if (isHazard)
        {
            Die();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(detectionRadius * 2f, maxVerticalDistance * 2f, 0.1f));

        // Draw ledge detection ray in editor
        if (enemyCollider != null)
        {
            float dirSign = (spriteRenderer != null && spriteRenderer.flipX) ? 1f : -1f;
            Bounds bounds = enemyCollider.bounds;
            float frontX = bounds.center.x + dirSign * (bounds.extents.x + 0.15f);
            float footY = bounds.min.y + 0.15f;
            Vector2 checkOrigin = new Vector2(frontX, footY);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(checkOrigin, checkOrigin + Vector2.down * 0.8f);
        }
    }
}
