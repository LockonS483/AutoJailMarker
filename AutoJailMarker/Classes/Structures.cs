using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoJailMarker.Classes;

public struct PartyIndex
{
    public readonly string Name;
    public readonly int Index;

    public PartyIndex(string n, int i)
    {
        Name = n;
        Index = i;
    }
}

[StructLayout(LayoutKind.Explicit)]
public readonly struct ChatPayload : IDisposable
{
    [FieldOffset(0)] private readonly IntPtr textPtr;

    internal ChatPayload(string text)
    {
        var stringBytes = Encoding.UTF8.GetBytes(text);
        textPtr = Marshal.AllocHGlobal(stringBytes.Length + 30);

        Marshal.Copy(stringBytes, 0, textPtr, stringBytes.Length);
        Marshal.WriteByte(textPtr + stringBytes.Length, 0);
    }

    public void Dispose()
    {
        Marshal.FreeHGlobal(textPtr);
    }
}