using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FpsRange.Core;
using FpsRange.UI;

namespace FpsRange.Gameplay;

/// <summary>
/// Owns the NPC Battle map, NPC pool/AI, player HP, kill count, minimap, and mode-specific
/// HUD. Resolves both directions of combat: the player's weapon ray against NPC head/body
/// boxes, and each attacking NPC's shot against the player - both checked against
/// CollisionWorld.RaycastBlocked first so walls actually stop bullets either way.
/// </summary>
public class NpcBattleMode
{
    public NpcBattleMap Map { get; } = new();
    public NpcSpawner Spawner { get; } = new();
    public PlayerHealth Health { get; } = new();

    public event Action OnPlayerDied;
    public int BestKills => _bestKills;

    private readonly Minimap _minimap = new();
    private readonly NpcBattleHud _hud = new();
    private readonly HighScoreStore _highScoreStore = new();
    private readonly Random _rng = new();
    private int _kills;
    private int _bestKills;

    public static readonly Vector3 SpawnPoint = NpcBattleMap.PlayerSpawn;

    public void Load(GraphicsDevice device, SpriteFont font)
    {
        Map.Load(device);
        _minimap.Load(device, Map.Collision.WallBoxes);
        _hud.Load(device, font);
        Npc.LoadShared(device);
        _bestKills = _highScoreStore.TryGetBest();

        Health.OnDied += () =>
        {
            _highScoreStore.SaveIfBetter(_kills);
            _bestKills = Math.Max(_bestKills, _kills);
            OnPlayerDied?.Invoke();
        };
    }

    public void ResetRun()
    {
        Health.Reset();
        _kills = 0;
        Spawner.Npcs.Clear();
    }

    public void Update(GameTime gameTime, PlayerController player, Ray? fireRay, double totalTime)
    {
        Health.Update(gameTime);

        var mapMin = new Vector2(-NpcBattleMap.HalfX, -NpcBattleMap.HalfZ);
        var mapMax = new Vector2(NpcBattleMap.HalfX, NpcBattleMap.HalfZ);
        Vector3 playerPos = player.Camera.Position;

        Spawner.Update(playerPos, Map.Collision, mapMin, mapMax, _kills);

        foreach (var npc in Spawner.Npcs)
        {
            npc.Update(gameTime, playerPos, Map.Collision, mapMin, mapMax, _rng,
                out bool didFire, out Vector3 fireOrigin, out Vector3 fireDir, out float hitChance);

            if (didFire)
            {
                float distToPlayer = Vector3.Distance(fireOrigin, playerPos);
                bool blocked = Map.Collision.RaycastBlocked(new Ray(fireOrigin, fireDir), distToPlayer, out _);
                if (!blocked && _rng.NextDouble() < hitChance)
                    Health.TakeHit();
            }
        }

        if (fireRay.HasValue)
            ResolvePlayerShot(fireRay.Value);
    }

    private void ResolvePlayerShot(Ray ray)
    {
        Npc closestNpc = null;
        bool closestIsHead = false;
        float closestDist = float.MaxValue;

        foreach (var npc in Spawner.Npcs)
        {
            if (!npc.Alive) continue;

            float? headHit = ray.Intersects(npc.GetHeadBounds());
            if (headHit.HasValue && headHit.Value < closestDist)
            {
                closestDist = headHit.Value;
                closestNpc = npc;
                closestIsHead = true;
            }

            float? bodyHit = ray.Intersects(npc.GetBodyBounds());
            if (bodyHit.HasValue && bodyHit.Value < closestDist)
            {
                closestDist = bodyHit.Value;
                closestNpc = npc;
                closestIsHead = false;
            }
        }

        if (closestNpc == null) return;

        bool blocked = Map.Collision.RaycastBlocked(ray, closestDist, out _);
        if (blocked) return;

        if (closestIsHead) closestNpc.TakeHeadHit();
        else closestNpc.TakeBodyHit();

        if (!closestNpc.Alive)
            _kills++;
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix proj)
    {
        Map.Draw(device, view, proj);
        foreach (var npc in Spawner.Npcs)
            npc.Draw(device, view, proj);
    }

    public void DrawHud(SpriteBatch sb, GraphicsDevice device, Camera camera, Matrix view, Matrix proj)
    {
        _hud.Draw(sb, device, _kills, _bestKills, Health);

        foreach (var npc in Spawner.Npcs)
        {
            if (!npc.Alive) continue;
            Vector3 barPos = npc.Position + new Vector3(0, Npc.BodySize.Y + Npc.HeadWorldSize + 0.15f, 0);
            _hud.DrawNpcHealthBar(sb, device, view, proj, barPos, (float)npc.BodyHp / Npc.BodyMaxHp);
        }

        var npcPositions = new List<Vector3>();
        foreach (var npc in Spawner.Npcs)
            if (npc.Alive) npcPositions.Add(npc.Position);
        _minimap.Draw(sb, device, camera.Position, camera.Yaw, npcPositions);
    }
}
