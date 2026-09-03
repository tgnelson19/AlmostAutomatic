using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using FpsRange.Core;

namespace FpsRange.Gameplay;

/// <summary>
/// Keeps up to 20 targets alive at all times, spawned in an annulus around the player
/// (half the platform radius out to the full radius) and at a height between the platform
/// surface and 2x the player's eye height.
/// </summary>
public class TargetSpawner
{
    public const int MaxTargets = 20;
    private const float SpawnInterval = 0.35f; // trickle new targets in rather than all at once

    public List<Target> Targets { get; } = new();

    private readonly Random _rng = new();
    private float _spawnTimer;

    public void Update(GameTime gameTime, Vector3 playerPosition)
    {
        Targets.RemoveAll(t => !t.Alive);

        _spawnTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_spawnTimer <= 0f && Targets.Count < MaxTargets)
        {
            _spawnTimer = SpawnInterval;
            Targets.Add(SpawnOne(playerPosition));
        }
    }

    /// <summary>
    /// Immediately removes dead targets and tops the pool back up to MaxTargets, bypassing the
    /// trickle timer - called right after a hit so a destroyed target is replaced at once rather
    /// than waiting up to SpawnInterval for the next Update tick to notice.
    /// </summary>
    public void ReplaceDead(Vector3 playerPosition)
    {
        Targets.RemoveAll(t => !t.Alive);
        while (Targets.Count < MaxTargets)
            Targets.Add(SpawnOne(playerPosition));
    }

    private Target SpawnOne(Vector3 playerPosition)
    {
        float radius = PlayerController.PlatformRadius;
        float minR = radius * 0.5f;
        float maxR = radius - Target.HalfSize - 1f;

        float dist = MathHelper.Lerp(minR, maxR, (float)_rng.NextDouble());
        float angle = (float)(_rng.NextDouble() * MathHelper.TwoPi);

        float x = playerPosition.X + dist * MathF.Cos(angle);
        float z = playerPosition.Z + dist * MathF.Sin(angle);

        // Clamp to platform bounds in case the offset from player pushed it past the edge.
        float horizDist = MathF.Sqrt(x * x + z * z);
        float maxPlatformDist = radius - Target.HalfSize - 0.5f;
        if (horizDist > maxPlatformDist)
        {
            float scale = maxPlatformDist / horizDist;
            x *= scale;
            z *= scale;
        }

        float maxHeight = 2f * PlayerController.EyeHeight;
        float y = (float)(_rng.NextDouble() * maxHeight) + Target.HalfSize;

        return new Target { Position = new Vector3(x, y, z) };
    }
}
