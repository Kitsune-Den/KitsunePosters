using System.Collections.Generic;
using UnityEngine;

namespace KitsunePosters
{
    /// <summary>Server-side admin tools. Permission level 0 (admins), runs on the server.</summary>
    public class ConsoleCmdKPAdmin : ConsoleCmdAbstract
    {
        public override string[] getCommands() => new[] { "kpadmin" };

        public override string getDescription() => "KitsunePosters admin: wipe poster URLs, inspect posters";

        public override string getHelp() =>
            "kpadmin wipe <x> <y> <z> [radius]  clear the poster text there (and around, with a radius)\n" +
            "kpadmin info <x> <y> <z>           show a poster's text and its tiling group\n" +
            "kpadmin place <x> <y> <z> [block]  put a poster block there (testing)\n" +
            "kpadmin text <x> <y> <z> <text..>  set a poster's text server side (testing)\n" +
            "kpadmin load <x> <z>               keep that area loaded with no one there (testing)";

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            World world = GameManager.Instance.World;
            if (world == null || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                Out("Run this on the server once the game has started.");
                return;
            }
            string cmd = _params.Count > 0 ? _params[0].ToLowerInvariant() : "";
            switch (cmd)
            {
                case "wipe" when _params.Count >= 4 && TryPos(_params, out Vector3i wipeAt):
                {
                    int radius = 0;
                    if (_params.Count > 4)
                    {
                        int.TryParse(_params[4], out radius);
                    }
                    radius = Mathf.Clamp(radius, 0, 8);
                    int wiped = 0;
                    for (int x = -radius; x <= radius; x++)
                    for (int y = -radius; y <= radius; y++)
                    for (int z = -radius; z <= radius; z++)
                    {
                        var p = wipeAt + new Vector3i(x, y, z);
                        if (!PosterTiling.IsPosterBlock(world.GetBlock(p)))
                        {
                            continue;
                        }
                        var signable = PosterTiling.SignableAt(world, p);
                        if (signable != null && !string.IsNullOrEmpty(signable.signText?.Text))
                        {
                            signable.SetText("");
                            wiped++;
                        }
                    }
                    Out(wiped == 0 ? "No poster with text there." : $"Wiped {wiped} poster(s).");
                    return;
                }
                case "info" when _params.Count >= 4 && TryPos(_params, out Vector3i infoAt):
                {
                    if (!PosterTiling.IsPosterBlock(world.GetBlock(infoAt)))
                    {
                        Out("Not a poster block.");
                        return;
                    }
                    var signable = PosterTiling.SignableAt(world, infoAt);
                    if (signable == null)
                    {
                        Out("No sign tile entity there (chunk loaded?).");
                        return;
                    }
                    Out($"text: '{signable.signText?.Text}'");
                    var layout = PosterTiling.Compute(world, infoAt);
                    Out(layout.Tiled
                        ? $"group: {layout.Cols}x{layout.Rows}, this block at col {layout.Col}, row {layout.Row}"
                        : "group: single");
                    return;
                }
                case "place" when _params.Count >= 4 && TryPos(_params, out Vector3i placeAt):
                {
                    string blockName = _params.Count > 4 ? _params[4] : "kitsunePoster1x1";
                    BlockValue bv = Block.GetBlockValue(blockName);
                    if (bv.isair)
                    {
                        Out($"Block {blockName} not known - is the mod's blocks.xml loaded?");
                        return;
                    }
                    world.SetBlockRPC(placeAt, bv);
                    Out($"{blockName} placed at {placeAt}.");
                    return;
                }
                case "text" when _params.Count >= 5 && TryPos(_params, out Vector3i textAt):
                {
                    var signable = PosterTiling.SignableAt(world, textAt);
                    if (signable == null)
                    {
                        Out("No sign tile entity there (chunk loaded? block placed?).");
                        return;
                    }
                    string text = string.Join(" ", _params.GetRange(4, _params.Count - 4));
                    signable.SetText(text);
                    Out($"Text set. Now: '{signable.signText?.Text}'");
                    return;
                }
                case "load" when _params.Count >= 3
                                 && int.TryParse(_params[1], out int lx)
                                 && int.TryParse(_params[2], out int lz):
                    GameManager.Instance.AddChunkObserver(new Vector3(lx, 0f, lz), true, 4, -1);
                    Out($"Holding the chunks round {lx} {lz} loaded until restart.");
                    return;
                default:
                    Out(getHelp());
                    return;
            }
        }

        private static bool TryPos(List<string> p, out Vector3i pos)
        {
            pos = Vector3i.zero;
            if (int.TryParse(p[1], out int x) && int.TryParse(p[2], out int y) && int.TryParse(p[3], out int z))
            {
                pos = new Vector3i(x, y, z);
                return true;
            }
            return false;
        }

        private static void Out(string s) => SingletonMonoBehaviour<SdtdConsole>.Instance.Output(s);
    }
}
