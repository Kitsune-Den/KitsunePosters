using System;
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
            // The E menu's "Poster settings" entry on poster blocks.
            var initCommands = AccessTools.Method(typeof(TEFeatureSignable), nameof(TEFeatureSignable.InitBlockActivationCommands));
            var onActivated = AccessTools.Method(typeof(TEFeatureSignable), nameof(TEFeatureSignable.OnBlockActivated));
            if (initCommands != null && onActivated != null)
            {
                harmony.Patch(initCommands, postfix: new HarmonyMethod(typeof(SignCommandsPatch), nameof(SignCommandsPatch.InitPostfix)));
                harmony.Patch(onActivated, prefix: new HarmonyMethod(typeof(SignCommandsPatch), nameof(SignCommandsPatch.ActivatedPrefix)));
            }
            else
            {
                Log.Warning("[KitsunePosters] Activation command methods not found - settings window only via 'kposters menu'.");
            }
            // E > Take on posters (TEFeaturePickup in blocks.xml).
            var pickupAllow = AccessTools.Method(typeof(TEFeaturePickup), nameof(TEFeaturePickup.AllowBlockActivationCommand));
            if (pickupAllow != null)
            {
                harmony.Patch(pickupAllow, postfix: new HarmonyMethod(typeof(PosterPickupPatch), nameof(PosterPickupPatch.AllowPostfix)));
            }
            else
            {
                Log.Warning("[KitsunePosters] TEFeaturePickup.AllowBlockActivationCommand not found - Take works inside your own land claim only.");
            }
            if (!GameManager.IsDedicatedServer)
            {
                UnityEngine.Font.textureRebuilt += PosterSurface.OnFontRebuilt;
            }
            Log.Out("[KitsunePosters] Patched (RefreshTextMesh postfix, SetText prefix, E-menu commands, pickup).");
        }
    }

    /// <summary>Which blocks this mod drives, by block name.</summary>
    public static class PosterBlocks
    {
        /// <summary>Any of ours: solid poster boards and graffiti decals.</summary>
        public static bool IsPoster(string _blockName)
        {
            return _blockName != null
                && (_blockName.StartsWith("kitsunePoster") || _blockName.StartsWith("kitsuneGraffiti"));
        }

        /// <summary>Graffiti: the board hides once an image shows, and the
        /// image keeps its PNG transparency (decal on the wall).</summary>
        public static bool IsGraffiti(string _blockName)
        {
            return _blockName != null && _blockName.StartsWith("kitsuneGraffiti");
        }
    }

    /// <summary>Adds "Poster settings" to a poster's E menu and opens the window on it.</summary>
    public static class SignCommandsPatch
    {
        public static void InitPostfix(TEFeatureSignable __instance, Action<BlockActivationCommand, TileEntityComposite.EBlockCommandOrder, TileEntityFeatureData> _addCallback)
        {
            string blockName = __instance.Parent?.TeData?.Block?.GetBlockName();
            if (PosterBlocks.IsPoster(blockName))
            {
                _addCallback(new BlockActivationCommand("kposters", "wrench", _enabled: true),
                    TileEntityComposite.EBlockCommandOrder.Normal, __instance.FeatureData);
            }
        }

        public static bool ActivatedPrefix(ReadOnlySpan<char> _commandName, ref bool __result)
        {
            if (!_commandName.SequenceEqual("kposters".AsSpan()))
            {
                return true;
            }
            XUiC_KitsunePosterSettings.Open();
            __result = true;
            return false;
        }
    }

    /// <summary>
    /// Vanilla TEFeaturePickup only offers Take inside the player's OWN land
    /// claim (it was made for the combine station). Posters are decor and get
    /// the usual CanPickup rule instead: anywhere the player could place one
    /// (not someone else's claim, not a trader), and a locked poster only for
    /// whoever may edit it.
    /// </summary>
    public static class PosterPickupPatch
    {
        public static void AllowPostfix(TEFeaturePickup __instance, ITileEntityFeature _module, WorldBase _world, Vector3i _blockPos, ref bool __result)
        {
            if (!__instance.Equals(_module))
            {
                return; // another feature's command
            }
            var parent = __instance.Parent;
            if (!PosterBlocks.IsPoster(parent?.TeData?.Block?.GetBlockName()))
            {
                return;
            }
            bool allowed = parent.PlayerPlaced
                && _world.CanPickupBlockAt(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
            var lockable = parent.GetFeature<TEFeatureLockable>();
            if (allowed && lockable != null && lockable.IsLocked())
            {
                allowed = lockable.IsUserAllowed(Platform.PlatformManager.InternalLocalUserIdentifier);
            }
            __result = allowed;
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
            if (!PosterBlocks.IsPoster(blockName))
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
            if (!PosterBlocks.IsPoster(blockName))
            {
                return;
            }
            PosterRegistry.Track(__instance);
            PosterSurface.BindTextMaterial(meshes);
            bool graffiti = PosterBlocks.IsGraffiti(blockName);

            var world = GameManager.Instance.World;
            Vector3i pos = __instance.Parent.ToWorldPos();
            float roll = PosterSurface.UprightRoll(world.GetBlock(pos));
            PosterSurface.SetRoll(meshes, roll);

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

            var mode = PosterUrl.ExtractMode(__instance.signText?.Text);
            PosterTextureManager.Get(url, tex =>
            {
                if (tex == null)
                {
                    // Download/decode failed: show the text again so the player
                    // can see (and fix) the URL they typed.
                    PosterSurface.ShowText(meshes);
                    return;
                }
                PosterSurface.Apply(meshes, tex, faceW, faceH, layout, mode, roll, graffiti);
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

    public enum PosterFit
    {
        Fill,    // cover the board, crop the overflow (the default)
        Fit,     // whole image visible, board shows round it
        Stretch, // cover the board, ignore aspect
    }

    public static class PosterUrl
    {
        /// <summary>A bare keyword next to the URL picks the fit mode.</summary>
        public static PosterFit ExtractMode(string _text)
        {
            if (string.IsNullOrEmpty(_text))
            {
                return PosterFit.Fill;
            }
            foreach (var token in _text.Split(' ', '\n', '\r', '\t'))
            {
                switch (token.Trim().ToLowerInvariant())
                {
                    case "fit": return PosterFit.Fit;
                    case "stretch": return PosterFit.Stretch;
                    case "fill": return PosterFit.Fill;
                }
            }
            return PosterFit.Fill;
        }

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
                    return Normalize(token.Trim());
                }
            }
            return null;
        }

        /// <summary>
        /// Players paste page links, not image links. The classic case is an
        /// imgur PAGE (imgur.com/abc123), which serves HTML and can never
        /// decode: rewrite it to the direct image (i.imgur.com/abc123.png,
        /// which imgur serves in the right format whatever the original was).
        /// Galleries and albums (/a/, /gallery/) can't be rewritten reliably.
        /// </summary>
        public static string Normalize(string _url)
        {
            // Fragments are client-side only; UnityWebRequest is known to
            // mangle them into the request path, which got imgur's 404 shell
            // instead of the gallery page. Strip before anything else.
            int hash = _url.IndexOf('#');
            if (hash >= 0)
            {
                _url = _url.Substring(0, hash);
            }
            try
            {
                var uri = new System.Uri(_url);
                string host = uri.Host.ToLowerInvariant();
                if (host == "imgur.com" || host == "www.imgur.com" || host == "m.imgur.com")
                {
                    string path = uri.AbsolutePath.Trim('/');
                    if (path.Length > 0 && !path.Contains("/") && !path.Contains("."))
                    {
                        return "https://i.imgur.com/" + path + ".png";
                    }
                }

                // Giphy: pages advertise WebP and files are GIF, neither of
                // which Unity decodes. Giphy's CDN serves a JPG still for
                // every id, so all giphy link shapes rewrite to that. A
                // poster shows the first frame; animation is a later feature.
                if (host == "giphy.com" || host == "www.giphy.com")
                {
                    var parts = uri.AbsolutePath.Trim('/').Split('/');
                    if (parts.Length >= 2 && parts[0] == "gifs")
                    {
                        string slug = parts[parts.Length - 1];
                        int dash = slug.LastIndexOf('-');
                        string id = dash >= 0 ? slug.Substring(dash + 1) : slug;
                        if (id.Length > 0)
                        {
                            return "https://i.giphy.com/media/" + id + "/480w_s.jpg";
                        }
                    }
                }
                else if (host.EndsWith(".giphy.com"))
                {
                    var parts = uri.AbsolutePath.Trim('/').Split('/');
                    int media = System.Array.IndexOf(parts, "media");
                    if (media >= 0 && parts.Length >= media + 3)
                    {
                        // .../media/(version junk/)*<id>/<file>
                        return "https://i.giphy.com/media/" + parts[parts.Length - 2] + "/480w_s.jpg";
                    }
                    if (parts.Length == 1 && parts[0].Contains("."))
                    {
                        // i.giphy.com/<id>.gif
                        return "https://i.giphy.com/media/" + parts[0].Substring(0, parts[0].IndexOf('.')) + "/480w_s.jpg";
                    }
                }
            }
            catch { }
            return _url;
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
