using AutoJailMarker.Managers;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace AutoJailMarker;

internal class Service
{
    [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static ClientState ClientState { get; private set; } = null!;
    [PluginService] public static CommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static DataManager DataManager { get; private set; } = null!;
    [PluginService] public static Framework Framework { get; private set; } = null!;
    [PluginService] public static PartyList PartyList { get; private set; } = null!;
    [PluginService] public static SigScanner SigScanner { get; private set; } = null!;
    [PluginService] public static TargetManager TargetManager { get; private set; } = null!;
    [PluginService] public static ObjectTable ObjectTable { get; private set; } = null!;

    public static ChatManager ChatManager { get; set; } = null!;
}