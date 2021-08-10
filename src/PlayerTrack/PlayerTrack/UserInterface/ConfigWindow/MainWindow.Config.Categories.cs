using System;
using System.Collections.Generic;
using System.Linq;

using CheapLoc;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ImGuiNET;

namespace PlayerTrack
{
    /// <summary>
    /// Category Config.
    /// </summary>
    public partial class ConfigWindow
    {
        private void CategoryConfig()
        {
             // get sorted categories
            var categories = this.Plugin.CategoryService.GetSortedCategories().ToList();

            // don't display if no categories (shouldn't happen in theory)
            if (!categories.Any()) return;

            // add category
            if (ImGui.SmallButton(Loc.Localize("Add", "Add Category") + "###PlayerTrack_CategoryAdd_Button"))
            {
                this.Plugin.CategoryService.AddCategory();
            }

            // setup category table
            ImGui.Separator();
            ImGui.Columns(7, "###PlayerTrack_CategoryTable_Columns", true);
            var baseWidth = ImGui.GetWindowSize().X / 6 * ImGuiHelpers.GlobalScale;
            ImGui.SetColumnWidth(0, baseWidth + 20f);                // name
            ImGui.SetColumnWidth(1, ImGuiHelpers.GlobalScale * 70f); // isDefault
            ImGui.SetColumnWidth(2, ImGuiHelpers.GlobalScale * 80f); // nameplates
            ImGui.SetColumnWidth(3, ImGuiHelpers.GlobalScale * 50f); // alerts
            ImGui.SetColumnWidth(4, ImGuiHelpers.GlobalScale * 70f); // colors
            ImGui.SetColumnWidth(5, baseWidth + 40f);                // icon
            ImGui.SetColumnWidth(6, baseWidth + 80f);                // controls

            // add table headings
            ImGui.Text(Loc.Localize("CategoryName", "Name"));
            ImGui.NextColumn();
            ImGui.Text(Loc.Localize("CategoryDefault", "IsDefault"));
            ImGui.NextColumn();
            ImGui.Text(Loc.Localize("CategoryNamePlates", "NamePlates"));
            ImGui.NextColumn();
            ImGui.Text(Loc.Localize("CategoryAlerts", "Alerts"));
            ImGui.NextColumn();
            ImGui.Text(Loc.Localize("CategoryColors", "Colors"));
            ImGui.NextColumn();
            ImGui.Text(Loc.Localize("CategoryIcon", "Icon"));
            ImGui.NextColumn();
            ImGui.Text(Loc.Localize("CategoryAction", "Actions"));
            ImGui.NextColumn();
            ImGui.Separator();

            // loop through categories
            for (var i = 0; i < categories.Count; i++)
            {
                var category = categories[i].Value;

                // category name
                var categoryName = category.Name;
                ImGui.SetNextItemWidth(baseWidth);
                if (ImGui.InputText("###PlayerTrack_CategoryName_Input" + i, ref categoryName, 20))
                {
                    category.Name = categoryName;
                    this.Plugin.CategoryService.SaveCategory(category);
                }

                // category default
                ImGui.NextColumn();
                var isDefault = category.IsDefault;
                if (isDefault)
                {
                    ImGui.PushFont(UiBuilder.IconFont);
                    ImGui.Text(FontAwesomeIcon.Check.ToIconString());
                    ImGui.PopFont();
                }
                else
                {
                    if (ImGui.Checkbox(
                        "###PlayerTrack_CategoryDefault_Checkbox" + i,
                        ref isDefault))
                    {
                        var oldDefaultCategory = this.Plugin.CategoryService.GetDefaultCategory();
                        category.IsDefault = isDefault;
                        this.Plugin.CategoryService.SaveCategory(category);
                        oldDefaultCategory.IsDefault = false;
                        this.Plugin.CategoryService.SaveCategory(oldDefaultCategory);
                    }
                }

                // category nameplates
                ImGui.NextColumn();
                var enableNamePlates = category.IsNamePlateEnabled;
                if (ImGui.Checkbox(
                    "###PlayerTrack_EnableNamePlates_Checkbox" + i,
                    ref enableNamePlates))
                {
                    category.IsNamePlateEnabled = enableNamePlates;
                    this.Plugin.CategoryService.SaveCategory(category);
                }

                // category alerts
                ImGui.NextColumn();
                var enableAlerts = category.IsAlertEnabled;
                if (ImGui.Checkbox(
                    "###PlayerTrack_EnableCategoryAlerts_Checkbox" + i,
                    ref enableAlerts))
                {
                    category.IsAlertEnabled = enableAlerts;
                    this.Plugin.CategoryService.SaveCategory(category);
                }

                // category list color
                ImGui.NextColumn();
                var categoryListColor = category.EffectiveListColor();
                if (ImGui.ColorButton("List Color###PlayerTrack_CategoryListColor_Button" + i, categoryListColor))
                    ImGui.OpenPopup("###PlayerTrack_CategoryListColor_Popup" + i);
                if (ImGui.BeginPopup("###PlayerTrack_CategoryListColor_Popup" + i))
                {
                    if (ImGui.ColorPicker4("List Color###PlayerTrack_CategoryListColor_ColorPicker" + i, ref categoryListColor))
                    {
                        category.ListColor = categoryListColor;
                        this.Plugin.CategoryService.SaveCategory(category);
                    }

                    this.CategoryListColorSwatchRow(category, category.Id, 0, 8);
                    this.CategoryListColorSwatchRow(category, category.Id, 8, 16);
                    this.CategoryListColorSwatchRow(category, category.Id, 16, 24);
                    this.CategoryListColorSwatchRow(category, category.Id, 24, 32);
                    ImGui.EndPopup();
                }

                // category nameplate color
                ImGui.SameLine();
                var categoryNamePlateColor = category.EffectiveNamePlateColor();
                if (ImGui.ColorButton("NamePlate Color###PlayerTrack_CategoryNamePlateColor_Button" + i, categoryNamePlateColor))
                    ImGui.OpenPopup("###PlayerTrack_CategoryNamePlateColor_Popup" + i);
                if (ImGui.BeginPopup("###PlayerTrack_CategoryNamePlateColor_Popup" + i))
                {
                    if (ImGui.ColorPicker4("NamePlate Color###PlayerTrack_CategoryNamePlateColor_ColorPicker" + i, ref categoryNamePlateColor))
                    {
                        category.NamePlateColor = categoryNamePlateColor;
                        this.Plugin.CategoryService.SaveCategory(category);
                        this.plugin.NamePlateManager.ForceRedraw();
                    }

                    this.CategoryNamePlateColorSwatchRow(category, category.Id, 0, 8);
                    this.CategoryNamePlateColorSwatchRow(category, category.Id, 8, 16);
                    this.CategoryNamePlateColorSwatchRow(category, category.Id, 16, 24);
                    this.CategoryNamePlateColorSwatchRow(category, category.Id, 24, 32);
                    ImGui.EndPopup();
                }

                // category icon
                ImGui.NextColumn();

                var categoryIcon = category.Icon;
                var namesList = new List<string> { Loc.Localize("Default", "Default") };
                namesList.AddRange(this.Plugin.Configuration.EnabledIcons.ToList()
                                       .Select(icon => icon.ToString()));
                var names = namesList.ToArray();
                var codesList = new List<int>
                {
                    0,
                };
                codesList.AddRange(this.Plugin.Configuration.EnabledIcons.ToList().Select(icon => (int)icon));
                var codes = codesList.ToArray();
                var iconIndex = Array.IndexOf(codes, categoryIcon);
                ImGui.SetNextItemWidth(baseWidth);
                if (ImGui.Combo("###PlayerTrack_SelectCategoryIcon_Combo" + i, ref iconIndex, names, names.Length))
                {
                    category.Icon = codes[iconIndex];
                    this.Plugin.CategoryService.SaveCategory(category);
                }

                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.Text(categoryIcon != 0
                               ? ((FontAwesomeIcon)categoryIcon).ToIconString()
                               : FontAwesomeIcon.User.ToIconString());
                ImGui.PopFont();

                // category actions
                ImGui.NextColumn();
                if (category.Rank != 0)
                {
                    if (ImGuiComponents.IconButton(category.Id + 1, FontAwesomeIcon.ArrowUp))
                    {
                        this.Plugin.CategoryService.IncreaseCategoryRank(category.Id);
                    }

                    ImGui.SameLine();
                }

                if (category.Rank != this.Plugin.CategoryService.MaxRank())
                {
                    if (ImGuiComponents.IconButton(category.Id + 2, FontAwesomeIcon.ArrowDown))
                    {
                        this.Plugin.CategoryService.DecreaseCategoryRank(category.Id);
                    }

                    ImGui.SameLine();
                }

                if (ImGuiComponents.IconButton(category.Id, FontAwesomeIcon.Redo))
                {
                    category.Reset();
                    this.Plugin.CategoryService.SaveCategory(category);
                }

                ImGui.SameLine();

                if (!category.IsDefault)
                {
                    if (ImGuiComponents.IconButton(category.Id + 3, FontAwesomeIcon.Trash))
                    {
                        this.Plugin.CategoryService.DeleteCategory(category.Id);
                    }
                }

                ImGui.NextColumn();
            }

            ImGui.Separator();
        }

        private void CategoryListColorSwatchRow(Category category, int id, int min, int max)
        {
            ImGui.Spacing();
            for (var i = min; i < max; i++)
            {
                if (ImGui.ColorButton("###PlayerTrack_CategoryListColor_Swatch_" + id + i, this.colorPalette[i]))
                {
                    category.ListColor = this.colorPalette[i];
                    this.Plugin.CategoryService.SaveCategory(category);
                }

                ImGui.SameLine();
            }
        }

        private void CategoryNamePlateColorSwatchRow(Category category, int id, int min, int max)
        {
            ImGui.Spacing();
            for (var i = min; i < max; i++)
            {
                if (ImGui.ColorButton("###PlayerTrack_CategoryNamePlateColor_Swatch_" + id + i, this.colorPalette[i]))
                {
                    category.NamePlateColor = this.colorPalette[i];
                    this.Plugin.CategoryService.SaveCategory(category);
                    this.plugin.NamePlateManager.ForceRedraw();
                }

                ImGui.SameLine();
            }
        }
    }
}
