using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > Scam Sense > Visual Style > Add Soft Depth To Scene UI
/// UI built at runtime gets UISoftDepth from UIKit automatically. UI saved in a scene (the main menu and account
/// panels made by "Build Menu UI In Scene") was built before that, so this adds the same effect to the open scene,
/// using the same rules as UIKit: rounded panels -> gradient + soft shadow, rounded buttons -> gradient + lip +
/// small shadow, chamfered HUD buttons -> gradient only. It only adds components; nothing else is changed, and
/// elements that already have UISoftDepth are skipped, so it is safe to run again.
/// </summary>
public static class SoftRealismUI
{
    [MenuItem("Tools/Scam Sense/Visual Style/Add Soft Depth To Scene UI")]
    public static void AddToOpenScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        int panels = 0, buttons = 0, hud = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var img in root.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite == null || img.GetComponent<UISoftDepth>() != null) continue;
                var go = img.gameObject;
                if (img.sprite.name == "UIKit_Chamfer" && go.GetComponent<Mask>() != null)
                {
                    Undo.AddComponent<UISoftDepth>(go).Configure(1.12f, 0.88f);
                    hud++;
                }
                else if (img.sprite.name == "UIKit_Rounded" && go.GetComponent<Outline>() != null)
                {
                    if (go.GetComponent<Button>() != null)
                    {
                        Undo.AddComponent<UISoftDepth>(go).Configure(1.14f, 0.86f, layers: 3, spread: 6f, offsetY: -5f, alpha: 0.35f, lip: 3f);
                        buttons++;
                    }
                    else
                    {
                        Undo.AddComponent<UISoftDepth>(go).Configure(1.08f, 0.92f, layers: 4, spread: 14f, offsetY: -10f, alpha: 0.4f);
                        panels++;
                    }
                }
            }
        if (panels + buttons + hud > 0) EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"SoftRealismUI: added soft depth to {panels} panels, {buttons} buttons and {hud} HUD buttons in {scene.name}. Save the scene to keep it.");
    }
}
