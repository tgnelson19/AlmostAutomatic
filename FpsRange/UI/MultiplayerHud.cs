using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FpsRange.Gameplay;

namespace FpsRange.UI;

/// <summary>
/// Multiplayer's HUD: local HP bar, connection status/shareable host address, player count,
/// crosshair, and per-remote-player health bars/nametags - same 2D-overlay idioms as
/// NpcBattleHud (a dedicated small class rather than extending that one, since the fields
/// involved - hosting address, player count - are unrelated to NPC Battle's kill counter).
/// </summary>
public class MultiplayerHud
{
    private static readonly Color Gold = new Color(255, 215, 0);

    private Texture2D _pixel;
    private SpriteFont _font;

    public void Load(GraphicsDevice device, SpriteFont font)
    {
        _font = font;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Draw(SpriteBatch sb, GraphicsDevice device, PlayerHealth health, string statusText, int playerCount, bool isHost, string hostAddressLabel)
    {
        int screenW = device.Viewport.Width;
        int screenH = device.Viewport.Height;

        sb.Begin();

        int cx = screenW / 2, cy = screenH / 2;
        sb.Draw(_pixel, new Rectangle(cx - 8, cy - 1, 16, 2), Color.White);
        sb.Draw(_pixel, new Rectangle(cx - 1, cy - 8, 2, 16), Color.White);

        const int barW = 260, barH = 26;
        int barX = cx - barW / 2;
        int barY = screenH - 50;
        float hpFrac = MathHelper.Clamp(health.Hp / PlayerHealth.MaxHp, 0f, 1f);

        sb.Draw(_pixel, new Rectangle(barX - 2, barY - 2, barW + 4, barH + 4), new Color(20, 20, 22));
        sb.Draw(_pixel, new Rectangle(barX, barY, barW, barH), new Color(55, 20, 20));
        Color fillColor = hpFrac > 0.5f ? new Color(70, 195, 80) : hpFrac > 0.25f ? new Color(220, 190, 50) : new Color(215, 60, 55);
        sb.Draw(_pixel, new Rectangle(barX, barY, (int)(barW * hpFrac), barH), fillColor);

        if (_font != null)
        {
            string hpLabel = $"{(int)System.MathF.Ceiling(health.Hp)} / {(int)PlayerHealth.MaxHp}";
            Vector2 hpLabelSize = _font.MeasureString(hpLabel);
            sb.DrawString(_font, hpLabel, new Vector2(cx - hpLabelSize.X / 2f, barY + barH / 2f - hpLabelSize.Y / 2f), Color.White);

            string playersLine = $"Players: {playerCount}";
            Vector2 playersSize = _font.MeasureString(playersLine);
            sb.DrawString(_font, playersLine, new Vector2(cx - playersSize.X / 2f, barY - 32), Color.White);

            // Top-left: connection status / shareable host address.
            sb.DrawString(_font, statusText, new Vector2(16, 16), Color.White);
            if (isHost && !string.IsNullOrEmpty(hostAddressLabel))
                sb.DrawString(_font, $"Share this address: {hostAddressLabel}", new Vector2(16, 42), Gold);
        }

        sb.End();
    }

    /// <summary>Green health bar + name above a remote player's head, screen-projected - skipped
    /// if the projected point is behind the camera. Mirrors NpcBattleHud.DrawNpcHealthBar.</summary>
    public void DrawPlayerHealthBar(SpriteBatch sb, GraphicsDevice device, Matrix view, Matrix proj, Vector3 worldPos, float hpFraction, string name)
    {
        Viewport vp = device.Viewport;
        Vector3 screenPos = vp.Project(worldPos, proj, view, Matrix.Identity);
        if (screenPos.Z >= 1f || screenPos.Z <= 0f) return;

        const int barW = 60, barH = 6;
        int x = (int)screenPos.X - barW / 2;
        int y = (int)screenPos.Y;

        sb.Begin();
        sb.Draw(_pixel, new Rectangle(x, y, barW, barH), new Color(40, 40, 40, 200));
        sb.Draw(_pixel, new Rectangle(x, y, (int)(barW * MathHelper.Clamp(hpFraction, 0f, 1f)), barH), new Color(60, 200, 70));
        if (_font != null)
        {
            Vector2 nameSize = _font.MeasureString(name);
            sb.DrawString(_font, name, new Vector2(screenPos.X - nameSize.X / 2f, y - nameSize.Y - 2), Color.White);
        }
        sb.End();
    }
}
