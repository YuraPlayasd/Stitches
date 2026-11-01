using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages looping ambient background music for Level 1 and Level 2.
/// Automatically switches between level music tracks on scene load.
/// </summary>
public class LevelAmbientMusic : MonoBehaviour
{
    public static LevelAmbientMusic Instance { get; private set; }

    [Header("Ambient Audio Settings")]
    public AudioClip level1AmbientClip;
    public AudioClip level2AmbientClip;
    [Range(0f, 1f)]
    [Tooltip("Ambient background music volume - kept softer than footsteps")]
    public float ambientVolume = 0.28f;

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // Pure 2D sound
        audioSource.volume = ambientVolume;

        EnsureAudioClips();
    }

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        CheckSceneAmbient(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckSceneAmbient(scene.name);
    }

    public void EnsureAudioClips()
    {
        if (level1AmbientClip == null)
        {
            level1AmbientClip = Resources.Load<AudioClip>("Audio/level1_ambient");
            if (level1AmbientClip == null)
            {
                level1AmbientClip = Resources.Load<AudioClip>("level1_ambient");
            }
        }

        if (level2AmbientClip == null)
        {
            level2AmbientClip = Resources.Load<AudioClip>("Audio/level2_embient");
            if (level2AmbientClip == null)
            {
                level2AmbientClip = Resources.Load<AudioClip>("level2_embient");
            }
        }
    }

    public static bool IsLevel1(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        return sceneName.IndexOf("Level 1", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               sceneName.IndexOf("Level1", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               sceneName.IndexOf("GameScene", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public void CheckSceneAmbient(string sceneName)
    {
        EnsureAudioClips();

        AudioClip targetClip = null;

        if (IsLevel1(sceneName))
        {
            targetClip = level1AmbientClip;
        }
        else if (BodyPartsTracker.IsHubOrLevel2(sceneName))
        {
            targetClip = level2AmbientClip;
        }

        if (targetClip != null)
        {
            if (audioSource != null)
            {
                if (!audioSource.isPlaying || audioSource.clip != targetClip)
                {
                    audioSource.clip = targetClip;
                    audioSource.loop = true;
                    audioSource.volume = ambientVolume;
                    audioSource.Play();
                }
            }
        }
        else
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureAmbientExists()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("LevelAmbientMusic");
            go.AddComponent<LevelAmbientMusic>();
        }
    }
}
