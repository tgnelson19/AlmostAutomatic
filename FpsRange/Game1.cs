using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FpsRange.Core;
using FpsRange.Rendering;
using FpsRange.Gameplay;
using FpsRange.UI;

namespace FpsRange;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    // Mode-agnostic (shared by every screen/mode).
    private Camera _camera;
    private PlayerController _player;
    private InputManager _input;
    private Skybox _skybox;
    private ViewModel _viewModel;
    private WeaponController _weapon;
    private SettingsMenu _settingsMenu;

    // Screens/modes.
    private AppState _appState = AppState.MainMenu;
    private MainMenu _mainMenu;
    private AimTestingMode _aimTesting;
    private NpcBattleMode _npcBattle;
    private MultiplayerMenu _multiplayerMenu;
    private MultiplayerMode _multiplayer;

    private double _totalTime;
    private bool _paused;
    private int _targetFps = 60;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            // VSync would otherwise clamp the frame rate to the monitor's refresh, defeating
            // the FPS slider above that value - the slider is the sole frame-rate control instead.
            SynchronizeWithVerticalRetrace = false,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        Window.AllowUserResizing = true;

        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / _targetFps);
    }

    protected override void Initialize()
    {
        _camera = new Camera { AspectRatio = GraphicsDevice.Viewport.AspectRatio };
        _player = new PlayerController(_camera, Vector3.Zero);
        _input = new InputManager(Window);

        _weapon = new WeaponController();
        _settingsMenu = new SettingsMenu();
        _mainMenu = new MainMenu();
        _aimTesting = new AimTestingMode();
        _npcBattle = new NpcBattleMode();
        _multiplayerMenu = new MultiplayerMenu();
        _multiplayer = new MultiplayerMode();

        _mainMenu.OnAimTestingSelected += () => EnterMode(AppState.AimTesting);
        _mainMenu.OnNpcBattleSelected += () => EnterMode(AppState.NpcBattle);
        _mainMenu.OnMultiplayerSelected += () =>
        {
            _multiplayerMenu.ResetToRoleChoice();
            _appState = AppState.MultiplayerMenu;
            _input.CaptureMouse = false;
            IsMouseVisible = true;
        };
        _npcBattle.OnPlayerDied += () => EnterMode(AppState.MainMenu);

        _multiplayerMenu.OnBackToMainMenu += () => EnterMode(AppState.MainMenu);
        _multiplayerMenu.OnHostRequested += port =>
        {
            _multiplayer.HostGame(port);
            EnterMode(AppState.Multiplayer);
        };
        _multiplayerMenu.OnJoinRequested += (ip, port) =>
        {
            _multiplayer.JoinGame(ip, port);
            EnterMode(AppState.Multiplayer);
        };

        _settingsMenu.OnQuitRequested += Exit;
        _settingsMenu.OnMainMenuRequested += () => EnterMode(AppState.MainMenu);
        _settingsMenu.OnFullscreenToggled += () =>
        {
            _graphics.IsFullScreen = !_graphics.IsFullScreen;
            _graphics.ApplyChanges();
        };
        _settingsMenu.OnFpsChanged += fps =>
        {
            _targetFps = fps;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / _targetFps);
        };

        // The game starts on the main menu, which needs a free (uncaptured) cursor to click
        // buttons - EnterMode() normally handles this on every mode switch, but the initial
        // AppState.MainMenu is set as a field default and never goes through EnterMode, so it
        // has to be set here too or the cursor stays locked/re-centered for FPS look instead.
        Mouse.SetPosition(Window.ClientBounds.Width / 2, Window.ClientBounds.Height / 2);
        _input.CaptureMouse = false;
        IsMouseVisible = true;

        // Disable back-face culling globally: several hand-built meshes' winding isn't guaranteed
        // consistent, and this project favors correctness/simplicity over perf at this scale.
        _rasterizerState = new RasterizerState { CullMode = CullMode.None };

        base.Initialize();
    }

    private RasterizerState _rasterizerState;

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _skybox = new Skybox(GraphicsDevice);

        _viewModel = new ViewModel();
        _viewModel.Load(GraphicsDevice);

        Target.LoadShared(GraphicsDevice);

        SpriteFont font = null;
        try { font = Content.Load<SpriteFont>("HudFont"); } catch { /* font optional if content build unavailable */ }

        _mainMenu.Load(GraphicsDevice, font);
        _aimTesting.Load(GraphicsDevice, font);
        _npcBattle.Load(GraphicsDevice, font);
        _multiplayerMenu.Load(GraphicsDevice, Window, font);
        _multiplayer.Load(GraphicsDevice, font);
        _settingsMenu.Load(GraphicsDevice, font);
    }

    /// <summary>Centralizes everything needed to switch top-level screens: unpausing, mouse
    /// capture/visibility, repositioning the player, swapping movement-collision bounds, and
    /// resetting whichever mode is being entered.</summary>
    private void EnterMode(AppState newState)
    {
        // Leaving Multiplayer (to anywhere else) must tear down its networking session, or the
        // socket/port stays bound and peers are left dangling instead of cleanly disconnected.
        if (_appState == AppState.Multiplayer && newState != AppState.Multiplayer)
            _multiplayer.Shutdown();

        _appState = newState;
        _paused = false;

        bool isMenu = newState == AppState.MainMenu || newState == AppState.MultiplayerMenu;
        _input.CaptureMouse = !isMenu;
        IsMouseVisible = isMenu;
        if (!isMenu) _input.ResetMouseCapture();

        switch (newState)
        {
            case AppState.AimTesting:
                _player.Bounds = new FlatCircleBounds(PlayerController.PlatformRadius);
                _player.Teleport(AimTestingMode.SpawnPoint);
                break;
            case AppState.NpcBattle:
                _player.Bounds = new MapCollisionBounds(_npcBattle.Map.Collision, NpcBattleMap.HalfX, NpcBattleMap.HalfZ);
                _player.Teleport(NpcBattleMode.SpawnPoint);
                _npcBattle.ResetRun();
                break;
            case AppState.Multiplayer:
                _player.Bounds = new MapCollisionBounds(_multiplayer.Map.Collision, NpcBattleMap.HalfX, NpcBattleMap.HalfZ);
                _player.Teleport(NpcBattleMap.PlayerSpawn);
                _multiplayer.ResetRun();
                break;
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            Exit();

        // Network traffic must keep flowing even when this window isn't the focused one (e.g.
        // testing host+join with two instances side by side, where only one can be focused at a
        // time) - otherwise a backgrounded host stops answering connection requests and a join
        // hangs at "Connecting..." forever. Poll unconditionally, before the IsActive early-out.
        if (_appState == AppState.Multiplayer)
            _multiplayer.PollNetworkOnly();

        if (!IsActive)
        {
            base.Update(gameTime);
            return;
        }

        _totalTime = gameTime.TotalGameTime.TotalSeconds;

        _input.Update();
        _camera.AspectRatio = GraphicsDevice.Viewport.AspectRatio;

        if (_appState == AppState.MainMenu)
        {
            _mainMenu.Update(_input, GraphicsDevice);
            base.Update(gameTime);
            return;
        }

        if (_appState == AppState.MultiplayerMenu)
        {
            if (_input.IsKeyPressed(Keys.Escape)) EnterMode(AppState.MainMenu);
            else _multiplayerMenu.Update(_input, GraphicsDevice);
            base.Update(gameTime);
            return;
        }

        if (_input.IsKeyPressed(Keys.Escape))
        {
            _paused = !_paused;
            _input.CaptureMouse = !_paused;
            IsMouseVisible = _paused;
            if (!_paused) _input.ResetMouseCapture();
        }

        if (_paused)
        {
            _settingsMenu.Update(_input, GraphicsDevice, _targetFps);
            base.Update(gameTime);
            return;
        }

        // While dead in multiplayer (HP at 0, waiting on the respawn timer) the player is frozen in
        // place instead of being able to keep walking/looking/shooting around as a "ghost" - see
        // MultiplayerMode.IsLocalPlayerDead.
        bool multiplayerDead = _appState == AppState.Multiplayer && _multiplayer.IsLocalPlayerDead;
        if (!multiplayerDead) _player.Update(gameTime, _input);

        bool isMoving = !multiplayerDead && new Vector2(_player.Velocity.X, _player.Velocity.Z).LengthSquared() > 0.01f;
        _viewModel.Update(gameTime, isMoving);

        bool fired = _weapon.TryFire(gameTime, _input, _camera, _viewModel, out Ray fireRay) && !multiplayerDead;

        if (_appState == AppState.AimTesting)
            _aimTesting.Update(gameTime, _player.Camera.Position, fired ? fireRay : null, _totalTime);
        else if (_appState == AppState.NpcBattle)
            _npcBattle.Update(gameTime, _player, fired ? fireRay : null, _totalTime);
        else if (_appState == AppState.Multiplayer)
            _multiplayer.Update(gameTime, _player, fired ? fireRay : null, _totalTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        if (_appState == AppState.MainMenu)
        {
            _mainMenu.Draw(_spriteBatch, GraphicsDevice, _npcBattle.BestKills);
            base.Draw(gameTime);
            return;
        }

        if (_appState == AppState.MultiplayerMenu)
        {
            _multiplayerMenu.Draw(_spriteBatch, GraphicsDevice);
            base.Draw(gameTime);
            return;
        }

        GraphicsDevice.RasterizerState = _rasterizerState;
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;

        Matrix view = _camera.ViewMatrix;
        Matrix proj = _camera.ProjectionMatrix;

        _skybox.Draw(GraphicsDevice, _camera.Position, view, proj);

        if (_appState == AppState.AimTesting)
            _aimTesting.Draw(GraphicsDevice, view, proj);
        else if (_appState == AppState.NpcBattle)
            _npcBattle.Draw(GraphicsDevice, view, proj);
        else if (_appState == AppState.Multiplayer)
            _multiplayer.Draw(GraphicsDevice, view, proj);

        // Viewmodel drawn last, own depth pass, so it never clips into world geometry.
        _viewModel.Draw(GraphicsDevice, _camera);

        if (_appState == AppState.AimTesting)
            _aimTesting.DrawHud(_spriteBatch, GraphicsDevice);
        else if (_appState == AppState.NpcBattle)
            _npcBattle.DrawHud(_spriteBatch, GraphicsDevice, _camera, view, proj);
        else if (_appState == AppState.Multiplayer)
            _multiplayer.DrawHud(_spriteBatch, GraphicsDevice, view, proj);

        if (_paused)
            _settingsMenu.Draw(_spriteBatch, GraphicsDevice, _graphics.IsFullScreen, _targetFps);

        base.Draw(gameTime);
    }
}
