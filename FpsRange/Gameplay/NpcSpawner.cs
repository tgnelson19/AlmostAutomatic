using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using FpsRange.Core;

namespace FpsRange.Gameplay;

/// <summary>
/// Maintains the roaming NPC pool: starts at 3, grows by 1 every 10 player kills (capped),
/// and immediately backfills any NPC that dies somewhere away from the player - the same
/// "replace on death" pattern as TargetSpawner.ReplaceDead, adapted to a rectangular map
/// with obstacles instead of TargetSpawner's circular-platform annulus.
/// </summary>
public class NpcSpawner
{
    public const int BasePoolSize = 3;
    public const int ScaleEveryKills = 10;
    public const int MaxNpcs = 12; // perf/difficulty safeguard - placeholder, open to tuning
    public const float MinSpawnDistanceFromPlayer = 15f;

    public List<Npc> Npcs { get; } = new();

    private readonly Random _rng = new();

    public void Update(Vector3 playerPos, CollisionWorld world, Vector2 mapMin, Vector2 mapMax, int totalPlayerKills)
    {
        Npcs.RemoveAll(n => !n.Alive);

        int targetCount = Math.Min(MaxNpcs, BasePoolSize + totalPlayerKills / ScaleEveryKills);
        while (Npcs.Count < targetCount)
        {
            var npc = new Npc();
            npc.Spawn(FindSpawnPoint(playerPos, world, mapMin, mapMax));
            Npcs.Add(npc);
        }
    }

    private Vector3 FindSpawnPoint(Vector3 playerPos, CollisionWorld world, Vector2 mapMin, Vector2 mapMax)
    {
        Vector3 best = playerPos;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            float x = MathHelper.Lerp(mapMin.X, mapMax.X, (float)_rng.NextDouble());
            float z = MathHelper.Lerp(mapMin.Y, mapMax.Y, (float)_rng.NextDouble());
            var candidate = new Vector3(x, 0, z);

            float distToPlayer = Vector2.Distance(new Vector2(x, z), new Vector2(playerPos.X, playerPos.Z));
            if (distToPlayer < MinSpawnDistanceFromPlayer) { best = candidate; continue; }

            bool overlapsWall = false;
            foreach (var box in world.WallBoxes)
            {
                if (x >= box.Min.X - 0.6f && x <= box.Max.X + 0.6f && z >= box.Min.Z - 0.6f && z <= box.Max.Z + 0.6f)
                {
                    overlapsWall = true;
                    break;
                }
            }
            if (overlapsWall) { best = candidate; continue; }

            candidate.Y = world.SampleGroundHeight(x, z, 0f);
            return candidate;
        }

        best.Y = world.SampleGroundHeight(best.X, best.Z, 0f);
        return best;
    }
}
