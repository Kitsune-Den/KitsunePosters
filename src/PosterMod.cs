using HarmonyLib;

namespace KitsunePosters
{
    public class PosterMod : IModApi
    {
        public void InitMod(Mod _modInstance)
        {
            var harmony = new Harmony("net.kitsuneden.posters");

            // Explicit per-method patching; PatchAll has bitten us before (toolbelt null refs).
            var refresh = AccessTools.Method(typeof(TEFeatureSignable), "RefreshTextMesh");
            if (refresh == null)
            {
                Log.Error("[KitsunePosters] TEFeatureSignable.RefreshTextMesh not found - mod disabled.");
                return;
            }
            harmony.Patch(refresh, postfix: new HarmonyMethod(typeof(SignRefreshPatch), nameof(SignRefreshPatch.Postfix)));
            Log.Out("[KitsunePosters] RefreshTextMesh patched.");
        }
    }

    public static class SignRefreshPatch
    {
        public static void Postfix(TEFeatureSignable __instance)
        {
            if (GameManager.IsDedicatedServer)
            {
                return;
            }
            var meshes = __instance.smartTextMesh;
            if (meshes == null || meshes.Length == 0)
            {
                return;
            }

            // Only our poster blocks react to URLs; ordinary signs are untouched.
            string blockName = __instance.Parent?.TeData?.Block?.GetBlockName();
            if (blockName == null || !blockName.StartsWith("kitsunePoster"))
            {
                return;
            }

            // The raw text, not the filtered display text: the profanity filter
            // may touch the display copy, but the URL lives in the authored text.
            string url = PosterUrl.Extract(__instance.signText?.Text);
            if (url == null)
            {
                PosterSurface.Clear(meshes);
                return;
            }

            PosterSurface.HideText(meshes);
            float width = __instance.lineWidth;
            PosterTextureManager.Get(url, tex =>
            {
                if (tex == null)
                {
                    // Download/decode failed: show the text again so the player
                    // can see (and fix) the URL they typed.
                    PosterSurface.ShowText(meshes);
                    return;
                }
                PosterSurface.Apply(meshes, tex, width);
            });
        }
    }

    public static class PosterUrl
    {
        /// <summary>First http(s) token in the sign text, or null.</summary>
        public static string Extract(string _text)
        {
            if (string.IsNullOrEmpty(_text))
            {
                return null;
            }
            foreach (var token in _text.Split(' ', '\n', '\r', '\t'))
            {
                if (token.StartsWith("http://") || token.StartsWith("https://"))
                {
                    return token.Trim();
                }
            }
            return null;
        }
    }
}
