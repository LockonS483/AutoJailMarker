using AutoJailMarker.Classes;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace AutoJailMarker.Managers;

public class ChatManager : IDisposable
{
    private readonly Channel<string> chatBoxMessages = Channel.CreateUnbounded<string>();

    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9")]
    private readonly ProcessChatBoxDelegate processChatBox = null!;

    private unsafe delegate void ProcessChatBoxDelegate(UIModule* uiModule, IntPtr message, IntPtr unused, byte a4);

    public ChatManager()
    {
        Service.Hooks.InitializeFromAttributes(this);
        Service.Framework.Update += FrameworkUpdate;
    }

    public void Dispose()
    {
        Service.Framework.Update -= FrameworkUpdate;
        chatBoxMessages.Writer.Complete();
        GC.SuppressFinalize(this);
    }

    private void FrameworkUpdate(IFramework framework)
    {
        if (chatBoxMessages.Reader.TryRead(out var message)) ExecuteCommand(message);
    }

    public async void SendCommand(string command)
    {
        await chatBoxMessages.Writer.WriteAsync(command);
    }

    private unsafe void ExecuteCommand(string command)
    {
        var framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        var uiModule = framework->GetUiModule();

        using var payload = new ChatPayload(command);
        var payloadPtr = Marshal.AllocHGlobal(400);
        Marshal.StructureToPtr(payload, payloadPtr, false);

        processChatBox(uiModule, payloadPtr, IntPtr.Zero, 0);
    }

    public static void PrintEcho(string message, bool echo = true)
    {
        if (echo) Service.ChatGui.Print($"[JailMarker] {message}");
    }

    public static void PrintError(string message)
    {
        Service.ChatGui.PrintError($"[JailMarker] {message}");
    }
}