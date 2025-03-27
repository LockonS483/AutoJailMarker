using static FFXIVClientStructs.FFXIV.Client.UI.UIModule;
using System;
using FFXIVClientStructs.FFXIV.Client.System.String;

namespace AutoJailMarker.Managers;

public abstract class ChatManager : IDisposable
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public static unsafe void ExecuteCommand(string command)
    {
        var utfMessage = Utf8String.FromString(command);
        utfMessage->SanitizeString((AllowedEntities)0x27F);
        
        Instance()->ProcessChatBoxEntry(utfMessage, nint.Zero);
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