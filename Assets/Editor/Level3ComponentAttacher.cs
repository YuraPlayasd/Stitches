#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Level3ComponentAttacher
{
    static Level3ComponentAttacher()
    {
        EditorApplication.delayCall += AttachComponentsToLevel3;
    }

    [MenuItem("Tools/Attach Level 3 Scripts (Safe)")]
    public static void AttachComponentsToLevel3()
    {
        string level3Path = "Assets/Scenes/Level 3.unity";
        Scene currentScene = SceneManager.GetActiveScene();
        bool wasOpen = currentScene.path == level3Path;

        Scene level3Scene;
        if (wasOpen)
        {
            level3Scene = currentScene;
        }
        else
        {
            level3Scene = EditorSceneManager.OpenScene(level3Path, OpenSceneMode.Single);
        }

        if (!level3Scene.IsValid())
        {
            Debug.LogError("[Level3ComponentAttacher] Could not open Level 3 scene!");
            return;
        }

        bool modified = false;

        // 1. Find Torso GameObject
        GameObject torso = GameObject.Find("Torso");
        if (torso == null)
        {
            foreach (var root in level3Scene.GetRootGameObjects())
            {
                if (root.name == "Torso") { torso = root; break; }
                Transform t = root.transform.Find("Torso");
                if (t != null) { torso = t.gameObject; break; }
            }
        }

        // 2. Find Full_Frank GameObject
        GameObject fullFrank = GameObject.Find("Full_Frank");
        if (fullFrank == null)
        {
            foreach (var root in level3Scene.GetRootGameObjects())
            {
                if (root.name == "Full_Frank") { fullFrank = root; break; }
                Transform t = root.transform.Find("Full_Frank");
                if (t != null) { fullFrank = t.gameObject; break; }
            }
        }

        // 3. Find both spirits
        Enemy spirit1 = null;
        Enemy spirit2 = null;
        GameObject s1Obj = GameObject.Find("Enemy_Spirit");
        if (s1Obj != null) spirit1 = s1Obj.GetComponent<Enemy>();

        GameObject s2Obj = GameObject.Find("Enemy_Spirit (1)");
        if (s2Obj != null) spirit2 = s2Obj.GetComponent<Enemy>();

        if (spirit1 == null || spirit2 == null)
        {
            Enemy[] allEnemies = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var e in allEnemies)
            {
                if (e.enemyType == EnemyType.Spirit)
                {
                    if (spirit1 == null) spirit1 = e;
                    else if (spirit2 == null && e != spirit1) spirit2 = e;
                }
            }
        }

        // 4. Attach FullFrankEnding to Full_Frank
        FullFrankEnding fullFrankScript = null;
        if (fullFrank != null)
        {
            fullFrankScript = fullFrank.GetComponent<FullFrankEnding>();
            if (fullFrankScript == null)
            {
                fullFrankScript = Undo.AddComponent<FullFrankEnding>(fullFrank);
                modified = true;
                Debug.Log("[Level3ComponentAttacher] Added FullFrankEnding to Full_Frank.");
            }

            // Ensure sprite is initially hidden in the scene
            SpriteRenderer sr = fullFrank.GetComponent<SpriteRenderer>();
            if (sr != null && sr.enabled)
            {
                sr.enabled = false;
                EditorUtility.SetDirty(sr);
                modified = true;
            }
            EditorUtility.SetDirty(fullFrank);
        }
        else
        {
            Debug.LogWarning("[Level3ComponentAttacher] Full_Frank object not found in scene!");
        }

        // 5. Attach TorsoCollectible to Torso
        TorsoCollectible torsoScript = null;
        if (torso != null)
        {
            // Ensure Torso has a trigger collider
            Collider2D col = torso.GetComponent<Collider2D>();
            if (col == null)
            {
                CircleCollider2D circle = Undo.AddComponent<CircleCollider2D>(torso);
                circle.isTrigger = true;
                circle.radius = 1.0f;
                modified = true;
            }
            else if (!col.isTrigger)
            {
                col.isTrigger = true;
                EditorUtility.SetDirty(col);
                modified = true;
            }

            torsoScript = torso.GetComponent<TorsoCollectible>();
            if (torsoScript == null)
            {
                torsoScript = Undo.AddComponent<TorsoCollectible>(torso);
                modified = true;
                Debug.Log("[Level3ComponentAttacher] Added TorsoCollectible to Torso.");
            }

            if (torsoScript != null)
            {
                if (torsoScript.fullFrank != fullFrankScript)
                {
                    torsoScript.fullFrank = fullFrankScript;
                    modified = true;
                }
                if (torsoScript.spirit1 != spirit1)
                {
                    torsoScript.spirit1 = spirit1;
                    modified = true;
                }
                if (torsoScript.spirit2 != spirit2)
                {
                    torsoScript.spirit2 = spirit2;
                    modified = true;
                }
            }

            EditorUtility.SetDirty(torso);
        }
        else
        {
            Debug.LogWarning("[Level3ComponentAttacher] Torso object not found in scene!");
        }

        if (modified)
        {
            EditorSceneManager.MarkSceneDirty(level3Scene);
            bool saved = EditorSceneManager.SaveScene(level3Scene);
            Debug.Log($"<color=#5CE6B0><b>[Level3ComponentAttacher]</b></color> Successfully saved Level 3 scene with attached scripts! (Saved: {saved})");
        }
        else
        {
            Debug.Log("[Level3ComponentAttacher] Level 3 already has components attached and up to date.");
        }
    }
}
#endif
