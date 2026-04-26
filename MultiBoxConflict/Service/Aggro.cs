using System;
using ECommons.DalamudServices;

namespace MultiBoxConflict.Service;

public static class Aggro
{
    public static float Value = 0;

    public static void Reset()
    {
        Value = 0;
    }

    public static void Tick()
    {
        Value = (float)Math.Max(0, Value * 0.9 - 100);
        Value += 100 * Utils.EnemiesTargetingPlayer();
    }
}