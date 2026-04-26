using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
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
                Map = GetMap(Config.DesiredTeam);
                PluginStatus = Map == null ? "idle" : "match";
                if (Map != null) Map.DutyStartTime = DateTime.Now;
                MatchStatus = "skirmish";
                Aggro.Reset();
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

        if (PluginStatus == "idle" && Conditions.Instance()->HasPermission(119) &&
            Conditions.Instance()->HasPermission(120))
        {
            ContentsFinder.Instance()->QueueInfo.QueueRoulette(40);
            PluginStatus = "in_queue";
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
            
            default:
                Map.SkirmishMove();
                break;
        }
    }

    public void DisplayEntities()
    {
        if (Splatoon.IsConnected())
        {
            foreach (var obj in Svc.Objects.Where(o => o.ObjectKind != ObjectKind.Player)){
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
            _ => null
        };
        return map;
    }
    
    public void OnDutyStart(object? sender, ushort @ushort)
    {
        Map = GetMap();
        if (Map != null)
        {
            Map.ExitSpawnFlag = 0;
            Map.DutyStartTime = DateTime.Now;
        }
        Chat.ExecuteCommand("/rotation Auto");
        PluginStatus = "match";
        MatchStatus = "match_start";
        Aggro.Reset();
        PvPActionManager.Reset();
    }
    
    public void OnDutyEnd(object? sender, ushort @ushort)
    {
        EventFramework.LeaveCurrentContent(true);
        Map = null;
        if (FinishAfteNext)
        {
            IsRunning = false;
            FinishAfteNext = false;
        }
        PluginStatus = "idle";
    }

    public void OnTerritoryChange(ushort obj)
    {
        Map = null;
        MatchStatus = "none";
    }
    
    private unsafe void CheckForMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
        if (type is XivChatType.ErrorMessage && message.TextValue.ToString() == Svc.Data.GetExcelSheet<LogMessage>()[7392].Text.ToString()) {
            Svc.Log.Debug("Arena change error occured");
            TaskManager.EnqueueDelay(500);
            TaskManager.Enqueue(()=>
            {
                Svc.Log.Information("CC Arena Changed Error - Trying to requeue");
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
    }
    
    public void Dispose()
    {
        if(IsRunning) IsRunning = false;
    }
}
