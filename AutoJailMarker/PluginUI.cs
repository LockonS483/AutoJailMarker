using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Lumina.Excel.GeneratedSheets;

namespace AutoJailMarker
{
    internal class PluginUi : IDisposable
    {
        private readonly Configuration config;
        private readonly ImGuiScene.TextureWrap titanImage;
        private readonly AutoJailMarker autoJailMarker;

        public bool SettingsVisible;

        public PluginUi(Configuration config, ImGuiScene.TextureWrap titanImage, AutoJailMarker autoJailMarker)
        {
            this.config = config;
            this.titanImage = titanImage;
            this.autoJailMarker = autoJailMarker;
        }

        public void Dispose()
        {
            titanImage.Dispose();
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

            var minSize = new Vector2(300, 395);
            var partySize = DalamudApi.PartyList.Length;

            ImGui.SetNextWindowSize(minSize, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(minSize, new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin("Auto Jail Markers", ref SettingsVisible))
            {
                DrawPriorityTable();
                DrawChecks(partySize);
                DrawInformation();
                DrawPartyTable(partySize);
                
                // Titan image
                ImGui.Indent(10);
                ImGui.Image(this.titanImage.ImGuiHandle, new Vector2(this.titanImage.Width, this.titanImage.Height));
                ImGui.Unindent(10);
            }

            ImGui.End();
        }

        /// <summary>
        /// Sets a hover tooltip for the last item
        /// </summary>
        /// <param name="tooltip">Hovertext</param>
        private static void SetHoverTooltip(string tooltip)
        {
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
        }

        /// <summary>
        /// Draws the prio list table
        /// </summary>
        private void DrawPriorityTable()
        {
            ImGui.Text("Priority Characters List");
                ImGui.Indent(25 * ImGuiHelpers.GlobalScale);

                if (ImGui.BeginTable("PrioTable", 3, ImGuiTableFlags.PreciseWidths))
                {
                    var charSize = ImGui.CalcTextSize("1");
                    var cursorPosAppendY = (ImGuiHelpers.GetButtonSize("1").Y / 2) - (charSize.Y / 2);

                    ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, charSize.X);
                    ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthStretch, 180);
                    ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 46);

                    for (var i = 0; i < 8; i++)
                    {
                        ImGui.PushID($"prioCharacter_{i}");

                        ImGui.TableNextColumn();

                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + cursorPosAppendY);
                        ImGui.Text($"{(i + 1)}");

                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);

                        if (ImGui.InputTextWithHint("##prioInput", "Firstname Lastname", ref config.Prio[i], 30))
                        {
                            config.Save();
                        }

                        ImGui.TableNextColumn();
                        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,
                            new Vector2(2 * ImGuiHelpers.GlobalScale, ImGui.GetStyle().ItemSpacing.Y));

                        if (i != 7 && ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown))
                        {
                            config.MovePrio(i, i + 1);
                        }
                        else if (i == 7)
                        {
                            ImGuiHelpers.ScaledDummy(22 * ImGuiHelpers.GlobalScale, 0);
                        }

                        if (i != 0)
                        {
                            ImGui.SameLine();
                        }

                        if (i != 0 && ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp))
                        {
                            config.MovePrio(i, i - 1);
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
        private void DrawTryMarksButton(int partySize)
        {
            if (ImGui.Button("Try Marks"))
            {
                partySize = partySize > 3 ? 3 : partySize == 0 ? 1 : partySize;

                for (var i = 1; i <= partySize; i++)
                {
                    if (i == 1 || (partySize > 1 && DalamudApi.PartyList[i - 1]?.ObjectId != 0))
                    {
                        Game.ExecuteCommand($"/mk attack{i} <{i}>");
                    }
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
            if (partySize > 2)
            {
                if (ImGui.Button("Check Partyprio"))
                {
                    autoJailMarker.UpdateOrderedParty(false);

                    var rnd = new Random();

                    var randomized = Enumerable.Range(0, partySize).ToList();
                    randomized = randomized.OrderBy(_ => rnd.Next()).ToList().GetRange(0, 3);

                    var prio = new Dictionary<int, string>();

                    for (var i = 0; i < autoJailMarker.orderedPartyList.Count; i++)
                    {
                        if (randomized.Contains(i)) prio.Add(i, autoJailMarker.orderedPartyList[i]);
                    }

                    var orderedPrio = new Dictionary<int, string>();

                    foreach (var name in config.Prio)
                    {
                        foreach (var (i, n) in prio)
                        {
                            if (!n.Contains(name)) continue;

                            orderedPrio.Add(i, n);
                            prio.Remove(i);
                            break;
                        }

                        if (prio.Count == 0) break;
                    }

                    var markCount = 0;
                    var markText = new[] { "First", "Second", "Third" };

                    foreach (var (i, n) in orderedPrio)
                    {
                        AutoJailMarker.PrintEcho(markText[markCount] + $" mark: {n} Partylist position: {i + 1}");
                        markCount++;
                    }

                    if (prio.Count > 0)
                    {
                        AutoJailMarker.PrintEcho("Not found in priolist - using partylist as prio");
                        foreach (var (i, n) in prio)
                        {
                            AutoJailMarker.PrintEcho(markText[markCount] + $" mark: {n} Partylist position: {i + 1}");
                            markCount++;
                        }
                    }
                }

                SetHoverTooltip("Check if the party prio is working. 3 players are selected randomly.");
            }
            else
            {
                ImGuiComponents.DisabledButton("Check Partyprio");
                var hoverText = partySize == 0
                    ? "Not in a party. Crossworld party are not supported"
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
                
            var territoryType = DalamudApi.ClientState.TerritoryType;
            var placeName = DalamudApi.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryType)?
                .PlaceName.Value?.Name;
            var cPlace = placeName == null ? $"{territoryType}" : $"{placeName} ({territoryType})";

            ImGui.Text($"Current place: {cPlace}");

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(0, ImGui.GetStyle().ItemSpacing.Y));
                
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
            if (!AutoJailMarker.PlayerExists || partySize <= 1) return;
            
            ImGui.SetNextWindowCollapsed(true);
            if (!ImGui.CollapsingHeader("Current Partylist from Gamedata")) return;
            
            ImGui.Indent(25 * ImGuiHelpers.GlobalScale);
                        
            if (ImGui.BeginTable("PartyTable", 2, ImGuiTableFlags.PreciseWidths))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 120);
                ImGui.TableSetupColumn("ObjectID", ImGuiTableColumnFlags.WidthStretch, 46);
                            
                ImGui.TableHeadersRow();

                foreach (var p in DalamudApi.PartyList)
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
}