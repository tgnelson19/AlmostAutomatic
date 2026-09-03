using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FpsRange.Gameplay;

namespace FpsRange.UI;

/// <summary>
/// NPC Battle's HUD: kill count (with gold "Best"), a player HP bar, crosshair, and per-NPC
/// green health bars drawn above their heads via screen-projection - the same 2D-overlay idiom
/// used everywhere else (HudRenderer, SettingsMenu) rather than a 3D billboard quad.
/// </summary>
public class NpcBattleHud
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

    public void Draw(SpriteBatch sb, GraphicsDevice device, int kills, int bestKills, PlayerHealth health)
    {
        int screenW = device.Viewport.Width;
        int screenH = device.Viewport.Height;

        sb.Begin();

        int cx = screenW / 2, cy = screenH / 2;
        sb.Draw(_pixel, new Rectangle(cx - 8, cy - 1, 16, 2), Color.White);
        sb.Draw(_pixel, new Rectangle(cx - 1, cy - 8, 2, 16), Color.White);

        // HP bar, bottom-center.
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
            string hpLabel = $"{(int)MathF.Ceiling(health.Hp)} / {(int)PlayerHealth.MaxHp}";
            Vector2 hpLabelSize = _font.MeasureString(hpLabel);
            sb.DrawString(_font, hpLabel, new Vector2(cx - hpLabelSize.X / 2f, barY + barH / 2f - hpLabelSize.Y / 2f), Color.White);

            string leftPart = $"Kills: {kills}   ";
            string rightPart = $"Best: {bestKills}";
            Vector2 leftSize = _font.MeasureString(leftPart);
            Vector2 rightSize = _font.MeasureString(rightPart);
            float startTextX = cx - (leftSize.X + rightSize.X) / 2f;
            float textY = barY - 32;
            sb.DrawString(_font, leftPart, new Vector2(startTextX, textY), Color.White);
            sb.DrawString(_font, rightPart, new Vector2(startTextX + leftSize.X, textY), Gold);
        }

        sb.End();
    }

    /// <summary>Draws a green health bar above an NPC's head, skipped if it projects behind the camera.</summary>
    public void DrawNpcHealthBar(SpriteBatch sb, GraphicsDevice device, Matrix view, Matrix proj, Vector3 worldPos, float hpFraction)
    {
        Viewport vp = device.Viewport;
        Vector3 screenPos = vp.Project(worldPos, proj, view, Matrix.Identity);
        if (screenPos.Z >= 1f || screenPos.Z <= 0f) return; // behind camera or beyond far plane

        const int barW = 46, barH = 6;
        int x = (int)screenPos.X - barW / 2;
        int y = (int)screenPos.Y;

        sb.Begin();
        sb.Draw(_pixel, new Rectangle(x, y, barW, barH), new Color(40, 40, 40, 200));
        sb.Draw(_pixel, new Rectangle(x, y, (int)(barW * MathHelper.Clamp(hpFraction, 0f, 1f)), barH), new Color(60, 200, 70));
        sb.End();
    }
}
