using AutoJailMarker.Classes;
using Dalamud.Hooking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using static FFXIVClientStructs.FFXIV.Client.Game.Character.ActionEffectHandler;

namespace AutoJailMarker.Hooks;

internal unsafe class ActionEffectHook : IDisposable
{
    private readonly AutoJailMarkerPlugin _autoJailMarkerPlugin;
    private readonly Hook<Delegates.Receive>? _receiveActionEffectHook;

    private static readonly uint[] SkillIds = [645, 1652, 11115, 11116];

    public List<ulong> CollectionTargets = [];
    public readonly Stopwatch ClearMarkers = new();

    public ActionEffectHook(AutoJailMarkerPlugin autoJailMarkerPlugin)
    {
        _autoJailMarkerPlugin = autoJailMarkerPlugin;

        Service.Hooks.InitializeFromAttributes(this);

        _receiveActionEffectHook = Service.Hooks.HookFromAddress<Delegates.Receive>(MemberFunctionPointers.Receive, ReceiveActionEffect);
        _receiveActionEffectHook.Enable();
    }

    public void Dispose()
    {
        _receiveActionEffectHook?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ReceiveActionEffect(uint actorId, Character* casterPtr, Vector3* targetPos, Header* header,
        TargetEffects* effects, GameObjectId* targetEntityIds)
    {
        if (!Helper.PlayerExists || !_autoJailMarkerPlugin.PluginConfig.Enabled)
        {
            _receiveActionEffectHook?.Original(actorId, casterPtr, targetPos, header, effects, targetEntityIds);
            return;
        }

        var id = header->ActionId;
        var targetCount = header->NumTargets;
        var targetsParty = false;

        var targetEntries = 1;
        switch (targetCount)
        {
            case 0:
            case 1:
                targetEntries = 1;
                break;
            case <= 8:
                targetEntries = 8;
                break;
            case <= 16:
                targetEntries = 16;
                break;
            case <= 24:
                targetEntries = 24;
                break;
            case <= 32:
                targetEntries = 32;
                break;
        }

        var targets = new ulong[targetEntries];
        for (var i = 0; i < targetCount; i++)
        {
            targets[i] = *(ulong*)(targetEntityIds + i * 8);
            if (Helper.IsIdInParty(targets[i])) targetsParty = true;
        }

        if (targetsParty)
            if (SkillIds.Contains(id))
                foreach (var l in targets)
                {
                    if (Helper.IsMarking) continue;

                    if (!ClearMarkers.IsRunning) ClearMarkers.Start();

                    CollectionTargets.Add((uint)l);

                    if (CollectionTargets.Count == Helper.JailCount) break;
                }

        _receiveActionEffectHook?.Original(actorId, casterPtr, targetPos, header, effects, targetEntityIds);
    }
}