using System.Collections;
using UnityEngine;

/// <summary>
/// Collectible arrow pickup.
/// Supports both standing arrow bundles (+3) and single fallen arrows (+1) that drop with physics.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ArrowPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    [Tooltip("Number of arrows added to the player upon collection")]
    public int arrowsToAdd = 3;

    [Header("Falling Physics")]
    public bool isFalling = false;
    public LayerMask groundLayer = ~0;

    [Header("Bobbing Animation")]
    public bool enableBobbing = true;
    public float bobbingSpeed = 2.5f;
    public float bobbingHeight = 0.12f;

    private Vector3 initialPosition;
    private bool isCollected = false;
    private Rigidbody2D rb;
    private Collider2D col;
    private bool hasLanded = false;

    private void Awake()
    {
        if (groundLayer.value == 0)
        {
            groundLayer = ~0;
        }
    }

    private void Start()
    {
        initialPosition = transform.position;

        col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        rb = GetComponent<Rigidbody2D>();
        if (isFalling)
        {
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }
            rb.gravityScale = 2f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // Check if already on or inside ground at spawn
            CheckGroundAhead(0.5f);
        }
        else
        {
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f;
            }
            hasLanded = true;
        }
    }

    private void Update()
    {
        if (isCollected) return;

        if (hasLanded && enableBobbing)
        {
            float newY = initialPosition.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
            transform.position = new Vector3(initialPosition.x, newY, initialPosition.z);
        }
    }

    private void FixedUpdate()
    {
        if (isCollected || !isFalling || hasLanded) return;

        float fallSpeed = rb != null ? Mathf.Abs(rb.linearVelocity.y) : 6f;
        float checkDist = Mathf.Max(0.6f, fallSpeed * Time.fixedDeltaTime + 0.35f);

        CheckGroundAhead(checkDist);

        // Despawn if fallen way into the void
        if (transform.position.y < -60f)
        {
            Destroy(gameObject);
        }
    }

    private void CheckGroundAhead(float distance)
    {
        if (hasLanded || isCollected) return;

        Vector2 pos = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 startPoint = pos + Vector2.up * 0.25f;

        Vector2[] origins = new Vector2[]
        {
            startPoint,
            startPoint + Vector2.left * 0.2f,
            startPoint + Vector2.right * 0.2f
        };

        RaycastHit2D bestHit = default;
        float bestDist = float.MaxValue;
        bool foundGround = false;

        foreach (var o in origins)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(o, Vector2.down, distance + 0.3f, groundLayer);
            foreach (var hit in hits)
            {
                if (!IsValidGroundHit(hit)) continue;

                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    bestHit = hit;
                    foundGround = true;
                }
            }
        }

        // Also BoxCast downward
        RaycastHit2D[] boxHits = Physics2D.BoxCastAll(startPoint, new Vector2(0.4f, 0.2f), 0f, Vector2.down, distance, groundLayer);
        foreach (var hit in boxHits)
        {
            if (!IsValidGroundHit(hit)) continue;

            if (hit.distance < bestDist)
            {
                bestDist = hit.distance;
                bestHit = hit;
                foundGround = true;
            }
        }

        if (foundGround && bestHit.collider != null)
        {
            bool isSpike = IsSpikeOrPlantOrDamage(bestHit.collider, bestHit.point);
            float offset = isSpike ? 1.25f : 1.05f;
            LandOnGround(bestHit.point.y + offset);
        }
    }

    private bool IsValidGroundHit(RaycastHit2D hit)
    {
        if (hit.collider == null) return false;
        if (hit.collider.gameObject == gameObject) return false;
        if (hit.collider.transform.IsChildOf(transform)) return false;

        // Ignore Player
        if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<Player>() != null)
            return false;

        // Ignore other arrows/pickups
        if (hit.collider.GetComponent<Arrow>() != null || hit.collider.GetComponent<ArrowPickup>() != null)
            return false;

        // Ignore triggers (only land on solid ground / TileMap / platforms)
        if (hit.collider.isTrigger)
            return false;

        return true;
    }

    public void LandOnGround(float targetY)
    {
        if (hasLanded) return;
        hasLanded = true;
        isFalling = false;

        transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
        initialPosition = transform.position;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (col == null) col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    public static bool IsSpikeOrPlantOrDamage(Collider2D col, Vector3 position)
    {
        if (col == null) return false;

        // 1. Tag checks
        if (col.CompareTag("Damage")) return true;
        if (col.transform.parent != null && col.transform.parent.CompareTag("Damage")) return true;

        // 2. Name checks
        string colName = col.name;
        if (colName.IndexOf("Damage", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Spike", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Plant", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Hazard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("Thorn", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (col.transform.parent != null)
        {
            string pName = col.transform.parent.name;
            if (pName.IndexOf("Damage", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                pName.IndexOf("Spike", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                pName.IndexOf("Plant", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                pName.IndexOf("Hazard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                pName.IndexOf("Thorn", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        // 3. Tilemap tile inspection at hit position
        UnityEngine.Tilemaps.Tilemap tilemap = col.GetComponent<UnityEngine.Tilemaps.Tilemap>();
        if (tilemap == null)
        {
            tilemap = col.GetComponentInParent<UnityEngine.Tilemaps.Tilemap>();
        }

        if (tilemap != null)
        {
            Vector3Int cellPos = tilemap.WorldToCell(position);
            Vector3Int[] candidateCells = new Vector3Int[]
            {
                cellPos,
                cellPos + Vector3Int.down,
                cellPos + Vector3Int.left,
                cellPos + Vector3Int.right
            };

            foreach (var cell in candidateCells)
            {
                var tile = tilemap.GetTile(cell);
                if (tile != null)
                {
                    string tileName = tile.name;
                    if (tileName.IndexOf("Spike", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        tileName.IndexOf("Plant", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        tileName.IndexOf("Damage", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        tileName.IndexOf("Thorn", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isFalling && !hasLanded && collision != null)
        {
            if (!collision.isTrigger && !collision.CompareTag("Player") && collision.GetComponentInParent<Player>() == null &&
                collision.GetComponent<Arrow>() == null && collision.GetComponent<ArrowPickup>() == null)
            {
                // Only land if arrow is above the collider's top surface
                float topY = collision.bounds.max.y;
                if (transform.position.y >= topY - 0.25f)
                {
                    bool isSpike = IsSpikeOrPlantOrDamage(collision, transform.position);
                    float offset = isSpike ? 1.25f : 1.05f;
                    LandOnGround(topY + offset);
                    return;
                }
                // If hitting the side of a wall/plant while falling, do not freeze; let gravity continue pulling down
            }
        }

        TryCollect(collision.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isFalling && !hasLanded && collision != null && collision.gameObject != null)
        {
            if (!collision.gameObject.CompareTag("Player") && collision.gameObject.GetComponentInParent<Player>() == null &&
                collision.gameObject.GetComponent<Arrow>() == null && collision.gameObject.GetComponent<ArrowPickup>() == null)
            {
                // Check if hit surface is facing upward (normal.y > 0.4f)
                if (collision.contactCount > 0 && collision.GetContact(0).normal.y > 0.4f)
                {
                    Vector2 contact = collision.GetContact(0).point;
                    bool isSpike = IsSpikeOrPlantOrDamage(collision.collider, contact);
                    float offset = isSpike ? 1.25f : 1.05f;
                    LandOnGround(contact.y + offset);
                    return;
                }
                else if (transform.position.y >= collision.collider.bounds.max.y - 0.25f)
                {
                    float topY = collision.collider.bounds.max.y;
                    bool isSpike = IsSpikeOrPlantOrDamage(collision.collider, transform.position);
                    float offset = isSpike ? 1.25f : 1.05f;
                    LandOnGround(topY + offset);
                    return;
                }
            }
        }

        TryCollect(collision.gameObject);
    }

    private void TryCollect(GameObject target)
    {
        if (isCollected || target == null) return;

        Player player = target.GetComponent<Player>();
        if (player == null)
        {
            player = target.GetComponentInParent<Player>();
        }

        if (player != null)
        {
            isCollected = true;
            player.AddArrows(arrowsToAdd);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Spawns a single fallen arrow pickup pointing straight UP (+1 arrow on collect).
    /// </summary>
    public static GameObject SpawnSingleArrowPickup(Vector3 position, Transform parent = null, bool isFalling = false)
    {
        GameObject obj = new GameObject("SingleArrowPickup");
        if (parent != null) obj.transform.SetParent(parent);
        obj.transform.position = position;
        // Arrow sprite tip is on the left by default (0 deg).
        // -90 deg rotation around Z rotates tip straight UP (+Y).
        obj.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
        obj.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        Sprite arrowSprite = Resources.Load<Sprite>("Art/Arrow/sprite_0") ?? Resources.Load<Sprite>("sprite_0");
        if (arrowSprite == null)
        {
            GameObject tempArrow = Resources.Load<GameObject>("Arrow") ?? Resources.Load<GameObject>("Prefabs/Arrow");
            if (tempArrow != null)
            {
                SpriteRenderer tempSr = tempArrow.GetComponent<SpriteRenderer>();
                if (tempSr != null) arrowSprite = tempSr.sprite;
            }
        }
        sr.sprite = arrowSprite;
        sr.sortingLayerName = "Player";
        sr.sortingOrder = 5;
        sr.color = Color.white;

        BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(0.5f, 1.4f);

        Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        ArrowPickup pickup = obj.AddComponent<ArrowPickup>();
        pickup.arrowsToAdd = 1;
        pickup.groundLayer = ~0;

        if (isFalling)
        {
            rb.gravityScale = 2f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            pickup.isFalling = true;
        }
        else
        {
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            pickup.isFalling = false;
            pickup.LandOnGround(position.y);
        }

        return obj;
    }
}
