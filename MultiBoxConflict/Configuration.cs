using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace MultiBoxConflict;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool DisplayEntityMarkers = false;
    public bool DisplayPositionMarkers = false;
    public bool DisplayDotMap = false;
    public ushort? DesiredTeam = null; 

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
