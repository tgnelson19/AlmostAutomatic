using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FpsRange.Core;
using FpsRange.Rendering;

namespace FpsRange.Gameplay;

public enum NpcState { Patrol, Aiming, Attacking, Chasing }

/// <summary>
/// A wandering, shooting NPC. Body takes 4 hits to kill; a headshot is always instant death.
/// Simple AI (per spec): a 120 deg forward cone + range decides detection, cone/range only -
/// NOT occluded by walls (a documented simplification, see the plan). The NPC's own SHOT is
/// still occluded (checked by the caller via CollisionWorld.RaycastBlocked before applying
/// damage), so a wall-blocked NPC can "notice" a player it can't actually hit.
/// </summary>
public class Npc
{
    public const int BodyMaxHp = 4;
    public static readonly Vector3 BodySize = new Vector3(1.0f, 1.9f, 1.0f);
    public const float HeadWorldSize = 0.55f;
    private static readonly float HeadScale = HeadWorldSize / Target.Size;

    public const float MoveSpeedPatrol = 2.0f;
    public const float MoveSpeedChase = 2.0f; // walks, not sprints, toward a lost player - per spec
    public const float DetectionRange = 25f;
    public const float DetectionConeDegrees = 120f;
    public const float AimDelay = 0.5f;
    public const float ShotInterval = 0.5f;
    public const float MinHitChance = 0.15f, MaxHitChance = 0.85f;
    public const float MinEffectiveRange = 3f, MaxEffectiveRange = 25f;
    public const float LoseTargetGraceTime = 3f;
    public const float ChaseGiveUpDistance = 2f;
    private const float PatrolRepickTimeout = 8f;

    public Vector3 Position; // feet/ground position (not eye height, unlike PlayerController)
    public float Yaw;
    public int BodyHp = BodyMaxHp;
    public bool Alive = true;
    public NpcState State = NpcState.Patrol;

    private float _aimTimer;
    private float _shotTimer;
    private float _loseSightTimer;
    private Vector3 _lastKnownPlayerPos;
    private Vector3 _patrolTarget;
    private float _patrolTimer;

    private static Mesh _bodyMesh;

    // Optional muzzle-flash support, used by multiplayer remote-player avatars (a real NPC never
    // triggers this - only Multiplayer's RemotePlayerAvatar calls TriggerMuzzleFlash). Loaded
    // lazily per-instance since only avatars that actually need it call LoadMuzzleFlash.
    private readonly MuzzleFlash _muzzleFlash = new();
    private bool _muzzleFlashLoaded;

    public static void LoadShared(GraphicsDevice device)
    {
        var (verts, indices) = PrimitiveMeshBuilder.BuildBox(BodySize, new Color(95, 105, 70));
        _bodyMesh = new Mesh(device, verts, indices);
    }

    public void LoadMuzzleFlash(GraphicsDevice device)
    {
        if (_muzzleFlashLoaded) return;
        _muzzleFlash.Load(device);
        _muzzleFlashLoaded = true;
    }

    public void TriggerMuzzleFlash() => _muzzleFlash.Trigger();

    public void TickMuzzleFlash(GameTime gameTime)
    {
        if (_muzzleFlashLoaded) _muzzleFlash.Update(gameTime);
    }

    public Vector3 EyePosition => Position + new Vector3(0, BodySize.Y * 0.85f, 0);
    private Vector3 HeadCenter => Position + new Vector3(0, BodySize.Y + HeadWorldSize / 2f, 0);

    public void Spawn(Vector3 groundPosition)
    {
        Position = groundPosition;
        BodyHp = BodyMaxHp;
        Alive = true;
        State = NpcState.Patrol;
        _aimTimer = _shotTimer = _loseSightTimer = _patrolTimer = 0f;
        _patrolTarget = groundPosition;
    }

    public void TakeBodyHit()
    {
        if (!Alive) return;
        BodyHp--;
        if (BodyHp <= 0) Alive = false;
    }

    public void TakeHeadHit()
    {
        if (!Alive) return;
        BodyHp = 0;
        Alive = false;
    }

    public BoundingBox GetBodyBounds() => ComputeBodyBounds(Position);
    public BoundingBox GetHeadBounds() => ComputeHeadBounds(Position);

    /// <summary>
    /// Static equivalents of GetBodyBounds/GetHeadBounds usable without an Npc instance - used by
    /// MultiplayerMode to hit-test the host's own player (who has no avatar/body rendered to
    /// itself, but still needs to be a valid combat target for other players' shots).
    /// </summary>
    public static BoundingBox ComputeBodyBounds(Vector3 groundPos)
    {
        Vector3 center = groundPos + new Vector3(0, BodySize.Y / 2f, 0);
        Vector3 half = BodySize / 2f;
        return new BoundingBox(center - half, center + half);
    }

    public static BoundingBox ComputeHeadBounds(Vector3 groundPos)
    {
        Vector3 center = groundPos + new Vector3(0, BodySize.Y + HeadWorldSize / 2f, 0);
        var half = new Vector3(HeadWorldSize / 2f);
        return new BoundingBox(center - half, center + half);
    }

    private bool CanDetect(Vector3 playerPos)
    {
        Vector3 toPlayer = playerPos - EyePosition;
        toPlayer.Y = 0;
        float dist = toPlayer.Length();
        if (dist > DetectionRange) return false;
        if (dist < 0.01f) return true;

        Vector3 dir = toPlayer / dist;
        Vector3 facing = new Vector3(MathF.Sin(Yaw), 0, -MathF.Cos(Yaw));
        float dot = MathHelper.Clamp(Vector3.Dot(facing, dir), -1f, 1f);
        float angle = MathF.Acos(dot);
        return angle <= MathHelper.ToRadians(DetectionConeDegrees / 2f);
    }

    private void FaceTarget(Vector3 playerPos)
    {
        Vector3 dir = playerPos - Position;
        dir.Y = 0;
        if (dir.LengthSquared() > 0.0001f)
            Yaw = MathF.Atan2(dir.X, -dir.Z);
    }

    private void MoveToward(Vector3 targetGroundPos, float speed, float dt, CollisionWorld world)
    {
        Vector3 toTarget = targetGroundPos - Position;
        toTarget.Y = 0;
        float dist = toTarget.Length();
        if (dist > 0.1f)
        {
            Vector3 dir = toTarget / dist;
            Yaw = MathF.Atan2(dir.X, -dir.Z);
            float moveDist = MathF.Min(speed * dt, dist);
            Vector3 desired = Position + dir * moveDist;

            var resolved = world.ResolveHorizontal(Position, new Vector3(desired.X, Position.Y, desired.Z), Position.Y);
            Position.X = resolved.X;
            Position.Z = resolved.Z;
        }
        Position.Y = world.SampleGroundHeight(Position.X, Position.Z, Position.Y);
    }

    private void PatrolTick(float dt, CollisionWorld world, Vector2 mapMin, Vector2 mapMax, Random rng)
    {
        _patrolTimer -= dt;
        float distToTarget = Vector2.Distance(new Vector2(Position.X, Position.Z), new Vector2(_patrolTarget.X, _patrolTarget.Z));
        if (_patrolTimer <= 0f || distToTarget < 0.75f)
        {
            _patrolTarget = new Vector3(
                MathHelper.Lerp(mapMin.X, mapMax.X, (float)rng.NextDouble()),
                0,
                MathHelper.Lerp(mapMin.Y, mapMax.Y, (float)rng.NextDouble()));
            _patrolTimer = PatrolRepickTimeout;
        }
        MoveToward(_patrolTarget, MoveSpeedPatrol, dt, world);
    }

    /// <summary>
    /// Advances AI for one frame. Movement/state transitions happen here; if this NPC decides
    /// to take a shot this frame, didFire is set and the caller (NpcBattleMode) resolves whether
    /// it's blocked by a wall and rolls the hit-chance, since only the caller has the player's
    /// PlayerHealth to apply damage to.
    /// </summary>
    public void Update(GameTime gameTime, Vector3 playerPos, CollisionWorld world, Vector2 mapMin, Vector2 mapMax, Random rng,
                        out bool didFire, out Vector3 fireOrigin, out Vector3 fireDir, out float hitChance)
    {
        didFire = false;
        fireOrigin = default;
        fireDir = default;
        hitChance = 0f;
        if (!Alive) return;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        bool detected = CanDetect(playerPos);
        float dist = Vector2.Distance(new Vector2(Position.X, Position.Z), new Vector2(playerPos.X, playerPos.Z));

        switch (State)
        {
            case NpcState.Patrol:
                PatrolTick(dt, world, mapMin, mapMax, rng);
                if (detected) { State = NpcState.Aiming; _aimTimer = 0f; }
                break;

            case NpcState.Aiming:
                FaceTarget(playerPos);
                if (detected)
                {
                    _aimTimer += dt;
                    if (_aimTimer >= AimDelay) { State = NpcState.Attacking; _shotTimer = 0f; }
                }
                else
                {
                    State = NpcState.Chasing;
                    _lastKnownPlayerPos = playerPos;
                    _loseSightTimer = 0f;
                }
                break;

            case NpcState.Attacking:
                FaceTarget(playerPos);
                if (detected)
                {
                    _shotTimer -= dt;
                    if (_shotTimer <= 0f)
                    {
                        _shotTimer = ShotInterval;
                        didFire = true;
                        fireOrigin = EyePosition;
                        Vector3 toPlayer = playerPos - fireOrigin;
                        fireDir = toPlayer.LengthSquared() > 0.0001f ? Vector3.Normalize(toPlayer) : Vector3.Forward;

                        float t = MathHelper.Clamp((dist - MinEffectiveRange) / (MaxEffectiveRange - MinEffectiveRange), 0f, 1f);
                        hitChance = MathHelper.Lerp(MaxHitChance, MinHitChance, t);
                    }
                }
                else
                {
                    State = NpcState.Chasing;
                    _lastKnownPlayerPos = playerPos;
                    _loseSightTimer = 0f;
                }
                break;

            case NpcState.Chasing:
                MoveToward(_lastKnownPlayerPos, MoveSpeedChase, dt, world);
                if (detected)
                {
                    State = NpcState.Aiming;
                    _aimTimer = 0f;
                }
                else
                {
                    _loseSightTimer += dt;
                    float distToLastKnown = Vector2.Distance(new Vector2(Position.X, Position.Z), new Vector2(_lastKnownPlayerPos.X, _lastKnownPlayerPos.Z));
                    if (distToLastKnown <= ChaseGiveUpDistance || _loseSightTimer >= LoseTargetGraceTime)
                        State = NpcState.Patrol;
                }
                break;
        }
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix proj)
    {
        if (!Alive) return;

        Matrix bodyWorld = Matrix.CreateRotationY(Yaw) * Matrix.CreateTranslation(Position + new Vector3(0, BodySize.Y / 2f, 0));
        _bodyMesh.Draw(device, bodyWorld, view, proj);

        DrawHead(device, view, proj);

        if (_muzzleFlashLoaded)
        {
            Vector3 forward = new Vector3(MathF.Sin(Yaw), 0, -MathF.Cos(Yaw));
            Vector3 right = new Vector3(-forward.Z, 0, forward.X);
            Vector3 muzzlePos = Position + forward * 0.6f + right * 0.3f + new Vector3(0, BodySize.Y * 0.55f, 0);
            Matrix muzzleWorld = Matrix.CreateRotationY(Yaw) * Matrix.CreateTranslation(muzzlePos);
            _muzzleFlash.Draw(device, muzzleWorld, view, proj);
        }
    }

    /// <summary>
    /// Draws the head by reusing Target's shared cube+ring meshes (cosmetic consistency with the
    /// aim-testing targets), scaled down from Target.Size to HeadWorldSize. The head's hit-test
    /// (GetHeadBounds/TakeHeadHit) is binary, unlike Target's scored rings - the rings here are
    /// purely decorative.
    /// </summary>
    private void DrawHead(GraphicsDevice device, Matrix view, Matrix proj)
    {
        Matrix baseWorld = Matrix.CreateScale(HeadScale) * Matrix.CreateRotationY(Yaw) * Matrix.CreateTranslation(HeadCenter);
        Target.SharedCubeMesh.Draw(device, baseWorld, view, proj);

        const float epsilon = 0.006f;
        foreach (var face in Target.SharedFaces)
        {
            Vector3 faceCenter = face.normal * Target.HalfSize + face.normal * epsilon;
            Matrix rot = new Matrix(
                face.right.X, face.right.Y, face.right.Z, 0,
                face.up.X, face.up.Y, face.up.Z, 0,
                -face.normal.X, -face.normal.Y, -face.normal.Z, 0,
                0, 0, 0, 1);
            Matrix faceWorld = rot * Matrix.CreateTranslation(faceCenter) * baseWorld;

            foreach (var ringMesh in Target.SharedRingDiscMeshes)
                ringMesh.Draw(device, faceWorld, view, proj);
        }
    }
}
