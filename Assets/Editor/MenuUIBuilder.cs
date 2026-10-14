using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Scam Sense > Build Menu UI In Scene
/// Creates the main menu and the account panels as real objects in the open Menu scene:
///   1. Saves the UI sprites (button shapes, light sweep, rounded panels, menu background) as assets in
///      Assets/Resources/UIKit, so scene objects can reference them (UIKit loads these assets from now on).
///   2. Builds "MainMenu Canvas" (+ "MainMenu Fade Canvas") under the MainMenuUI object, assigns every
///      Inspector reference and adds persistent OnClick / OnValueChanged events to every button and slider.
///   3. Builds the account panels (AuthenticationPanel, PlayerSignInPanel, CreateAccountPanel, AccountPanel,
///      BusyPanel, AccountChip) inside the same canvas and wires them to AccountUI.
/// Running it again replaces the generated menu objects (your other scene objects are not touched).
/// </summary>
public static class MenuUIBuilder
{
    const string SpriteDir = "Assets/Resources/UIKit";

    [MenuItem("Tools/Scam Sense/Build Menu UI In Scene")]
    public static void BuildMenuUI()
    {
        var menu = Object.FindFirstObjectByType<MainMenuUI>();
        if (menu == null)
        {
            EditorUtility.DisplayDialog("Build Menu UI", "Open the Menu scene first (no MainMenuUI found).", "OK");
            return;
        }
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Build Menu UI", "Stop Play Mode first.", "OK");
            return;
        }
        Run(menu);
        Debug.Log("MenuUIBuilder: menu UI built in the scene and saved. See 'MainMenu Canvas' under " + menu.name + ".");
    }

    /// <summary>Does the work (also callable from scripts/tests). Returns the number of generated sprite assets.</summary>
    public static int Run(MainMenuUI menu)
    {
        int sprites = ExportSprites();
        UIKit.ClearSpriteCache();

        // Replace a previous build
        foreach (var name in new[] { "MainMenu Canvas", "MainMenu Fade Canvas" })
        {
            var old = menu.transform.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        var account = menu.GetComponent<AccountUI>();
        if (account == null) account = menu.gameObject.AddComponent<AccountUI>();

        menu.BuildIntoScene();
        account.BuildIntoScene(menu);

        EditorUtility.SetDirty(menu);
        EditorUtility.SetDirty(account);
        var scene = menu.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return sprites;
    }

    static int ExportSprites()
    {
        Directory.CreateDirectory(SpriteDir);
        int n = 0;
        foreach (var (name, sprite) in UIKit.GenerateSpriteSet()) { Save(name, sprite); n++; }
        foreach (var (name, sprite) in MainMenuUI.GenerateMenuSprites()) { Save(name, sprite); n++; }
        return n;
    }

    static void Save(string name, Sprite sprite)
    {
        string path = $"{SpriteDir}/{name}.png";
        var tex = sprite.texture;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = sprite.pixelsPerUnit;
        imp.spriteBorder = sprite.border;                 // keeps the 9-slice corners (chamfer, rounded)
        imp.wrapMode = tex.wrapMode;                      // the menu grid tiles
        imp.filterMode = FilterMode.Bilinear;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.sRGBTexture = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }
}
