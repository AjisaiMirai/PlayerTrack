using System.Linq;

using CheapLoc;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ImGuiNET;

namespace PlayerTrack
{
    /// <summary>
    /// Icon Settings.
    /// </summary>
    public partial class MainWindow
    {
        private int selectedIconIndex = 4;

        private void IconSettings()
        {
            ImGui.Text(Loc.Localize("Icons", "Add / Remove Icons"));
            ImGuiComponents.HelpMarker(Loc.Localize(
                "AddRemoveIcons",
                "add new icons using dropdown or remove icons by clicking on them"));
            ImGui.Spacing();
            ImGui.SetNextItemWidth(ImGui.GetWindowSize().X / 2);
            ImGui.Combo(
                "###PlayerTrack_Icon_Combo",
                ref this.selectedIconIndex,
                this.Plugin.PluginService.IconNames,
                this.Plugin.PluginService.Icons.Length);
            ImGui.SameLine();

            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.Text(this.Plugin.PluginService.Icons[this.selectedIconIndex].ToIconString());
            ImGui.PopFont();
            ImGui.SameLine();

            if (ImGui.SmallButton(Loc.Localize("Add", "Add") + "###PlayerTrack_IconAdd_Button"))
            {
                if (this.Plugin.Configuration.EnabledIcons.Contains(this.Plugin.PluginService.Icons[this.selectedIconIndex]))
                {
                    ImGui.OpenPopup("###PlayerTrack_DupeIcon_Popup");
                }
                else
                {
                    this.Plugin.Configuration.EnabledIcons.Add(this.Plugin.PluginService.Icons[this.selectedIconIndex]);
                    this.Plugin.SaveConfig();
                }
            }

            ImGui.SameLine();
            if (ImGui.SmallButton(Loc.Localize("Reset", "Reset") + "###PlayerTrack_IconReset_Button"))
            {
                this.selectedIconIndex = 4;
                this.Plugin.SaveConfig();
            }

            if (ImGui.BeginPopup("###PlayerTrack_DupeIcon_Popup"))
            {
                ImGui.Text(Loc.Localize("DupeIcon", "This icon is already added!"));
                ImGui.EndPopup();
            }

            ImGui.Spacing();

            foreach (var enabledIcon in this.Plugin.Configuration.EnabledIcons.ToList())
            {
                ImGui.BeginGroup();
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.Text(enabledIcon.ToIconString());
                ImGui.PopFont();
                ImGui.SameLine();
                ImGui.Text(enabledIcon.ToString());
                ImGui.EndGroup();
                if (ImGui.IsItemClicked())
                {
                    this.Plugin.Configuration.EnabledIcons.Remove(enabledIcon);
                    this.Plugin.SaveConfig();
                }
            }

            ImGui.Spacing();
        }
    }
}
