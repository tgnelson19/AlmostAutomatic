using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using FpsRange.Gameplay;

namespace FpsRange.UI;

/// <summary>
/// Draws the bottom-center HUD: current score for the last 1s/10s/60s windows,
/// with each window's all-time-high rolling sum shown alongside in gold.
/// </summary>
public class HudRenderer
{
    private static readonly Color Gold = new Color(255, 215, 0);

    private SpriteFont _font;
    private Texture2D _crosshair;
    private Texture2D _pixel;

    public void Load(GraphicsDevice device, ContentManager content = null, SpriteFont font = null)
    {
        _font = font;

        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _crosshair = _pixel;
    }

    public void Draw(SpriteBatch spriteBatch, GraphicsDevice device, ScoreTracker score)
    {
        int screenW = device.Viewport.Width;
        int screenH = device.Viewport.Height;

        spriteBatch.Begin();

        // Crosshair (small cross of pixels) at screen center.
        int cx = screenW / 2, cy = screenH / 2;
        spriteBatch.Draw(_pixel, new Rectangle(cx - 8, cy - 1, 16, 2), Color.White);
        spriteBatch.Draw(_pixel, new Rectangle(cx - 1, cy - 8, 2, 16), Color.White);

        if (_font != null)
        {
            string line1 = $"1s: {score.Last1s}   Best: {score.Best1s}";
            string line2 = $"10s: {score.Last10s}   Best: {score.Best10s}";
            string line3 = $"60s: {score.Last60s}   Best: {score.Best60s}";

            DrawCenteredLine(spriteBatch, line1, screenW, screenH - 84, score.Last1s, score.Best1s, "1s");
            DrawCenteredLine(spriteBatch, line2, screenW, screenH - 58, score.Last10s, score.Best10s, "10s");
            DrawCenteredLine(spriteBatch, line3, screenW, screenH - 32, score.Last60s, score.Best60s, "60s");
        }

        spriteBatch.End();
    }

    private void DrawCenteredLine(SpriteBatch sb, string fullLine, int screenW, int y, int current, int best, string label)
    {
        // Compose with two colors: white for current, gold for best. Measure/draw in two segments.
        string leftPart = $"{label}: {current}   ";
        string rightPart = $"Best: {best}";

        Vector2 leftSize = _font.MeasureString(leftPart);
        Vector2 rightSize = _font.MeasureString(rightPart);
        float totalWidth = leftSize.X + rightSize.X;
        float startX = screenW / 2f - totalWidth / 2f;

        sb.DrawString(_font, leftPart, new Vector2(startX, y), Color.White);
        sb.DrawString(_font, rightPart, new Vector2(startX + leftSize.X, y), Gold);
    }
}
