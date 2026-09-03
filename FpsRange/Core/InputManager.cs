using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace FpsRange.Core;

/// <summary>
/// Wraps keyboard/mouse polling with previous-frame state so edge-detected
/// actions (jump press, fire click) are trivial for callers.
/// </summary>
public class InputManager
{
    private KeyboardState _prevKeyboard;
    private KeyboardState _curKeyboard;
    private MouseState _prevMouse;
    private MouseState _curMouse;
    private readonly GameWindow _window;
    private Point _windowCenter;

    public Point MouseDelta { get; private set; }
    public Point MousePosition { get; private set; }

    /// <summary>
    /// When true (normal gameplay), the mouse is re-centered every frame to drive look
    /// rotation via delta. When false (a menu is open), the cursor is left free to move
    /// so UI code can hit-test MousePosition against on-screen controls.
    /// </summary>
    public bool CaptureMouse { get; set; } = true;

    public InputManager(GameWindow window)
    {
        _window = window;
        _curKeyboard = _prevKeyboard = Keyboard.GetState();
        _curMouse = _prevMouse = Mouse.GetState();
    }

    public void Update()
    {
        _windowCenter = new Point(_window.ClientBounds.Width / 2, _window.ClientBounds.Height / 2);

        _prevKeyboard = _curKeyboard;
        _curKeyboard = Keyboard.GetState();

        _prevMouse = _curMouse;
        _curMouse = Mouse.GetState();

        MousePosition = new Point(_curMouse.X, _curMouse.Y);

        if (CaptureMouse)
        {
            MouseDelta = new Point(_curMouse.X - _windowCenter.X, _curMouse.Y - _windowCenter.Y);
            // Re-center the mouse each frame to allow unlimited look rotation.
            Mouse.SetPosition(_windowCenter.X, _windowCenter.Y);
        }
        else
        {
            MouseDelta = Point.Zero;
        }
    }

    /// <summary>
    /// Snaps the cursor back to the window center and clears stale prev/cur state so that
    /// re-enabling CaptureMouse (e.g. closing a menu) doesn't produce one huge look-delta
    /// spike from wherever the cursor happened to be over the UI.
    /// </summary>
    public void ResetMouseCapture()
    {
        _windowCenter = new Point(_window.ClientBounds.Width / 2, _window.ClientBounds.Height / 2);
        Mouse.SetPosition(_windowCenter.X, _windowCenter.Y);
        _curMouse = _prevMouse = Mouse.GetState();
        MouseDelta = Point.Zero;
    }

    public bool IsKeyDown(Keys key) => _curKeyboard.IsKeyDown(key);
    public bool IsKeyPressed(Keys key) => _curKeyboard.IsKeyDown(key) && !_prevKeyboard.IsKeyDown(key);

    public bool IsLeftMouseDown => _curMouse.LeftButton == ButtonState.Pressed;
    public bool IsLeftMousePressed => _curMouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
}
