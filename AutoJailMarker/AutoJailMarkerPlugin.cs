using AutoJailMarker.Classes;
using AutoJailMarker.Data;
using AutoJailMarker.Hooks;
using AutoJailMarker.Managers;
using AutoJailMarker.Windows;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AutoJailMarker;

internal class AutoJailMarkerPlugin : IDalamudPlugin
{
    public string Name => "Auto Jail Marker";
    public List<PlayerCharacter> OrderedPartyList;
    private List<int> markedIndexes = new();

    public AutoJailMarkerConfig PluginConfig { get; }
    private readonly ConfigWindow configWindow;
    private readonly PriorityListWindow priorityListWindow;
    private readonly ActionEffectHook actionEffectHook;

    public AutoJailMarkerPlugin(DalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();
        PluginConfig = pluginInterface.GetPluginConfig() as AutoJailMarkerConfig ?? new AutoJailMarkerConfig();

        Service.ChatManager = new ChatManager();

        ChatManager.PrintEcho("-Initializing Plugin-", PluginConfig.Debug);

        // load titan image from embedded resources
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "AutoJailMarker.Data.Titan.png";

        var titanData = Array.Empty<byte>();
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream != null)
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                titanData = ms.ToArray();
            }
        }

        var titanImage = titanData.Length != 0 ? Service.PluginInterface.UiBuilder.LoadImage(titanData) : null;

        configWindow = new ConfigWindow(PluginConfig, titanImage, this);
        priorityListWindow = new PriorityListWindow(PluginConfig, this);
        actionEffectHook = new ActionEffectHook(this);
        Service.Framework.Update += FrameworkUpdate;

        Service.CommandManager.AddHandler(Helper.SettingsCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = $"{Name} - Settings"
        });

        Service.CommandManager.AddHandler(Helper.PriorityCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = $"{Name} - Priority list"
        });

        Service.PluginInterface.UiBuilder.Draw += DrawUi;
        Service.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUi;

        var jobClassSheet = Service.DataManager.GameData.GetExcelSheet<ClassJob>();
        if (jobClassSheet != null)
            Helper.Classes = jobClassSheet.ToArray().Where(row => Enum.IsDefined(typeof(ClassEnum), row.JobIndex))
                .ToDictionary<ClassJob, int, string>(row => row.JobIndex, row => row.Abbreviation);
        ChatManager.PrintEcho(Service.PartyList.Length.ToString());
    }

    public void Dispose()
    {
        configWindow.Dispose();
        priorityListWindow.Dispose();
        actionEffectHook.Dispose();
        Service.Framework.Update -= FrameworkUpdate;
        Service.CommandManager.RemoveHandler(Helper.SettingsCommand);
        Service.CommandManager.RemoveHandler(Helper.PriorityCommand);
        Service.PluginInterface.UiBuilder.Draw -= DrawUi;
        Service.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUi;
    }

    private void FrameworkUpdate(Framework framework)
    {
        if (!Helper.IsMarking && actionEffectHook.CollectionTargets.Count >= Helper.JailCount)
        {
            Helper.IsMarking = true;
            ExecuteMarkers(PluginConfig.Debug);
        }

        if (actionEffectHook.ClearMarkers.ElapsedMilliseconds < Helper.CollectionTimeout) return;

        actionEffectHook.CollectionTargets = new List<uint>();
        if (Helper.IsMarking) ClearMarkers(PluginConfig.Debug);
        actionEffectHook.ClearMarkers.Stop();
        actionEffectHook.ClearMarkers.Reset();
    }

    public void OnCommand(string command, string args)
    {
        switch (command)
        {
            case "":
            case Helper.SettingsCommand:
                DrawConfigUi();
                break;
            case Helper.PriorityCommand:
                priorityListWindow.Visible = !priorityListWindow.Visible;
                break;
        }
    }

    private void DrawUi()
    {
        configWindow.Draw();
        priorityListWindow.Draw();
    }

    private void DrawConfigUi()
    {
        configWindow.Visible = !configWindow.Visible;
    }

    private void ExecuteMarkers(bool echo = false)
    {
        if (!PluginConfig.Enabled) return;

        ChatManager.PrintEcho("---Marking Players---", echo);
        var playersMarked = 0;
        var notInPrio = false;

        UpdateOrderedParty(echo);
        // Service.ChatManager.PrintEcho($"ordered party list size: {OrderedPartyList.Count}", PluginConfig.Debug);
        var partyPrioList = CreatePartyPrioList(echo);

        ChatManager.PrintEcho("---Begin Matching Targets---", echo);
        for (var i = 0; i < partyPrioList.Count; i++)
        {
            if (!PluginConfig.UseJobPrio &&
                !PluginConfig.Prio.Any(n => n != "" && partyPrioList[i].Name.ToLower().StartsWith(n.ToLower())) &&
                !notInPrio)
            {
                ChatManager.PrintError(Helper.NotInPrioMessage);
                notInPrio = true;
            }

            ChatManager.PrintEcho($">start match for {partyPrioList[i].Name}", echo);
            if (actionEffectHook.CollectionTargets.Contains(partyPrioList[i].ObjectId))
            {
                ChatManager.PrintEcho(
                    Helper.MarkPrefix[playersMarked] + string.Format(Helper.MarkMessage, partyPrioList[i].Name, i + 1),
                    !echo);

                var commandBuilder = $"/mk attack{playersMarked + 1} <{partyPrioList[i].Index}>";

                markedIndexes.Add(partyPrioList[i].Index);
                Service.ChatManager.SendCommand(commandBuilder);
                playersMarked++;

                ChatManager.PrintEcho($"--> FOUND", echo);

                if (playersMarked == Helper.JailCount) break;
            }
            else
            {
                ChatManager.PrintEcho($"--> NOT FOUND", echo);
            }
        }

        ChatManager.PrintEcho("Finished Marking", echo);
    }

    public void UpdateOrderedParty(bool echo = false)
    {
        OrderedPartyList = new List<PlayerCharacter>();

        for (var i = 0; i < Service.PartyList.Length; i++)
        {
            var objectId = (uint)Helper.GetHudGroupMember(i);

            if (objectId is 0 or 0xE000_0000) continue;

            var player = Helper.GetPlayerByObjectId(objectId);
            if (player == null) continue;

            var world = player.HomeWorld.GameData;
            var worldName = world != null ? "@" + world.Name.RawString : "";

            OrderedPartyList.Add(player);
            ChatManager.PrintEcho(player.Name.TextValue + worldName, echo);
        }
    }

    public List<PartyIndex> CreatePartyPrioList(bool echo = false)
    {
        var partyPrioList = new List<PartyIndex>();
        const string message = "Added {0} to PartyIndex list as {1}.";

        if (PluginConfig.UseJobPrio)
        {
            foreach (var classEnum in PluginConfig.PrioJobs)
            {
                foreach (var partyIndex in OrderedPartyList.Where(p => p.ClassJob.GameData?.JobIndex == (uint)classEnum)
                             .Select(p =>
                                 new PartyIndex(
                                     p.Name.TextValue + (p.HomeWorld.GameData != null
                                         ? "@" + p.HomeWorld.GameData.Name.RawString
                                         : ""),
                                     p.ObjectId,
                                     OrderedPartyList.IndexOf(p) + 1)
                             ))
                {
                    if (partyPrioList.Count == 8) break;

                    partyPrioList.Add(partyIndex);
                    ChatManager.PrintEcho(string.Format(message, partyIndex.Name, partyIndex.Index), echo);
                }

                if (partyPrioList.Count == 8) break;
            }
        }
        else
        {
            foreach (var prio in PluginConfig.Prio)
            {
                var prioName = prio;
                var world = string.Empty;

                if (prioName.Contains('@'))
                {
                    var splitName = prioName.Split('@');
                    prioName = splitName[0];
                    world = splitName[1];
                }

                var partyIndexes = OrderedPartyList.Where(p =>
                    p.Name.TextValue.ToLower().Contains(prioName.ToLower()) &&
                    (world == string.Empty || p.HomeWorld.GameData?.Name.RawString.ToLower() == world.ToLower())
                ).Select(p =>
                    new PartyIndex(
                        p.Name.TextValue + (p.HomeWorld.GameData != null
                            ? "@" + p.HomeWorld.GameData.Name.RawString
                            : ""),
                        p.ObjectId,
                        OrderedPartyList.IndexOf(p) + 1
                    )
                );

                var partyIndex = partyIndexes.Where(partyIndex => !partyPrioList.Contains(partyIndex)).ToList();
                if (partyIndex.Count <= 0) continue;

                partyPrioList.Add(partyIndex[0]);
                ChatManager.PrintEcho(string.Format(message, partyIndex[0].Name, partyIndex[0].Index),
                    PluginConfig.Debug);
            }

            if (partyPrioList.Count == Service.PartyList.Length) return partyPrioList;

            foreach (var partyIndex in from pChar in OrderedPartyList
                     where partyPrioList.All(pIndex => pIndex.Name != pChar.Name.TextValue)
                     select
                         new PartyIndex(
                             pChar.Name.TextValue + (pChar.HomeWorld.GameData != null
                                 ? "@" + pChar.HomeWorld.GameData.Name.RawString
                                 : ""),
                             pChar.ObjectId,
                             OrderedPartyList.IndexOf(pChar) + 1
                         )
                    )
            {
                if (partyPrioList.Contains(partyIndex)) continue;

                partyPrioList.Add(partyIndex);
                ChatManager.PrintEcho(string.Format(message, partyIndex.Name, partyIndex.Index), echo);
            }
        }

        return partyPrioList;
    }

    private void ClearMarkers(bool echo = false)
    {
        ChatManager.PrintEcho("---Clearing Marks---", echo);
        foreach (var i in markedIndexes) Service.ChatManager.SendCommand($"/mk clear <{i}>");

        markedIndexes = new List<int>();
        Helper.IsMarking = false;
    }
}