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

    private static readonly (string name, int w, int h)[] Sizes =
    {
        ("poster_1x1", 1, 1),
        ("poster_2x2", 2, 2),
        ("poster_3x2", 3, 2),
    };

    public static void Build()
    {
        Directory.CreateDirectory(Dir);
        AssetDatabase.Refresh();

        var board = new Material(Shader.Find("Standard")) { name = "PosterBoard" };
        board.color = new Color(0.91f, 0.88f, 0.82f); // raw canvas
        board.SetFloat("_Glossiness", 0.08f);
        AssetDatabase.CreateAsset(board, Dir + "/PosterBoard.mat");

        foreach (var (name, w, h) in Sizes)
        {
            MakePrefab(name, w, h, board);
        }
        WriteBundle();
    }

    private static void MakePrefab(string name, int w, int h, Material mat)
    {
        var root = new GameObject(name);

        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Board";
        board.transform.SetParent(root.transform, false);
        board.transform.localPosition = new Vector3(0f, 0f, BoardSide * (0.5f - Thickness / 2f));
        board.transform.localScale = new Vector3(w - 0.02f, h - 0.02f, Thickness);
        var renderer = board.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Object.DestroyImmediate(board.GetComponent<Collider>());

        // KitsuneSleepyCat's lesson, learned in game: the aim ray only treats a
        // model collider as part of a block when the collider's OWN transform is
        // tagged T_Block (T_Mesh is the chunk-mesh tag and does nothing here).
        // One box on the root, tagged, Default layer - the proven pattern.
        root.tag = "T_Block";
        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0f, BoardSide * (0.5f - Thickness / 2f));
        box.size = new Vector3(w - 0.02f, h - 0.02f, Thickness);

        // TEFeatureSignable finds this TextMesh and drives it; our DLL parents
        // the image quad to it. TextMesh reads correctly from its -Z side,
        // which is the board's front.
        var textGo = new GameObject("TextFace");
        textGo.transform.SetParent(root.transform, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, BoardSide * (0.5f - Thickness) - 0.005f);
        var tm = textGo.AddComponent<TextMesh>();
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.12f, 0.1f, 0.08f);
        textGo.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;

        PrefabUtility.SaveAsPrefabAsset(root, $"{Dir}/{name}.prefab");
        Object.DestroyImmediate(root);
        Debug.Log($"[posters] prefab {name}: {w}x{h}");
    }

    private static void WriteBundle()
    {
        var outDir = Path.GetFullPath("../../KitsunePosters/Resources");
        Directory.CreateDirectory(outDir);
        var names = new string[Sizes.Length];
        for (int i = 0; i < Sizes.Length; i++)
        {
            names[i] = $"{Dir}/{Sizes[i].name}.prefab";
        }
        var build = new AssetBundleBuild { assetBundleName = BundleName, assetNames = names };
        var tmp = Path.GetFullPath("Temp/bundles");
        Directory.CreateDirectory(tmp);
        BuildPipeline.BuildAssetBundles(tmp, new[] { build }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        File.Copy(Path.Combine(tmp, BundleName), Path.Combine(outDir, "KitsunePosters.unity3d"), true);
        Debug.Log("[posters] bundle written to " + outDir);
    }
}
