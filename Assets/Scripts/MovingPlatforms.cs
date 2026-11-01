using UnityEngine;

public class MovingPlatforms : MonoBehaviour
{
    public float speed = 2f;
    public Transform[] points;
    private int i;
    private Rigidbody2D rb;

    public Vector2 Velocity { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Start()
    {
        // Unparent any Player child on start (nesting Rigidbody2D breaks Box2D physics)
        for (int childIdx = transform.childCount - 1; childIdx >= 0; childIdx--)
        {
            Transform child = transform.GetChild(childIdx);
            if (child.CompareTag("Player"))
            {
                child.SetParent(null);
            }
        }

        if (points != null && points.Length > 0 && points[0] != null)
        {
            transform.position = points[0].position;
            if (rb != null)
            {
                rb.position = points[0].position;
            }
        }
    }

    private void FixedUpdate()
    {
        if (points == null || points.Length == 0)
        {
            Velocity = Vector2.zero;
            return;
        }

        Transform targetPoint = points[i];
        if (targetPoint == null)
        {
            Velocity = Vector2.zero;
            return;
        }

        Vector2 currentPos = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 targetPos = targetPoint.position;

        if (Vector2.Distance(currentPos, targetPos) < 0.05f)
        {
            i++;
            if (i >= points.Length)
            {
                i = 0;
            }
            targetPoint = points[i];
            if (targetPoint == null)
            {
                Velocity = Vector2.zero;
                return;
            }
            targetPos = targetPoint.position;
        }

        Vector2 nextPos = Vector2.MoveTowards(currentPos, targetPos, speed * Time.fixedDeltaTime);
        Velocity = (nextPos - currentPos) / Time.fixedDeltaTime;

        if (rb != null)
        {
            rb.MovePosition(nextPos);
        }
        else
        {
            transform.position = nextPos;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Do NOT parent the player. A Rigidbody2D child of another Rigidbody2D breaks Box2D physics,
        // distorts scale, and fights world velocities. Player inherits platform Velocity instead.
        if (collision.gameObject.CompareTag("Player") && collision.transform.parent == transform)
        {
            collision.transform.SetParent(null);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && collision.transform.parent == transform)
        {
            collision.transform.SetParent(null);
        }
    }
}
