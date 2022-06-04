using Dalamud.Configuration;
using System;

namespace AutoJailMarker
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public string[] Prio = { "","","","","","","","" };

        public void Save()
        {
            DalamudApi.PluginInterface.SavePluginConfig(this);
        }
        
        /// <summary>
        /// Swaps two entries in Prio
        /// </summary>
        /// <param name="i">Current index in Prio</param>
        /// <param name="iNew">New index in Prio</param>
        public void MovePrio(int i, int iNew)
        {
            (Prio[i], Prio[iNew]) = (Prio[iNew], Prio[i]);
            Save();
        }
    }
}