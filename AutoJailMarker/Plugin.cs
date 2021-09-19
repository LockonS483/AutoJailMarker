using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Utility;

using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;
using System.IO;

using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.ClientState.Objects.Types;

using Dalamud.Hooking;
using Dalamud.Logging;

using AutoJailMarker.Helper;
using FFXIVClientStructs.FFXIV.Client.System.String;


namespace AutoJailMarker
{
    public sealed partial class AutoJailMarker : IDalamudPlugin
    {
        public string Name => "Auto Jail Marker";

        private const string commandName = "/jailmarker";
        public static uint collectionTimeout = 11000;
        public static uint jailCount = 3;
        public static bool printSkillID = false;

        public static uint[] skillIds = new uint[] { 645, 1652, 11115, 11116 };
        public List<String> collectionTargets;
        public List<int> markedInds;
        public List<string> orderedPartyList;

        public DateTime collectionExpireTime;
        public bool isCollecting = false;
        public bool marked = false;
        

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        public static SigScanner SigScanner { get; private set; }
        private Configuration Configuration { get; init; }
        private PartyList PList { get; init; }
        public Character myTitan { get; set; }
        public bool titanLocked = false;
        private GameObject titanLastTarget { get; set; }
        public string titanName { get => myTitan == null ? myTitan.Name.ToString() : ""; }
        private PluginUI PluginUi { get; init; }

        public static bool PlayerExists => DalamudApi.ClientState?.LocalPlayer != null;

        //Party stuff
        public int currentPartySize = 1;

        private bool pluginReady = false;

        //actioneffect
        private delegate void ReceiveActionEffectDelegate(int sourceId, IntPtr sourceCharacter, IntPtr pos, IntPtr effectHeader, IntPtr effectArray, IntPtr effectTrail);
        private Hook<ReceiveActionEffectDelegate> ReceiveActionEffectHook;

        public AutoJailMarker(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            SigScanner sigscanner)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            DalamudApi.Initialize(this, pluginInterface);
            PrintEcho("-Initializing Plugin-");
            SigScanner = sigscanner;


            if (!FFXIVClientStructs.Resolver.Initialized) FFXIVClientStructs.Resolver.Initialize(); //ocealot

            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);

            IntPtr receiveActionEffectFuncPtr = SigScanner.ScanText("4C 89 44 24 18 53 56 57 41 54 41 57 48 81 EC ?? 00 00 00 8B F9"); //ocealot
            ReceiveActionEffectHook = new Hook<ReceiveActionEffectDelegate>(receiveActionEffectFuncPtr, ReceiveActionEffect);
            ReceiveActionEffectHook.Enable();

            // you might normally want to embed resources and load them from the manifest stream
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var imagePath = Path.Combine(Path.GetDirectoryName(assemblyLocation)!, "Titanos_45.png");
            var goatImage = this.PluginInterface.UiBuilder.LoadImage(imagePath);
            this.PluginUi = new PluginUI(this.Configuration, goatImage, this);

            DalamudApi.Framework.Update += Update;
            Game.Initialize();
            this.pluginReady = true;

            this.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "A scuffed jail automarker"
            });

            

            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        }

        public static float RunTime => (float)DalamudApi.PluginInterface.LoadTimeDelta.TotalSeconds;
        public static long FrameCount => (long)DalamudApi.PluginInterface.UiBuilder.FrameCount;
        private void Update(Framework framework)
        {
            if (!pluginReady) return;

            Game.ReadyCommand();
            this.currentPartySize = Game.GetPartySize();

            //ocealot
            if (isCollecting || marked)
            {
                UpdateCollectionTime();
            }
        }
        private void InitializeJailCollection()
        {
            collectionTargets = new List<String>();
        }
        private void UpdateCollectionTime()
        {
            isCollecting = DateTime.Now <= collectionExpireTime ? true : false;
            if (marked && !isCollecting)
            {
                ClearMarkers();
            }
        }

        private void ExecuteMarkers()
        {
            PrintEcho("---Marking Players---");
            int playersMarked = 0;
            List<NameInd> PartyPrioList = new List<NameInd>();
            UpdateOrderedParty();
            for(int i=0; i<8; i++)
            {
                for(int j=0; j<orderedPartyList.Count; j++)
                {
                    if(orderedPartyList[j] == Configuration.prio[i])
                    {
                        NameInd tpair = new NameInd(Configuration.prio[i], (j+1));
                        PartyPrioList.Add(tpair);
                        //PrintEcho($"Added {tpair.name} to NameInd list as {tpair.partynum.ToString()}. ");
                        break;
                    }
                }
            }

            PrintEcho("---Begin Matching Targets---");
            markedInds = new List<int>();
            for(int i=0; i<PartyPrioList.Count; i++)
            {
                PrintEcho($">start match for {PartyPrioList[i].name}");
                if (collectionTargets.Contains(PartyPrioList[i].name))
                {
                    PrintEcho($"--> FOUND");
                    string commandbuilder = $"/mk attack{(playersMarked+1)} <{PartyPrioList[i].partynum}>";
                    markedInds.Add(PartyPrioList[i].partynum);
                    Game.ExecuteCommand(commandbuilder);
                    playersMarked++;
                    if(playersMarked >= jailCount)
                    {
                        break;
                    }
                }
                PrintEcho($"--> NOT FOUND");
            }


            //PrintEcho("Finished Marking");
            marked = true;
        }
        public unsafe void UpdateOrderedParty()
        {
            var partyMembers = UIHelper.PartyListAddon->PartyMember;
            int psize = DalamudApi.PartyList.Length;
            orderedPartyList = new List<string>();

            if (psize < 1) return;
            Utf8String uStr = partyMembers.PartyMember0.Name->NodeText;
            string str = UIHelper.utf8tostring(uStr);
            str = str.Substring(4);
            orderedPartyList.Add(str);
            //PrintEcho(str);

            if (psize < 2) return;
            uStr = partyMembers.PartyMember1.Name->NodeText;
            str = UIHelper.utf8tostring(uStr);
            str = str.Substring(4);
            orderedPartyList.Add(str);
            //PrintEcho(str);

            if (psize < 3) return;
            uStr = partyMembers.PartyMember2.Name->NodeText;
            str = UIHelper.utf8tostring(uStr);
            str = str.Substring(4);
            orderedPartyList.Add(str);
            //PrintEcho(str);

            if (psize < 4) return;
            uStr = partyMembers.PartyMember3.Name->NodeText;
            str = UIHelper.utf8tostring(uStr);
            str = str.Substring(4);
            orderedPartyList.Add(str);
            //PrintEcho(str);

            if (psize < 5) return;
            uStr = partyMembers.PartyMember4.Name->NodeText;
            str = UIHelper.utf8tostring(uStr);
            str = str.Substring(4);
            orderedPartyList.Add(str);
            //PrintEcho(str);

            if (psize < 6) return;
            uStr = partyMembers.PartyMember5.Name->NodeText;
            str = UIHelper.utf8tostring(uStr);
            str = str.Substring(4);
            orderedPartyList.Add(str);
            //PrintEcho(str);

            if (psize < 7) return;
            uStr = partyMembers.PartyMember6.Name->NodeText;
            str = UIHelper.utf8tostring(uStr);
            str = str.Substring(4);
            orderedPartyList.Add(str);
            //PrintEcho(str);

            if (psize < 8) return;
            uStr = partyMembers.PartyMember7.Name->NodeText;
            str = UIHelper.utf8tostring(uStr);
            str = str.Substring(4);
            orderedPartyList.Add(str);
            //PrintEcho(str);
        }

        public void ClearMarkers()
        {
            PrintEcho("---Clearing Marks---");
            foreach(int ind in markedInds)
            {
                Game.ExecuteCommand($"/mk clear <{ind}>");
            }
            marked = false;
            UpdateCollectionTime();
        }

        public void Dispose()
        {
            this.PluginUi.Dispose();
            this.CommandManager.RemoveHandler(commandName);
            DalamudApi.Framework.Update -= Update;

        }

        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
            this.PluginUi.Visible = true;
        }

        private void DrawUI()
        {
            this.PluginUi.Draw();
        }

        private void DrawConfigUI()
        {
            this.PluginUi.SettingsVisible = true;
        }

        public static void PrintEcho(string message) => DalamudApi.ChatGui.Print($"[JailMarker] {message}");
        public static void PrintError(string message) => DalamudApi.ChatGui.PrintError($"[JailMarker] {message}");
    }
}

public static class Extensions
{
    public static object Cast(this Type Type, object data)
    {
        var DataParam = Expression.Parameter(typeof(object), "data");
        var Body = Expression.Block(Expression.Convert(Expression.Convert(DataParam, data.GetType()), Type));

        var Run = Expression.Lambda(Body, DataParam).Compile();
        var ret = Run.DynamicInvoke(data);
        return ret;
    }

    public static bool In<T>(this T item, params T[] list)
    {
        return list.Contains(item);
    }
}

public struct NameInd
{
    public string name;
    public int partynum;

    public NameInd(string n, int i)
    {
        name = n;
        partynum = i;
    }
}