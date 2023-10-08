using AutoJailMarker.Classes;
using AutoJailMarker.Data;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoJailMarker.Windows;

internal class PriorityListWindow : IDisposable
{
    private readonly AutoJailMarkerConfig config;
    private readonly AutoJailMarkerPlugin autoJailMarkerPlugin;

    public bool Visible;
    private string inlineError = string.Empty;

    public PriorityListWindow(AutoJailMarkerConfig config, AutoJailMarkerPlugin autoJailMarkerPlugin)
    {
        this.config = config;
        this.autoJailMarkerPlugin = autoJailMarkerPlugin;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Main draw handler
    /// </summary>
    public void Draw()
    {
        DrawPriorityWindow();
    }

    /// <summary>
    /// Priority list draw handler
    /// </summary>
    private void DrawPriorityWindow()
    {
        if (!Visible) return;

        var minSize = new Vector2(220, 200);
        var partySize = Service.PartyList.Length;

        ImGui.SetNextWindowSizeConstraints(minSize, new Vector2(float.MaxValue, float.MaxValue));
        if (ImGui.Begin("Auto Jail Marker - Priority List", ref Visible,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            DrawPriorityTable(config.UseJobPrio);
            DrawImportButton(config.UseJobPrio, partySize);
        }

        ImGui.End();
    }

    /// <summary>
    /// Draws the prio list table
    /// </summary>
    /// <param name="useJobPrio">True = PrioJobs; False = Prio</param>
    private void DrawPriorityTable(bool useJobPrio)
    {
        ImGui.Text($"Priority {(useJobPrio ? "Job" : "Character")} List");
        if (!string.IsNullOrEmpty(inlineError))
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DPSRed, $"({inlineError})");
        }

        ImGui.Indent(25 * ImGuiHelpers.GlobalScale);

        if (ImGui.BeginTable("PrioTable", 3, ImGuiTableFlags.PreciseWidths))
        {
            var indexColumnSize = useJobPrio ? 23 : 16;
            var inputColumnSize = useJobPrio ? 91 : 185;
            var buttonColumnSize = useJobPrio ? 46 : 72;

            ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, indexColumnSize);
            ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, inputColumnSize);
            ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, buttonColumnSize);

            var fieldCount = useJobPrio ? 19 : 8;

            var duplicates = config.Prio.GroupBy(n => n.ToLower()).Where(g => g.Key != "" && g.Count() > 1)
                .Select(g => g.Key).ToList();

            for (var i = 0; i < fieldCount; i++)
            {
                ImGui.PushID($"prio_{i}");

                ImGui.TableNextColumn();

                var index = i + 1;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputInt("##indexInput", ref index, 0, 0, ImGuiInputTextFlags.EnterReturnsTrue))
                    if (index > 0 && index <= fieldCount)
                        MovePrio(useJobPrio, i, index - 1);

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);

                if (useJobPrio)
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.75f);
                    var abbreviation = Helper.Classes.Count == 19
                        ? Helper.Classes[(int)config.PrioJobs[i]]
                        : config.PrioJobs[i].ToString();
                    ImGui.InputText("##prioInput", ref abbreviation, 3, ImGuiInputTextFlags.ReadOnly);
                    ImGui.PopStyleVar();
                    SetHoverTooltip("Read-only");
                }
                else
                {
                    var duplicate = duplicates.Contains(config.Prio[i].ToLower());

                    if (duplicate) ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DPSRed);

                    if (ImGui.InputTextWithHint("##prioInput", "Firstname Lastname[@Server]", ref config.Prio[i], 30))
                        config.Save();

                    if (duplicate)
                    {
                        ImGui.PopStyleColor();
                        SetHoverTooltip(
                            "Duplicate character.\nIf you have two characters with the same name in your group,\nplease add the server for both so that the plugin works properly.\ne.g. Firstname Lastname@Server");
                    }
                }

                ImGui.TableNextColumn();
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,
                    new Vector2(2 * ImGuiHelpers.GlobalScale, ImGui.GetStyle().ItemSpacing.Y));

                if (i != fieldCount - 1 && ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown))
                    MovePrio(useJobPrio, i, i + 1);
                else if (i == fieldCount - 1) ImGuiHelpers.ScaledDummy(22 * ImGuiHelpers.GlobalScale, 0);

                if ((useJobPrio && i != 0) || !useJobPrio) ImGui.SameLine();

                if (i != 0 && ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp)) MovePrio(useJobPrio, i, i - 1);

                if (!useJobPrio)
                {
                    if (i == 0) ImGuiHelpers.ScaledDummy(22 * ImGuiHelpers.GlobalScale, 0);

                    ImGui.SameLine();
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Crosshairs)) SetCharacterFromTarget(i);
                    SetHoverTooltip("Set target");
                }

                ImGui.PopStyleVar();

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        ImGui.Unindent(25 * ImGuiHelpers.GlobalScale);

        ImGui.Spacing();
    }

    /// <summary>
    /// MovePrio helper function
    /// </summary>
    /// <param name="useJobPrio">True = PrioJobs; False = Prio</param>
    /// <param name="i">Current index in Prio</param>
    /// <param name="iNew">New index in Prio</param>
    private void MovePrio(bool useJobPrio, int i, int iNew)
    {
        if (useJobPrio)
            config.MovePrio(config.PrioJobs, i, iNew);
        else
            config.MovePrio(config.Prio, i, iNew);
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
                config.MovePrio(config.Prio, oldIndex, currentIndex);
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
    /// Draws the import party list button
    /// </summary>
    /// <param name="useJobPrio">True = PrioJobs; False = Prio</param>
    /// <param name="partySize">Size of the current party</param>
    private void DrawImportButton(bool useJobPrio, int partySize)
    {
        if (useJobPrio) return;

        const string buttonName = "Import party list";
        if (partySize > 0)
        {
            if (ImGui.Button(buttonName))
            {
                autoJailMarkerPlugin.UpdateOrderedParty();

                var newPrio = autoJailMarkerPlugin.OrderedPartyList.Select(p => p.Name.TextValue).ToList();

                if (newPrio.Count != 8)
                {
                    var missing = 8 - newPrio.Count;
                    for (var i = 0; i < missing; i++) newPrio.Add("");
                }

                config.Prio = newPrio.ToArray();
                config.Save();
            }

            SetHoverTooltip("Check if the party prio is working. 3 players are selected randomly.");
        }
        else
        {
            ImGuiComponents.DisabledButton(buttonName);
            SetHoverTooltip("Not in a party. Cross-World party are not supported");
        }
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