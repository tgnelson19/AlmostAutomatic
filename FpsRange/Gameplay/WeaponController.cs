using Microsoft.Xna.Framework;
using FpsRange.Core;
using FpsRange.Rendering;

namespace FpsRange.Gameplay;

/// <summary>
/// Semi-automatic hitscan pistol: fires on left-click as long as the 0.1s cooldown has
/// elapsed, regardless of frame rate. No ammo, no reload.
/// Pull-model: TryFire only handles cooldown/edge-detect/recoil/muzzle-flash and hands back
/// the fire ray - each game mode resolves what the ray actually hits (Aim Testing's Targets,
/// NPC Battle's NPCs), so the weapon itself isn't coupled to either.
/// </summary>
public class WeaponController
{
    public const float FireCooldown = 0.1f;

    private float _cooldownRemaining;

    public bool TryFire(GameTime gameTime, InputManager input, Camera camera, ViewModel viewModel, out Ray ray)
    {
        if (_cooldownRemaining > 0f)
            _cooldownRemaining -= (float)gameTime.ElapsedGameTime.TotalSeconds;

        // True semi-auto: IsLeftMousePressed only fires on the rising edge of the click, so
        // holding the button down never re-fires - the button must be released and pressed
        // again for the next shot, independent of the 0.1s cooldown gate below.
        if (input.IsLeftMousePressed && _cooldownRemaining <= 0f)
        {
            viewModel.TriggerFire();
            viewModel.AddRecoil();
            _cooldownRemaining = FireCooldown;

            ray = new Ray(camera.Position, camera.Forward);
            return true;
        }

        ray = default;
        return false;
    }
}
