using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FpsRange.Core;

namespace FpsRange.UI;

/// <summary>
/// A simple centered pause/settings menu: Quit, a Fullscreen toggle, and an FPS
/// cap slider (30-360, snapped to steps of 10). Pure immediate-mode style - layout
/// is recomputed each frame from the current viewport, hit-tested against the mouse.
/// </summary>
public class SettingsMenu
{
    private const int FpsMin = 30;
    private const int FpsMax = 360;
    private const int FpsStep = 10;

    public event Action OnQuitRequested;
    public event Action OnFullscreenToggled;
    public event Action<int> OnFpsChanged;
    public event Action OnMainMenuRequested;

    private Texture2D _pixel;
    private SpriteFont _font;

    private Rectangle _panelRect;
    private Rectangle _quitRect;
    private Rectangle _mainMenuRect;
    private Rectangle _fullscreenRect;
    private Rectangle _sliderTrackRect;
    private Rectangle _sliderHandleRect;

    private bool _draggingSlider;
    private bool _hoverQuit, _hoverFullscreen, _hoverMainMenu;

    public void Load(GraphicsDevice device, SpriteFont font)
    {
        _font = font;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    private void ComputeLayout(Viewport vp, int currentFps)
    {
        int panelW = 440, panelH = 360;
        int cx = vp.Width / 2, cy = vp.Height / 2;
        _panelRect = new Rectangle(cx - panelW / 2, cy - panelH / 2, panelW, panelH);

        int pad = 24;
        int contentW = panelW - pad * 2;

        _fullscreenRect = new Rectangle(_panelRect.X + pad, _panelRect.Y + 96, contentW, 44);
        _sliderTrackRect = new Rectangle(_panelRect.X + pad, _panelRect.Y + 190, contentW, 10);
        _mainMenuRect = new Rectangle(_panelRect.X + pad, _panelRect.Y + panelH - pad - 44 - 54, contentW, 44);
        _quitRect = new Rectangle(_panelRect.X + pad, _panelRect.Y + panelH - pad - 44, contentW, 44);

        float t = (float)(currentFps - FpsMin) / (FpsMax - FpsMin);
        int handleX = _sliderTrackRect.X + (int)(t * _sliderTrackRect.Width) - 6;
        _sliderHandleRect = new Rectangle(handleX, _sliderTrackRect.Y - 7, 12, 24);
    }

    /// <summary>Call every frame while the menu is open, before Draw.</summary>
    public void Update(InputManager input, GraphicsDevice device, int currentFps)
    {
        ComputeLayout(device.Viewport, currentFps);

        Point mouse = input.MousePosition;
        _hoverQuit = _quitRect.Contains(mouse);
        _hoverFullscreen = _fullscreenRect.Contains(mouse);
        _hoverMainMenu = _mainMenuRect.Contains(mouse);

        if (input.IsLeftMousePressed)
        {
            if (_hoverQuit) OnQuitRequested?.Invoke();
            else if (_hoverMainMenu) OnMainMenuRequested?.Invoke();
            else if (_hoverFullscreen) OnFullscreenToggled?.Invoke();
            else if (_sliderTrackRect.Contains(mouse) || _sliderHandleRect.Contains(mouse))
                _draggingSlider = true;
        }

        if (!input.IsLeftMouseDown)
        {
            _draggingSlider = false;
        }
        else if (_draggingSlider)
        {
            float t = MathHelper.Clamp((mouse.X - _sliderTrackRect.X) / (float)_sliderTrackRect.Width, 0f, 1f);
            int raw = FpsMin + (int)MathF.Round(t * (FpsMax - FpsMin));
            int snapped = MathHelper.Clamp((int)MathF.Round(raw / (float)FpsStep) * FpsStep, FpsMin, FpsMax);
            if (snapped != currentFps)
                OnFpsChanged?.Invoke(snapped);
        }
    }

    public void Draw(SpriteBatch sb, GraphicsDevice device, bool isFullscreen, int currentFps)
    {
        ComputeLayout(device.Viewport, currentFps);

        sb.Begin(blendState: BlendState.AlphaBlend);

        // Dim the whole screen behind the panel.
        sb.Draw(_pixel, new Rectangle(0, 0, device.Viewport.Width, device.Viewport.Height), new Color(0, 0, 0, 150));

        // Panel background.
        sb.Draw(_pixel, _panelRect, new Color(30, 32, 38, 235));
        DrawBorder(sb, _panelRect, new Color(90, 95, 105), 2);

        if (_font != null)
        {
            DrawCentered(sb, "PAUSED", _panelRect.X + _panelRect.Width / 2, _panelRect.Y + 20, Color.White);

            // Fullscreen toggle button.
            DrawButton(sb, _fullscreenRect, $"Fullscreen: {(isFullscreen ? "ON" : "OFF")}", _hoverFullscreen);

            // FPS slider.
            string fpsLabel = $"Target FPS: {currentFps}";
            DrawCentered(sb, fpsLabel, _panelRect.X + _panelRect.Width / 2, _sliderTrackRect.Y - 34, Color.White);

            sb.Draw(_pixel, _sliderTrackRect, new Color(70, 74, 82));
            float t = (currentFps - FpsMin) / (float)(FpsMax - FpsMin);
            var filled = new Rectangle(_sliderTrackRect.X, _sliderTrackRect.Y, (int)(t * _sliderTrackRect.Width), _sliderTrackRect.Height);
            sb.Draw(_pixel, filled, new Color(120, 170, 230));
            sb.Draw(_pixel, _sliderHandleRect, Color.White);

            DrawCentered(sb, $"{FpsMin}", _sliderTrackRect.X + 12, _sliderTrackRect.Y + 16, new Color(160, 160, 165));
            DrawCentered(sb, $"{FpsMax}", _sliderTrackRect.X + _sliderTrackRect.Width - 12, _sliderTrackRect.Y + 16, new Color(160, 160, 165));

            // Main Menu + Quit buttons.
            DrawButton(sb, _mainMenuRect, "Main Menu", _hoverMainMenu);
            DrawButton(sb, _quitRect, "Quit Game", _hoverQuit, new Color(140, 45, 45));

            DrawCentered(sb, "Press ESC to resume", _panelRect.X + _panelRect.Width / 2, _panelRect.Y + _panelRect.Height - 24, new Color(150, 150, 155));
        }

        sb.End();
    }

    private void DrawButton(SpriteBatch sb, Rectangle rect, string label, bool hover, Color? baseColor = null)
    {
        Color bg = baseColor ?? new Color(55, 60, 70);
        if (hover) bg = Color.Lerp(bg, Color.White, 0.18f);
        sb.Draw(_pixel, rect, bg);
        DrawBorder(sb, rect, new Color(100, 104, 112), 1);
        DrawCentered(sb, label, rect.X + rect.Width / 2, rect.Y + rect.Height / 2 - 9, Color.White);
    }

    private void DrawBorder(SpriteBatch sb, Rectangle r, Color color, int thickness)
    {
        sb.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, thickness), color);
        sb.Draw(_pixel, new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), color);
        sb.Draw(_pixel, new Rectangle(r.X, r.Y, thickness, r.Height), color);
        sb.Draw(_pixel, new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), color);
    }

    private void DrawCentered(SpriteBatch sb, string text, int centerX, int y, Color color)
    {
        Vector2 size = _font.MeasureString(text);
        sb.DrawString(_font, text, new Vector2(centerX - size.X / 2f, y), color);
    }
}
