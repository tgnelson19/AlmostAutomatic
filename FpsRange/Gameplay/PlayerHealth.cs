using System;
using Microsoft.Xna.Framework;

namespace FpsRange.Gameplay;

/// <summary>
/// Player HP for NPC Battle: 100 max, analog regen at 10 HP/sec once 3 seconds have passed
/// since the last hit (the regen rate is continuous/fractional, not a stepped tick). NPCs deal
/// 34 damage per landed shot (~3 hits to kill). NPCs don't get this component - they don't regenerate.
/// </summary>
public class PlayerHealth
{
    public const float MaxHp = 100f;
    public const float RegenDelay = 3f;         // seconds since last hit before regen starts
    public const float RegenRatePerSecond = 10f; // analog HP/sec once regen has kicked in
    public const float DamagePerHit = 34f;

    public float Hp { get; private set; } = MaxHp;
    public event Action OnDied;

    private float _timeSinceHit;

    public void Reset()
    {
        Hp = MaxHp;
        _timeSinceHit = 0f;
    }

    public void TakeHit()
    {
        if (Hp <= 0f) return;
        Hp = Math.Max(0f, Hp - DamagePerHit);
        _timeSinceHit = 0f;
        if (Hp <= 0f)
            OnDied?.Invoke();
    }

    /// <summary>Directly sets HP to a specific value, bypassing damage/regen logic and without
    /// firing OnDied - used by multiplayer clients to reconcile their local display with the
    /// host's authoritative value (received via HitEvent/RespawnEvent).</summary>
    public void SetHp(float hp) => Hp = Math.Clamp(hp, 0f, MaxHp);

    /// <summary>Instant kill (headshot in multiplayer PvP) - matches Npc.TakeHeadHit's semantics.</summary>
    public void TakeHeadshot()
    {
        if (Hp <= 0f) return;
        Hp = 0f;
        _timeSinceHit = 0f;
        OnDied?.Invoke();
    }

    public void Update(GameTime gameTime)
    {
        if (Hp <= 0f || Hp >= MaxHp) return;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _timeSinceHit += dt;
        if (_timeSinceHit >= RegenDelay)
            Hp = Math.Min(MaxHp, Hp + RegenRatePerSecond * dt);
    }
}
