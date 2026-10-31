using UnityEngine;
using System.Collections; 
using UnityEngine.SceneManagement; 
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    public int health = 100;

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

    public Image healthImage;

    private SpriteRenderer spriteRender;

    [Header("Shooting")] 
    public GameObject arrowPrefab;
    public Transform arrowSpawnPoint;
    public float arrowCooldown = 0.5f;

    private float nextArrowTime;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        extraJumps = extraJumpesValue;
        spriteRender = GetComponent<SpriteRenderer>();

        IntroCutscene.EnsureCutsceneExists();
    }

    void Update()
    {
        float moveInput = Input.GetAxis("Horizontal");
        if (Input.GetKeyDown(KeyCode.F))
        {
            Shoot();
        }
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        if (isGrounded)
        {
            extraJumps = extraJumpesValue;
        }

        jumpBufferCounter -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isGrounded)
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

        if (isGrounded && jumpBufferCounter > 0f)
        {
            Jump();
            jumpBufferCounter = 0f;
        }

        if (healthImage is not null)
    {
        healthImage.fillAmount = health / 100f;
    }

        SetAnimation(moveInput);
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpforce);
    }

    private void FixedUpdate()
    {
        isGrounded = Physics2D.OverlapCircle(GroundChek.position, GroundChekRadius, groundLayer);
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

        if (isGrounded)
        {
            if (moveInput == 0)
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
            if (rb.linearVelocityY > 0.1f)
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
        if (currentAnimation == newAnimation) return;

        animator.Play(newAnimation);
        currentAnimation = newAnimation;
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Damage"))
        {
            health -= 25;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 1.5f * jumpforce);
            StartCoroutine(BlinkRed());
            if (health <= 0)
            {
                Die();
            }
        }
    }

    private IEnumerator BlinkRed()
    {
        spriteRender.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        spriteRender.color = Color.white;
    }

    public void Die()
    {
        SceneManager.LoadScene("GameScene");
    }

    private void Shoot()
    {
        if (arrowPrefab == null)
        {
            return;
        }

        if (Time.time < nextArrowTime)
        {
            return;
        }
        
        nextArrowTime = Time.time + arrowCooldown;
        
        Vector3 spawnPos = arrowSpawnPoint != null ? arrowSpawnPoint.position : transform.position;
        GameObject arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);

        float shootDir = transform.localScale.x > 0 ? 1f : -1f;

        if (transform.localScale.x > 0)
        {
            arrow.transform.rotation = Quaternion.Euler(0, 0, 180);
        }
        else
        {
            arrow.transform.rotation = Quaternion.identity;
        }

        Rigidbody2D arrowRb = arrow.GetComponent<Rigidbody2D>();
        if (arrowRb != null)
        {
            float arrowSpeed = 10f;
            arrowRb.linearVelocity = new Vector2(shootDir * arrowSpeed, 0f);
        }
    }
    
}