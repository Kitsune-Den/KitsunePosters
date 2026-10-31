using System.IO;
using UnityEditor;
using UnityEngine;

// Builds Resources/KitsunePosters.unity3d for the mod.
//   Unity.exe -batchmode -quit -projectPath <this project> -executeMethod BuildPosters.Build -logFile <log>
//
// Everything is procedural: a thin frameless board (scaled cube, flush against
// the block volume's +Z face, front facing -Z) with a TextMesh child on the
// front for TEFeatureSignable. The 7D2D tag-order rule applies: this project's
// tags are exactly Item, T_Mesh, B_Mesh, T_Mesh_B, T_Block (TagManager.asset),
// because bundles store tags as indexes into the game's own tag list.
//
// Orientation constants, should the in-game test disagree:
// - board on the wrong face of the block: flip BoardSide to -1
// - image/text on the back of the board: see FaceOffset in PosterSurface.cs
public static class BuildPosters
{
    private const string Dir = "Assets/Posters";
    private const string BundleName = "kitsuneposters.unity3d";
    private const float Thickness = 0.04f;
    private const float BoardSide = 1f; // +1: flush against +Z; -1: flush against -Z

    // Board width and height in metres, block-sized unless noted. The portrait
    // boards sit centred in a 1x2 block volume (MultiBlockDim in blocks.xml),
    // one block wide at the movie-poster ratios.
    private static readonly (string name, float w, float h)[] Sizes =
    {
        ("poster_1x1", 1, 1),
        ("poster_2x2", 2, 2),
        ("poster_3x2", 3, 2),
        ("poster_3by4", 1, 4f / 3f),
        ("poster_9by16", 1, 16f / 9f),
    };

    private static readonly (string name, float w, float h)[] GraffitiSizes =
    {
        ("graffiti_1x1", 1, 1),
        ("graffiti_2x2", 2, 2),
        ("graffiti_3x2", 3, 2),
    };

    public static void Build()
    {
        Directory.CreateDirectory(Dir);
        AssetDatabase.Refresh();

        // Dark mat: with the default fill mode the image covers it anyway, and
        // in fit mode the margins read as a frame mat, not a lightbox.
        var board = new Material(Shader.Find("Standard")) { name = "PosterBoard" };
        board.color = new Color(0.16f, 0.15f, 0.14f);
        board.SetFloat("_Glossiness", 0.08f);
        AssetDatabase.CreateAsset(board, Dir + "/PosterBoard.mat");

        // Ghost mat for graffiti: Standard in Fade mode, faint, so the empty
        // block can be found. PosterSurface clones the Board's material for the
        // image quad (color reset to white), so the SAME Fade material is what
        // keeps the sprayed PNG's transparency - don't change one without the
        // other. Fade keywords must be baked here or the bundle strips them.
        var ghost = new Material(Shader.Find("Standard")) { name = "GraffitiGhost" };
        ghost.SetFloat("_Mode", 2f); // Fade
        ghost.SetOverrideTag("RenderType", "Transparent");
        ghost.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        ghost.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        ghost.SetInt("_ZWrite", 0);
        ghost.DisableKeyword("_ALPHATEST_ON");
        ghost.EnableKeyword("_ALPHABLEND_ON");
        ghost.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        ghost.renderQueue = 3000;
        ghost.color = new Color(0.55f, 0.75f, 0.85f, 0.18f);
        ghost.SetFloat("_Glossiness", 0f);
        AssetDatabase.CreateAsset(ghost, Dir + "/GraffitiGhost.mat");

        // Sign text material: the built-in font material draws over everything
        // (ZTest Always), which leaks poster URLs through walls. Our shader is
        // the same alpha blend with a normal depth test; the DLL rebinds its
        // _MainTex to the live font atlas at runtime.
        var text = new Material(Shader.Find("KitsunePosters/TextOccluded")) { name = "PosterText" };
        AssetDatabase.CreateAsset(text, Dir + "/PosterText.mat");

        foreach (var (name, w, h) in Sizes)
        {
            MakePrefab(name, w, h, board, Thickness, text);
        }
        // Graffiti: near-flat ghost panes; the DLL hides them once an image shows.
        foreach (var (name, w, h) in GraffitiSizes)
        {
            MakePrefab(name, w, h, ghost, 0.02f, text);
        }
        WriteBundle();
    }

    private static void MakePrefab(string name, float w, float h, Material mat, float thickness, Material textMat)
    {
        var root = new GameObject(name);

        // A 1 cm gap on the short side, the long side shrunk to match, so the
        // board keeps its exact ratio (the DLL fills the image to that ratio).
        float inset = 1f - 0.02f / Mathf.Min(w, h);

        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Board";
        board.transform.SetParent(root.transform, false);
        board.transform.localPosition = new Vector3(0f, 0f, BoardSide * (0.5f - thickness / 2f));
        board.transform.localScale = new Vector3(w * inset, h * inset, thickness);
        var renderer = board.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Object.DestroyImmediate(board.GetComponent<Collider>());

        // KitsuneSleepyCat's lesson, learned in game: the aim ray only treats a
        // model collider as part of a block when the collider's OWN transform is
        // tagged T_Block (T_Mesh is the chunk-mesh tag and does nothing here).
        // One box on the root, tagged, Default layer - the proven pattern.
        // The collider stays a hand's width thick even on near-flat graffiti,
        // or the block could not be aimed at.
        root.tag = "T_Block";
        var box = root.AddComponent<BoxCollider>();
        float grip = Mathf.Max(thickness, 0.06f);
        box.center = new Vector3(0f, 0f, BoardSide * (0.5f - grip / 2f));
        box.size = new Vector3(w * inset, h * inset, grip);

        // TEFeatureSignable finds this TextMesh and drives it; our DLL parents
        // the image quad to it. TextMesh reads correctly from its -Z side,
        // which is the board's front.
        var textGo = new GameObject("TextFace");
        textGo.transform.SetParent(root.transform, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, BoardSide * (0.5f - thickness) - 0.005f);
        var tm = textGo.AddComponent<TextMesh>();
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.88f, 0.85f, 0.8f); // light on the dark board
        textGo.GetComponent<MeshRenderer>().sharedMaterial = textMat;

        PrefabUtility.SaveAsPrefabAsset(root, $"{Dir}/{name}.prefab");
        Object.DestroyImmediate(root);
        Debug.Log($"[posters] prefab {name}: {w}x{h}");
    }

    private static void WriteBundle()
    {
        var outDir = Path.GetFullPath("../../KitsunePosters/Resources");
        Directory.CreateDirectory(outDir);
        var names = new System.Collections.Generic.List<string>();
        foreach (var (name, _, _) in Sizes)
        {
            names.Add($"{Dir}/{name}.prefab");
        }
        foreach (var (name, _, _) in GraffitiSizes)
        {
            names.Add($"{Dir}/{name}.prefab");
        }
        var build = new AssetBundleBuild { assetBundleName = BundleName, assetNames = names.ToArray() };
        var tmp = Path.GetFullPath("Temp/bundles");
        Directory.CreateDirectory(tmp);
        BuildPipeline.BuildAssetBundles(tmp, new[] { build }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        File.Copy(Path.Combine(tmp, BundleName), Path.Combine(outDir, "KitsunePosters.unity3d"), true);
        Debug.Log("[posters] bundle written to " + outDir);
    }
}
