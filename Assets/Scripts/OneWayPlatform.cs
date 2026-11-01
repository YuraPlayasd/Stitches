using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One-way (jump-through) platform.
/// Allows the player to jump through from underneath, stand on top,
/// and drop down through it by pressing Down (S or DownArrow).
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PlatformEffector2D))]
public class OneWayPlatform : MonoBehaviour
{
    [Header("Drop-Through Settings")]
    [Tooltip("Key to drop down through the platform")]
    public KeyCode dropKey = KeyCode.S;
    public KeyCode alternateDropKey = KeyCode.DownArrow;

    [Tooltip("Duration in seconds for which collision is disabled when dropping down")]
    public float dropDuration = 0.35f;

    private Collider2D platformCollider;
    private PlatformEffector2D effector;
    private bool playerOnPlatform = false;
    private Collider2D currentPlayerCol;

    private void Awake()
    {
        SetupComponents();
    }

    public void SetupComponents()
    {
        platformCollider = GetComponent<Collider2D>();
        if (platformCollider != null)
        {
            platformCollider.usedByEffector = true;
            platformCollider.isTrigger = false;
        }

        // If AreaEffector2D was mistakenly added instead of PlatformEffector2D, clean it up
        AreaEffector2D area = GetComponent<AreaEffector2D>();
        if (area != null)
        {
            if (Application.isPlaying)
            {
                Destroy(area);
            }
            else
            {
                DestroyImmediate(area);
            }
        }

        effector = GetComponent<PlatformEffector2D>();
        if (effector == null)
        {
            effector = gameObject.AddComponent<PlatformEffector2D>();
        }

        if (effector != null)
        {
            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
            effector.surfaceArc = 170f;
            effector.rotationalOffset = 0f;
            effector.useColliderMask = false;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SetupComponents();
    }

    private void Reset()
    {
        SetupComponents();
    }
#endif

    private void Update()
    {
        if (playerOnPlatform && (Input.GetKeyDown(dropKey) || Input.GetKeyDown(alternateDropKey)))
        {
            if (currentPlayerCol != null)
            {
                DropPlayer(currentPlayerCol);
            }
            else
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    Collider2D col = player.GetComponent<Collider2D>();
                    if (col != null) DropPlayer(col);
                }
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckPlayerCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckPlayerCollision(collision);
    }

    private void CheckPlayerCollision(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponentInParent<Player>() != null)
        {
            playerOnPlatform = true;
            currentPlayerCol = collision.collider;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponentInParent<Player>() != null)
        {
            playerOnPlatform = false;
            currentPlayerCol = null;
        }
    }

    public void DropPlayer(Collider2D playerCol)
    {
        if (playerCol == null || platformCollider == null) return;

        // Collect all colliders on player (BoxCollider2D, PolygonCollider2D, etc.)
        List<Collider2D> allCols = new List<Collider2D>();
        allCols.AddRange(playerCol.GetComponents<Collider2D>());
        if (playerCol.transform.parent != null)
        {
            allCols.AddRange(playerCol.transform.parent.GetComponents<Collider2D>());
        }

        StartCoroutine(DropPlayerThrough(allCols.ToArray()));
    }

    private IEnumerator DropPlayerThrough(Collider2D[] playerCols)
    {
        if (playerCols != null && platformCollider != null)
        {
            foreach (var col in playerCols)
            {
                if (col != null) Physics2D.IgnoreCollision(col, platformCollider, true);
            }
        }

        yield return new WaitForSeconds(dropDuration);

        if (playerCols != null && platformCollider != null)
        {
            foreach (var col in playerCols)
            {
                if (col != null) Physics2D.IgnoreCollision(col, platformCollider, false);
            }
        }
    }
}
