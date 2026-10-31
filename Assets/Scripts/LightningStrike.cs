using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(AudioSource))]
public class LightningStrike : MonoBehaviour
{
    [Header("Pixel Art Lightning Settings")]
    [Tooltip("Core inner lightning width (in world units)")]
    public float corePixelWidth = 0.22f;
    [Tooltip("Outer cyan glow pixel width")]
    public float glowPixelWidth = 0.48f;
    [Tooltip("Vertical step height per pixel block")]
    public float pixelStepY = 0.45f;
    [Tooltip("Horizontal step amount for pixel stairs")]
    public float horizontalStep = 0.35f;
    [Tooltip("Color of inner core")]
    public Color coreColor = Color.white;
    [Tooltip("Color of outer electric border")]
    public Color glowColor = new Color(0.20f, 0.90f, 1.0f, 1f);
    [Tooltip("Total duration of lightning strike")]
    public float strikeDuration = 0.36f;
    [Tooltip("How often the pixel bolt re-rolls its jagged path")]
    public float flickerInterval = 0.045f;

    [Header("Screen Flash (Guaranteed Impact)")]
    [Tooltip("Canvas Image for UI flash (optional, OnGUI fallback also runs)")]
    public Image flashScreenImage;
    [Range(0.5f, 1f)]
    public float flashPeakAlpha = 1.0f;
    public float flashFadeDuration = 0.42f;

    [Header("Camera Shake")]
    public Camera targetCamera;
    public float shakeMagnitude = 0.35f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip thunderSound;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh lightningMesh;

    private float flashAlpha = 0f;
    private static Texture2D whiteFlashTexture;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        lightningMesh = new Mesh();
        lightningMesh.name = "PixelLightningMesh";
        meshFilter.mesh = lightningMesh;

        Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
        if (unlitShader != null)
        {
            Material mat = new Material(unlitShader);
            meshRenderer.material = mat;
        }

        meshRenderer.sortingLayerName = "Player";
        meshRenderer.sortingOrder = 25;
        meshRenderer.enabled = false;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound ensures full volume everywhere
        }

        // Load thunder audio if not already assigned
        LoadThunderAudio();
    }

    public void LoadThunderAudio()
    {
        if (thunderSound == null)
        {
            thunderSound = Resources.Load<AudioClip>("thunder");
        }

#if UNITY_EDITOR
        if (thunderSound == null)
        {
            thunderSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/thunder.wav")
                        ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/thunder_trimmed.wav")
                        ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/soundreality-thunder-city-377703.mp3");
        }
#endif
    }

    public void TriggerStrike(Vector3 startPoint, Vector3 hitPoint, System.Action onComplete = null)
    {
        StartCoroutine(StrikeRoutine(startPoint, hitPoint, onComplete));
    }

    private IEnumerator StrikeRoutine(Vector3 start, Vector3 end, System.Action onComplete)
    {
        // 1. Audio: Play crisp trimmed thunder immediately
        PlayThunder();

        // 2. Screen Flash: Trigger both Canvas Image & full-screen OnGUI flash
        StartCoroutine(FlashRoutine());

        // 3. Camera Shake
        StartCoroutine(CameraShakeRoutine(strikeDuration + 0.15f));

        // 4. Pixel Lightning Animation: Rapidly generate pixel steps & flicker
        meshRenderer.enabled = true;
        float elapsed = 0f;

        while (elapsed < strikeDuration)
        {
            GeneratePixelLightningMesh(start, end);
            yield return new WaitForSeconds(flickerInterval);
            elapsed += flickerInterval;
        }

        meshRenderer.enabled = false;
        lightningMesh.Clear();

        onComplete?.Invoke();
    }

    public void CancelStrike()
    {
        StopAllCoroutines();
        flashAlpha = 0f;
        if (flashScreenImage != null)
        {
            flashScreenImage.color = new Color(1f, 1f, 1f, 0f);
            flashScreenImage.gameObject.SetActive(false);
        }
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }
        if (lightningMesh != null)
        {
            lightningMesh.Clear();
        }
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private void PlayThunder()
    {
        if (thunderSound == null)
        {
            LoadThunderAudio();
        }

        if (thunderSound != null && audioSource != null)
        {
            audioSource.spatialBlend = 0f; // Pure 2D impact
            audioSource.volume = 1f;
            audioSource.clip = thunderSound;
            audioSource.time = 0f;
            audioSource.Play();
        }
    }

    /// <summary>
    /// Generates pure, stepped, orthogonal pixel-art quads from start to end point.
    /// Outer cyan glow quads are drawn first, followed by inner white core quads.
    /// </summary>
    private void GeneratePixelLightningMesh(Vector3 start, Vector3 end)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();

        // Generate the stepped spine points
        List<Vector2> spinePoints = GenerateSpinePoints(start, end);

        // 1. Draw outer cyan pixel glow
        DrawSteppedPath(spinePoints, glowPixelWidth, glowColor, vertices, triangles, colors);

        // 2. Draw side branches (pixel steps shooting outward)
        DrawBranches(spinePoints, glowPixelWidth * 0.7f, glowColor, vertices, triangles, colors);

        // 3. Draw inner white core
        DrawSteppedPath(spinePoints, corePixelWidth, coreColor, vertices, triangles, colors);

        // 4. Draw impact sparks at the table
        DrawImpactSparks(end, vertices, triangles, colors);

        lightningMesh.Clear();
        lightningMesh.SetVertices(vertices);
        lightningMesh.SetTriangles(triangles, 0);
        lightningMesh.SetColors(colors);
        lightningMesh.RecalculateBounds();
    }

    private List<Vector2> GenerateSpinePoints(Vector3 start, Vector3 end)
    {
        List<Vector2> points = new List<Vector2>();
        float currX = start.x;
        float currY = start.y;
        float targetX = end.x;
        float targetY = end.y;

        points.Add(new Vector2(currX, currY));

        int maxSteps = Mathf.CeilToInt((start.y - end.y) / pixelStepY) + 4;
        int stepCount = 0;

        while (currY > targetY && stepCount < maxSteps)
        {
            stepCount++;
            float nextY = Mathf.Max(currY - pixelStepY, targetY);

            // Calculate horizontal drift toward target with random pixel stair jumps
            float towardTarget = Mathf.Sign(targetX - currX);
            float rand = Random.value;
            float shift = 0f;

            if (rand < 0.45f)
            {
                // Step toward target
                shift = towardTarget * horizontalStep;
            }
            else if (rand < 0.75f)
            {
                // Zig-zag stair in opposite direction
                shift = -towardTarget * horizontalStep;
            }

            // If close to bottom, snap directly toward target
            if (currY - targetY < pixelStepY * 2f)
            {
                shift = (targetX - currX) * 0.5f;
            }

            currX += shift;
            currY = nextY;

            points.Add(new Vector2(currX, currY));
        }

        points.Add(new Vector2(targetX, targetY));
        return points;
    }

    private void DrawSteppedPath(List<Vector2> points, float width, Color col, List<Vector3> verts, List<int> tris, List<Color> colors)
    {
        float halfW = width * 0.5f;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 p1 = points[i];
            Vector2 p2 = points[i + 1];

            // 1. Vertical segment block
            AddPixelRect(p1.x - halfW, p1.x + halfW, p2.y - halfW, p1.y + halfW, col, verts, tris, colors);

            // 2. Horizontal step connector block
            if (Mathf.Abs(p2.x - p1.x) > 0.01f)
            {
                float minX = Mathf.Min(p1.x, p2.x) - halfW;
                float maxX = Mathf.Max(p1.x, p2.x) + halfW;
                AddPixelRect(minX, maxX, p2.y - halfW, p2.y + halfW, col, verts, tris, colors);
            }
        }
    }

    private void DrawBranches(List<Vector2> spinePoints, float branchWidth, Color col, List<Vector3> verts, List<int> tris, List<Color> colors)
    {
        int branchCount = Random.Range(2, 4);
        for (int b = 0; b < branchCount; b++)
        {
            int index = Random.Range(3, Mathf.Max(4, spinePoints.Count - 4));
            Vector2 origin = spinePoints[index];
            float dir = (Random.value > 0.5f) ? 1f : -1f;

            float bx = origin.x;
            float by = origin.y;
            int branchSteps = Random.Range(2, 4);

            for (int s = 0; s < branchSteps; s++)
            {
                float nextBx = bx + dir * horizontalStep;
                float nextBy = by - pixelStepY * 0.6f;

                // Horizontal block
                float minX = Mathf.Min(bx, nextBx) - branchWidth * 0.5f;
                float maxX = Mathf.Max(bx, nextBx) + branchWidth * 0.5f;
                AddPixelRect(minX, maxX, nextBy - branchWidth * 0.5f, nextBy + branchWidth * 0.5f, col, verts, tris, colors);

                // Vertical block
                AddPixelRect(nextBx - branchWidth * 0.5f, nextBx + branchWidth * 0.5f, nextBy - branchWidth * 0.5f, by + branchWidth * 0.5f, col, verts, tris, colors);

                bx = nextBx;
                by = nextBy;
            }
        }
    }

    private void DrawImpactSparks(Vector3 hitPoint, List<Vector3> verts, List<int> tris, List<Color> colors)
    {
        int sparkCount = 8;
        float sparkSize = 0.18f;

        for (int i = 0; i < sparkCount; i++)
        {
            float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad;
            float dist = Random.Range(0.4f, 1.4f);
            float sx = hitPoint.x + Mathf.Cos(angle) * dist;
            float sy = hitPoint.y + Mathf.Sin(angle) * dist * 0.6f;

            Color c = (i % 2 == 0) ? coreColor : glowColor;
            AddPixelRect(sx - sparkSize * 0.5f, sx + sparkSize * 0.5f, sy - sparkSize * 0.5f, sy + sparkSize * 0.5f, c, verts, tris, colors);
        }
    }

    private void AddPixelRect(float xMin, float xMax, float yMin, float yMax, Color col, List<Vector3> verts, List<int> tris, List<Color> colors)
    {
        int startIndex = verts.Count;

        verts.Add(new Vector3(xMin, yMin, 0));
        verts.Add(new Vector3(xMax, yMin, 0));
        verts.Add(new Vector3(xMax, yMax, 0));
        verts.Add(new Vector3(xMin, yMax, 0));

        colors.Add(col);
        colors.Add(col);
        colors.Add(col);
        colors.Add(col);

        tris.Add(startIndex);
        tris.Add(startIndex + 1);
        tris.Add(startIndex + 2);

        tris.Add(startIndex);
        tris.Add(startIndex + 2);
        tris.Add(startIndex + 3);
    }

    private IEnumerator FlashRoutine()
    {
        flashAlpha = flashPeakAlpha;

        if (flashScreenImage != null)
        {
            flashScreenImage.gameObject.SetActive(true);
            Color c = Color.white;
            c.a = flashAlpha;
            flashScreenImage.color = c;
        }

        // Hold solid blinding white for 0.06s for tremendous punch/impact
        yield return new WaitForSeconds(0.06f);

        float elapsed = 0f;
        while (elapsed < flashFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flashFadeDuration;
            // Quadratic falloff gives a sharp initial hit and smooth cinematic tail
            flashAlpha = Mathf.Lerp(flashPeakAlpha, 0f, t * t);

            if (flashScreenImage != null)
            {
                Color c = Color.white;
                c.a = flashAlpha;
                flashScreenImage.color = c;
            }

            yield return null;
        }

        flashAlpha = 0f;
        if (flashScreenImage != null)
        {
            flashScreenImage.gameObject.SetActive(false);
        }
    }

    void OnGUI()
    {
        // Guaranteed fullscreen blit: directly covers the entire screen with pure white flash
        if (flashAlpha > 0.005f)
        {
            if (whiteFlashTexture == null)
            {
                whiteFlashTexture = new Texture2D(1, 1);
                whiteFlashTexture.SetPixel(0, 0, Color.white);
                whiteFlashTexture.Apply();
            }

            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, flashAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteFlashTexture, ScaleMode.StretchToFill);
            GUI.color = prev;
        }
    }

    private IEnumerator CameraShakeRoutine(float duration)
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) yield break;

        Vector3 originalPos = targetCamera.transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float decay = 1f - (elapsed / duration);
            float x = Random.Range(-1f, 1f) * shakeMagnitude * decay;
            float y = Random.Range(-1f, 1f) * shakeMagnitude * decay;
            targetCamera.transform.position = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        targetCamera.transform.position = originalPos;
    }
}
