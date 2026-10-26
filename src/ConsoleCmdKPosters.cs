using System.Collections.Generic;

namespace KitsunePosters
{
    public class ConsoleCmdKPosters : ConsoleCmdAbstract
    {
        // A per-machine display toggle: every player may use it, and it runs
        // on their own client, not the server.
        public override bool IsExecuteOnClient => true;
        public override int DefaultPermissionLevel => 1000;

        public override string[] getCommands() => new[] { "kposters" };

        public override string getDescription() => "KitsunePosters: toggle poster images on this client, reload settings";

        public override string getHelp() =>
            "kposters              show the current settings\n" +
            "kposters on|off       show or hide poster images on THIS machine (saved)\n" +
            "kposters reload       re-read Settings.xml (allowlist)\n" +
            "kposters allow <url>  say whether the allowlist passes that URL";

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            string cmd = _params.Count > 0 ? _params[0].ToLowerInvariant() : "status";
            switch (cmd)
            {
                case "on":
                case "off":
                    PosterSettings.SetShowRemoteImages(cmd == "on");
                    PosterRegistry.Reapply();
                    Out($"Poster images {cmd} on this machine (saved).");
                    return;
                case "reload":
                    PosterSettings.Reload();
                    PosterTextureManager.ClearFailures(); // failed URLs get another try
                    PosterRegistry.Reapply();
                    Out($"Settings reloaded. AllowedDomains: {string.Join(", ", PosterSettings.AllowedDomains)}");
                    return;
                case "allow" when _params.Count >= 2:
                    Out(PosterSettings.IsDomainAllowed(_params[1])
                        ? $"Allowed: {_params[1]}"
                        : $"Blocked: {_params[1]}");
                    return;
                case "status":
                    Out($"Poster images: {(PosterSettings.ShowRemoteImages ? "on" : "off")}");
                    Out($"AllowedDomains: {string.Join(", ", PosterSettings.AllowedDomains)}");
                    return;
                default:
                    Out(getHelp());
                    return;
            }
        }

        private static void Out(string s) => SingletonMonoBehaviour<SdtdConsole>.Instance.Output(s);
    }
}
