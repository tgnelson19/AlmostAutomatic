using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace FpsRange.Core;

/// <summary>
/// Hand-written first-person movement: mouse-look, WASD, gravity + jump, and a flat-plane
/// ground/platform-bounds constraint. No physics engine - this is a simple integrator.
/// </summary>
public class PlayerController
{
    public const float PlayerSize = 1f;          // defines the "player size" unit used elsewhere
    public const float EyeHeight = 1.7f;
    public const float PlatformRadius = 60f;      // half of the original 120f arena

    private const float MoveSpeed = 8f;
    private const float SprintSpeed = MoveSpeed * 1.8f;
    // 8.0 gives a max jump height of v^2/(2*|Gravity|) ~= 1.78 units - comfortably clears a
    // barrel (1.4 tall) with margin, needed for NPC Battle's jumpable tables/barrels. The old
    // 6.5 only reached ~1.17, too low to land on a barrel at all.
    private const float JumpVelocity = 8f;
    private const float Gravity = -18f;
    private const float MouseSensitivity = 0.0028f;

    public Camera Camera { get; }
    public Vector3 Velocity;
    public bool IsGrounded { get; private set; } = true;

    /// <summary>How the player's next position gets constrained to the current map's floor/walls.
    /// Defaults to the original flat-plane+circle behavior so Aim Testing is unaffected;
    /// NPC Battle swaps in MapCollisionBounds on entry.</summary>
    public IMovementBounds Bounds { get; set; }

    // Toggled (not held) by Shift: stays on until Shift is pressed again OR W is released.
    private bool _sprintToggled;

    public PlayerController(Camera camera, Vector3 startPosition)
    {
        Camera = camera;
        Camera.Position = startPosition + new Vector3(0, EyeHeight, 0);
        Bounds = new FlatCircleBounds(PlatformRadius);
    }

    /// <summary>Teleports the player to a new spawn point, resetting velocity/grounded state
    /// (used when switching game modes).</summary>
    public void Teleport(Vector3 groundPosition)
    {
        Camera.Position = groundPosition + new Vector3(0, EyeHeight, 0);
        Velocity = Vector3.Zero;
        IsGrounded = true;
    }

    public void Update(GameTime gameTime, InputManager input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // --- Look ---
        // X is NOT negated: moving the mouse right must increase yaw (turn right), given
        // Camera.Forward's convention where increasing yaw rotates from -Z toward +X.
        Camera.AddYawPitch(input.MouseDelta.X * MouseSensitivity, -input.MouseDelta.Y * MouseSensitivity);

        // --- Move (horizontal, relative to yaw only, ignoring pitch so looking up/down doesn't fly you) ---
        Vector3 flatForward = new Vector3(MathF.Sin(Camera.Yaw), 0, -MathF.Cos(Camera.Yaw));
        // right = cross(forward, up): (-forward.Z, 0, forward.X). The previous (forward.Z, 0, -forward.X)
        // was the left vector, which is why A/D felt swapped.
        Vector3 flatRight = new Vector3(-flatForward.Z, 0, flatForward.X);

        bool wDown = input.IsKeyDown(Keys.W);

        Vector3 moveDir = Vector3.Zero;
        if (wDown) moveDir += flatForward;
        if (input.IsKeyDown(Keys.S)) moveDir -= flatForward;
        if (input.IsKeyDown(Keys.D)) moveDir += flatRight;
        if (input.IsKeyDown(Keys.A)) moveDir -= flatRight;
        if (moveDir != Vector3.Zero) moveDir.Normalize();

        // --- Sprint toggle ---
        // Shift flips the toggle; releasing W (or hitting Shift again) turns it back off.
        if (input.IsKeyPressed(Keys.LeftShift) || input.IsKeyPressed(Keys.RightShift))
            _sprintToggled = !_sprintToggled;
        if (!wDown)
            _sprintToggled = false;

        bool sprinting = _sprintToggled && wDown;
        float speed = sprinting ? SprintSpeed : MoveSpeed;

        Velocity.X = moveDir.X * speed;
        Velocity.Z = moveDir.Z * speed;

        // --- Jump / gravity ---
        if (IsGrounded && input.IsKeyPressed(Keys.Space))
        {
            Velocity.Y = JumpVelocity;
            IsGrounded = false;
        }
        Velocity.Y += Gravity * dt;

        // --- Integrate ---
        Vector3 desiredPos = Camera.Position + Velocity * dt;

        Vector3 pos = Bounds.Constrain(Camera.Position, desiredPos, EyeHeight, out bool grounded);
        IsGrounded = grounded;
        if (grounded) Velocity.Y = 0;

        Camera.Position = pos;
    }
}
