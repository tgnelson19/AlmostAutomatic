using System;
using Microsoft.Xna.Framework;

namespace FpsRange.Core;

/// <summary>
/// First-person camera: tracks position + yaw/pitch, exposes view/projection matrices
/// and derived forward/right/up vectors. All hand-written (no scene graph).
/// </summary>
public class Camera
{
    public Vector3 Position;
    public float Yaw;   // radians, rotation around Y
    public float Pitch; // radians, clamped to avoid gimbal flip

    private const float MaxPitch = MathHelper.PiOver2 - 0.01f;

    public float AspectRatio { get; set; } = 16f / 9f;
    public float FieldOfView { get; set; } = MathHelper.ToRadians(75f);
    public float NearPlane { get; set; } = 0.05f;
    // Must comfortably exceed the skybox cube's corner-to-center distance (Size * sqrt(3))
    // or the far plane clips the cube's corners, showing the black clear color through as
    // a triangular gap near the horizon/corners - the "black triangle in the sky" bug.
    public float FarPlane { get; set; } = 2500f;

    public Vector3 Forward => new Vector3(
        MathF.Cos(Pitch) * MathF.Sin(Yaw),
        MathF.Sin(Pitch),
        -MathF.Cos(Pitch) * MathF.Cos(Yaw));

    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Vector3.Up));
    public Vector3 Up => Vector3.Normalize(Vector3.Cross(Right, Forward));

    public void AddYawPitch(float dYaw, float dPitch)
    {
        Yaw += dYaw;
        Pitch = MathHelper.Clamp(Pitch + dPitch, -MaxPitch, MaxPitch);
    }

    public Matrix ViewMatrix => Matrix.CreateLookAt(Position, Position + Forward, Vector3.Up);

    public Matrix ProjectionMatrix => Matrix.CreatePerspectiveFieldOfView(FieldOfView, AspectRatio, NearPlane, FarPlane);
}
