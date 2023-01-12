using AutoJailMarker.Classes;
using AutoJailMarker.Data;
using AutoJailMarker.Managers;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ImGuiNET;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Logging;

namespace AutoJailMarker.Windows;

internal class ConfigWindow : IDisposable
{
    private readonly AutoJailMarkerConfig config;
    private readonly TextureWrap titanImage;
    private readonly AutoJailMarkerPlugin autoJailMarkerPlugin;

    public bool Visible = false;
    private bool headerOpened;

    public ConfigWindow(AutoJailMarkerConfig config, TextureWrap titanImage, AutoJailMarkerPlugin autoJailMarkerPlugin)
    {
        this.config = config;
        this.titanImage = titanImage;
        this.autoJailMarkerPlugin = autoJailMarkerPlugin;
    }

    public void Dispose()
    {
        titanImage?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Main draw handler
    /// </summary>
    public void Draw()
    {
        DrawSettingsWindow();
    }

    /// <summary>
    /// Settings draw handler
    /// </summary>
    private void DrawSettingsWindow()
    {
        if (!Visible) return;

        var minSize = new Vector2(282, 280);
        var initSize = new Vector2(282, 430);
        var partySize = Service.PartyList.Length;

        ImGui.SetNextWindowSize(initSize, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(minSize, new Vector2(float.MaxValue, float.MaxValue));
        if (ImGui.Begin("Auto Jail Marker - Settings", ref Visible))
        {
            DrawGeneralSection();
            DrawPrioritySection();
            DrawChecks(partySize);
            DrawInformation();
            DrawPartyTable(partySize);

            // Titan image
            if (titanImage != null)
            {
                ImGui.Indent(10 * ImGuiHelpers.GlobalScale);
                ImGui.Image(titanImage.ImGuiHandle, new Vector2(titanImage.Width, titanImage.Height));
                ImGui.Unindent(10 * ImGuiHelpers.GlobalScale);
            }
        }

        ImGui.End();
    }

    /// <summary>
    /// Draws the general section
    /// </summary>
    private void DrawGeneralSection()
    {
        ImGui.Text($"General Settings");

        ImGui.Indent(25 * ImGuiHelpers.GlobalScale);

        if (ImGui.Checkbox("Enabled", ref config.Enabled)) config.Save();

        if (ImGui.Checkbox("Debug messages", ref config.Debug)) config.Save();
        SetHoverTooltip("On: Output debug messages");

        ImGui.Unindent(25 * ImGuiHelpers.GlobalScale);

        ImGui.Spacing();
    }

    /// <summary>
    /// Draws the priority section
    /// </summary>
    private void DrawPrioritySection()
    {
        const string checkName = "Use job priority";

        ImGui.Text($"Priority Settings");

        ImGui.Indent(25 * ImGuiHelpers.GlobalScale);

        if (!config.UseJobPrio && config.Prio.All(p => p == string.Empty))
            ImGui.PushStyleColor(ImGuiCol.Text, 4284769535);

        if (ImGui.Button("Change Priority")) autoJailMarkerPlugin.OnCommand(Helper.PriorityCommand, "");

        if (!config.UseJobPrio && config.Prio.All(p => p == string.Empty))
        {
            SetHoverTooltip("No priorities have been configured yet\nClick here to configure");
            ImGui.PopStyleColor();
        }

        ImGui.SameLine();

        if (ImGui.Checkbox(checkName, ref config.UseJobPrio)) config.Save();
        SetHoverTooltip("On: Use jobs as priority\nOff: Use player names as priority");

        ImGui.Unindent(25 * ImGuiHelpers.GlobalScale);

        ImGui.Spacing();
    }

    /// <summary>
    /// Draws the checks section
    /// </summary>
    /// <param name="partySize"></param>
    private void DrawChecks(int partySize)
    {
        ImGui.Text("Checks");
        ImGui.Indent(25 * ImGuiHelpers.GlobalScale);

        DrawTryMarksButton(partySize);

        ImGui.SameLine();

        DrawCheckPrioButton(partySize);

        ImGui.Unindent(25 * ImGuiHelpers.GlobalScale);

        ImGui.Spacing();
    }

    /// <summary>
    /// Draws the try marks button
    /// </summary>
    /// <param name="partySize">Size of the current party</param>
    private static void DrawTryMarksButton(int partySize)
    {
        if (ImGui.Button("Try Marks"))
        {
            var currentMark = 1;
            partySize = partySize > 3 ? 3 : partySize == 0 ? 1 : partySize;

            for (var i = 1; i <= partySize; i++)
            {
                if (partySize <= 1 || Service.PartyList[i - 1]?.ObjectId == 0) continue;
                
                PluginLog.Debug($"/mk attack{i} <{currentMark}>");
                Service.ChatManager.SendCommand($"/mk attack{i} <{currentMark}>");
                currentMark++;
            }
        }

        SetHoverTooltip("Try to see if the plugin can set marks in general");
    }

    /// <summary>
    /// Draws the check prio button
    /// </summary>
    /// <param name="partySize">Size of the current party</param>
    private void DrawCheckPrioButton(int partySize)
    {
        if (partySize >= Helper.JailCount)
        {
            if (ImGui.Button("Check Priority"))
            {
                autoJailMarkerPlugin.UpdateOrderedParty(config.Debug);
                var partyPrioList = autoJailMarkerPlugin.CreatePartyPrioList(config.Debug);

                var playersMarked = 0;
                var notInPrio = false;
                var rnd = new Random();

                var randomized = Enumerable.Range(0, partySize).ToList();
                randomized = randomized.OrderBy(_ => rnd.Next()).ToList().GetRange(0, Helper.JailCount);

                var marked = autoJailMarkerPlugin.OrderedPartyList.Where((_, i) => randomized.Contains(i))
                    .Select(pChar => pChar.ObjectId).ToList();

                for (var i = 0; i < partyPrioList.Count; i++)
                {
                    if (!config.UseJobPrio &&
                        !config.Prio.Any(n => n != "" && partyPrioList[i].Name.ToLower().StartsWith(n.ToLower())) &&
                        !notInPrio)
                    {
                        ChatManager.PrintError(Helper.NotInPrioMessage);
                        notInPrio = true;
                    }

                    if (!marked.Contains(partyPrioList[i].ObjectId)) continue;

                    ChatManager.PrintEcho(Helper.MarkPrefix[playersMarked] +
                                          string.Format(Helper.MarkMessage, partyPrioList[i].Name, i + 1));
                    playersMarked++;
                }
            }

            SetHoverTooltip("Check if the party prio is working. 3 players are selected randomly.");
        }
        else
        {
            ImGuiComponents.DisabledButton("Check Priority");
            var hoverText = partySize == 0
                ? "Not in a party. Cross-World party are not supported"
                : "Party size must be greater than 2";

            SetHoverTooltip(hoverText);
        }
    }

    /// <summary>
    /// Draws the information section
    /// </summary>
    private static void DrawInformation()
    {
        ImGui.Text("Information");
        ImGui.Indent(25 * ImGuiHelpers.GlobalScale);

        var territoryType = Service.ClientState.TerritoryType;
        var placeName = Service.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryType)?
            .PlaceName.Value?.Name;
        var cPlace = placeName == null ? $"{territoryType}" : $"{placeName} ({territoryType})";

        ImGui.Text($"Current place: {cPlace}");

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, ImGui.GetStyle().ItemSpacing.Y));

        ImGui.Text("Marking ");
        ImGui.SameLine();
        if (territoryType == 777)
        {
            ImGui.TextColored(ImGuiColors.HealerGreen, "activated");
            ImGui.SameLine();
            ImGui.Text(": In UwU");
        }
        else
        {
            ImGui.TextColored(ImGuiColors.DPSRed, "deactivated");
            ImGui.SameLine();
            ImGui.Text(": Not in UwU");
        }

        ImGui.PopStyleVar();

        ImGui.Unindent(25 * ImGuiHelpers.GlobalScale);

        ImGui.Spacing();
    }

    /// <summary>
    /// Draws the collapsible party list from game data
    /// </summary>
    /// <param name="partySize">Size of the current party</param>
    private void DrawPartyTable(int partySize)
    {
        if (!Helper.PlayerExists || partySize <= 1) return;

        ImGui.SetNextItemOpen(headerOpened);
        if (ImGui.CollapsingHeader("Current Party list from Game data"))
        {
            headerOpened = true;

            ImGui.Indent(25 * ImGuiHelpers.GlobalScale);

            if (ImGui.BeginTable("PartyTable", 2, ImGuiTableFlags.PreciseWidths))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 120);
                ImGui.TableSetupColumn("ObjectID", ImGuiTableColumnFlags.WidthStretch, 46);

                ImGui.TableHeadersRow();

                foreach (var p in Service.PartyList)
                {
                    ImGui.TableNextColumn();
                    ImGui.Text(p.Name.TextValue);

                    ImGui.TableNextColumn();
                    ImGui.Text(p.ObjectId.ToString());
                }

                ImGui.EndTable();
            }

            ImGui.Unindent(25 * ImGuiHelpers.GlobalScale);
        }
        else
        {
            headerOpened = false;
        }

        ImGui.Spacing();
    }

    /// <summary>
    /// Sets a hover tooltip for the last item
    /// </summary>
    /// <param name="hovertext">Hovertext</param>
    private static void SetHoverTooltip(string hovertext)
    {
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(hovertext);
    }
}