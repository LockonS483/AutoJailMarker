using AutoJailMarker.Classes;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AutoJailMarker.Hooks;

internal unsafe class ActionEffectHook : IDisposable
{
    [Signature("4C 89 44 24 ?? 55 56 41 54 41 55 41 56")]
    private readonly IntPtr receiveAEtPtr = new();
    
    private readonly AutoJailMarkerPlugin autoJailMarkerPlugin;
    private readonly Hook<ReceiveActionEffectDelegate> receiveActionEffectHook;

    private delegate void ReceiveActionEffectDelegate(int sourceId, IntPtr sourceCharacter, IntPtr pos,
        IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail);

    private static readonly uint[] SkillIds = { 645, 1652, 11115, 11116 };

    public List<uint> CollectionTargets = new();
    public readonly Stopwatch ClearMarkers = new();

    public ActionEffectHook(AutoJailMarkerPlugin autoJailMarkerPlugin)
    {
        this.autoJailMarkerPlugin = autoJailMarkerPlugin;

        SignatureHelper.Initialise(this);

        receiveActionEffectHook = Hook<ReceiveActionEffectDelegate>.FromAddress(receiveAEtPtr, ReceiveActionEffect);
        receiveActionEffectHook.Enable();
    }

    public void Dispose()
    {
        receiveActionEffectHook.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ReceiveActionEffect(int sourceId, IntPtr sourceCharacter, IntPtr pos, IntPtr effectHeader,
        IntPtr effectArray, IntPtr effectTrail)
    {
        if (!Helper.PlayerExists || !autoJailMarkerPlugin.PluginConfig.Enabled)
        {
            receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray,
                effectTrail);
            return;
        }

        var id = *((uint*)effectHeader.ToPointer() + 0x2);
        var targetCount = *(byte*)(effectHeader + 0x21);
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
            targets[i] = *(ulong*)(effectTrail + i * 8);
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

        receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);
    }
}