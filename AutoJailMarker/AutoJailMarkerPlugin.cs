using AutoJailMarker.Classes;
using AutoJailMarker.Hooks;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoJailMarker.Data;
using AutoJailMarker.Managers;
using AutoJailMarker.Windows;

namespace AutoJailMarker;

internal class AutoJailMarkerPlugin : IDalamudPlugin
{
    public string Name => "Auto Jail Marker";
    public List<string> OrderedPartyList;
    private List<int> markedIndexes = new();

    private AutoJailMarkerConfig PluginConfig { get; }
    private readonly AutoJailMarkerConfigWindow autoJailMarkerConfigWindow;
    private readonly ActionEffectHook actionEffectHook;

    public AutoJailMarkerPlugin(DalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();
        PluginConfig = pluginInterface.GetPluginConfig() as AutoJailMarkerConfig ?? new AutoJailMarkerConfig();

        Service.ChatManager = new ChatManager();

        ChatManager.PrintEcho("-Initializing Plugin-");

        if (!FFXIVClientStructs.Resolver.Initialized) FFXIVClientStructs.Resolver.Initialize();

        // load titan image from embedded resources
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "AutoJailMarker.Data.Titan.png";

        var titanData = System.Array.Empty<byte>();
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

        autoJailMarkerConfigWindow = new AutoJailMarkerConfigWindow(PluginConfig, titanImage, this);
        actionEffectHook = new ActionEffectHook();
        Service.Framework.Update += FrameworkUpdate;

        Service.CommandManager.AddHandler(Helper.CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A scuffed jail auto marker"
        });

        Service.PluginInterface.UiBuilder.Draw += DrawUi;
        Service.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUi;
    }

    public void Dispose()
    {
        autoJailMarkerConfigWindow.Dispose();
        actionEffectHook.Dispose();
        Service.Framework.Update -= FrameworkUpdate;
        Service.CommandManager.RemoveHandler(Helper.CommandName);
        Service.PluginInterface.UiBuilder.Draw -= DrawUi;
        Service.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUi;
    }

    private void FrameworkUpdate(Framework framework)
    {
        if (!Helper.IsMarking && actionEffectHook.CollectionTargets.Count >= Helper.JailCount)
        {
            Helper.IsMarking = true;
            ExecuteMarkers(PluginConfig.FullEcho);
        }

        if (actionEffectHook.ClearMarkers.ElapsedMilliseconds < Helper.CollectionTimeout) return;

        actionEffectHook.CollectionTargets = new List<string>();
        if (Helper.IsMarking) ClearMarkers(PluginConfig.FullEcho);
        actionEffectHook.ClearMarkers.Stop();
        actionEffectHook.ClearMarkers.Reset();
    }

    private void OnCommand(string command, string args)
    {
        DrawConfigUi();
    }

    private void DrawUi()
    {
        autoJailMarkerConfigWindow.Draw();
    }

    private void DrawConfigUi()
    {
        autoJailMarkerConfigWindow.SettingsVisible = !autoJailMarkerConfigWindow.SettingsVisible;
    }

    private void ExecuteMarkers(bool echo = true)
    {
        ChatManager.PrintEcho("---Marking Players---", echo);
        var playersMarked = 0;
        var notInPrio = false;

        UpdateOrderedParty(PluginConfig.FullEcho);
        // Service.ChatManager.PrintEcho($"ordered party list size: {OrderedPartyList.Count}");
        var partyPrioList = CreatePartyPrioList(PluginConfig.FullEcho);

        ChatManager.PrintEcho("---Begin Matching Targets---", echo);
        for (var i = 0; i < partyPrioList.Count; i++)
        {
            if (!PluginConfig.Prio.Contains(partyPrioList[i].Name) && !notInPrio)
            {
                ChatManager.PrintError(Helper.NotInPrioMessage);
                notInPrio = true;
            }

            ChatManager.PrintEcho($">start match for {partyPrioList[i].Name}", echo);
            if (actionEffectHook.CollectionTargets.Contains(partyPrioList[i].Name))
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

    public void UpdateOrderedParty(bool echo = true)
    {
        OrderedPartyList = new List<string>();

        for (var i = 0; i < Service.PartyList.Length; i++)
        {
            var objectId = (uint)Helper.GetHudGroupMember(i);

            if (objectId is 0 or 0xE000_0000) continue;

            var playerName = Helper.GetPlayerNameByObjectId(objectId);
            if (playerName == string.Empty) continue;

            OrderedPartyList.Add(playerName);
            ChatManager.PrintEcho(playerName, echo);
        }
    }

    public List<PartyIndex> CreatePartyPrioList(bool echo = true)
    {
        var partyPrioList = new List<PartyIndex>();
        const string message = "Added {0} to PartyIndex list as {1}.";

        for (var prI = 0; prI < 8; prI++)
        for (var paI = 0; paI < OrderedPartyList.Count; paI++)
        {
            // Service.ChatManager.PrintEcho($"{PluginConfig.Prio[prI]} <> {OrderedPartyList[paI]}");
            if (!OrderedPartyList[paI].Contains(PluginConfig.Prio[prI])) continue;

            var partyIndex = new PartyIndex(PluginConfig.Prio[prI], paI + 1);
            partyPrioList.Add(partyIndex);
            ChatManager.PrintEcho(string.Format(message, partyIndex.Name, partyIndex.Index), echo);
            break;
        }

        if (partyPrioList.Count == 8) return partyPrioList;

        for (var i = 0; i < OrderedPartyList.Count; i++)
        {
            if (partyPrioList.Any(partyIndex => partyIndex.Name == OrderedPartyList[i])) continue;

            var partyIndex = new PartyIndex(OrderedPartyList[i], i + 1);
            partyPrioList.Add(partyIndex);
            ChatManager.PrintEcho(string.Format(message, partyIndex.Name, partyIndex.Index), echo);
        }

        return partyPrioList;
    }

    private void ClearMarkers(bool echo = true)
    {
        ChatManager.PrintEcho("---Clearing Marks---", echo);
        foreach (var i in markedIndexes) Service.ChatManager.SendCommand($"/mk clear <{i}>");

        markedIndexes = new List<int>();
        Helper.IsMarking = false;
    }
}