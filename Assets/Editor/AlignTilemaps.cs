using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class AlignTilemaps
{
    static AlignTilemaps()
    {
        EditorApplication.delayCall += OnEditorReady;
        EditorSceneManager.sceneOpened += (scene, mode) => FixTilemapsInScene(scene);
    }

    private static void OnEditorReady()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.isLoaded)
        {
            FixTilemapsInScene(activeScene);
        }
    }

    [MenuItem("Tools/Align Decorations Tilemap (Active Scene)", false, 25)]
    public static void AlignActiveScene()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        bool changed = FixTilemapsInScene(activeScene);
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log($"<color=green>[AlignTilemaps]</color> Successfully aligned Decorations with Tilemap in '{activeScene.name}'.");
        }
        else
        {
            Debug.Log($"<color=cyan>[AlignTilemaps]</color> Decorations is already aligned with Tilemap in '{activeScene.name}'.");
        }
    }

    [MenuItem("Tools/Align Decorations Tilemap (All Scenes)", false, 26)]
    public static void AlignAllScenes()
    {
        string[] scenePaths = new[]
        {
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/LevelHub.unity"
        };

        var currentActive = EditorSceneManager.GetActiveScene().path;

        foreach (var path in scenePaths)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool changed = FixTilemapsInScene(scene);
            if (changed || scene.isDirty)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"<color=green>[AlignTilemaps]</color> Aligned and saved Decorations in {path}.");
            }
        }

        if (!string.IsNullOrEmpty(currentActive) && currentActive != EditorSceneManager.GetActiveScene().path)
        {
            EditorSceneManager.OpenScene(currentActive, OpenSceneMode.Single);
        }
    }

    [MenuItem("Tools/Rebuild Tilemap Colliders (Active Scene)", false, 27)]
    public static void RebuildTilemapCollidersActiveScene()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.isLoaded) return;

        bool changed = RebuildCollidersInScene(activeScene);
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log($"<color=green>[TilemapColliders]</color> Successfully regenerated colliders in '{activeScene.name}'.");
        }
        else
        {
            Debug.Log($"<color=cyan>[TilemapColliders]</color> No Tilemaps found in '{activeScene.name}'.");
        }
    }

    public static bool RebuildCollidersInScene(UnityEngine.SceneManagement.Scene scene)
    {
        if (!scene.isLoaded) return false;

        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        bool any = false;

        foreach (var tm in tilemaps)
        {
            if (tm.gameObject.scene != scene) continue;

            any = true;
            tm.RefreshAllTiles();

            var tc = tm.GetComponent<TilemapCollider2D>();
            if (tc != null)
            {
                tc.ProcessTilemapChanges();
                tc.enabled = false;
                tc.enabled = true;
            }

            var cc = tm.GetComponent<CompositeCollider2D>();
            if (cc != null)
            {
                cc.GenerateGeometry();
            }
        }

        return any;
    }

    public static bool FixTilemapsInScene(UnityEngine.SceneManagement.Scene scene)
    {
        if (!scene.isLoaded) return false;

        Grid[] grids = Object.FindObjectsByType<Grid>(FindObjectsInactive.Include);
        bool anyChanged = false;

        foreach (var grid in grids)
        {
            if (grid.gameObject.scene != scene) continue;

            Transform tilemapTransform = null;
            Transform decorationsTransform = null;

            for (int i = 0; i < grid.transform.childCount; i++)
            {
                var child = grid.transform.GetChild(i);
                if (child.name == "Tilemap")
                {
                    tilemapTransform = child;
                }
                else if (child.name == "Decorations")
                {
                    decorationsTransform = child;
                }
            }

            if (tilemapTransform != null && decorationsTransform != null)
            {
                if (decorationsTransform.localPosition != tilemapTransform.localPosition ||
                    decorationsTransform.localScale != tilemapTransform.localScale ||
                    decorationsTransform.localRotation != tilemapTransform.localRotation)
                {
                    Undo.RecordObject(decorationsTransform, "Align Decorations Tilemap");
                    decorationsTransform.localPosition = tilemapTransform.localPosition;
                    decorationsTransform.localScale = tilemapTransform.localScale;
                    decorationsTransform.localRotation = tilemapTransform.localRotation;
                    EditorUtility.SetDirty(decorationsTransform);
                    EditorSceneManager.MarkSceneDirty(scene);
                    anyChanged = true;
                    Debug.Log($"<color=green>[AlignTilemaps]</color> Aligned Decorations Transform in '{scene.name}' to position {tilemapTransform.localPosition} and scale {tilemapTransform.localScale}.");
                }
            }
        }

        return anyChanged;
    }
}
