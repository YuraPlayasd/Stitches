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
        EditorSceneManager.sceneOpened += (scene, mode) =>
        {
            FixTilemapsInScene(scene);
            FixTilemapCullingAndBounds(scene);
            FixSkyBackground(scene);
        };
    }

    private static void OnEditorReady()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.isLoaded)
        {
            FixTilemapsInScene(activeScene);
            FixTilemapCullingAndBounds(activeScene);
            FixSkyBackground(activeScene);
        }
    }

    [MenuItem("Tools/Fix Sky Background (Active Scene)", false, 15)]
    public static void FixSkyActiveScene()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.isLoaded) return;

        bool changed = FixSkyBackground(activeScene);
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log($"<color=green>[BackgroundFollow]</color> Successfully restored sky background in '{activeScene.name}'.");
        }
        else
        {
            Debug.Log($"<color=cyan>[BackgroundFollow]</color> Sky background is already correctly configured in '{activeScene.name}'.");
        }
    }

    [MenuItem("Tools/Fix Tilemap Clipping and Culling (Active Scene)", false, 20)]
    public static void FixCullingActiveScene()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.isLoaded) return;

        bool changed = FixTilemapCullingAndBounds(activeScene);
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log($"<color=green>[TilemapCulling]</color> Successfully fixed Tilemap culling and bounds in '{activeScene.name}'.");
        }
        else
        {
            Debug.Log($"<color=cyan>[TilemapCulling]</color> All Tilemaps already have optimal culling settings in '{activeScene.name}'.");
        }
    }

    [MenuItem("Tools/Fix Tilemap Clipping and Culling (All Scenes)", false, 21)]
    public static void FixCullingAllScenes()
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
            bool changed = FixTilemapCullingAndBounds(scene) || FixSkyBackground(scene);
            if (changed || scene.isDirty)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"<color=green>[TilemapCulling]</color> Fixed and saved settings in {path}.");
            }
        }

        if (!string.IsNullOrEmpty(currentActive) && currentActive != EditorSceneManager.GetActiveScene().path)
        {
            EditorSceneManager.OpenScene(currentActive, OpenSceneMode.Single);
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

    public static bool FixTilemapCullingAndBounds(UnityEngine.SceneManagement.Scene scene)
    {
        if (!scene.isLoaded) return false;

        bool anyChanged = false;

        // 1. Remove runaway stray tiles (far away from level area) and compress bounds
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        foreach (var tm in tilemaps)
        {
            if (tm.gameObject.scene != scene) continue;

            bool removedTile = false;
            BoundsInt bounds = tm.cellBounds;
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    if (Mathf.Abs(x) > 200 || Mathf.Abs(y) > 200)
                    {
                        Vector3Int cellPos = new Vector3Int(x, y, 0);
                        if (tm.HasTile(cellPos))
                        {
                            Undo.RecordObject(tm, "Remove Stray Tile");
                            tm.SetTile(cellPos, null);
                            removedTile = true;
                            Debug.Log($"<color=yellow>[TilemapCulling]</color> Removed stray tile at {cellPos} on '{tm.gameObject.name}'.");
                        }
                    }
                }
            }

            if (removedTile)
            {
                tm.CompressBounds();
                EditorUtility.SetDirty(tm);
                anyChanged = true;
            }
        }

        // 2. Fix TilemapRenderers: disable chunk culling artifacts by setting Mode to Individual
        // and setting manual chunk culling bounds with generous padding (50, 50, 0)
        TilemapRenderer[] renderers = Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include);
        foreach (var tr in renderers)
        {
            if (tr.gameObject.scene != scene) continue;

            bool rChanged = false;
            if (tr.mode != TilemapRenderer.Mode.Individual)
            {
                Undo.RecordObject(tr, "Fix TilemapRenderer Mode");
                tr.mode = TilemapRenderer.Mode.Individual;
                rChanged = true;
            }

            if (tr.detectChunkCullingBounds != TilemapRenderer.DetectChunkCullingBounds.Manual)
            {
                Undo.RecordObject(tr, "Fix TilemapRenderer Culling Bounds Detect");
                tr.detectChunkCullingBounds = TilemapRenderer.DetectChunkCullingBounds.Manual;
                rChanged = true;
            }

            Vector3 targetBounds = new Vector3(50f, 50f, 0f);
            if (tr.chunkCullingBounds != targetBounds)
            {
                Undo.RecordObject(tr, "Fix TilemapRenderer Chunk Culling Bounds");
                tr.chunkCullingBounds = targetBounds;
                rChanged = true;
            }

            if (rChanged)
            {
                EditorUtility.SetDirty(tr);
                anyChanged = true;
                Debug.Log($"<color=green>[TilemapCulling]</color> Updated TilemapRenderer on '{tr.gameObject.name}' to Mode: Individual, Manual Culling Bounds (50, 50, 0).");
            }
        }

        if (anyChanged)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        return anyChanged;
    }

    public static bool FixSkyBackground(UnityEngine.SceneManagement.Scene scene)
    {
        if (!scene.isLoaded) return false;

        bool anyChanged = false;
        var bgFollows = Object.FindObjectsByType<BackgroundFollow>(FindObjectsInactive.Include);
        foreach (var bg in bgFollows)
        {
            if (bg.gameObject.scene != scene) continue;

            // 1. Ensure scale is large enough to cover widescreen and parallax
            if (bg.transform.localScale.x < 20f || bg.transform.localScale.y < 20f)
            {
                Undo.RecordObject(bg.transform, "Fix Sky Scale");
                bg.transform.localScale = new Vector3(25f, 25f, 1f);
                EditorUtility.SetDirty(bg.transform);
                anyChanged = true;
                Debug.Log($"<color=green>[BackgroundFollow]</color> Restored sky scale on '{bg.gameObject.name}' to (25, 25, 1).");
            }

            // 2. Ensure cameraTransform is assigned directly
            if (bg.cameraTransform == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Undo.RecordObject(bg, "Assign Background Camera");
                    bg.cameraTransform = cam.transform;
                    EditorUtility.SetDirty(bg);
                    anyChanged = true;
                    Debug.Log($"<color=green>[BackgroundFollow]</color> Assigned Main Camera to '{bg.gameObject.name}'.");
                }
            }

            // 3. Ensure vertical follow so sky does not stay stuck below when camera pans up
            if (!bg.followY)
            {
                Undo.RecordObject(bg, "Enable Sky followY");
                bg.followY = true;
                bg.parallaxEffectY = 1f;
                EditorUtility.SetDirty(bg);
                anyChanged = true;
                Debug.Log($"<color=green>[BackgroundFollow]</color> Enabled followY on '{bg.gameObject.name}'.");
            }
        }

        if (anyChanged)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        return anyChanged;
    }
}
