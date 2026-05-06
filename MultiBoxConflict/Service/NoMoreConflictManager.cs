using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.DutyState;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.SplatoonAPI;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using MultiBoxConflict.Service.CCMaps;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace MultiBoxConflict.Service;

public unsafe class MultiBoxConflictManager : IDisposable
{
    public Configuration Config;
    public bool FinishAfteNext = false;
    public ECommons.Automation.NeoTaskManager.TaskManager TaskManager = new ();

    public bool IsRunning
    {
        get;
        set
        {
            field = value;
            if (value)
            {
                Svc.Framework.Update += OnTick;
                Svc.DutyState.DutyStarted += OnDutyStart;
                Svc.DutyState.DutyCompleted += OnDutyEnd;
                Svc.ClientState.TerritoryChanged += OnTerritoryChange;
                Svc.Chat.ChatMessage += CheckForMessage;
                
                Chat.ExecuteCommand("/rotation Settings PvpStateControl true");
                Chat.ExecuteCommand("/rotation ToggleActions Standard-issue Elixir false");
                Chat.ExecuteCommand("/rotation Settings PoslockCasting true");
                Chat.ExecuteCommand("/rotation Settings AutoOffAfterCombat false");
                Chat.ExecuteCommand("/rotation Settings AutoOffWhenDeadPvP false");
                Chat.ExecuteCommand("/rotation Settings FilterStopMark false");
                
                Map = GetMap(Config.DesiredTeam);
                PluginStatus = Map == null ? "idle" : "match";
                if (Map != null)
                {
                    Map.DutyStartTime = DateTime.Now;

                    var playerNames = Svc.Objects.PlayerObjects.Select(p => p.Name.TextValue);
                    HasExternalPlayers = !Config.RegisteredCharacters.ContainsAll(playerNames);
                    MatchStatus = (Config.Wintrade && !HasExternalPlayers && Config.RegisteredLosers.Contains(Svc.PlayerState.CharacterName)) ? "anti_afk" : "skirmish";
                    
                    Aggro.Reset();
                    PvPActionManager.Reset();
                }
            }
            else
            {
                Svc.Framework.Update -= OnTick;
                Svc.DutyState.DutyStarted -= OnDutyStart;
                Svc.DutyState.DutyCompleted -= OnDutyEnd;
                Svc.ClientState.TerritoryChanged -= OnTerritoryChange;
                Svc.Chat.ChatMessage -= CheckForMessage;
            }
        }
    } = false;

    public string PluginStatus = "idle";
    public string MatchStatus = "match_start";
    public bool HasExternalPlayers = false;
    public DateTime? LastHealFail;
    public CrystallineConflictMap? Map;

    public MultiBoxConflictManager(Configuration config)
    {
        Config = config;
    }

    public void OnTick(IFramework framework)
    {
        if (!IsRunning) return;
        
        if (Config.DisplayEntityMarkers)
        {
            DisplayEntities();
        }

        if (Config.DisplayDotMap)
        {
            Utils.DisplayDotMap();
        }

        if (PluginStatus == "idle" 
            && Conditions.Instance()->HasPermission(119) 
            && Conditions.Instance()->HasPermission(120))
        {
            if (GenericHelpers.TryGetAddonByName("ContentsFinder", out AtkUnitBase* addonContentsFinder) && GenericHelpers.IsAddonReady(addonContentsFinder))
            {
                var selected = AgentContentsFinder.Instance()->SelectedContent;
                if (selected.Count == 1 && selected[0].Id == 40)
                {
                    PluginStatus = "in_queue";
                    if (Config.AddQueueDelay)
                        TaskManager.EnqueueDelay(1000);
                    
                    TaskManager.Enqueue(()=>{
                        Callback.Fire(addonContentsFinder, true, 12, 0); //join
                    });
                }
                else
                {
                    Callback.Fire(addonContentsFinder, true, 3, 1); //click duty
                }
            }
            else
                AgentContentsFinder.Instance()->OpenRouletteDuty(40); //open duty finder
            
            return;
        }
        
        if (PluginStatus == "in_queue" && (GenericHelpers.TryGetAddonByName("ContentsFinderConfirm", out AtkUnitBase* addonContentsFinderConfirm) && GenericHelpers.IsAddonReady(addonContentsFinderConfirm)))
        {
            Callback.Fire(addonContentsFinderConfirm, true, 8);
            return;
        }
        
        if (PluginStatus == "match" && Map != null && EzThrottler.Throttle("game_logic", 200))
        {
            Aggro.Tick();
            MatchLogic();
        }
    }

    public void UpdateGameState()
    {
        //Svc.Log.Debug("Current state: " + MatchStatus);
        if (Map == null) return;
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return;
        }
        
        if (localPlayer.IsDead)
        {
            if(MatchStatus != "dead")
            {
                MatchStatus = "dead";
                Svc.Log.Info("Changed match status to " + MatchStatus);
            }
            return;
        }
        if (MatchStatus == "dead" && !localPlayer.IsDead)
        {
            Map.ExitSpawnFlag = 0;
            MatchStatus = "respawned";
            Svc.Log.Info("Changed match status to " + MatchStatus);
            return;
        }
        if (MatchStatus is "healing" or "retreating" && (LastHealFail != null && DateTime.Now - LastHealFail < TimeSpan.FromSeconds(10)))
        {
            MatchStatus = "skirmish";
            Svc.Log.Info("Changed match status to " + MatchStatus);
            return;
        }
        if (MatchStatus != "healing" && (float)localPlayer.CurrentMp / localPlayer.MaxMp <= 0.20f && (LastHealFail == null || DateTime.Now - LastHealFail > TimeSpan.FromSeconds(10)))
        {
            MatchStatus = "healing";
            Svc.Log.Info("Changed match status to " + MatchStatus);
            return;
        }
        if (MatchStatus == "skirmish" && Utils.GetBodyDifference() <= -3 && DateTime.Now - Map.DutyStartTime < TimeSpan.FromSeconds(295))
        {
            var closestEnemy = Utils.GetEnemiesAlive()
                .OrderBy(o => Vector3.DistanceSquared(o.Position, Map.GetSafespot()))
                .FirstOrDefault();
            if(closestEnemy == null || Vector3.DistanceSquared(closestEnemy.Position, Map.GetSafespot()) >= 400)
            {
                MatchStatus = "retreating";
                Svc.Log.Info("Changed match status to " + MatchStatus);
                return;
            }
        }
        if (MatchStatus == "retreating" && (Utils.GetBodyDifference() >= -1 || 
            (Utils.GetBodyDifference() >= -2 && Utils.GetAlliesAlive().Count(a => Vector3.DistanceSquared(a.Position, 
                                                    Svc.Objects.Where(t=>t.Name.TextValue == "Tactical Crystal").Select(t=>t.Position).FirstOrDefault()) <= 625) >= 2)))
        {
            MatchStatus = "reengaging";
            Svc.Log.Info("Changed match status to " + MatchStatus);
            return;
        }
        if (MatchStatus == "healing" && (float)localPlayer.CurrentMp / localPlayer.MaxMp >= 0.55f)
        {
            MatchStatus = "reengaging";
            Svc.Log.Info("Changed match status to " + MatchStatus);
            return;
        }
    }

    public void MatchLogic()
    {
        if (Map == null) return;
        
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return;
        }

        var returnFlag = Map.PriorityMapActions();
        
        UpdateGameState();
        
        if (localPlayer.IsDead) return;

        PvPActionManager.ExecuteAll();
        
        switch (MatchStatus)
        {
            case "match_start":
                if (!Map.OnMatchStart())
                {
                    //when has reached deploy spot
                    MatchStatus = "skirmish";
                    Svc.Log.Debug("Reached start pos");
                    EzThrottler.Throttle("game_logic", Map.MatchStartDelay);
                }
                break;
            
            case "healing":
                if (!Map.GoHeal())
                {
                    //failed to heal due to nearby player
                    LastHealFail = DateTime.Now;
                    MatchStatus = "skirmish";
                    Svc.Log.Debug("Changed match status to " + MatchStatus);
                    MatchLogic();
                }
                break;
            
            case "retreating":
                Map.RetreatMove();
                break;
            
            case "reengaging":
                if (!Map.Reengage())
                {
                    //is close enough to player or crystal
                    MatchStatus = "skirmish";
                    Svc.Log.Debug("Changed match status to " + MatchStatus);
                    MatchLogic();
                }
                break;
            
            case "respawned":
                if (!Map.Respawn())
                {
                    //is close enough to player or crystal
                    MatchStatus = "skirmish";
                    Svc.Log.Debug("Changed match status to " + MatchStatus);
                    MatchLogic();
                }
                break;
            
            case "anti_afk":
                if (EzThrottler.Check("AntiAfkJump"))
                {
                    EzThrottler.Throttle("AntiAfkJump", 30000);
                    ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
                }
                break;
            
            default:
                Map.SkirmishMove();
                break;
        }
    }

    public void DisplayEntities()
    {
        if (Splatoon.IsConnected())
        {
            foreach (var obj in Svc.Objects.Where(o => o.ObjectKind != ObjectKind.Pc)){
                var ele = new Element(ElementType.CircleRelativeToActorPosition);
                ele.refActorName = obj.Name.TextValue;
                var text = obj.Name.TextValue;
                if (obj is IBattleChara battleNpc)
                {
                    var ownerId = battleNpc.OwnerId;
                    var owner = Svc.Objects.SearchById(ownerId);
                    if (owner != null)
                    {
                        text += " - Owner:" + owner.Name.TextValue;
                    }
                    if (battleNpc.IsCasting)
                    {
                        var castId = battleNpc.CastActionId;
                        text += " - Casting(" + castId + ")";
                    }
                    foreach (var status in battleNpc.StatusList)
                    {
                        text += " - Status:" + status.StatusId;
                    }
                }
                ele.overlayText = text;
                Svc.Log.Debug(text);
                Splatoon.DisplayOnce(ele);
            }
        }
    }
    
    public CrystallineConflictMap? GetMap(ushort? predefinedTeam = null)
    {
        var territory = Content.TerritoryName;
        if (territory == null) 
            return null;
        Svc.Log.Debug(territory);
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return null;
        }

        Vector3 givenPos;
        if (predefinedTeam.HasValue)
        {
            if (predefinedTeam == 0)
                givenPos = new Vector3(-100, 0, -100);
            else
                givenPos = new Vector3(100, 0, 100);
        }
        else givenPos = localPlayer.Position;
        
        CrystallineConflictMap? map = territory switch
        {
            "The Palaistra" => new ThePalaistra(Config, givenPos),
            "The Volcanic Heart" => new TheVolcanicHeart(Config, givenPos),
            "Cloud Nine" => new CloudNine(Config, givenPos),
            "The Clockwork Castletown" => new TheClockworkCastletown(Config, givenPos),
            "The Red Sands" => new TheRedSands(Config, givenPos),
            "The Bayside Battleground" => new TheBaysideBattleground(Config, givenPos),
            "Archeia Harmonias" => new ArcheiaHarmonias(Config, givenPos),
            _ => null
        };
        return map;
    }
    
    public void OnDutyStart(IDutyStateEventArgs args)
    {
        Map = GetMap();
        if (Map != null)
        {
            Map.ExitSpawnFlag = 0;
            Map.DutyStartTime = DateTime.Now;
            
            Chat.ExecuteCommand("/rotation Auto");
            PluginStatus = "match";
            var playerNames = Svc.Objects.PlayerObjects.Select(p => p.Name.TextValue);
            HasExternalPlayers = !Config.RegisteredCharacters.ContainsAll(playerNames);
            MatchStatus = (Config.Wintrade && !HasExternalPlayers && Config.RegisteredLosers.Contains(Svc.PlayerState.CharacterName)) ? "anti_afk" : "match_start";
            if(Config.RegisteredCharacters.Count > 0 && MatchStatus == "anti_afk" && Config.TeamUp && Svc.Objects.PlayerObjects.Where(o => !o.IsDead && !o.IsHostile()).Select(o=>o.Name.TextValue).Contains(Config.RegisteredCharacters[0]))
                MatchStatus = "match_start";
            
            Aggro.Reset();
            PvPActionManager.Reset();
        }
    }
    
    public void OnDutyEnd(IDutyStateEventArgs args)
    {
        EventFramework.LeaveCurrentContent(true);
        Map = null;
        if (FinishAfteNext || (Config.Wintrade && HasExternalPlayers && Config.KeepPlayingWithExternals))
        {
            IsRunning = false;
            FinishAfteNext = false;
        }
        PluginStatus = "idle";
    }

    public void OnTerritoryChange(uint obj)
    {
        Map = null;
        MatchStatus = "none";
    }
    
    private void CheckForMessageOld(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
        if (type is XivChatType.ErrorMessage && message.TextValue == Svc.Data.GetExcelSheet<LogMessage>()[7392].Text.ToString()) {
            Svc.Log.Debug("Arena change error occured");
            TaskManager.EnqueueDelay(500);
            TaskManager.Enqueue(()=>
            {
                Svc.Log.Information("CC Arena Changed Error - Trying to requeue");
                PluginStatus = "idle";
            });
        }
        else if (type is XivChatType.ErrorMessage)
        {
            Svc.Log.Debug("Error trying to queue up - Trying to requeue");
            TaskManager.EnqueueDelay(500);
            TaskManager.Enqueue(()=>
            {
                PluginStatus = "idle";
            });
        }
    }
    private void CheckForMessage(IHandleableChatMessage chatMessage) {
        if (chatMessage.LogKind is XivChatType.ErrorMessage && chatMessage.Message.TextValue == Svc.Data.GetExcelSheet<LogMessage>()[7392].Text.ToString()) {
            Svc.Log.Debug("Arena change error occured");
            TaskManager.EnqueueDelay(500);
            TaskManager.Enqueue(()=>
            {
                Svc.Log.Information("CC Arena Changed Error - Trying to requeue");
                PluginStatus = "idle";
            });
        }
        else if (chatMessage.LogKind is XivChatType.ErrorMessage)
        {
            Svc.Log.Debug("Error trying to queue up - Trying to requeue");
            TaskManager.EnqueueDelay(500);
            TaskManager.Enqueue(()=>
            {
                PluginStatus = "idle";
            });
        }
    }

    public void LogEntities()
    {
        Svc.Log.Debug("Displaying entities:");
        foreach (var obj in Svc.Objects)
        {
            var text = "Name: " + obj.Name.TextValue + "@" + obj.Position.ToString("0.0");
            if (obj is IBattleChara battleNpc)
            {
                var ownerId = battleNpc.OwnerId;
                var owner = Svc.Objects.SearchById(ownerId);
                if (owner != null)
                {
                    text += " - Owner: " + owner.Name.TextValue;
                }

                if (battleNpc.IsCasting)
                {
                    var castId = battleNpc.CastActionId;
                    text += " - Casting(" + castId + ")";
                }

                foreach (var status in battleNpc.StatusList)
                {
                    text += " - Status(" + status.StatusId + ")";
                }
            }
            Svc.Log.Debug(text);
        }
        
        Svc.Log.Debug("-----------------------------------");
    }

    
    public unsafe void Debug()
    {
        if (GenericHelpers.TryGetAddonByName("ContentsFinder", out AtkUnitBase* addonContentsFinder) && GenericHelpers.IsAddonReady(addonContentsFinder))
        {
            var selected = AgentContentsFinder.Instance()->SelectedContent;
            if (selected.Count == 1 && selected[0].Id == 40)
            {
                Callback.Fire(addonContentsFinder, true, 12, 0);
            }
            else
            {
                Callback.Fire(addonContentsFinder, true, 3, 1);
            }
        }
        else
            AgentContentsFinder.Instance()->OpenRouletteDuty(40);
        
    }
    
    public void Dispose()
    {
        if(IsRunning) IsRunning = false;
    }
}
