using Microsoft.Xna.Framework;

namespace FpsRange.Gameplay;

/// <summary>
/// One other connected player, as seen by this peer: an Npc instance reused purely as a visual
/// proxy (its AI Update() is never called - Position/Yaw are driven directly from network
/// snapshots) plus an interpolation buffer so movement stays smooth despite the network tick
/// (~30Hz) running far slower than rendering. The standard technique for low-tick-rate
/// multiplayer: store the last two received samples and linearly interpolate between them
/// over the tick interval rather than snapping to each new sample.
/// </summary>
public class RemotePlayerAvatar
{
    public readonly Npc Avatar = new();
    public string Name = "Player";
    public float DisplayHp = PlayerHealth.MaxHp;
    public float LastPitch; // not used for rendering (avatars don't tilt), only for the host's shot-direction reconstruction

    private const float SnapInterval = 1f / 30f; // matches MultiplayerMode's network tick rate

    private Vector3 _prevPos, _targetPos;
    private float _prevYaw, _targetYaw;
    private float _snapTimer;
    private bool _hasSnapshot;

    /// <summary>Called whenever a fresh position/yaw sample arrives over the network - begins
    /// interpolating from wherever the avatar currently displays toward the new sample.</summary>
    public void ApplySnapshot(Vector3 pos, float yaw)
    {
        if (!_hasSnapshot)
        {
            _prevPos = _targetPos = pos;
            _prevYaw = _targetYaw = yaw;
            _hasSnapshot = true;
        }
        else
        {
            _prevPos = _targetPos;
            _prevYaw = _targetYaw;
            _targetPos = pos;
            _targetYaw = yaw;
        }
        _snapTimer = 0f;
    }

    /// <summary>Instantly moves the avatar with no interpolation - used for respawns, where
    /// smoothly gliding from the old position to the new one would look like teleporting
    /// through walls instead of an actual teleport.</summary>
    public void SnapTo(Vector3 pos, float yaw)
    {
        _prevPos = _targetPos = pos;
        _prevYaw = _targetYaw = yaw;
        _hasSnapshot = true;
        _snapTimer = 0f;
        Avatar.Position = pos;
        Avatar.Yaw = yaw;
    }

    public void Tick(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _snapTimer += dt;
        float t = MathHelper.Clamp(_snapTimer / SnapInterval, 0f, 1f);
        Avatar.Position = Vector3.Lerp(_prevPos, _targetPos, t);
        Avatar.Yaw = LerpAngle(_prevYaw, _targetYaw, t);
        Avatar.TickMuzzleFlash(gameTime);
        Avatar.TickAnimation(gameTime);
    }

    private static float LerpAngle(float a, float b, float t)
    {
        float diff = b - a;
        while (diff > MathHelper.Pi) diff -= MathHelper.TwoPi;
        while (diff < -MathHelper.Pi) diff += MathHelper.TwoPi;
        return a + diff * t;
    }
}
