using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FpsRange.Core;
using FpsRange.Networking;

namespace FpsRange.UI;

/// <summary>
/// The multiplayer entry screen: choose Host or Join, following SettingsMenu/MainMenu's
/// immediate-mode button pattern. Join reveals an IP field alongside the shared Port field.
/// Clicking Connect/Host transitions straight into the Multiplayer game state - connection
/// progress ("Connecting...", failure) is then shown in that mode's own HUD rather than a
/// separate waiting-room screen, keeping this a "generic" join flow as asked rather than a
/// full lobby system.
/// </summary>
public class MultiplayerMenu
{
    private enum SubScreen { RoleChoice, Join }

    public event Action<int> OnHostRequested;             // port
    public event Action<string, int> OnJoinRequested;     // ip, port
    public event Action OnBackToMainMenu;

    private SubScreen _screen = SubScreen.RoleChoice;

    private Texture2D _pixel;
    private SpriteFont _font;

    private readonly TextInputField _portField = new();
    private readonly TextInputField _ipField = new();

    private Rectangle _hostRect, _joinRect, _backRect, _connectRect, _portRect, _ipRect;
    private bool _hoverHost, _hoverJoin, _hoverBack, _hoverConnect;

    public void Load(GraphicsDevice device, GameWindow window, SpriteFont font)
    {
        _font = font;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _portField.Load(device, window, font, "Port");
        _portField.Text = NetProtocol.DefaultPort.ToString();
        _portField.NumericOnly = true;
        _portField.MaxLength = 5;

        _ipField.Load(device, window, font, "Host IP (e.g. 192.168.1.42)");
        _ipField.MaxLength = 45;
    }

    public void Unload()
    {
        _portField.Unload();
        _ipField.Unload();
    }

    /// <summary>Reset to the role-choice sub-screen - call when re-entering this menu.</summary>
    public void ResetToRoleChoice() => _screen = SubScreen.RoleChoice;

    private void ComputeLayout(Viewport vp)
    {
        int btnW = 360, btnH = 64, gap = 24;
        int cx = vp.Width / 2, cy = vp.Height / 2;

        if (_screen == SubScreen.RoleChoice)
        {
            _hostRect = new Rectangle(cx - btnW / 2, cy - btnH - gap / 2, btnW, btnH);
            _joinRect = new Rectangle(cx - btnW / 2, cy + gap / 2, btnW, btnH);
            _portRect = new Rectangle(cx - btnW / 2, _joinRect.Bottom + 40, btnW, 48);
            _backRect = new Rectangle(cx - btnW / 2, _portRect.Bottom + 24, btnW, 48);
        }
        else
        {
            _ipRect = new Rectangle(cx - btnW / 2, cy - 90, btnW, 48);
            _portRect = new Rectangle(cx - btnW / 2, _ipRect.Bottom + 16, btnW, 48);
            _connectRect = new Rectangle(cx - btnW / 2, _portRect.Bottom + 30, btnW, btnH);
            _backRect = new Rectangle(cx - btnW / 2, _connectRect.Bottom + 24, btnW, 48);
        }
    }

    public void Update(InputManager input, GraphicsDevice device)
    {
        ComputeLayout(device.Viewport);

        Point mouse = input.MousePosition;
        _hoverBack = _backRect.Contains(mouse);

        if (_screen == SubScreen.RoleChoice)
        {
            _hoverHost = _hostRect.Contains(mouse);
            _hoverJoin = _joinRect.Contains(mouse);
            _portField.Update(input, _portRect);

            if (input.IsLeftMousePressed)
            {
                if (_hoverHost) OnHostRequested?.Invoke(ParsePort());
                else if (_hoverJoin) _screen = SubScreen.Join;
                else if (_hoverBack) OnBackToMainMenu?.Invoke();
            }
        }
        else
        {
            _hoverConnect = _connectRect.Contains(mouse);
            _ipField.Update(input, _ipRect);
            _portField.Update(input, _portRect);

            if (input.IsLeftMousePressed)
            {
                if (_hoverConnect && _ipField.Text.Length > 0) OnJoinRequested?.Invoke(_ipField.Text, ParsePort());
                else if (_hoverBack) _screen = SubScreen.RoleChoice;
            }
        }
    }

    private int ParsePort() => int.TryParse(_portField.Text, out int p) && p > 0 && p <= 65535 ? p : NetProtocol.DefaultPort;

    public void Draw(SpriteBatch sb, GraphicsDevice device)
    {
        ComputeLayout(device.Viewport);

        sb.Begin(blendState: BlendState.AlphaBlend);
        sb.Draw(_pixel, new Rectangle(0, 0, device.Viewport.Width, device.Viewport.Height), new Color(18, 20, 24));

        if (_font != null)
        {
            if (_screen == SubScreen.RoleChoice)
            {
                DrawCentered(sb, "MULTIPLAYER", device.Viewport.Width / 2, _hostRect.Y - 90, Color.White, 2f);
                DrawButton(sb, _hostRect, "Host Game", _hoverHost);
                DrawButton(sb, _joinRect, "Join Game", _hoverJoin);
            }
            else
            {
                DrawCentered(sb, "JOIN GAME", device.Viewport.Width / 2, _ipRect.Y - 60, Color.White, 2f);
                DrawButton(sb, _connectRect, "Connect", _hoverConnect, new Color(50, 110, 70));
            }
            DrawButton(sb, _backRect, "Back", _hoverBack);
        }

        sb.End();

        // Text fields draw after the button pass so their cursor/border sit above the panel.
        sb.Begin(blendState: BlendState.AlphaBlend);
        if (_screen == SubScreen.RoleChoice)
        {
            _portField.Draw(sb, _pixel, _portRect);
        }
        else
        {
            _ipField.Draw(sb, _pixel, _ipRect);
            _portField.Draw(sb, _pixel, _portRect);
        }
        sb.End();
    }

    private void DrawButton(SpriteBatch sb, Rectangle rect, string label, bool hover, Color? baseColor = null)
    {
        Color bg = baseColor ?? new Color(55, 60, 70);
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
