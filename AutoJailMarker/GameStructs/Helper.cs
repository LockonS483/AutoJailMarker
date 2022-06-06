using System;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using System.Runtime.InteropServices;
using Dalamud.Memory;
using Dalamud.Game.Gui;
using Dalamud.Data;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace AutoJailMarker.Helper
{
    public unsafe class UIHelper
    {
        /*public unsafe static UIModule* GetModule()
        {
            var uimod = Framework.Instance()->GetUiModule();
            return uimod;
        }*/

        public unsafe static string utf8tostring(Utf8String ustr)
        {
            //AutoJailMarker.PrintEcho("Converting string");
            string str = MemoryHelper.ReadString(new IntPtr(ustr.StringPtr), 64);
            //AutoJailMarker.PrintEcho(str);
            return str;
        }

        public unsafe static int FindFirstSpace(string s)
        {
            for(int i=0; i<s.Length; i++)
            {
                char c = s[i];
                if ((s[i]) == ' ') return i;
            }

            return 0;
        }
    }
}