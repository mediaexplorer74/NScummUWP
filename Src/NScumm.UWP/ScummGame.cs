// ScummGame.cs
// This file is part of NScumm.


using Microsoft.Xna.Framework;
using NScumm.Core.IO;
using System;
using System.Diagnostics;

namespace NScumm.MonoGame
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    class ScummGame : Game
    {
        readonly ScreenManager _screenManager;

        public GameSettings Settings { get; private set; }

        public GraphicsDeviceManager GraphicsDeviceManager { get; private set; }

        public ScreenManager ScreenManager { get { return _screenManager; } }

        public ScummGame()
            : this(null)
        {

        }

        public ScummGame(GameSettings settings)
        {
            try
            {
                IsMouseVisible = false;
                IsFixedTimeStep = false;
                Window.AllowUserResizing = true;

                Content.RootDirectory = "Content";

                GraphicsDeviceManager = new GraphicsDeviceManager(this);
#if !WINDOWS_UWP
            Settings = settings;
            GraphicsDeviceManager.PreferredBackBufferWidth 
               = 800;
            GraphicsDeviceManager.PreferredBackBufferHeight 
                = (int)(800.0 * Settings.Game.Height / Settings.Game.Width);
#else
                Settings = new GameSettings(GamePage.Info.Game, GamePage.Info.Engine);
                GraphicsDeviceManager.PreferredBackBufferWidth = Settings.Game.Width;
                GraphicsDeviceManager.PreferredBackBufferHeight = Settings.Game.Height;
#endif
                _screenManager = new ScreenManager(this);
                Components.Add(_screenManager);
            }
            catch(Exception ex)
            {
                Debug.WriteLine("[ex] ScummGame (constructor) : " + ex.Message);
            }
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            try
            {
                Window.Title = string.Format("nSCUMM - {0} [{1}]", Settings.Game.Description, Settings.Game.Culture.NativeName);
                _screenManager.AddScreen(new BackgroundScreen());
                _screenManager.AddScreen(new ScummScreen(this, Settings));

                base.Initialize();
            }
            catch(Exception ex)
            {
                Debug.WriteLine("[ex] ScummGame / Initialize : " + ex.Message);
            }
        }

        protected override void EndRun()
        {
            try 
            {
                _screenManager.EndRun();
                base.EndRun();
            }
            catch(Exception ex)
            {
                Debug.WriteLine("[ex] ScummGame (EndRun) : " + ex.Message);
            }
        }
    }
}
