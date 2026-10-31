using UnityEngine;

public class Arrow : MonoBehaviour
{
    public float speed = 12f;
    public float lifeTime = 3f;

    private Rigidbody2D rb;
    private float direction = 1f;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        direction = transform.right.x >= 0 ? 1f : -1f;

        Debug.Log("Arrow created!");
        Debug.Log("Direction: " + direction);
        Debug.Log("Rigidbody: " + rb);
        
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
        if (collision.CompareTag("Player"))
            return;

        Destroy(gameObject);
    }
}