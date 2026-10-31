using UnityEngine;

public class BackgroundFollow : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Камера, за якою слідує фон. Якщо порожньо — знайдеться автоматично.")]
    public Transform cameraTransform;

    [Header("Horizontal Follow (X)")]
    [Tooltip("1 = повністю слідує за камерою (небо завжди на екрані).\n0.8 - 0.9 = паралакс (небо рухається трохи повільніше за камеру, створюючи глибину).")]
    [Range(0f, 1f)]
    public float parallaxEffectX = 1f;

    [Header("Vertical Follow (Y)")]
    [Tooltip("Чи потрібно слідувати за камерою по вертикалі (при стрибках). Зазвичай для 2D-неба це вимкнено.")]
    public bool followY = false;

    [Range(0f, 1f)]
    public float parallaxEffectY = 0f;

    private float startPosX;
    private float startPosY;
    private float startCameraX;
    private float startCameraY;

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        startPosX = transform.position.x;
        startPosY = transform.position.y;

        if (cameraTransform != null)
        {
            startCameraX = cameraTransform.position.x;
            startCameraY = cameraTransform.position.y;
        }
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;

        // Зміщення по X залежно від переміщення камери
        float distanceX = (cameraTransform.position.x - startCameraX) * parallaxEffectX;
        float newPosX = startPosX + distanceX;

        float newPosY = transform.position.y;
        if (followY)
        {
            float distanceY = (cameraTransform.position.y - startCameraY) * parallaxEffectY;
            newPosY = startPosY + distanceY;
        }

        transform.position = new Vector3(newPosX, newPosY, transform.position.z);
    }
}
