using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class Ensure2DLighting
{
    static Ensure2DLighting()
    {
        EditorApplication.delayCall += OnEditorReady;
        EditorSceneManager.sceneOpened += (scene, mode) => FixLightsInScene(scene);
    }

    private static void OnEditorReady()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.isLoaded)
        {
            FixLightsInScene(activeScene);
        }
    }

    [MenuItem("Tools/Fix 2D Lighting for Decorations (Active Scene)", false, 20)]
    public static void FixActiveScene()
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        int fixedCount = FixLightsInScene(activeScene);
        Debug.Log($"<color=green>[Ensure2DLighting]</color> Checked active scene '{activeScene.name}'. Updated {fixedCount} light(s).");
    }

    [MenuItem("Tools/Fix 2D Lighting for All Scenes (Освітлення для всіх сцен)", false, 21)]
    public static void FixAllScenes()
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
            int fixedCount = FixLightsInScene(scene);
            if (fixedCount > 0 || scene.isDirty)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"<color=green>[Ensure2DLighting]</color> Updated and saved {fixedCount} light(s) in {path}.");
            }
        }

        if (!string.IsNullOrEmpty(currentActive) && currentActive != EditorSceneManager.GetActiveScene().path)
        {
            EditorSceneManager.OpenScene(currentActive, OpenSceneMode.Single);
        }
    }

    public static int FixLightsInScene(UnityEngine.SceneManagement.Scene scene)
    {
        if (!scene.isLoaded) return 0;

        var allSortingLayers = SortingLayer.layers;
        int[] allLayerIds = allSortingLayers.Select(l => l.id).ToArray();
        HashSet<int> allLayerSet = new HashSet<int>(allLayerIds);

        var lights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include);
        int modifiedLights = 0;

        foreach (var light in lights)
        {
            if (light.gameObject.scene != scene) continue;

            // Global lights should typically illuminate all sorting layers
            if (light.lightType == Light2D.LightType.Global)
            {
                var currentLayers = light.targetSortingLayers ?? new int[0];
                bool missingAny = allLayerIds.Any(id => !currentLayers.Contains(id));

                if (missingAny || currentLayers.Length != allLayerIds.Length)
                {
                    Undo.RecordObject(light, "Fix Light2D Sorting Layers");
                    light.targetSortingLayers = allLayerIds;
                    EditorUtility.SetDirty(light);
                    EditorSceneManager.MarkSceneDirty(scene);
                    modifiedLights++;
                    Debug.Log($"<color=cyan>[Ensure2DLighting]</color> Light2D '{light.name}' in '{scene.name}' updated with all {allLayerIds.Length} sorting layers (including Decorations).");
                }
            }
            else
            {
                // For non-global lights, ensure Decorations layer is included if TileMap or Player is included
                int decorId = SortingLayer.NameToID("Decorations");
                int tilemapId = SortingLayer.NameToID("TileMap");
                var currentLayers = light.targetSortingLayers ?? new int[0];

                if (currentLayers.Contains(tilemapId) && !currentLayers.Contains(decorId))
                {
                    Undo.RecordObject(light, "Fix Light2D Decorations Layer");
                    var list = new List<int>(currentLayers) { decorId };
                    light.targetSortingLayers = list.ToArray();
                    EditorUtility.SetDirty(light);
                    EditorSceneManager.MarkSceneDirty(scene);
                    modifiedLights++;
                }
            }
        }

        return modifiedLights;
    }
}
