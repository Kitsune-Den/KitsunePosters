using System.Collections.Generic;

namespace KitsunePosters
{
    /// <summary>
    /// The clickable settings window: a per-machine image toggle for every
    /// player, and the server's site allowlist with add/remove buttons for
    /// admins (the server refuses edits from anyone else and the window says
    /// so). Opened from a poster's E menu or with `kposters menu`.
    /// </summary>
    public class XUiC_KitsunePosterSettings : XUiController
    {
        public const string GroupName = "kitsunePosterSettings";
        private const int RowCount = 8;

        private XUiC_TextInput input;
        private XUiV_Label status;
        private XUiV_Label hint;
        private XUiV_Label toggleLabel;
        private XUiV_Label toggleState;
        private readonly XUiV_Label[] rows = new XUiV_Label[RowCount];
        private readonly string[] shown = new string[RowCount];

        public override void Init()
        {
            base.Init();
            input = GetChildById("siteInput") as XUiC_TextInput;
            status = (XUiV_Label)GetChildById("status").ViewComponent;
            hint = (XUiV_Label)GetChildById("hint").ViewComponent;
            toggleLabel = (XUiV_Label)GetChildById("toggleLabel").ViewComponent;
            toggleState = (XUiV_Label)GetChildById("toggleState").ViewComponent;

            Wire("btnToggle", ToggleImages);
            Wire("btnAdd", AddSite);
            Wire("btnAllowAll", () => PosterNet.Send(NetPackageKitsunePosters.Op.AllowAll, ""));
            Wire("btnCloseSettings", () => xui.playerUI.windowManager.Close(GroupName));
            for (int i = 0; i < RowCount; i++)
            {
                int row = i; // captured
                rows[i] = (XUiV_Label)GetChildById("row" + i).ViewComponent;
                Wire("btnRemove" + i, () => RemoveRow(row));
            }
            PosterNet.Changed += Refresh;
        }

        private void Wire(string _id, System.Action _onPress)
        {
            var child = GetChildById(_id);
            if (child == null)
            {
                Log.Warning($"[KitsunePosters] Settings window: no '{_id}' in the XML; its button does nothing.");
                return;
            }
            child.OnPress += (_s, _b) =>
            {
                Log.Out($"[KitsunePosters] Settings window: {_id} pressed.");
                _onPress();
            };
        }

        public override void OnOpen()
        {
            base.OnOpen();
            hint.Text = "";
            PosterNet.Ask();
            Refresh();
        }

        private void ToggleImages()
        {
            PosterSettings.SetShowRemoteImages(!PosterSettings.ShowRemoteImages);
            PosterRegistry.Reapply();
            Refresh();
        }

        private void AddSite()
        {
            string site = input?.Text?.Trim();
            if (string.IsNullOrEmpty(site))
            {
                return;
            }
            PosterNet.Send(NetPackageKitsunePosters.Op.Add, site);
            if (input != null)
            {
                input.Text = "";
            }
        }

        private void RemoveRow(int _row)
        {
            if (shown[_row] != null)
            {
                PosterNet.Send(NetPackageKitsunePosters.Op.Remove, shown[_row]);
            }
        }

        private void Refresh()
        {
            toggleLabel.Text = "Show poster images on this PC";
            toggleState.Text = PosterSettings.ShowRemoteImages ? "[00E676]ON[-]" : "[FF5252]OFF[-]";

            string csv = PosterNet.CurrentCsv;
            var domains = new List<string>();
            bool allowAll = false;
            if (csv != null)
            {
                foreach (var d in csv.Split(','))
                {
                    var trimmed = d.Trim();
                    if (trimmed == "*")
                    {
                        allowAll = true;
                    }
                    else if (trimmed.Length > 0)
                    {
                        domains.Add(trimmed);
                    }
                }
            }

            if (csv == null)
            {
                status.Text = "Asking the server...";
            }
            else if (allowAll)
            {
                status.Text = "Posters may load images from EVERY site.";
            }
            else if (domains.Count == 0)
            {
                status.Text = "No sites allowed: every poster image is blocked.";
            }
            else
            {
                status.Text = "Posters may load images from these sites only:";
            }

            for (int i = 0; i < RowCount; i++)
            {
                shown[i] = i < domains.Count ? domains[i] : null;
                rows[i].Text = shown[i] ?? "";
            }

            if (PosterNet.LastDenied)
            {
                hint.Text = "Only server admins can change the site list.";
                PosterNet.LastDenied = false;
            }
            else if (domains.Count > RowCount)
            {
                hint.Text = $"...and {domains.Count - RowCount} more (console: kpadmin domains)";
            }
            else
            {
                hint.Text = "A site covers its subdomains: imgur.com also allows i.imgur.com.";
            }
        }

        public static void Open()
        {
            var ui = LocalPlayerUI.GetUIForPrimaryPlayer();
            if (ui == null)
            {
                return;
            }
            if (ui.windowManager.GetWindow(GroupName) == null)
            {
                // Happens when a server pushes XUi configs from an older copy
                // of the mod: the client code is current but the window's XML
                // never arrived.
                Log.Warning("[KitsunePosters] Settings window XML not loaded (older mod copy on the server?). Falling back to the console: kposters / kpadmin domains.");
                GameManager.ShowTooltip(ui.entityPlayer, "Poster settings window missing; use the console: kposters", string.Empty, "ui_denied");
                return;
            }
            if (!ui.windowManager.IsWindowOpen(GroupName))
            {
                ui.windowManager.Open(GroupName, _bModal: true);
            }
        }
    }
}
