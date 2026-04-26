using System.Linq;
using Dalamud.Game.ClientState.Statuses;

namespace MultiBoxConflict.Service;

public static class PvPStatus
{
    public const uint Guard = 3054;
    public const uint Sprint = 1342;
    public const uint Resilience = 3248;
        
    public const uint Stun = 1343;
    public const uint Heavy = 1344;
    public const uint Bind = 1345;
    public const uint Silence = 1347;
    public const uint MiracleOfNature = 3085;
    public const uint DeepFreeze = 3219;
        
    public const uint Covered = 2413;
    public const uint Unguarded = 3021;
    public const uint Hysteria = 3023;
    public const uint Seduced = 3024;
    public const uint Meteodrive = 3174;
    public const uint SkyHigh = 3180;
    
    public const uint HallowedGround = 1302;
    public const uint InnerRelease = 1303;

    public const uint UmbralFireIii = 3381;
    public const uint UmbralIceIii = 3382;
    public const uint Paradox = 3223;
    public const uint SoulResonance = 3222;

    public static bool StatusCanAct(StatusList statusList)
    {
        return !statusList.Select(s => s.StatusId)
            .Intersect([Guard, Stun, Silence, MiracleOfNature, DeepFreeze, Hysteria, Seduced, Meteodrive]).Any();
    }
}