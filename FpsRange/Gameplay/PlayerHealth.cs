using System;
using Microsoft.Xna.Framework;

namespace FpsRange.Gameplay;

/// <summary>
/// Player HP, shared by NPC Battle and Multiplayer: 100 max, analog regen at 10 HP/sec once 3
/// seconds have passed since the last hit (the regen rate is continuous/fractional, not a stepped
/// tick). NPCs don't get this component - they don't regenerate. NPC Battle deals a fixed 34
/// damage per landed shot (~3 hits to kill; the RNG there only decides whether a shot lands at
/// all, in NpcBattleMode/Npc.cs - it never touches the damage amount). Multiplayer PvP is fully
/// deterministic end to end: MultiplayerMode resolves every shot as a raycast box test with no
/// hit-chance roll, and deals a fixed 25 damage per body shot (MultiplayerMode.BodyShotDamage);
/// a headshot always deals the full 100 via TakeHeadshot below, i.e. always lethal.
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

    /// <summary>Applies a fixed amount of damage (defaults to NPC Battle's DamagePerHit).
    /// Multiplayer PvP body shots pass their own fixed amount here instead - see
    /// MultiplayerMode.BodyShotDamage.</summary>
    public void TakeHit(float damage = DamagePerHit)
    {
        if (Hp <= 0f) return;
        Hp = Math.Max(0f, Hp - damage);
        _timeSinceHit = 0f;
        if (Hp <= 0f)
            OnDied?.Invoke();
    }

    /// <summary>Directly sets HP to a specific value, bypassing damage/regen logic - used by
    /// multiplayer clients to reconcile their local display with the host's authoritative value
    /// (received via HitEvent/RespawnEvent). Still fires OnDied the moment this crosses to 0, so a
    /// client who gets killed reacts the same way a host who kills itself locally does.</summary>
    public void SetHp(float hp)
    {
        bool justDied = hp <= 0f && Hp > 0f;
        Hp = Math.Clamp(hp, 0f, MaxHp);
        _timeSinceHit = 0f;
        if (justDied) OnDied?.Invoke();
    }

    /// <summary>Headshot in multiplayer PvP: always deals the full 100 HP (i.e. always lethal
    /// regardless of current HP), matching Npc.TakeHeadHit's semantics.</summary>
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
