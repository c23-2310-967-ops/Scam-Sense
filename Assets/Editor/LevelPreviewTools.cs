using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Scam Sense > Menu > Capture Level Previews.
/// Renders a still of every room in the Level Catalog from a hand-picked camera angle, saves it as a sprite in
/// Assets/Art/LevelPreviews/ and assigns it to the level's <see cref="LevelDefinition.previewImage"/>.
/// The real scenes are only opened additively for a moment and closed without saving; whatever scene you have
/// open stays open and untouched. Re-run it after changing a room.
/// </summary>
public static class LevelPreviewTools
{
    public const string Folder = "Assets/Art/LevelPreviews";
    const int Width = 1280, Height = 720;

    public struct View
    {
        public Vector3 position, lookAt;
        public float fov;
        public View(Vector3 position, Vector3 lookAt, float fov = 60f) { this.position = position; this.lookAt = lookAt; this.fov = fov; }
    }

    /// <summary>Camera angle per level id (world space of that level's scene). Levels not listed use their player camera.</summary>
    public static readonly Dictionary<string, View> Views = new Dictionary<string, View>
    {
        { "level_1", new View(new Vector3(3.9f, 4.25f, 3.1f), new Vector3(0.2f, 3.75f, 7.3f), 62f) },
        { "level_2", new View(new Vector3(3.0f, 1.75f, -3.0f), new Vector3(-0.9f, 1.05f, 2.8f), 64f) },
        { "level_3", new View(new Vector3(4.6f, 1.85f, -3.4f), new Vector3(-0.8f, 1.15f, 2.6f), 64f) },
        { "level_4", new View(new Vector3(4.6f, 1.85f, -3.5f), new Vector3(-1.0f, 1.0f, 2.4f), 64f) },
        // MacLab: from the back corner by the windows towards the display and instructor counter (like the reference photo)
        { "level_5", new View(new Vector3(7.6f, 1.8f, -5.9f), new Vector3(-2.0f, 1.0f, 3.6f), 64f) },
    };

    [MenuItem("Tools/Scam Sense/Menu/Capture Level Previews")]
    public static void CaptureAll()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Levels/LevelCatalog.asset");
        if (catalog == null) { Debug.LogError("LevelPreviewTools: Assets/Levels/LevelCatalog.asset not found."); return; }
        Directory.CreateDirectory(Folder);
        int done = 0;
        foreach (var level in catalog.levels)
        {
            if (level == null || string.IsNullOrEmpty(level.sceneName)) continue;
            string scenePath = FindScenePath(level.sceneName);
            if (scenePath == null) { Debug.LogWarning($"LevelPreviewTools: scene '{level.sceneName}' not found - {level.levelId} keeps its current preview."); continue; }

            if (Capture(level, scenePath)) done++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"LevelPreviewTools: captured {done} level preview(s) into {Folder}.");
    }

    /// <summary>
    /// Renders one level's preview from its real scene and assigns it to the level (used by the room builders after a
    /// rebuild, so the main menu's sneak peek always shows the playable room). Returns false if nothing was captured.
    /// </summary>
    public static bool CaptureLevel(LevelDefinition level)
    {
        if (level == null || string.IsNullOrEmpty(level.sceneName)) return false;
        string scenePath = FindScenePath(level.sceneName);
        if (scenePath == null) return false;
        Directory.CreateDirectory(Folder);
        bool ok = Capture(level, scenePath);
        if (ok) AssetDatabase.SaveAssets();
        return ok;
    }

    static bool Capture(LevelDefinition level, string scenePath)
    {
        string png = $"{Folder}/{level.levelId}_preview.png";
        View? view = Views.TryGetValue(level.levelId, out var v) ? v : (View?)null;
        if (!Render(scenePath, view, Width, Height, png)) return false;
        ImportAsSprite(png);
        level.previewImage = AssetDatabase.LoadAssetAtPath<Sprite>(png);
        EditorUtility.SetDirty(level);
        return true;
    }

    public static string FindScenePath(string sceneName)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Scene " + sceneName))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == sceneName) return p;
        }
        return null;
    }

    /// <summary>
    /// Renders one still of a scene. <paramref name="view"/> null = the scene's own player camera.
    /// Only that scene is visible while rendering, lit with its own lighting settings.
    /// </summary>
    public static bool Render(string scenePath, View? view, int width, int height, string outPath)
    {
        var previousActive = SceneManager.GetActiveScene();
        var existing = SceneManager.GetSceneByPath(scenePath);
        bool openedHere = !existing.IsValid() || !existing.isLoaded;
        var scene = openedHere ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive) : existing;
        GameObject camGo = null;
        RenderTexture rt = null;
        Texture2D tex = null;
        // Rooms share the world origin: hide every other open scene (objects and lights) while rendering.
        // Toggling from code doesn't mark those scenes as modified; each object gets its own state back.
        var hidden = new List<GameObject>();
        try
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var other = SceneManager.GetSceneAt(i);
                if (other == scene || !other.isLoaded) continue;
                foreach (var root in other.GetRootGameObjects())
                    if (root.activeSelf) { root.SetActive(false); hidden.Add(root); }
            }
            SceneManager.SetActiveScene(scene);   // ambient light, skybox and fog come from the active scene

            camGo = new GameObject("LevelPreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            cam.allowMSAA = true;

            if (view.HasValue)
            {
                cam.transform.position = view.Value.position;
                cam.transform.rotation = Quaternion.LookRotation(view.Value.lookAt - view.Value.position, Vector3.up);
                cam.fieldOfView = view.Value.fov;
            }
            else
            {
                Camera playerCam = null;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var c in root.GetComponentsInChildren<Camera>(true))
                        if (playerCam == null || c.CompareTag("MainCamera")) playerCam = c;
                if (playerCam == null) { Debug.LogWarning($"LevelPreviewTools: no camera angle for {scenePath}."); return false; }
                cam.transform.SetPositionAndRotation(playerCam.transform.position, playerCam.transform.rotation);
                cam.fieldOfView = playerCam.fieldOfView;
            }

            rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;

            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            return true;
        }
        finally
        {
            if (tex != null) Object.DestroyImmediate(tex);
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            if (camGo != null) Object.DestroyImmediate(camGo);
            foreach (var root in hidden) if (root != null) root.SetActive(true);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);   // never saved
        }
    }

    public static void ImportAsSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.mipmapEnabled = false;
        imp.maxTextureSize = 2048;
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.SaveAndReimport();
    }
}
