using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tools > Scam Sense > Add Kid Character To Player
/// Builds (once) the low-poly "kid in a hoodie" prefab at Assets/Prefabs/Player/KidHoodie.prefab and puts it
/// on the Player of the open scene. The Player's capsule mesh is hidden (its collider / CharacterController
/// stay, so movement is unchanged). Edit colours by changing the Kid_* materials in Assets/Materials.
/// Tools > Scam Sense > Rebuild Kid Character Prefab regenerates the prefab from code.
/// </summary>
public static class PlayerCharacterBuilder
{
    const string PrefabPath = "Assets/Prefabs/Player/KidHoodie.prefab";

    [MenuItem("Tools/Scam Sense/Add Kid Character To Player")]
    public static void AddToOpenScene()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Kid character", "Stop Play Mode first.", "OK"); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) prefab = BuildPrefab();
        string result = Install(prefab);
        Debug.Log("PlayerCharacterBuilder: " + result);
    }

    [MenuItem("Tools/Scam Sense/Rebuild Kid Character Prefab")]
    public static void RebuildPrefabMenu() => BuildPrefab();

    /// <summary>Puts the prefab under the open scene's Player and hides the capsule mesh. Saves the scene.</summary>
    public static string Install(GameObject prefab)
    {
        GameObject player = null;
        foreach (var motor in Object.FindObjectsByType<PlayerMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (motor.GetComponent<CharacterController>() != null) { player = motor.gameObject; break; }
        if (player == null) return "no Player (PlayerMotor + CharacterController) in the open scene.";

        var old = player.transform.Find(prefab.name);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one;

        var capsule = player.GetComponent<MeshRenderer>();
        if (capsule != null) capsule.enabled = false;   // keep the component, just don't draw the grey capsule

        var scene = player.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"kid character added to '{player.name}' in {scene.name} (capsule mesh hidden).";
    }

    // ---------- Prefab ----------

    static Material Mat(string name, Color c, float smooth)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = c;
        m.SetFloat("_Glossiness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Transform Node(string name, Transform parent, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    static void Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m, bool shadowsOnly)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());   // visual only: never pushes the player or props
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        var r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        // Parts near the eye cast a shadow but aren't drawn, so they never block or clip the first-person view.
        r.shadowCastingMode = shadowsOnly ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
    }

    public static GameObject BuildPrefab()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs/Player");
        var hoodie = Mat("Kid_Hoodie", new Color32(0x2F, 0x80, 0xA8, 255), 0.12f);
        var hoodieDark = Mat("Kid_HoodieTrim", new Color32(0x23, 0x63, 0x85, 255), 0.12f);
        var jeans = Mat("Kid_Jeans", new Color32(0x2E, 0x3B, 0x5A, 255), 0.2f);
        var skin = Mat("Kid_Skin", new Color(0.87f, 0.69f, 0.55f), 0.15f);
        var hair = Mat("Kid_Hair", new Color32(0x3A, 0x2A, 0x1E, 255), 0.25f);
        var shoe = Mat("Kid_Sneaker", new Color32(0xEE, 0xEE, 0xEA, 255), 0.35f);
        var accent = Mat("Kid_SneakerAccent", new Color32(0xD9, 0x48, 0x3B, 255), 0.35f);
        var sole = Mat("Kid_Sole", new Color32(0x2A, 0x2A, 0x2E, 255), 0.2f);
        var cord = Mat("Kid_Drawstring", new Color32(0xF2, 0xF2, 0xF2, 255), 0.2f);

        // Root = Player origin. The CharacterController is 2 m tall with its centre at the origin,
        // so the feet are at y = -1 and the camera (eye) at y = +0.55.
        var root = new GameObject("KidHoodie");
        var visual = Node("Visual", root.transform, new Vector3(0f, 0f, -0.16f));   // slightly behind the eye
        var hips = Node("Hips", visual, new Vector3(0f, -0.18f, 0f));

        // Legs (jeans) with knee pivots, sneakers
        Transform legL = null, legR = null, kneeL = null, kneeR = null;
        foreach (float side in new[] { -1f, 1f })
        {
            string s = side < 0 ? "L" : "R";
            var leg = Node("Leg_" + s, hips, new Vector3(0.095f * side, 0f, 0f));
            Box("Thigh_" + s, leg, new Vector3(0f, -0.21f, 0f), new Vector3(0.135f, 0.42f, 0.15f), jeans, false);
            var knee = Node("Knee_" + s, leg, new Vector3(0f, -0.42f, 0f));
            Box("Shin_" + s, knee, new Vector3(0f, -0.19f, 0f), new Vector3(0.125f, 0.38f, 0.14f), jeans, false);
            Box("Sneaker_" + s, knee, new Vector3(0f, -0.355f, 0.045f), new Vector3(0.13f, 0.09f, 0.27f), shoe, false);
            Box("SneakerStripe_" + s, knee, new Vector3(0f, -0.35f, 0.0f), new Vector3(0.136f, 0.03f, 0.12f), accent, false);
            Box("Sole_" + s, knee, new Vector3(0f, -0.39f, 0.045f), new Vector3(0.136f, 0.025f, 0.276f), sole, false);
            if (side < 0) { legL = leg; kneeL = knee; } else { legR = leg; kneeR = knee; }
        }

        // Hoodie, head and arms: shadow-only (a box torso seen from above reads as a flat slab). In view when
        // looking down: jeans and sneakers. The full kid shows in the shadow; the hands show hoodie sleeves.
        Box("HoodieLower", hips, new Vector3(0f, 0.13f, 0f), new Vector3(0.38f, 0.32f, 0.22f), hoodie, true);
        Box("HoodieHem", hips, new Vector3(0f, -0.01f, 0f), new Vector3(0.386f, 0.05f, 0.226f), hoodieDark, true);
        Box("HoodiePocket", hips, new Vector3(0f, 0.10f, 0.113f), new Vector3(0.26f, 0.12f, 0.02f), hoodieDark, true);
        Box("HoodieChest", hips, new Vector3(0f, 0.41f, 0f), new Vector3(0.40f, 0.24f, 0.23f), hoodie, true);
        Box("Drawstring_L", hips, new Vector3(-0.04f, 0.42f, 0.12f), new Vector3(0.012f, 0.12f, 0.012f), cord, true);
        Box("Drawstring_R", hips, new Vector3(0.04f, 0.42f, 0.12f), new Vector3(0.012f, 0.12f, 0.012f), cord, true);
        Box("Hood", hips, new Vector3(0f, 0.57f, -0.1f), new Vector3(0.30f, 0.15f, 0.16f), hoodieDark, true);
        Box("Neck", hips, new Vector3(0f, 0.56f, 0f), new Vector3(0.10f, 0.06f, 0.10f), skin, true);
        Box("Head", hips, new Vector3(0f, 0.71f, 0f), new Vector3(0.22f, 0.24f, 0.22f), skin, true);
        Box("Hair", hips, new Vector3(0f, 0.84f, -0.005f), new Vector3(0.235f, 0.08f, 0.235f), hair, true);
        Box("HairBack", hips, new Vector3(0f, 0.76f, -0.1f), new Vector3(0.235f, 0.15f, 0.05f), hair, true);

        // Arms in hoodie sleeves (shadow-only: the first-person hands are shown by FloatingHand)
        Transform shoulderL = null, shoulderR = null, elbowL = null, elbowR = null;
        foreach (float side in new[] { -1f, 1f })
        {
            string s = side < 0 ? "L" : "R";
            var shoulder = Node("Shoulder_" + s, hips, new Vector3(0.235f * side, 0.50f, 0f));
            Box("UpperArm_" + s, shoulder, new Vector3(0f, -0.14f, 0f), new Vector3(0.10f, 0.28f, 0.11f), hoodie, true);
            var elbow = Node("Elbow_" + s, shoulder, new Vector3(0f, -0.28f, 0f));
            Box("Forearm_" + s, elbow, new Vector3(0f, -0.12f, 0f), new Vector3(0.095f, 0.24f, 0.105f), hoodie, true);
            Box("Cuff_" + s, elbow, new Vector3(0f, -0.25f, 0f), new Vector3(0.10f, 0.04f, 0.11f), hoodieDark, true);
            Box("Hand_" + s, elbow, new Vector3(0f, -0.31f, 0f), new Vector3(0.08f, 0.09f, 0.06f), skin, true);
            if (side < 0) { shoulderL = shoulder; elbowL = elbow; } else { shoulderR = shoulder; elbowR = elbow; }
        }

        // Wire the animator script
        var body = root.AddComponent<PlayerBody>();
        var so = new SerializedObject(body);
        so.FindProperty("visual").objectReferenceValue = visual;
        so.FindProperty("hips").objectReferenceValue = hips;
        so.FindProperty("legL").objectReferenceValue = legL;
        so.FindProperty("legR").objectReferenceValue = legR;
        so.FindProperty("kneeL").objectReferenceValue = kneeL;
        so.FindProperty("kneeR").objectReferenceValue = kneeR;
        so.FindProperty("shoulderL").objectReferenceValue = shoulderL;
        so.FindProperty("shoulderR").objectReferenceValue = shoulderR;
        so.FindProperty("elbowL").objectReferenceValue = elbowL;
        so.FindProperty("elbowR").objectReferenceValue = elbowR;
        so.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return prefab;
    }
}
