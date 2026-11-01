using UnityEngine;

public class Arrow : MonoBehaviour
{
    public float speed = 12f;
    public float lifeTime = 3f;
    public int damage = 1;

    private Rigidbody2D rb;
    private float direction = 1f;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        direction = transform.right.x >= 0 ? 1f : -1f;

        Destroy(gameObject, lifeTime);
    }

    private void FixedUpdate()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(
                direction * speed,
                rb.linearVelocity.y
            );
        }
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
        if (hitObj == null || hitObj.CompareTag("Player"))
            return;

        Enemy enemy = hitObj.GetComponent<Enemy>();
        if (enemy == null)
        {
            enemy = hitObj.GetComponentInParent<Enemy>();
        }

        if (enemy != null)
        {
            enemy.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}
