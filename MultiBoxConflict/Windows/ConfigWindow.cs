using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;

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
        Size = new Vector2(400, 500);

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
        if (ImGui.Checkbox("Add queue delay", ref Configuration.AddQueueDelay)) Configuration.Save();
        if (ImGui.Checkbox("Keep playing with externals", ref Configuration.KeepPlayingWithExternals)) Configuration.Save();
        string desiredTeamString = Configuration.DesiredTeam.HasValue ? Configuration.DesiredTeam.Value.ToString() : "";
        ImGui.Dummy(new Vector2(0, 10));
        ImGui.SetNextItemWidth(50);
        if (ImGui.InputText("Desired team (0/1)", ref desiredTeamString, 1))
        {
            if (desiredTeamString is "0" or "1")
            {
                Configuration.DesiredTeam = ushort.Parse(desiredTeamString);
            }
            else Configuration.DesiredTeam = null;
            Configuration.Save();
        }
        
        ImGui.Dummy(new Vector2(0, 30));
        ImGui.Text("Wintrading Configuration");
        if (ImGui.Checkbox("Wintrade", ref Configuration.Wintrade)) Configuration.Save();
        var isRegisteredLoserText = "Cannot locate character";
        if (Svc.Objects.LocalPlayer != null)
        {
            if (Configuration.RegisteredLosers.Contains(Svc.Objects.LocalPlayer.Name.TextValue))
            {
                isRegisteredLoserText = $"{Svc.Objects.LocalPlayer.Name.TextValue} will try to lose games.";
                if (ImGui.Button("Remove character from losers"))
                {
                    Configuration.RegisteredLosers.Remove(Svc.Objects.LocalPlayer.Name.TextValue);
                    Configuration.Save();
                }
            }
            else
            {
                isRegisteredLoserText = $"{Svc.Objects.LocalPlayer.Name.TextValue} will try to win.";
                if (ImGui.Button("Add character to losers"))
                {
                    Configuration.RegisteredLosers.Add(Svc.Objects.LocalPlayer.Name.TextValue);
                    Configuration.Save();
                }
            }
        }
        ImGui.Text(isRegisteredLoserText);
        
        ImGui.Dummy(new Vector2(0, 10));
        
        ImGui.Text("Registered Safe Characters");
        if (ImGui.Button("Import from clipboard"))
        {
            string clipboardText = ImGui.GetClipboardText();
            string[] lines = clipboardText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            bool canBeParsed = !(lines.Length == 0 || lines.Length > 50);
            
            //check that each line has 2 "words" separated by a space
            for (int i = 0; i < lines.Length; i++)
            {
                string[] words = lines[i].Split(' ');
                if (words.Length != 2)
                {
                    canBeParsed = false;
                    break;
                }
            }

            if (canBeParsed)
            {
                Configuration.RegisteredCharacters = lines.ToList();
                Configuration.Save();
            }
        }
        
        if (ImGui.Button("-", new Vector2(25, 0)))
        {
            if (Configuration.RegisteredCharacters.Count > 0)
            {
                Configuration.RegisteredCharacters.RemoveAt(Configuration.RegisteredCharacters.Count - 1);
                Configuration.Save();
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("+", new Vector2(25, 0)))
        {
            if (Configuration.RegisteredCharacters.Count < 50)
            {
                Configuration.RegisteredCharacters.Add("");
                Configuration.Save();
            }
        }

        for (int i = 0; i < Configuration.RegisteredCharacters.Count; i++)
        {
            string temp = Configuration.RegisteredCharacters[i];
            if (ImGui.InputText($"Character {i + 1}", ref temp))
            {
                Configuration.RegisteredCharacters[i] = temp;
                Configuration.Save();
            }
        }
        
    }
}
