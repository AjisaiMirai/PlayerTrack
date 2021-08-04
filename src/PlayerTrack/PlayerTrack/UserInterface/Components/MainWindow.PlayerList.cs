using System.Linq;
using System.Numerics;

using CheapLoc;
using Dalamud.Interface;
using ImGuiNET;

namespace PlayerTrack
{
    /// <summary>
    /// Player List.
    /// </summary>
    public partial class MainWindow
    {
        private string menuPlayerKey = string.Empty;

        private void ClearSelectedPlayer()
        {
            this.menuPlayerKey = string.Empty;
            this.SelectedPlayer = null;
        }

        private void PlayerList()
        {
            ImGui.BeginChild(
                "###PlayerTrack_PlayerList_Child",
                new Vector2(205 * ImGuiHelpers.GlobalScale, 0),
                true);

            var players = this.plugin.PlayerService.GetPlayers(this.searchInput);

            // use clipper to avoid performance hit on large player lists
            ImGuiListClipperPtr clipper;
            unsafe
            {
                clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
            }

            clipper.Begin(players.Length);
            while (clipper.Step())
            {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    ImGui.BeginGroup();
                    var color = this.plugin.PlayerService.GetPlayerListColor(players[i].Value);
                    ImGui.PushStyleColor(ImGuiCol.Text, color);
                    if (ImGui.Selectable(
                        "###PlayerTrack_Player_Selectable_" + i,
                        this.SelectedPlayer == players[i].Value,
                        ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        // suppress double clicks
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            // ignored
                        }

                        // hide right panel if clicking same user while already open
                        else if (this.SelectedPlayer?.Key == players[i].Key && this.plugin.Configuration.CurrentView == View.PlayerDetail)
                        {
                            this.ClearSelectedPlayer();
                            this.HideRightPanel();
                        }

                        // open player in right panel
                        else
                        {
                            this.SelectedPlayer = players[i].Value;
                            this.SelectedEncounters = this.plugin.EncounterService
                                                          .GetEncountersByPlayer(this.SelectedPlayer.Key)
                                                          .OrderByDescending(enc => enc.Created).ToList();
                            this.ShowRightPanel(View.PlayerDetail);
                        }
                    }

                    // remove extra padding
                    ImGuiHelpers.ScaledRelativeSameLine(-10f);

                    // player icon
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.TextColored(color,  this.plugin.PlayerService.GetPlayerIcon(players[i].Value));
                    ImGui.PopFont();

                    // player name;
                    ImGui.SameLine();
                    ImGui.Text(players[i].Value.Names[0]);

                    ImGui.PopStyleColor(1);
                    ImGui.EndGroup();

                    // open menu options for selected player
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    {
                        this.menuPlayerKey = players[i].Key;
                        ImGui.OpenPopup("###PlayerTrack_Player_Popup_" + players[i].Value.Id);
                    }

                    // menu for selected player
                    if (ImGui.BeginPopup("###PlayerTrack_Player_Popup_" + players[i].Value.Id))
                    {
                        // validate player
                        var menuPlayer = this.plugin.PlayerService.GetPlayer(this.menuPlayerKey);
                        if (menuPlayer == null) return;

                        // menu items for actions
                        if (ImGui.MenuItem(Loc.Localize("TargetPlayer", "Target"), menuPlayer.IsCurrent))
                        {
                            this.plugin.PluginService.ClientState.Targets.SetCurrentTarget(menuPlayer.ActorId);
                        }

                        if (ImGui.MenuItem(Loc.Localize("FocusTargetPlayer", "Focus Target"), menuPlayer.IsCurrent))
                        {
                            this.plugin.PluginService.ClientState.Targets.SetFocusTarget(menuPlayer.ActorId);
                        }

                        if (ImGui.MenuItem(Loc.Localize("ExaminePlayer", "Examine"), menuPlayer.IsCurrent))
                        {
                            this.plugin.OpenExamineWindow(menuPlayer.ActorId);
                        }

                        if (ImGui.MenuItem(
                            Loc.Localize("Lodestone", "Lodestone"),
                            menuPlayer.LodestoneStatus == LodestoneStatus.Verified))
                        {
                            this.plugin.LodestoneService.OpenLodestoneProfile(players[i].Value.LodestoneId);
                        }

                        ImGui.Separator();

                        // sub menu for selecting category
                        if (ImGui.BeginMenu(Loc.Localize("Category", "Category")))
                        {
                            foreach (var category in this.plugin.CategoryService.GetCategories())
                            {
                                if (ImGui.MenuItem(
                                    category.Value.Name,
                                    string.Empty,
                                    category.Key == menuPlayer.CategoryId,
                                    true))
                                {
                                    menuPlayer.CategoryId = category.Key;
                                    this.plugin.PlayerService.UpdatePlayer(menuPlayer);
                                }
                            }

                            ImGui.EndMenu();
                        }

                        // delete with modal confirmation
                        if (ImGui.MenuItem(Loc.Localize("Delete", "Delete"), !menuPlayer.IsCurrent))
                        {
                            this.plugin.WindowManager.ModalWindow.Open(ModalWindow.ModalType.ConfirmDelete, menuPlayer);
                        }

                        ImGui.EndPopup();
                    }
                }
            }

            clipper.End();
            ImGui.EndChild();
        }
    }
}
