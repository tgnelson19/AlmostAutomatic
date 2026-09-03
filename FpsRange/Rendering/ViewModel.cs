using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FpsRange.Core;

namespace FpsRange.Rendering;

/// <summary>
/// Generic blocky FPS viewmodel: two forearm boxes and a boxy pistol held in the right hand,
/// rendered relative to the camera (classic "gun in front of face, own depth pass" trick) so it
/// never clips into world geometry. Includes simple movement sway/bob and a fire recoil kick.
/// </summary>
public class ViewModel
{
    private Mesh _armMesh;
    private Mesh _pistolBodyMesh;
    private Mesh _pistolBarrelMesh;
    private Mesh _pistolGripMesh;
    private readonly MuzzleFlash _muzzleFlash = new();

    // Local offsets (in camera space: +X right, +Y up, -Z forward) for the right arm/pistol.
    // Pushed further toward the screen edges than the original (-0.22/0.32) so the arms
    // aren't clustered directly in the middle of the view.
    private static readonly Vector3 PistolBaseOffset = new Vector3(0.44f, -0.32f, -0.55f);
    private static readonly Vector3 LeftArmOffset = new Vector3(-0.34f, -0.38f, -0.5f);

    private float _bobTimer;
    private float _recoil;

    public void Load(GraphicsDevice device)
    {
        var skin = new Color(210, 175, 140); // generic "arm" tone

        var (av, ai) = PrimitiveMeshBuilder.BuildBox(new Vector3(0.14f, 0.14f, 0.55f), skin);
        _armMesh = new Mesh(device, av, ai);

        var (bv, bi) = PrimitiveMeshBuilder.BuildBox(new Vector3(0.16f, 0.22f, 0.32f), new Color(50, 50, 55));
        _pistolBodyMesh = new Mesh(device, bv, bi);

        var (rv, ri) = PrimitiveMeshBuilder.BuildBox(new Vector3(0.08f, 0.08f, 0.28f), new Color(30, 30, 33));
        _pistolBarrelMesh = new Mesh(device, rv, ri);

        var (gv, gi) = PrimitiveMeshBuilder.BuildBox(new Vector3(0.13f, 0.24f, 0.14f), new Color(40, 40, 44));
        _pistolGripMesh = new Mesh(device, gv, gi);

        _muzzleFlash.Load(device);
    }

    public void TriggerFire() => _muzzleFlash.Trigger();

    public void Update(GameTime gameTime, bool isMoving)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _bobTimer += isMoving ? dt * 9f : dt * 2f;
        _recoil = MathHelper.Max(0f, _recoil - dt * 9f);
        _muzzleFlash.Update(gameTime);
    }

    public void AddRecoil() => _recoil = 1f;

    /// <summary>
    /// Draws the viewmodel by building world matrices from the camera's basis vectors, so it
    /// always stays fixed in front of the player's view regardless of world position/rotation.
    /// Depth buffer is cleared beforehand so the viewmodel never gets clipped by world geometry.
    /// </summary>
    public void Draw(GraphicsDevice device, Camera camera)
    {
        device.Clear(ClearOptions.DepthBuffer, Color.Black, 1f, 0);

        Vector3 bobOffset = new Vector3(
            MathF.Sin(_bobTimer) * 0.012f,
            MathF.Abs(MathF.Sin(_bobTimer)) * 0.01f,
            0);

        Matrix camBasis = new Matrix(
            camera.Right.X, camera.Right.Y, camera.Right.Z, 0,
            camera.Up.X, camera.Up.Y, camera.Up.Z, 0,
            -camera.Forward.X, -camera.Forward.Y, -camera.Forward.Z, 0,
            0, 0, 0, 1);

        Matrix view = camera.ViewMatrix;
        // Use a narrower/consistent FOV feel but reuse main projection for simplicity.
        Matrix proj = camera.ProjectionMatrix;

        Vector3 recoilOffset = new Vector3(0, 0, _recoil * 0.08f);

        Vector3 pistolLocal = PistolBaseOffset + bobOffset + recoilOffset;
        Vector3 pistolWorldPos = camera.Position
            + camera.Right * pistolLocal.X
            + camera.Up * pistolLocal.Y
            + camera.Forward * (-pistolLocal.Z);

        Matrix pistolWorld = camBasis * Matrix.CreateTranslation(pistolWorldPos);

        _pistolBodyMesh.Draw(device, pistolWorld, view, proj);
        _pistolBarrelMesh.Draw(device, Matrix.CreateTranslation(0, 0.02f, -0.28f) * pistolWorld, view, proj);
        _pistolGripMesh.Draw(device, Matrix.CreateTranslation(0, -0.22f, 0.06f) * pistolWorld, view, proj);

        Vector3 armLocal = LeftArmOffset + bobOffset;
        Vector3 armWorldPos = camera.Position
            + camera.Right * armLocal.X
            + camera.Up * armLocal.Y
            + camera.Forward * (-armLocal.Z);
        Matrix armWorld = camBasis * Matrix.CreateTranslation(armWorldPos);
        _armMesh.Draw(device, armWorld, view, proj);

        // Right forearm, tucked behind the pistol grip.
        Vector3 rightArmLocal = PistolBaseOffset + new Vector3(0.02f, -0.28f, 0.18f) + bobOffset;
        Vector3 rightArmWorldPos = camera.Position
            + camera.Right * rightArmLocal.X
            + camera.Up * rightArmLocal.Y
            + camera.Forward * (-rightArmLocal.Z);
        Matrix rightArmWorld = camBasis * Matrix.CreateTranslation(rightArmWorldPos);
        _armMesh.Draw(device, rightArmWorld, view, proj);

        // Muzzle flash at barrel tip.
        Vector3 muzzleLocal = PistolBaseOffset + bobOffset + new Vector3(0, 0.02f, 0.42f);
        Vector3 muzzleWorldPos = camera.Position
            + camera.Right * muzzleLocal.X
            + camera.Up * muzzleLocal.Y
            + camera.Forward * (-muzzleLocal.Z);
        Matrix muzzleWorld = camBasis * Matrix.CreateTranslation(muzzleWorldPos);
        _muzzleFlash.Draw(device, muzzleWorld, view, proj);
    }
}
