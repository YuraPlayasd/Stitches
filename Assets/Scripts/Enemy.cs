using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Movement Speeds")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 3.5f;

    [Header("Patrol Waypoints")]
    public Transform[] waypoints;
    private int currentPointIndex;

    [Header("Target Tracking")]
    public Transform targetPlayer;
    public float detectionRadius = 5f;

    private SpriteRenderer spriteRenderer;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    void Update()
    {
        if (targetPlayer != null && Vector2.Distance(transform.position, targetPlayer.position) <= detectionRadius)
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }
    }

    private void Patrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Transform targetPoint = waypoints[currentPointIndex];

        if (Vector2.Distance(transform.position, targetPoint.position) < 0.25f)
        {
            currentPointIndex++;
            if (currentPointIndex >= waypoints.Length)
            {
                currentPointIndex = 0;
            }
        }

        transform.position = Vector2.MoveTowards(transform.position, targetPoint.position, patrolSpeed * Time.deltaTime);
        FlipSprite(targetPoint.position.x);
    }

    private void ChasePlayer()
    {
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}