using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace MultiBoxConflict;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool Wintrade = true;

    public bool DisplayEntityMarkers = false;
    public bool DisplayPositionMarkers = false;
    public bool DisplayDotMap = false;
    public bool AddQueueDelay = true;
    public ushort? DesiredTeam = null;

    public List<String> RegisteredCharacters = [];
    public List<String> RegisteredLosers = [];

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
