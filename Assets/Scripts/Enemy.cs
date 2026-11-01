using System.Collections;
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
    public float detectionRadius = 5f;

    [Header("Death Animation")]
    public float deathDuration = 0.65f;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Collider2D enemyCollider;
    private Rigidbody2D rb;
    private bool isDead = false;

    void Awake()
    {
        InitializeHealth();
    }

    void Start()
    {
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

        if (targetPlayer != null && Vector2.Distance(transform.position, targetPlayer.position) <= detectionRadius)
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }
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

    private void Patrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Transform targetPoint = waypoints[currentPointIndex];
        if (targetPoint == null) return;

        // Check distance purely along X axis
        float distanceX = Mathf.Abs(transform.position.x - targetPoint.position.x);

        if (distanceX < 0.2f)
        {
            currentPointIndex++;
            if (currentPointIndex >= waypoints.Length)
            {
                currentPointIndex = 0;
            }
            targetPoint = waypoints[currentPointIndex];
            if (targetPoint == null) return;
        }

        Vector2 targetPosition = new Vector2(targetPoint.position.x, transform.position.y);
        transform.position = Vector2.MoveTowards(transform.position, targetPosition, patrolSpeed * Time.deltaTime);

        FlipSprite(targetPoint.position.x);
    }

    private void ChasePlayer()
    {
        if (targetPlayer == null) return;

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
        CheckHazard(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
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
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
