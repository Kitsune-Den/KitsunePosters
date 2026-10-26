using HarmonyLib;

namespace KitsunePosters
{
    public class PosterMod : IModApi
    {
        public void InitMod(Mod _modInstance)
        {
            PosterSettings.Load(_modInstance);

            var harmony = new Harmony("net.kitsuneden.posters");

            // Explicit per-method patching; PatchAll has bitten us before (toolbelt null refs).
            var refresh = AccessTools.Method(typeof(TEFeatureSignable), "RefreshTextMesh");
            if (refresh == null)
            {
                Log.Error("[KitsunePosters] TEFeatureSignable.RefreshTextMesh not found - mod disabled.");
                return;
            }
            harmony.Patch(refresh, postfix: new HarmonyMethod(typeof(SignRefreshPatch), nameof(SignRefreshPatch.Postfix)));

            // Server-side allowlist enforcement on the sign text itself. SetText is
            // virtual, and we know from PaintUnlocked that patches on virtual methods
            // can fail to fire on the dedicated server, so the refresh postfix above
            // ALSO refuses disallowed URLs on every client as the second gate.
            var setText = AccessTools.Method(typeof(TEFeatureSignable), nameof(TEFeatureSignable.SetText),
                new[] { typeof(string), typeof(bool), typeof(PlatformUserIdentifierAbs) });
            if (setText != null)
            {
                harmony.Patch(setText, prefix: new HarmonyMethod(typeof(SignSetTextPatch), nameof(SignSetTextPatch.Prefix)));
            }
            else
            {
                Log.Warning("[KitsunePosters] SetText overload not found - allowlist runs client side only.");
            }
            Log.Out("[KitsunePosters] Patched (RefreshTextMesh postfix, SetText prefix).");
        }
    }

    /// <summary>On the authoritative side, a disallowed URL never even enters the sign text.</summary>
    public static class SignSetTextPatch
    {
        public static void Prefix(TEFeatureSignable __instance, ref string _text)
        {
            if (string.IsNullOrEmpty(_text) || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                return;
            }
            string blockName = __instance.Parent?.TeData?.Block?.GetBlockName();
            if (blockName == null || !blockName.StartsWith("kitsunePoster"))
            {
                return;
            }
            string sanitized = PosterUrl.Sanitize(_text, out string blockedHost);
            if (!ReferenceEquals(sanitized, _text))
            {
                _text = sanitized;
                Log.Out($"[KitsunePosters] Blocked poster URL (host {blockedHost}) at {__instance.Parent.ToWorldPos()}.");
            }
        }
    }

    public static class SignRefreshPatch
    {
        // While one poster's refresh is re-rendering its group, the members'
        // own refreshes must not nudge further, or 2 posters ping-pong forever.
        private static bool inGroupRefresh;

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
            PosterRegistry.Track(__instance);

            var world = GameManager.Instance.World;
            Vector3i pos = __instance.Parent.ToWorldPos();

            // The raw text, not the filtered display text: the profanity filter
            // may touch the display copy, but the URL lives in the authored text.
            string url = PosterUrl.Extract(__instance.signText?.Text);
            if (url == null || !PosterSettings.ShowRemoteImages || !PosterSettings.IsDomainAllowed(url))
            {
                PosterSurface.Clear(meshes);
                NudgeOthers(world, pos, PosterTiling.Single);
                return;
            }

            var layout = PosterTiling.Compute(world, pos);
            PosterSurface.HideText(meshes);

            // The image fills the board's face: the block extent, less a lip.
            var block = __instance.Parent.TeData.Block;
            float faceW = 1f, faceH = 1f;
            if (block.isMultiBlock)
            {
                faceW = block.multiBlockPos.dim.x;
                faceH = block.multiBlockPos.dim.y;
            }
            faceW -= 0.08f;
            faceH -= 0.08f;

            PosterTextureManager.Get(url, tex =>
            {
                if (tex == null)
                {
                    // Download/decode failed: show the text again so the player
                    // can see (and fix) the URL they typed.
                    PosterSurface.ShowText(meshes);
                    return;
                }
                PosterSurface.Apply(meshes, tex, faceW, faceH, layout);
            });
            NudgeOthers(world, pos, layout);
        }

        /// <summary>
        /// Re-renders the posters this change may have regrouped: the group's
        /// members, the in-plane neighbours, and THEIR groups (covers the
        /// diagonal member of a former 2x2 after one cell changes).
        /// </summary>
        private static void NudgeOthers(World _world, Vector3i _pos, PosterLayout _layout)
        {
            if (inGroupRefresh || _world == null)
            {
                return;
            }
            inGroupRefresh = true;
            try
            {
                var targets = new System.Collections.Generic.HashSet<Vector3i>();
                targets.UnionWith(PosterTiling.Members(_world, _pos, _layout));
                foreach (var n in PosterTiling.PosterNeighbours(_world, _pos))
                {
                    targets.Add(n);
                    targets.UnionWith(PosterTiling.Members(_world, n, PosterTiling.Compute(_world, n)));
                }
                targets.Remove(_pos);
                foreach (var p in targets)
                {
                    var signable = PosterTiling.SignableAt(_world, p);
                    if (signable != null)
                    {
                        Postfix(signable);
                    }
                }
            }
            finally
            {
                inGroupRefresh = false;
            }
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

        /// <summary>
        /// Replaces URL tokens the allowlist refuses with a note. Returns the
        /// SAME string instance when nothing changed.
        /// </summary>
        public static string Sanitize(string _text, out string _blockedHost)
        {
            string blocked = null;
            string result = System.Text.RegularExpressions.Regex.Replace(_text, @"https?://\S+", match =>
            {
                if (PosterSettings.IsDomainAllowed(match.Value))
                {
                    return match.Value;
                }
                try { blocked = new System.Uri(match.Value).Host; }
                catch { blocked = "?"; }
                return $"[blocked: {blocked}]";
            });
            _blockedHost = blocked;
            return blocked == null ? _text : result;
        }
    }
}
