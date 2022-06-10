using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using AutoJailMarker.Classes;
using AutoJailMarker.Data;
using AutoJailMarker.Managers;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace AutoJailMarker.Windows;

internal class AutoJailMarkerConfigWindow : IDisposable
{
    private readonly AutoJailMarkerConfig config;
    private readonly ImGuiScene.TextureWrap titanImage;
    private readonly AutoJailMarkerPlugin autoJailMarkerPlugin;

    public bool SettingsVisible;
    private string inlineError = string.Empty;

    public AutoJailMarkerConfigWindow(AutoJailMarkerConfig config, ImGuiScene.TextureWrap titanImage,
        AutoJailMarkerPlugin autoJailMarkerPlugin)
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
        if (!SettingsVisible) return;

        var minSize = new Vector2(330, 445);
        var partySize = Service.PartyList.Length;

        ImGui.SetNextWindowSize(minSize, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(minSize, new Vector2(float.MaxValue, float.MaxValue));
        if (ImGui.Begin("Auto Jail Markers", ref SettingsVisible))
        {
            DrawPriorityTable(partySize);
            DrawChecks(partySize);
            DrawOptionsSection();
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
    /// Sets a hover tooltip for the last item
    /// </summary>
    /// <param name="hovertext">Hovertext</param>
    private static void SetHoverTooltip(string hovertext)
    {
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(hovertext);
    }

    /// <summary>
    /// Draws the prio list table
    /// </summary>
    private void DrawPriorityTable(int partySize)
    {
        ImGui.Text("Priority Characters List");
        if (!string.IsNullOrEmpty(inlineError))
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DPSRed, $"({inlineError})");
        }

        ImGui.Indent(25 * ImGuiHelpers.GlobalScale);

        if (ImGui.BeginTable("PrioTable", 3, ImGuiTableFlags.PreciseWidths))
        {
            var charSize = ImGui.CalcTextSize("1");
            var cursorPosAppendY = ImGuiHelpers.GetButtonSize("1").Y / 2 - charSize.Y / 2;

            ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, charSize.X);
            ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthStretch, 180);
            ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 72);

            for (var i = 0; i < 8; i++)
            {
                ImGui.PushID($"prioCharacter_{i}");

                ImGui.TableNextColumn();

                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + cursorPosAppendY);
                ImGui.Text($"{i + 1}");

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);

                if (ImGui.InputTextWithHint("##prioInput", "Firstname Lastname", ref config.Prio[i], 30)) config.Save();

                ImGui.TableNextColumn();
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,
                    new Vector2(2 * ImGuiHelpers.GlobalScale, ImGui.GetStyle().ItemSpacing.Y));

                if (i != 7 && ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown))
                    config.MovePrio(i, i + 1);
                else if (i == 7) ImGuiHelpers.ScaledDummy(22 * ImGuiHelpers.GlobalScale, 0);

                ImGui.SameLine();

                if (i != 0 && ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp)) config.MovePrio(i, i - 1);

                if (i == 0) ImGuiHelpers.ScaledDummy(22 * ImGuiHelpers.GlobalScale, 0);

                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Crosshairs)) SetCharacterFromTarget(i);
                SetHoverTooltip("Set target");

                ImGui.PopStyleVar();

                ImGui.PopID();
            }

            ImGui.EndTable();
        }
        ImGui.Spacing();

        DrawAutofillButton(partySize);

        ImGui.Unindent(25 * ImGuiHelpers.GlobalScale);

        ImGui.Spacing();
    }

    private void DrawAutofillButton(int partySize)
    {
        if (partySize > 2)
        {
            if (ImGui.Button("Autofill"))
            {
                autoJailMarkerPlugin.UpdateOrderedParty(false);

                ChatManager.PrintEcho("Autofilling from party list.");
                for (var i = 0; i < autoJailMarkerPlugin.OrderedPartyList.Count; i++)
                {
                    ChatManager.PrintEcho((i+1).ToString() + ": " + autoJailMarkerPlugin.OrderedPartyList[i]);
                    config.Prio[i] = autoJailMarkerPlugin.OrderedPartyList[i];
                }
                config.Save();
            }

            SetHoverTooltip("Autofill from party list.");
        }
        else
        {
            ImGuiComponents.DisabledButton("Autofill");
            var hoverText = partySize == 0
                ? "Not in a party. Cross-World party are not supported"
                : "Party size must be greater than 2";

            SetHoverTooltip(hoverText);
        }
    }

    /// <summary>
    /// Sets the current target to the currentIndex in the prio list.
    /// If the name already exists in the prio list, then currentIndex is swapped with the index of the name.
    /// </summary>
    /// <param name="currentIndex">CurrentIndex from the prio list</param>
    private void SetCharacterFromTarget(int currentIndex)
    {
        var target = Service.TargetManager.Target;
        if (target is PlayerCharacter targetCharacter)
        {
            var fullName = targetCharacter.Name.TextValue;

            if (config.Prio.Contains(fullName))
            {
                var oldIndex = config.Prio.ToList().IndexOf(fullName);
                config.MovePrio(oldIndex, currentIndex);
            }
            else
            {
                config.Prio[currentIndex] = fullName;
                config.Save();
            }
        }
        else
        {
            SetInlineError("Target isn't a player");
        }
    }

    /// <summary>
    /// Sets an inline error message for 5000 milliseconds after the "Priority Characters List" text element.
    /// </summary>
    /// <param name="errorMessage">Error message</param>
    private void SetInlineError(string errorMessage)
    {
        inlineError = errorMessage;
        Task.Delay(5000).ContinueWith(_ => inlineError = string.Empty);
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
            partySize = partySize > 3 ? 3 : partySize == 0 ? 1 : partySize;

            for (var i = 1; i <= partySize; i++)
                if (i == 1 || (partySize > 1 && Service.PartyList[i - 1]?.ObjectId != 0))
                    Service.ChatManager.SendCommand($"/mk attack{i} <{i}>");
        }

        SetHoverTooltip("Try to see if the plugin can set marks in general");
    }

    /// <summary>
    /// Draws the check prio button
    /// </summary>
    /// <param name="partySize">Size of the current party</param>
    private void DrawCheckPrioButton(int partySize)
    {
        if (partySize > 2)
        {
            if (ImGui.Button("Check Priority"))
            {
                autoJailMarkerPlugin.UpdateOrderedParty(false);
                var partyPrioList = autoJailMarkerPlugin.CreatePartyPrioList(false);

                var playersMarked = 0;
                var notInPrio = false;
                var rnd = new Random();

                var randomized = Enumerable.Range(0, partySize).ToList();
                randomized = randomized.OrderBy(_ => rnd.Next()).ToList().GetRange(0, 3);

                var marked = autoJailMarkerPlugin.OrderedPartyList.Where((_, i) => randomized.Contains(i)).ToList();

                for (var i = 0; i < partyPrioList.Count; i++)
                {
                    if (!marked.Contains(partyPrioList[i].Name)) continue;

                    if (!config.Prio.Contains(partyPrioList[i].Name) && !notInPrio)
                    {
                        ChatManager.PrintError(Helper.NotInPrioMessage);
                        notInPrio = true;
                    }

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
    /// Draws the options section
    /// </summary>
    private void DrawOptionsSection()
    {
        ImGui.Text("Options");
        ImGui.Indent(25 * ImGuiHelpers.GlobalScale);

        if (ImGui.Checkbox("Complete Echo", ref config.FullEcho))
        {
            config.Save();
        }
        SetHoverTooltip("On: Output the full chat notifications\nOff: Output only the marked players");

        ImGui.Unindent(25 * ImGuiHelpers.GlobalScale);

        ImGui.Spacing();
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
    private static void DrawPartyTable(int partySize)
    {
        if (!Helper.PlayerExists || partySize <= 1) return;

        ImGui.SetNextWindowCollapsed(true);
        if (!ImGui.CollapsingHeader("Current Party list from Game data")) return;

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

        ImGui.Spacing();
    }
}