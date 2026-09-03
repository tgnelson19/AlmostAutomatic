using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FpsRange.Core;

namespace FpsRange.UI;

/// <summary>
/// A reusable focusable single-line text box - click-to-focus follows the same
/// Rectangle+IsLeftMousePressed hit-test pattern used everywhere else (SettingsMenu, MainMenu),
/// but text entry itself is new: MonoGame's GameWindow.TextInput event is the standard way to
/// get printable characters (KeyboardState alone doesn't give you shift/layout-aware chars),
/// so this subscribes to it while loaded and filters to only apply typed characters while focused.
/// </summary>
public class TextInputField
{
    public string Text = "";
    public int MaxLength = 40;
    public bool NumericOnly;

    private bool _focused;
    private GameWindow _window;
    private Texture2D _pixel;
    private SpriteFont _font;
    private string _placeholder;
    private float _cursorBlink;

    public bool Focused => _focused;

    public void Load(GraphicsDevice device, GameWindow window, SpriteFont font, string placeholder = "")
    {
        _window = window;
        _font = font;
        _placeholder = placeholder;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _window.TextInput += OnTextInput;
    }

    /// <summary>Must be called when this field's owning screen is torn down, or the TextInput
    /// subscription leaks and keeps consuming keystrokes after the field is gone.</summary>
    public void Unload()
    {
        if (_window != null) _window.TextInput -= OnTextInput;
    }

    private void OnTextInput(object sender, TextInputEventArgs e)
    {
        if (!_focused) return;
        char c = e.Character;

        if (c == '\b')
        {
            if (Text.Length > 0) Text = Text[..^1];
            return;
        }
        if (char.IsControl(c)) return; // ignore enter/tab/escape/etc. here
        if (NumericOnly && !char.IsDigit(c)) return;
        if (Text.Length < MaxLength) Text += c;
    }

    /// <summary>Call every frame while this field is on screen, before Draw. Clicking inside
    /// `rect` focuses it; clicking anywhere else unfocuses it.</summary>
    public void Update(InputManager input, Rectangle rect)
    {
        _cursorBlink += 1f / 60f;
        if (input.IsLeftMousePressed)
            _focused = rect.Contains(input.MousePosition);
    }

    public void Draw(SpriteBatch sb, Texture2D pixelOverride, Rectangle rect)
    {
        var pixel = pixelOverride ?? _pixel;
        sb.Draw(pixel, rect, _focused ? new Color(52, 56, 66) : new Color(38, 40, 46));
        DrawBorder(sb, pixel, rect, _focused ? new Color(140, 170, 220) : new Color(90, 94, 102), 2);

        if (_font == null) return;

        bool showPlaceholder = Text.Length == 0 && !_focused;
        string display = showPlaceholder ? _placeholder : Text;
        Color textColor = showPlaceholder ? new Color(120, 120, 128) : Color.White;
        Vector2 textSize = _font.MeasureString(display.Length > 0 ? display : "A");
        var textPos = new Vector2(rect.X + 10, rect.Y + rect.Height / 2f - textSize.Y / 2f);
        sb.DrawString(_font, display, textPos, textColor);

        if (_focused && ((int)(_cursorBlink * 2)) % 2 == 0)
        {
            float w = _font.MeasureString(Text).X;
            sb.Draw(pixel, new Rectangle((int)(rect.X + 10 + w), rect.Y + 6, 2, rect.Height - 12), Color.White);
        }
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle r, Color color, int thickness)
    {
        sb.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(r.X, r.Y, thickness, r.Height), color);
        sb.Draw(pixel, new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), color);
    }
}
