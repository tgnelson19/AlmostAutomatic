using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FpsRange.Core;

namespace FpsRange.UI;

/// <summary>
/// The title screen: two mode buttons. Follows SettingsMenu's immediate-mode pattern -
/// layout recomputed from the viewport every call, plain Rectangle hover/click hit-tests.
/// </summary>
public class MainMenu
{
    public event Action OnAimTestingSelected;
    public event Action OnNpcBattleSelected;
    public event Action OnMultiplayerSelected;

    private Texture2D _pixel;
    private SpriteFont _font;

    private Rectangle _aimTestingRect;
    private Rectangle _npcBattleRect;
    private Rectangle _multiplayerRect;
    private bool _hoverAimTesting, _hoverNpcBattle, _hoverMultiplayer;

    public void Load(GraphicsDevice device, SpriteFont font)
    {
        _font = font;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    private void ComputeLayout(Viewport vp)
    {
        int btnW = 360, btnH = 64, gap = 24;
        int cx = vp.Width / 2, cy = vp.Height / 2;
        _aimTestingRect = new Rectangle(cx - btnW / 2, cy - btnH - btnH - gap, btnW, btnH);
        _npcBattleRect = new Rectangle(cx - btnW / 2, cy - btnH / 2, btnW, btnH);
        _multiplayerRect = new Rectangle(cx - btnW / 2, cy + btnH / 2 + gap, btnW, btnH);
    }

    public void Update(InputManager input, GraphicsDevice device)
    {
        ComputeLayout(device.Viewport);

        Point mouse = input.MousePosition;
        _hoverAimTesting = _aimTestingRect.Contains(mouse);
        _hoverNpcBattle = _npcBattleRect.Contains(mouse);
        _hoverMultiplayer = _multiplayerRect.Contains(mouse);

        if (input.IsLeftMousePressed)
        {
            if (_hoverAimTesting) OnAimTestingSelected?.Invoke();
            else if (_hoverNpcBattle) OnNpcBattleSelected?.Invoke();
            else if (_hoverMultiplayer) OnMultiplayerSelected?.Invoke();
        }
    }

    public void Draw(SpriteBatch sb, GraphicsDevice device, int npcBattleBestKills)
    {
        ComputeLayout(device.Viewport);

        sb.Begin(blendState: BlendState.AlphaBlend);

        sb.Draw(_pixel, new Rectangle(0, 0, device.Viewport.Width, device.Viewport.Height), new Color(18, 20, 24));

        if (_font != null)
        {
            DrawCentered(sb, "FPS RANGE", device.Viewport.Width / 2, _aimTestingRect.Y - 100, Color.White, 2f);

            DrawButton(sb, _aimTestingRect, "Aim Testing", _hoverAimTesting);
            DrawButton(sb, _npcBattleRect, "NPC Battle", _hoverNpcBattle);
            DrawButton(sb, _multiplayerRect, "Multiplayer", _hoverMultiplayer);

            string bestLine = $"NPC Battle Best Kills: {npcBattleBestKills}";
            DrawCentered(sb, bestLine, device.Viewport.Width / 2, _multiplayerRect.Bottom + 14, new Color(255, 215, 0));

            DrawCentered(sb, "Click a mode to start", device.Viewport.Width / 2, _multiplayerRect.Bottom + 60, new Color(150, 150, 155));
        }

        sb.End();
    }

    private void DrawButton(SpriteBatch sb, Rectangle rect, string label, bool hover)
    {
        Color bg = new Color(55, 60, 70);
        if (hover) bg = Color.Lerp(bg, Color.White, 0.18f);
        sb.Draw(_pixel, rect, bg);
        DrawBorder(sb, rect, new Color(100, 104, 112), 2);
        DrawCentered(sb, label, rect.X + rect.Width / 2, rect.Y + rect.Height / 2 - 10, Color.White);
    }

    private void DrawBorder(SpriteBatch sb, Rectangle r, Color color, int thickness)
    {
        sb.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, thickness), color);
        sb.Draw(_pixel, new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), color);
        sb.Draw(_pixel, new Rectangle(r.X, r.Y, thickness, r.Height), color);
        sb.Draw(_pixel, new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), color);
    }

    private void DrawCentered(SpriteBatch sb, string text, int centerX, int y, Color color, float scale = 1f)
    {
        Vector2 size = _font.MeasureString(text) * scale;
        sb.DrawString(_font, text, new Vector2(centerX - size.X / 2f, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
