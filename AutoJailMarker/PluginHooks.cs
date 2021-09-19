using AutoJailMarker.GameStructs;
using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;
//using AutoJailMarker.Data;
//using AutoJailMarker.Helper;

namespace AutoJailMarker
{
    public unsafe partial class AutoJailMarker
    {
        private void ReceiveActionEffect(int sourceId, IntPtr sourceCharacter, IntPtr pos, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail)
        {
            if (!PlayerExists)
            {
                ReceiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);
                return;
            }

            //Character source = Game.GetCharFromPtr(sourceCharacter);

            uint id = *((uint*)effectHeader.ToPointer() + 0x2);
            byte type = *((byte*)effectHeader.ToPointer() + 0x1F); // 1 = action

            byte targetCount = *(byte*)(effectHeader + 0x21);
            bool targetsParty = false;

            int effectsEntries = 0;
            int targetEntries = 1;
            if (targetCount == 0)
            {
                effectsEntries = 0;
                targetEntries = 1;
            }
            else if (targetCount == 1)
            {
                effectsEntries = 8;
                targetEntries = 1;
            }
            else if (targetCount <= 8)
            {
                effectsEntries = 64;
                targetEntries = 8;
            }
            else if (targetCount <= 16)
            {
                effectsEntries = 128;
                targetEntries = 16;
            }
            else if (targetCount <= 24)
            {
                effectsEntries = 192;
                targetEntries = 24;
            }
            else if (targetCount <= 32)
            {
                effectsEntries = 256;
                targetEntries = 32;
            }

            List<EffectEntry> entries = new(effectsEntries);
            for (int i = 0; i < effectsEntries; i++)
            {
                entries.Add(*(EffectEntry*)(effectArray + i * 8));
            }

            ulong[] targets = new ulong[targetEntries];
            for (int i = 0; i < targetCount; i++)
            {
                targets[i] = *(ulong*)(effectTrail + i * 8);
                if (Game.IsIdInParty(targets[i]))
                {
                    targetsParty = true;
                }
            }


            if (targetsParty)
            {   
                if (id.In(AutoJailMarker.skillIds)){
                    //PrintEcho("--Titan Jailed Target--");
                    foreach (ulong l in targets)
                    {
                        if (!marked)
                        {
                            //PrintEcho(l.ToString());
                            UpdateCollectionTime();
                            if (!this.isCollecting)
                            {
                                InitializeJailCollection();
                                collectionExpireTime = DateTime.Now.AddMilliseconds(collectionTimeout);
                            }
                            PartyMember pchar = Game.GetPCharFromId((uint)l);
                            String nstring = pchar.Name.ToString();
                            collectionTargets.Add(nstring);
                            //PrintEcho($"{nstring}");
                            //PrintEcho($"Current Collection Count: {collectionTargets.Count}");

                            if (collectionTargets.Count >= jailCount)
                            {
                                ExecuteMarkers();
                            }
                        }
                    }
                }
                else
                {
                    if (printSkillID)
                    {
                        PrintEcho("--Party Targeted--");
                        PrintEcho($"Skill ID: {id}  ->  {targets[0].ToString()}");
                    }
                }
            }

            /*
            var selfId = (int)ClientState.LocalPlayer.ObjectId;
            var isSelf = sourceId == selfId;
            var isPet = !isSelf && (GaugeManager?.CurrentJob == JobIds.SMN || GaugeManager?.CurrentJob == JobIds.SCH) && IsPet(sourceId, selfId);
            var isParty = !isSelf && !isPet && IsInParty((uint)sourceId);

            if (type != 1 || !(isSelf || isPet || isParty))
            {
                ReceiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);
                return;
            }

            var actionItem = new Item
            {
                Id = id,
                Type = (UIHelper.IsGCD(id) ? ItemType.GCD : ItemType.OGCD)
            };

            if (!isParty)
            { // don't let party members affect our gauge
                GaugeManager?.PerformAction(actionItem);
            }
            if (!isPet)
            {
                BuffManager?.PerformAction(actionItem, (uint)sourceId);
                CooldownManager?.PerformAction(actionItem, (uint)sourceId);
            }
            */


            ReceiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);
        }

        /*private static bool IsPet(int objectId, int ownerId)
        {
            if (objectId == 0) return false;
            foreach (var actor in Objects)
            {
                if (actor == null) continue;
                if (actor.ObjectId == objectId)
                {
                    if (actor is BattleNpc npc)
                    {
                        if (npc.Address == IntPtr.Zero) return false;
                        return npc.OwnerId == ownerId;
                    }
                    return false;
                }
            }
            return false;
        }*/
    }
}
