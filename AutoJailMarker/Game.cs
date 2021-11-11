using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AutoJailMarker.Structures;
using Dalamud;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

using Dalamud.Hooking;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoJailMarker
{
	public static class Game
	{
		private const int maxCommandLength = 180;

		private static bool commandReady = true;
		private static float chatQueueTimer = 0;
		private static readonly Queue<string> commandQueue = new();
		private static readonly Queue<string> macroQueue = new();
		private static readonly Queue<string> chatQueue = new();

		[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)] private static extern IntPtr GetForegroundWindow();
		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

		public static bool IsGameFocused
        {
            get
            {
				var activatedHandle = GetForegroundWindow();
				if (activatedHandle == IntPtr.Zero)
					return false;

				var procId = Environment.ProcessId;
				_ = GetWindowThreadProcessId(activatedHandle, out var activeProcId);

				return activeProcId == procId;
            }
        }

		public static Wrappers.UIModule uiModule;

		public static IntPtr textActiveBoolPtr = IntPtr.Zero;
		public static unsafe bool IsGameTextInputActive => textActiveBoolPtr != IntPtr.Zero && *(bool*)textActiveBoolPtr;
		public static unsafe bool IsMacroRunning => *(int*)(raptureShellModule + 0x2C0) >= 0;
		public static IntPtr agentModule = IntPtr.Zero;
		public static IntPtr addonConfig = IntPtr.Zero;
		public static unsafe byte CurrentHUDLayout => *(byte*)(*(IntPtr*)(addonConfig + 0x50) + 0x59E8);

		//command execution
		public delegate void ProcessChatBoxDelegate(IntPtr uiModule, IntPtr message, IntPtr unused, byte a4);
		public static ProcessChatBoxDelegate ProcessChatBox;

		public delegate int GetCommandHandlerDelegate(IntPtr raptureShellModule, IntPtr message, IntPtr unused);
		public static GetCommandHandlerDelegate GetCommandHandler;

		//macro stuff
		public static IntPtr raptureShellModule = IntPtr.Zero;

		public static unsafe void Initialize()
        {
			uiModule = new Wrappers.UIModule(DalamudApi.GameGui.GetUIModule());
			raptureShellModule = uiModule.GetRaptureShellModule();
			addonConfig = uiModule.GetAddonConfig();
			agentModule = uiModule.GetAgentModule();

			try { textActiveBoolPtr = *(IntPtr*)((IntPtr)AtkStage.GetSingleton() + 0x28) + 0x188E; }
			catch { PluginLog.Error("Failed to load textActiveBoolPtre"); }

            try
            {
				GetCommandHandler = Marshal.GetDelegateForFunctionPointer<GetCommandHandlerDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 83 F8 FE 74 1E"));
                try
                {
					ProcessChatBox = Marshal.GetDelegateForFunctionPointer<ProcessChatBoxDelegate>(DalamudApi.SigScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9"));
                }
                catch { PluginLog.Error("Failed to load ExecuteCommand"); }
            }
			catch { PluginLog.Error("Failed to load plugin"); }
        }

		public static unsafe IntPtr GetAgentByInternalID(int id) => *(IntPtr*)(agentModule + 0x20 + id * 0x8);

		public static void DEBUG_FindAgent(long agent)
        {
			var found = false;
			for (int i=0; i<800; i++)
            {
				if (GetAgentByInternalID(i).ToInt64() != agent) continue;
				AutoJailMarker.PrintEcho(i.ToString());
				found = true;
				break;
            }

			if (!found)
				AutoJailMarker.PrintEcho($"Failed to find agent {agent:X}");
        }

		public static void ReadyCommand()
        {
			if (chatQueueTimer > 0 && (chatQueueTimer -= ImGuiNET.ImGui.GetIO().DeltaTime) <= 0 && chatQueue.Count > 0)
				ExecuteCommand(chatQueue.Dequeue(), true);

			commandReady = true;
        }

		public static void QueueCommand(string command)
        {
			foreach (var c in command.Split('\n'))
            {
				if (!string.IsNullOrEmpty(c))
					commandQueue.Enqueue(c.Substring(0, Math.Min(c.Length, maxCommandLength)));
            }
        }

		private static void RunCommandQueue()
        {
			while (commandQueue.Count > 0 && commandReady)
            {
				commandReady = false;
				var command = commandQueue.Dequeue();

				ExecuteCommand(command, IsChatSendCommand(command));
            }
        }

		public static void ExecuteCommand(string command, bool chat = false)
        {
			var stringPtr = IntPtr.Zero;

            try
            {
				stringPtr = Marshal.AllocHGlobal(UTF8String.size);
				using var str = new UTF8String(stringPtr, command);
				Marshal.StructureToPtr(str, stringPtr, false);

				if (!chat || chatQueueTimer <= 0)
				{
					if (chat)
						chatQueueTimer = 1f / 6f;

					ProcessChatBox(uiModule.Address, stringPtr, IntPtr.Zero, 0);
				}
				else
					chatQueue.Enqueue(command);

            }
            catch { AutoJailMarker.PrintError("Failed to inject command"); }
        }

		public static bool IsChatSendCommand(string command)
        {
			var split = command.IndexOf(' ');
			if (split < 1) return split == 0 || !command.StartsWith('/');

			var handler = 0;
			var stringPtr = IntPtr.Zero;

            try
            {
				stringPtr = Marshal.AllocHGlobal(UTF8String.size);
				using var str = new UTF8String(stringPtr, command.Substring(0, split));
				Marshal.StructureToPtr(str, stringPtr, false);
				handler = GetCommandHandler(raptureShellModule, stringPtr, IntPtr.Zero);
            }
            catch { }

			Marshal.FreeHGlobal(stringPtr);

			return handler switch
			{
				8 or (>= 13 and <= 20) or (>= 91 and <= 119 and not 116) => true,
				_ => false,
			};
        }

		public static PartyMember GetPCharFromId(uint id)
        {
			foreach(PartyMember p in DalamudApi.PartyList)
            {
				if (p.ObjectId == id) return p;
            }
			return null;
        }

		public static bool IsIdInParty(ulong id)
        {
            if (AutoJailMarker.PlayerExists)
            {
				foreach(PartyMember p in DalamudApi.PartyList)
                {
					if(p?.GameObject.ObjectId == (uint)id)
                    {
						return true;
                    }
                }
            }
			return false;
        }

		public static unsafe AtkUnitBase* GetAddonStructByName(string name, int index)
		{
			var atkStage = AtkStage.GetSingleton();
			if (atkStage == null)
				return null;

			var unitMgr = atkStage->RaptureAtkUnitManager;
			if (unitMgr == null)
				return null;

			var addon = unitMgr->GetAddonByName(name, index);
			return addon;
		}

		public static void Dispose()
        {
			
        }
	}
}