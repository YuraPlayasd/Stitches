using UnityEngine;

public class Arrow : MonoBehaviour
{
    public float speed = 12f;
    public float lifeTime = 3f;
    public int damage = 1;

    [Header("Flight Distance Settings")]
    [Tooltip("Fixed horizontal distance the arrow flies before dropping down as a collectible pickup")]
    public float maxFlightDistance = 8f;

    private Vector2 startPosition;
    private bool hasStartPosition = false;

    private Rigidbody2D rb;
    private Collider2D col;
    private float direction = 1f;
    private bool initialized = false;
    private bool hasEnteredCameraView = false;
    private bool hasDroppedAsPickup = false;
    private float spawnTime;
    private Vector2 lastCheckedPos;

    /// <summary>
    /// Explicitly initializes flight direction and visual orientation.
    /// Sprite arrow tip is on the LEFT by default.
    /// dir > 0: flying right, rotation = 180 deg (points right)
    /// dir < 0: flying left, rotation = 0 deg (points left)
    /// </summary>
    public void Init(float dir)
    {
        direction = dir >= 0 ? 1f : -1f;
        transform.rotation = direction > 0 ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
        initialized = true;

        if (!hasStartPosition)
        {
            startPosition = transform.position;
            hasStartPosition = true;
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(direction * speed, 0f);
        }
    }

    private void Start()
    {
        spawnTime = Time.time;

        if (!hasStartPosition)
        {
            startPosition = transform.position;
            hasStartPosition = true;
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (col == null)
        {
            col = GetComponent<Collider2D>();
        }

        if (!initialized)
        {
            float zRot = transform.eulerAngles.z;
            direction = (zRot > 90f && zRot < 270f) ? 1f : -1f;
        }

        // Configure solid physics for flight so Box2D's Continuous Collision Detection physically halts against Tilemaps
        if (col != null)
        {
            col.isTrigger = false;
        }

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.linearVelocity = new Vector2(direction * speed, 0f);
        }

        // Ignore collisions with player to prevent pushing the player
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null && col != null)
        {
            foreach (var pCol in playerObj.GetComponentsInChildren<Collider2D>())
            {
                if (pCol != null)
                {
                    Physics2D.IgnoreCollision(col, pCol, true);
                }
            }
        }

        // Ignore collisions with other arrows
        Arrow[] otherArrows = Object.FindObjectsByType<Arrow>();
        if (col != null)
        {
            foreach (var other in otherArrows)
            {
                if (other != null && other != this)
                {
                    Collider2D otherCol = other.GetComponent<Collider2D>();
                    if (otherCol != null)
                    {
                        Physics2D.IgnoreCollision(col, otherCol, true);
                    }
                }
            }
        }

        lastCheckedPos = rb != null ? rb.position : (Vector2)transform.position;

        // Check if spawned directly inside/against a wall
        CheckCollisionAhead(0.05f);

        Invoke(nameof(OnLifeTimeExpired), lifeTime);
    }

    private void OnLifeTimeExpired()
    {
        if (hasDroppedAsPickup) return;
        Destroy(gameObject);
    }

    private void Update()
    {
        if (hasDroppedAsPickup) return;

        // Perform continuous sweep check every render frame for instant wall response
        CheckCollisionAhead(Time.deltaTime);
        if (hasDroppedAsPickup) return;

        // Check if fixed flight distance reached
        float traveled = Mathf.Abs(transform.position.x - startPosition.x);
        if (traveled >= maxFlightDistance)
        {
            DropAsCollectible();
            return;
        }

        Camera cam = Camera.main;
        if (cam != null && !hasDroppedAsPickup)
        {
            Vector3 vp = cam.WorldToViewportPoint(transform.position);
            bool inView = vp.z > 0f && vp.x >= -0.02f && vp.x <= 1.02f && vp.y >= -0.05f && vp.y <= 1.05f;

            if (inView)
            {
                hasEnteredCameraView = true;
            }
            else if (hasEnteredCameraView || Time.time - spawnTime > 0.35f)
            {
                // Flown beyond camera: drop as a collectible single arrow pointing upwards
                DropAsCollectible();
            }
        }
    }

    private void FixedUpdate()
    {
        if (hasDroppedAsPickup) return;

        CheckCollisionAhead(Time.fixedDeltaTime);
        if (hasDroppedAsPickup) return;

        // Check if fixed flight distance reached
        float traveled = Mathf.Abs(transform.position.x - startPosition.x);
        if (traveled >= maxFlightDistance)
        {
            DropAsCollectible();
            return;
        }

        if (rb != null && !hasDroppedAsPickup)
        {
            rb.linearVelocity = new Vector2(
                direction * speed,
                0f
            );
            lastCheckedPos = rb.position;
        }
        else
        {
            lastCheckedPos = (Vector2)transform.position;
        }
    }

    /// <summary>
    /// Sweeps forward across the path to guarantee no tunneling through thin Tilemap walls, outlines, or enemies.
    /// Uses multiple parallel rays (center, top, bottom) plus boxcast.
    /// </summary>
    private void CheckCollisionAhead(float dt)
    {
        if (hasDroppedAsPickup) return;

        Vector2 sweepDir = new Vector2(direction, 0f);
        Vector2 currentPos = rb != null ? rb.position : (Vector2)transform.position;
        float stepDist = speed * Mathf.Max(dt, 0.02f);
        float checkDist = stepDist + 0.35f;

        Vector2[] origins = new Vector2[]
        {
            currentPos,
            currentPos + new Vector2(0f, 0.22f),
            currentPos + new Vector2(0f, -0.22f)
        };

        RaycastHit2D bestHit = default;
        float bestDist = float.MaxValue;
        bool foundHit = false;

        // 1. Multi-ray sweep (mathematically guaranteed to intersect CompositeCollider outline edges)
        foreach (var o in origins)
        {
            Vector2 rayStart = o - sweepDir * 0.25f;
            RaycastHit2D[] hits = Physics2D.RaycastAll(rayStart, sweepDir, checkDist + 0.35f);
            foreach (var hit in hits)
            {
                if (!IsValidSweepHit(hit)) continue;

                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    bestHit = hit;
                    foundHit = true;
                }
            }
        }

        // 2. BoxCast sweep to cover any volume
        RaycastHit2D[] boxHits = Physics2D.BoxCastAll(currentPos, new Vector2(0.5f, 0.4f), 0f, sweepDir, checkDist);
        foreach (var hit in boxHits)
        {
            if (!IsValidSweepHit(hit)) continue;

            if (hit.distance < bestDist)
            {
                bestDist = hit.distance;
                bestHit = hit;
                foundHit = true;
            }
        }

        if (foundHit && bestHit.collider != null)
        {
            Vector2 contact = bestHit.point;
            if (contact == Vector2.zero)
            {
                contact = currentPos;
            }
            transform.position = contact - sweepDir * 0.35f;
            HandleHit(bestHit.collider.gameObject);
        }
    }

    private bool IsValidSweepHit(RaycastHit2D hit)
    {
        if (hit.collider == null) return false;
        if (hit.collider.gameObject == gameObject) return false;
        if (hit.collider.transform.IsChildOf(transform)) return false;

        // Ignore Player
        if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<Player>() != null)
            return false;

        // Ignore other arrows and arrow pickups
        if (hit.collider.GetComponent<Arrow>() != null || hit.collider.GetComponent<ArrowPickup>() != null)
            return false;

        // If it's an enemy, it's a valid hit!
        if (hit.collider.GetComponentInParent<Enemy>() != null)
            return true;

        // Ignore non-enemy triggers (dialogue triggers, camera boundaries, checkpoints)
        if (hit.collider.isTrigger)
            return false;

        // Solid wall, TileMap, platform, or obstacle
        return true;
    }

    /// <summary>
    /// When arrow exits camera view or hits a solid wall, it drops down with gravity as a collectible pickup.
    /// Sprite is oriented straight UP (-90 deg rotation).
    /// </summary>
    public void DropAsCollectible()
    {
        if (hasDroppedAsPickup) return;
        hasDroppedAsPickup = true;

        CancelInvoke(nameof(OnLifeTimeExpired));

        // Orient tip pointing UP
        // Arrow sprite tip is on the left (0 deg); -90 deg rotates it straight UP (+Y)
        transform.rotation = Quaternion.Euler(0f, 0f, -90f);

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 2f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (col == null) col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
            if (col is BoxCollider2D box)
            {
                box.size = new Vector2(0.8f, 1.6f);
                box.offset = Vector2.zero;
            }

            // Re-enable collisions with player so dropped arrow can be picked up
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                foreach (var pCol in playerObj.GetComponentsInChildren<Collider2D>())
                {
                    if (pCol != null)
                    {
                        Physics2D.IgnoreCollision(col, pCol, false);
                    }
                }
            }
        }

        // Add or enable ArrowPickup component
        ArrowPickup pickup = GetComponent<ArrowPickup>();
        if (pickup == null)
        {
            pickup = gameObject.AddComponent<ArrowPickup>();
        }
        pickup.arrowsToAdd = 1;
        pickup.isFalling = true;
        pickup.groundLayer = ~0; // all layers

        damage = 0; // Disable damage
        this.enabled = false; // Disable arrow projectile logic
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandleHit(collision.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.gameObject);
    }

    private void HandleHit(GameObject hitObj)
    {
        if (hasDroppedAsPickup || hitObj == null) return;
        if (hitObj == gameObject || hitObj.transform.IsChildOf(transform)) return;
        if (hitObj.CompareTag("Player") || hitObj.GetComponentInParent<Player>() != null) return;
        if (hitObj.GetComponent<Arrow>() != null || hitObj.GetComponent<ArrowPickup>() != null) return;

        Enemy enemy = hitObj.GetComponent<Enemy>();
        if (enemy == null)
        {
            enemy = hitObj.GetComponentInParent<Enemy>();
        }

        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // Hit a Tilemap, wall, or solid obstacle:
        // Do not fly through the TileMap; stop immediately and drop down as a collectible arrow
        DropAsCollectible();
    }
}
