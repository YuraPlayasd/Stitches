using UnityEngine;
using UnityEngine.SceneManagement;

public class Door : MonoBehaviour
{
    [Header("Scene Transition Settings")]
    [Tooltip("Name of the scene to load upon entering the door (e.g. Level2 or LevelHub)")]
    public string nextSceneName = "Level2";

    [Tooltip("If enabled, loads scene by build index instead of name")]
    public bool loadByIndex = false;
    public int nextSceneIndex = 1;

    [Header("Interaction Settings")]
    [Tooltip("Key to enter the door")]
    public KeyCode interactKey = KeyCode.E;

    [Header("UI Prompt")]
    [Tooltip("UI prompt object on screen ('Press E - to Enter Level')")]
    public GameObject promptUI;

    [Header("Optional Door Visuals")]
    [Tooltip("Optional sprite for an opened door")]
    public Sprite openDoorSprite;

    private bool isPlayerNear = false;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Hide prompt at start
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }

    void Update()
    {
        // Check if player is near door and interact key pressed
        if (isPlayerNear && Input.GetKeyDown(interactKey))
        {
            EnterLevel();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerNear = true;

            if (promptUI != null)
            {
                promptUI.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            isPlayerNear = false;

            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }
        }
    }

    private void EnterLevel()
    {
        if (openDoorSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = openDoorSprite;
        }

        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }

        if (loadByIndex)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            int currentIndex = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(currentIndex + 1);
        }
    }
}
