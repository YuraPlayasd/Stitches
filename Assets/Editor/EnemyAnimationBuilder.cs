using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyAnimationBuilder
{
    static EnemyAnimationBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists("Assets/Animations/Spirit_run.anim") ||
                !File.Exists("Assets/Animations/Sceleton_run.anim") ||
                !File.Exists("Assets/Animations/Zombie_run.anim") ||
                !File.Exists("Assets/Animations/Enemy_Death.anim") ||
                !File.Exists("Assets/Prefabs/Enemy_Zombie.prefab"))
            {
                GenerateAll();
            }
        };
    }

    [MenuItem("Tools/Generate Enemy Animations, Controllers and Prefabs", false, 10)]
    public static void GenerateAll()
    {
        EnsureFolder("Assets/Animations");
        EnsureFolder("Assets/Resources/EnemyControllers");
        EnsureFolder("Assets/Prefabs");

        // 1. Create Death Animation
        AnimationClip deathClip = CreateDeathAnimation();

        // 2. Create Sceleton Animation & Controller (Medium speed ~8 fps)
        AnimationClip sceletonClip = CreateSpriteAnimation(
            "Assets/Animations/Sceleton_run.anim",
            "Assets/Art/Enemy/Sceleton",
            new[] { "sprite_0.png", "sprite_1.png", "sprite_2.png", "sprite_3.png" },
            8f, // Medium speed
            true
        );
        CreateEnemyController("Assets/Animations/Sceleton.controller", sceletonClip, deathClip);
        CreateEnemyController("Assets/Resources/EnemyControllers/Sceleton.controller", sceletonClip, deathClip);

        // 3. Create Spirit Animation & Controller (Fast speed ~14 fps, strictly 00-06!)
        AnimationClip spiritClip = CreateSpriteAnimation(
            "Assets/Animations/Spirit_run.anim",
            "Assets/Art/Enemy/Spirit",
            new[] { "sprite_00.png", "sprite_01.png", "sprite_02.png", "sprite_03.png", "sprite_04.png", "sprite_05.png", "sprite_06.png" },
            14f, // Fast swinging pickaxe
            true
        );
        CreateEnemyController("Assets/Animations/Spirit.controller", spiritClip, deathClip);
        CreateEnemyController("Assets/Resources/EnemyControllers/Spirit.controller", spiritClip, deathClip);

        // 4. Create Zombie Animation & Controller (Slow dancing speed ~3.5 fps)
        AnimationClip zombieClip = CreateSpriteAnimation(
            "Assets/Animations/Zombie_run.anim",
            "Assets/Art/Enemy/Zombie",
            new[] { "sprite_0.png", "sprite_1.png", "sprite_2.png" },
            3.5f, // Slow dancing
            true
        );
        CreateEnemyController("Assets/Animations/Zombie.controller", zombieClip, deathClip);
        CreateEnemyController("Assets/Resources/EnemyControllers/Zombie.controller", zombieClip, deathClip);

        // 5. Create Enemy Prefabs
        CreateEnemyPrefabs();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green><b>[EnemyAnimationBuilder]</b> All enemy animations, controllers, and prefabs generated successfully!</color>");
    }

    public static void CreateEnemyPrefabs()
    {
        EnsureFolder("Assets/Prefabs");
        CreateOrUpdateEnemyPrefab("Enemy_Zombie", EnemyType.Zombie, "Assets/Animations/Zombie.controller", "Assets/Art/Enemy/Zombie/sprite_0.png", 1);
        CreateOrUpdateEnemyPrefab("Enemy_Skeleton", EnemyType.Skeleton, "Assets/Animations/Sceleton.controller", "Assets/Art/Enemy/Sceleton/sprite_0.png", 2);
        CreateOrUpdateEnemyPrefab("Enemy_Spirit", EnemyType.Spirit, "Assets/Animations/Spirit.controller", "Assets/Art/Enemy/Spirit/sprite_00.png", 3);
    }

    private static void CreateOrUpdateEnemyPrefab(string name, EnemyType type, string controllerPath, string initialSpritePath, int health)
    {
        string prefabPath = $"Assets/Prefabs/{name}.prefab";
        if (File.Exists(prefabPath)) return;

        GameObject go = new GameObject(name);
        go.tag = "Damage";

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        Sprite sp = LoadSprite(initialSpritePath);
        if (sp != null) sr.sprite = sp;
        sr.color = Color.white;
        sr.sortingOrder = 4;

        Animator anim = go.AddComponent<Animator>();
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        if (controller != null) anim.runtimeAnimatorController = controller;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        if (sp != null)
        {
            col.size = sp.bounds.size;
        }
        else
        {
            col.size = new Vector2(1.5f, 2.5f);
        }

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 1f;

        Enemy enemy = go.AddComponent<Enemy>();
        enemy.enemyType = type;
        enemy.maxHealth = health;
        enemy.health = health;

        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
    }

    private static AnimationClip CreateDeathAnimation()
    {
        string[] deathFiles = new[]
        {
            "sprite_0.png", "sprite_1.png", "sprite_2.png", "sprite_3.png",
            "sprite_4.png", "sprite_5.png", "sprite_6.png"
        };

        return CreateSpriteAnimation(
            "Assets/Animations/Enemy_Death.anim",
            "Assets/Art/Death_Anim",
            deathFiles,
            12f, // 12 fps
            false // Non-looping
        );
    }

    private static AnimationClip CreateSpriteAnimation(string clipPath, string folder, string[] fileNames, float fps, bool loop)
    {
        List<Sprite> sprites = new List<Sprite>();
        foreach (string fileName in fileNames)
        {
            string fullPath = Path.Combine(folder, fileName).Replace('\\', '/');
            Sprite sp = LoadSprite(fullPath);
            if (sp != null)
            {
                sprites.Add(sp);
            }
            else
            {
                Debug.LogWarning($"[EnemyAnimationBuilder] Could not load sprite at: {fullPath}");
            }
        }

        if (sprites.Count == 0)
        {
            Debug.LogError($"[EnemyAnimationBuilder] No sprites found for: {clipPath}");
            return null;
        }

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.frameRate = 60f;

        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        float frameDuration = 1f / fps;
        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count + (loop ? 1 : 0)];

        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i * frameDuration,
                value = sprites[i]
            };
        }

        if (loop)
        {
            // End frame holding last frame until exact loop duration
            keyframes[sprites.Count] = new ObjectReferenceKeyframe
            {
                time = sprites.Count * frameDuration,
                value = sprites[sprites.Count - 1]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);

        // Configure loop settings
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        if (loop)
        {
            settings.stopTime = sprites.Count * frameDuration;
        }
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void CreateEnemyController(string controllerPath, AnimationClip runClip, AnimationClip deathClip)
    {
        if (runClip == null) return;

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        // Add or ensure parameters
        if (!controller.parameters.Any(p => p.name == "Die"))
        {
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        // Clear existing states to ensure fresh build
        foreach (var childState in stateMachine.states.ToArray())
        {
            stateMachine.RemoveState(childState.state);
        }

        // Add Run state
        AnimatorState runState = stateMachine.AddState("Run");
        runState.motion = runClip;
        stateMachine.defaultState = runState;

        // Add Death state
        if (deathClip != null)
        {
            AnimatorState deathState = stateMachine.AddState("Death");
            deathState.motion = deathClip;

            // Transition from AnyState to Death
            AnimatorStateTransition deathTransition = stateMachine.AddAnyStateTransition(deathState);
            deathTransition.AddCondition(AnimatorConditionMode.If, 0, "Die");
            deathTransition.hasExitTime = false;
            deathTransition.duration = 0f;
        }

        EditorUtility.SetDirty(controller);
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;

        Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var obj in all)
        {
            if (obj is Sprite sp) return sp;
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
