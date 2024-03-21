using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Collections.Generic;
using System.Linq;

namespace AutoJailMarker.Classes;

public static unsafe class Helper
{
    public const string SettingsCommand = "/jmsettings";
    public const string PriorityCommand = "/jmpriority";
    public const uint CollectionTimeout = 15000;
    public const int JailCount = 3;
    public static Dictionary<int, string> Classes { get; set; } = new();
    public static bool IsMarking { get; set; }

    public static readonly string[] MarkPrefix = ["First", "Second", "Third"];
    public const string MarkMessage = " mark: {0} - Party list position: {1}";
    public const string NotInPrioMessage = "Not in priority list - using party list as priority";

    public static bool PlayerExists => Service.ClientState?.LocalPlayer != null;

    public static bool IsIdInParty(ulong id)
    {
        if (!PlayerExists) return false;
        // 777 = UwU, 296 = Titan
        return Service.ClientState.TerritoryType is 777 /*or 296*/ &&
               Service.PartyList.Any(p => p.GameObject?.ObjectId == (uint)id);
    }

    public static int GetHudGroupMember(int index)
    {
        var frameworkInstance = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        var baseAddress = (byte*)frameworkInstance->GetUiModule()->GetAgentModule()->GetAgentByInternalId(AgentId.Hud);
        const int groupDataOffset = 0xCC8;

        var objectId = *(int*)(baseAddress + groupDataOffset + index * 0x20 + 0x18);

        return objectId;
    }

    public static PlayerCharacter GetPlayerByObjectId(uint objectId)
    {
        var result = Service.ObjectTable.SearchById(objectId);

        if (result?.GetType() == typeof(PlayerCharacter) && result as PlayerCharacter != null)
            return result as PlayerCharacter;

        return null;
    }
}