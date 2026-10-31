using UnityEngine;
using UnityEngine.SceneManagement;

public class Door : MonoBehaviour
{
    [Header("Scene Transition Settings")]
    [Tooltip("Назва сцени наступного рівня, на яку треба перейти (наприклад, Level2)")]
    public string nextSceneName = "Level2";

    [Tooltip("Якщо увімкнено, завантажує сцену за індексом у Build Settings")]
    public bool loadByIndex = false;
    public int nextSceneIndex = 1;

    [Header("Interaction Settings")]
    [Tooltip("Клавіша для входу в двері")]
    public KeyCode interactKey = KeyCode.E;

    [Header("UI Prompt")]
    [Tooltip("Об'єкт тексту або підказки на екрані ('Press E - to Enter Level')")]
    public GameObject promptUI;

    [Header("Optional Door Visuals")]
    [Tooltip("Необов'язковий спрайт відкритих дверей")]
    public Sprite openDoorSprite;

    private bool isPlayerNear = false;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Ховаємо підказку на старті
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }

    void Update()
    {
        // Перевіряємо, чи гравець біля дверей і чи натиснута клавіша E
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
