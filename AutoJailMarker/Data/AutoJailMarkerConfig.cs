using AutoJailMarker.Classes;
using Dalamud.Configuration;
using System;

namespace AutoJailMarker.Data;

[Serializable]
public class AutoJailMarkerConfig : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool Enabled = true;
    public bool UseJobPrio = false;
    public bool Debug = false;

    public string[] Prio = ["", "", "", "", "", "", "", ""];

    public ClassEnum[] PrioJobs =
    [
        ClassEnum.MNK, ClassEnum.DRG, ClassEnum.NIN, ClassEnum.SAM, ClassEnum.RPR, ClassEnum.VPR, ClassEnum.PLD,
        ClassEnum.WAR, ClassEnum.DRK, ClassEnum.GNB, ClassEnum.BRD, ClassEnum.MCH, ClassEnum.DNC, ClassEnum.SMN,
        ClassEnum.RDM, ClassEnum.BLM, ClassEnum.PCT, ClassEnum.WHM, ClassEnum.SCH, ClassEnum.AST, ClassEnum.SGE
    ];

    public void Save()
    {
        Service.PluginInterface.SavePluginConfig(this);
    }

    /// <summary>
    /// Swaps two entries in Prio
    /// </summary>
    /// <param name="array">Array that is to be changed</param>
    /// <param name="i">Current index in Prio</param>
    /// <param name="iNew">New index in Prio</param>
    public void MovePrio<T>(T[] array, int i, int iNew)
    {
        (array[i], array[iNew]) = (array[iNew], array[i]);
        Save();
    }
}