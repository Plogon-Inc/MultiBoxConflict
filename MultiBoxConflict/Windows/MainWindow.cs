using System;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using MultiBoxConflict.Service;

namespace MultiBoxConflict.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Configuration Configuration;

    public MainWindow(Plugin plugin)
        : base("MultiBoxConflict##0", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Plugin = plugin;
        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.Button("Show Settings"))
        {
            Plugin.ToggleConfigUI();
        }

        ImGui.Spacing();
        ImGui.Spacing();
        
        var enabled = Plugin.MultiBoxConflictManager.IsRunning;
        if (ImGui.Checkbox("Enable CC farm", ref enabled)) Plugin.MultiBoxConflictManager.IsRunning = enabled;
        ImGui.Checkbox("Stop after next match", ref Plugin.MultiBoxConflictManager.FinishAfteNext);
        
        if (ImGui.Button("Debug")) Plugin.MultiBoxConflictManager.Debug();
        if (ImGui.Button("Entities")) Plugin.MultiBoxConflictManager.LogEntities();
    }
}
