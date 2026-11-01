using UnityEngine;

public class BackgroundFollow : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Camera for the background to follow. If empty, Camera.main will be found automatically.")]
    public Transform cameraTransform;

    [Header("Horizontal Follow (X)")]
    [Tooltip("1 = fully follows camera (sky stays locked on screen).\n0.8 - 0.9 = parallax (sky moves slightly slower than camera, creating depth).")]
    [Range(0f, 1f)]
    public float parallaxEffectX = 1f;

    [Header("Vertical Follow (Y)")]
    [Tooltip("Whether to follow camera vertically (during jumps).")]
    public bool followY = true;

    [Range(0f, 1f)]
    public float parallaxEffectY = 1f;

    private float startPosX;
    private float startPosY;
    private float startCameraX;
    private float startCameraY;

    void Start()
    {
        FindCamera();
    }

    private void FindCamera()
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
        if (cameraTransform == null)
        {
            FindCamera();
            if (cameraTransform == null) return;
        }

        // X offset based on camera movement
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
