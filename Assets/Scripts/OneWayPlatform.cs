using System.Collections;
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

    void Awake()
    {
        platformCollider = GetComponent<Collider2D>();
        effector = GetComponent<PlatformEffector2D>();

        if (platformCollider != null)
        {
            platformCollider.usedByEffector = true;
        }

        if (effector != null)
        {
            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
            effector.surfaceArc = 170f;
            effector.rotationalOffset = 0f;
        }
    }

    public void DropPlayer(Collider2D playerCol)
    {
        if (playerCol != null && platformCollider != null)
        {
            StartCoroutine(DropPlayerThrough(playerCol));
        }
    }

    private IEnumerator DropPlayerThrough(Collider2D playerCol)
    {
        Physics2D.IgnoreCollision(playerCol, platformCollider, true);
        yield return new WaitForSeconds(dropDuration);
        if (playerCol != null && platformCollider != null)
        {
            Physics2D.IgnoreCollision(playerCol, platformCollider, false);
        }
    }
}
