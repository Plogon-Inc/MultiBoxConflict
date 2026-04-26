using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace MultiBoxConflict.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin Plugin;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("MultiBoxConflict Config####MultiBoxConflict")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        SizeCondition = ImGuiCond.Always;

        Configuration = plugin.Configuration;
        Plugin = plugin;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Flags must be added or removed before Draw() is being called, or they won't apply
        //if (Configuration.IsConfigWindowMovable)
        //{
        //    Flags &= ~ImGuiWindowFlags.NoMove;
        //}
        //else
        //{
        //    Flags |= ImGuiWindowFlags.NoMove;
        //}
    }

    public override void Draw()
    {
        if (ImGui.Checkbox("Display entity markers", ref Configuration.DisplayEntityMarkers)) Configuration.Save();
        if (ImGui.Checkbox("Display position markers", ref Configuration.DisplayPositionMarkers)) Configuration.Save();
        if (ImGui.Checkbox("Display 10 yalm dot map", ref Configuration.DisplayDotMap)) Configuration.Save();
        string desiredTeamString = Configuration.DesiredTeam.HasValue ? Configuration.DesiredTeam.Value.ToString() : "";
        if (ImGui.InputText("Desired team (0/1)", ref desiredTeamString, 1))
        {
            if (desiredTeamString is "0" or "1")
            {
                Configuration.DesiredTeam = ushort.Parse(desiredTeamString);
            }
            else Configuration.DesiredTeam = null;
            Configuration.Save();
        }
    }
}
