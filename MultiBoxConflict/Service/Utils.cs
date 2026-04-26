using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.SplatoonAPI;
using MultiBoxConflict.Service.IPC;

namespace MultiBoxConflict.Service;

public static class Utils
{
    public static readonly string[] RangedJobs = [
        "Bard",
        "Machinist",
        "Dancer",
        "Black Mage",
        "Summoner",
        "Pictomancer",
        "White Mage",
        "Scholar",
        "Astrologian",
        "Sage"
    ];
    
    public static readonly string[] SquishyJobs = [
        "White Mage",
        "Scholar",
        "Astrologian",
        "Sage"
    ];

    public static bool JobIsRanged(string name)
    {
        return RangedJobs.Contains(name);
    }
    
    public static bool JobIsSquishy(string name)
    {
        return SquishyJobs.Contains(name);
    }
    

    public static IEnumerable<IBattleChara> GetAlliesAlive()
    {
        var localPlayer = Svc.Objects.LocalPlayer;
        var players = Svc.Objects.PlayerObjects.Where(o => o != localPlayer && !o.IsDead && !o.IsHostile());
        return players;
    }
    
    public static IEnumerable<IBattleChara> GetEnemiesAlive()
    {
        var players = Svc.Objects.PlayerObjects.Where(o => !o.IsDead && o.IsHostile());
        return players;
    }

    public static int GetBodyDifference()
    {
        var partyIds = Svc.Party
            .Where(p => p.GameObject != null)
            .Select(p => p.GameObject!.GameObjectId)
            .ToHashSet();

        var alivePartyCount = Svc.Objects.PlayerObjects
            .Count(o => !o.IsDead && partyIds.Contains(o.GameObjectId));

        var aliveEnemyCount = Svc.Objects.PlayerObjects
            .Count(o => !o.IsDead && !partyIds.Contains(o.GameObjectId));

        return alivePartyCount - aliveEnemyCount;
    }
    
    public static int EnemiesTargetingPlayer()
    {
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return 0;
        }

        return Utils.GetEnemiesAlive().Count(o => o.TargetObjectId == localPlayer.GameObjectId);
    }
    
    public static bool LineIntersectsCircle(Vector3 a, Vector3 b, Vector3 center, float r = 7)
    {
        // 1. Create vectors ignoring the Y axis
        Vector2 p1 = new Vector2(a.X, a.Z);
        Vector2 p2 = new Vector2(b.X, b.Z);
        Vector2 c = new Vector2(center.X, center.Z);

        // 2. Get the direction of the line segment and its length squared
        Vector2 lineDir = p2 - p1;
        float lineLengthSq = lineDir.LengthSquared();

        // Handle case where a and b are the same point
        if (lineLengthSq == 0)
        {
            return Vector2.Distance(p1, c) <= r;
        }

        // 3. Project the center onto the line to find the closest point 't'
        // t is the normalized distance along the line segment [0, 1]
        float t = Vector2.Dot(c - p1, lineDir) / lineLengthSq;

        // 4. Clamp 't' to the range [0, 1] to stay on the segment
        t = Math.Clamp(t, 0.0f, 1.0f);

        // 5. Find the actual closest point on the segment
        Vector2 closestPoint = p1 + t * lineDir;

        // 6. Check if the distance to the center is within the radius
        // We use LengthSquared to avoid an expensive Square Root calculation
        return Vector2.DistanceSquared(c, closestPoint) <= (r * r);
    }

    public static bool IsInBomb(Vector3 playerPos, Vector3 bombPos)
    {
        const float BombArmLength = 70f;
        const float BombHalfArmWidth = 5f;

        float dx = Math.Abs(playerPos.X - bombPos.X);
        float dz = Math.Abs(playerPos.Z - bombPos.Z);
        
        bool inVerticalArm = dx <= BombHalfArmWidth && dz <= BombArmLength;
        
        bool inHorizontalArm = dz <= BombHalfArmWidth && dx <= BombArmLength;

        return (inVerticalArm || inHorizontalArm);
    }

    public static List<IBattleChara> GetPlayersInDirectionalRectangle(
        Vector3 origin, 
        Vector3 target, 
        IEnumerable<IBattleChara> players, 
        float length = 40f, 
        float halfWidth = 2.5f)
    {
        var validPlayers = new List<IBattleChara>();

        // 1. Flatten origin and target to 2D (X and Z)
        Vector2 origin2D = new Vector2(origin.X, origin.Z);
        Vector2 target2D = new Vector2(target.X, target.Z);

        // 2. Calculate the direction vector from origin to target
        Vector2 direction = target2D - origin2D;

        // Guard clause: If origin and target are practically the same, we have no direction.
        if (direction.LengthSquared() < 0.0001f)
        {
            return validPlayers;
        }

        // 3. Define the local axes for the rectangle
        // The forward axis (normalized direction towards target)
        Vector2 forward = Vector2.Normalize(direction);
        
        // The right/lateral axis (perpendicular to the forward axis)
        Vector2 right = new Vector2(forward.Y, -forward.X);

        // 4. Evaluate each player
        foreach (var player in players)
        {
            // Flatten the player's position to 2D (X and Z)
            Vector2 playerPos2D = new Vector2(player.Position.X, player.Position.Z);

            // Vector from the origin to the current player's 2D position
            Vector2 toPlayer = playerPos2D - origin2D;

            // Project 'toPlayer' onto the 'forward' axis
            float forwardDistance = Vector2.Dot(toPlayer, forward);

            // Check if it falls within the length boundaries [0, 40]
            if (forwardDistance >= 0f && forwardDistance <= length)
            {
                // Project 'toPlayer' onto the 'right' axis
                float lateralDistance = Vector2.Dot(toPlayer, right);

                // Check if it falls within the width boundaries [-2.5, 2.5]
                if (Math.Abs(lateralDistance) <= halfWidth)
                {
                    // The player is inside the rectangle; add the object reference to the list
                    validPlayers.Add(player);
                }
            }
        }

        return validPlayers;
    }

    public static uint GetScoreColor(float value)
    {
        // 1. Clamp and normalize the value to [0, 1]
        float normalized = (Math.Clamp(value, 0f, 15f)) / 15f;

        // 2. Calculate channels (0.0 is Red, 1.0 is Green)
        // Red: starts at 255 (1.0), goes to 0
        byte r = (byte)((1f - normalized) * 255);
        // Green: starts at 0, goes to 255 (1.0)
        byte g = (byte)(normalized * 255);
        // Blue: stays 0
        byte b = 0;
        byte a = 64;

        // 3. Pack into 0xAABBGGRR
        // Layout: [Alpha (8 bits)][Blue (8 bits)][Green (8 bits)][Red (8 bits)]
        return ((uint)a << 24) | ((uint)b << 16) | ((uint)g << 8) | r;
    }
    
    public static void DisplayPositions(Dictionary<Vector3, float> positions, Vector3 bestPosition)
    {
        if (!Splatoon.IsConnected()) return;
        
        List<Element> elements = [];
        foreach (var pair in positions)
        {
            Element element = new(ElementType.CircleAtFixedCoordinates);
            element.SetRefCoord(pair.Key);
            element.color = pair.Key == bestPosition ? (uint)0x80FF0000 : GetScoreColor(pair.Value);
            element.radius = 0.5f;
            element.overlayText = pair.Value.ToString("0.0");
            element.overlayBGColor = (uint)0x000000;
            elements.Add(element);
        }

        Splatoon.AddDynamicElements("nmc_position_indicators", elements.ToArray(), 0.2f);
    }
    
    public static void DisplayDotMap()
    {
        if (!Splatoon.IsConnected()) return;
        
        int extent = 70;
        int step = 10;
        for (int x = -extent; x <= extent; x += step)
        {
            for (int z = -extent; z <= extent; z += step)
            {
                Element ele = new(ElementType.CircleAtFixedCoordinates);
                ele.radius = 0.01f;
                ele.color = 0xFF000000;
                ele.overlayText = x + ", " + z;
                ele.overlayBGColor = 0x00000000;
                ele.SetRefCoord(new Vector3(x, 0, z));
                Splatoon.DisplayOnce(ele);
            }
        }
    
    }
}