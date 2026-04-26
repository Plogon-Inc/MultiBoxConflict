using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons;
using MultiBoxConflict.Service;
using MultiBoxConflict.Windows;

namespace MultiBoxConflict;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public required MultiBoxConflictManager MultiBoxConflictManager;

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("MultiBoxConflict");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ECommonsMain.Init(PluginInterface, this, Module.SplatoonAPI);
        MultiBoxConflictManager = new MultiBoxConflictManager(Configuration);

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler("/mbc", new CommandInfo(OnCommand)
        {
            HelpMessage = "/mbc [start|stop] --- By default opens UI"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        Log.Information($"==={PluginInterface.Manifest.Name} loaded up===");
    }

    public void Dispose()
    {
        MultiBoxConflictManager.Dispose();
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler("/mbc");
        ECommonsMain.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        if (args == "")
        {
            ToggleMainUI();
        }
        else if (args == "start")
        {
            MultiBoxConflictManager.IsRunning = true;
        }
        else if (args == "stop")
        {
            MultiBoxConflictManager.IsRunning = false;
        }
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
