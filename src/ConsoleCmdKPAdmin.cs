using System.Collections.Generic;
using UnityEngine;

namespace KitsunePosters
{
    /// <summary>Server-side admin tools. Permission level 0 (admins), runs on the server.</summary>
    public class ConsoleCmdKPAdmin : ConsoleCmdAbstract
    {
        public override string[] getCommands() => new[] { "kpadmin" };

        public override string getDescription() => "KitsunePosters admin: allowed sites, shared pictures, wipe and inspect posters";

        public override string getHelp() =>
            "kpadmin domains                    show which sites posters may load images from\n" +
            "kpadmin domains add <site>         allow a site (and its subdomains), e.g. imgur.com\n" +
            "kpadmin domains remove <site>      stop allowing a site\n" +
            "kpadmin domains allowall           allow every site (the default)\n" +
            "kpadmin images                     pictures players shared: on or off, space used\n" +
            "kpadmin images on|off              take players' own pictures, or stop taking new ones\n" +
            "kpadmin images list                every shared picture: who, when, its name and hash\n" +
            "kpadmin images remove <hash>       delete one for good, off every poster (the part after @)\n" +
            "kpadmin wipe <x> <y> <z> [radius]  clear the poster text there (and around, with a radius)\n" +
            "kpadmin info <x> <y> <z>           show a poster's text and its tiling group\n" +
            "kpadmin place <x> <y> <z> [block] [rotation 0-27]  put a poster block there (testing)\n" +
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
                case "domains" when _params.Count == 1:
                    Out("Posters may load images from: " + PosterSettings.DomainsSummary);
                    Out("(\"*\" means every site. kpadmin domains add/remove <site> changes it.)");
                    return;
                case "domains" when _params.Count >= 3 && _params[1].ToLowerInvariant() == "add":
                    Out(PosterSettings.AddDomain(_params[2]));
                    Out("Now allowed: " + PosterSettings.DomainsSummary);
                    return;
                case "domains" when _params.Count >= 3 && _params[1].ToLowerInvariant() == "remove":
                    Out(PosterSettings.RemoveDomain(_params[2]));
                    Out("Now allowed: " + PosterSettings.DomainsSummary);
                    return;
                case "domains" when _params.Count >= 2 && _params[1].ToLowerInvariant() == "allowall":
                    PosterSettings.AllowAll();
                    Out("Every site is allowed again.");
                    return;
                case "images":
                    Images(_params);
                    return;
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
                    BlockValue infoBv = world.GetBlock(infoAt);
                    Out($"rotation: {infoBv.rotation}, upright roll: {PosterSurface.UprightRoll(infoBv)}");
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
                    // Any of the 28 rotations, including the ones R can't reach on
                    // a multi-block poster, to check the uprighting against.
                    if (_params.Count > 5)
                    {
                        if (!byte.TryParse(_params[5], out byte rot) || rot > 27)
                        {
                            Out("Rotation must be 0-27.");
                            return;
                        }
                        bv.rotation = rot;
                    }
                    world.SetBlockRPC(placeAt, bv);
                    Out($"{blockName} placed at {placeAt}, rotation {bv.rotation}.");
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
                    // Counts as the host's own edit: their image files get shared.
                    PosterShare.LocalEdit = true;
                    try { signable.SetText(text); }
                    finally { PosterShare.LocalEdit = false; }
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

        private static void Images(List<string> _params)
        {
            string sub = _params.Count > 1 ? _params[1].ToLowerInvariant() : "";
            switch (sub)
            {
                case "on":
                case "off":
                    PosterSettings.SetAllowUploads(sub == "on");
                    PosterNet.RaiseChanged();
                    Out(sub == "on"
                        ? "Players can share pictures from their own PCs."
                        : "New shared pictures are refused. The ones already here still show; kpadmin images remove <hash> takes one away.");
                    return;
                case "list":
                {
                    var entries = PosterShare.Entries();
                    if (entries.Count == 0)
                    {
                        Out("Nobody has shared a picture in this world yet.");
                        return;
                    }
                    foreach (var e in entries)
                    {
                        Out($"{e.Name}@{e.Hash}  {e.Bytes / 1024} KB  by {e.Who} ({e.WhoId})  {e.When} UTC");
                    }
                    return;
                }
                case "remove" when _params.Count >= 3:
                {
                    // Takes the hash, or the whole poster word ("cat.png@3f9a...").
                    string hash = _params[2].ToLowerInvariant();
                    int at = hash.LastIndexOf('@');
                    if (at >= 0)
                    {
                        hash = hash.Substring(at + 1);
                    }
                    if (!PosterShare.IsHash(hash))
                    {
                        Out($"Not a picture's hash: {_params[2]}. It's the {PosterShare.HashLength} letters and digits after the @ (kpadmin images list).");
                        return;
                    }
                    Out(PosterShare.StoreRemove(hash)
                        ? $"Removed {hash}: deleted, taken off every poster, and refused if anyone shares it again."
                        : $"{hash} wasn't stored here, but it's now refused if anyone shares it.");
                    return;
                }
                case "":
                {
                    var entries = PosterShare.Entries();
                    long used = PosterShare.StoreUsed();
                    Out($"Players' own pictures: {(PosterSettings.AllowUploads ? "on" : "off")}.");
                    Out($"{entries.Count} stored, {used / (1024 * 1024.0):0.#} MB of {PosterSettings.UploadSpaceMB} MB (UploadSpaceMB in Settings.xml).");
                    return;
                }
                default:
                    Out("kpadmin images [on|off|list|remove <hash>]");
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
