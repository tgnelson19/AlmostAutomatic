using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FpsRange.Core;
using FpsRange.Rendering;
using FpsRange.UI;

namespace FpsRange.Gameplay;

/// <summary>
/// The original "Aim Testing" mode, extracted unchanged from Game1 into its own container so
/// Game1 can host a second mode (NPC Battle) alongside it. Behavior is identical to before the
/// main-menu/mode-switch refactor.
/// </summary>
public class AimTestingMode
{
    public Platform Platform { get; } = new();
    public TargetSpawner Spawner { get; } = new();
    public ScoreTracker Score { get; } = new();

    private readonly HudRenderer _hud = new();

    public static readonly Vector3 SpawnPoint = Vector3.Zero;

    public void Load(GraphicsDevice device, SpriteFont font)
    {
        Platform.Load(device, PlayerController.PlatformRadius);
        _hud.Load(device, null, font);
    }

    public void Update(GameTime gameTime, Vector3 playerPosition, Ray? fireRay, double totalTime)
    {
        Spawner.Update(gameTime, playerPosition);

        if (fireRay.HasValue)
        {
            var ray = fireRay.Value;
            Target closest = null;
            int closestPoints = 0;
            float closestDist = float.MaxValue;

            foreach (var target in Spawner.Targets)
            {
                if (target.TryHit(ray, out int points, out float dist) && dist < closestDist)
                {
                    closest = target;
                    closestPoints = points;
                    closestDist = dist;
                }
            }

            if (closest != null)
            {
                Score.RegisterHit(totalTime, closestPoints);
                closest.Alive = false;
                Spawner.ReplaceDead(playerPosition);
            }
        }

        Score.Update(totalTime);
    }

    public void Draw(GraphicsDevice device, Matrix view, Matrix proj)
    {
        Platform.Draw(device, view, proj);
        foreach (var target in Spawner.Targets)
            target.Draw(device, view, proj);
    }

    public void DrawHud(SpriteBatch sb, GraphicsDevice device)
    {
        _hud.Draw(sb, device, Score);
    }
}
