// ScummScreen.cs
//

using System;
using NScumm.Core;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;
using NScumm.MonoGame.Services;
using NScumm.Core.Audio;
using NScumm.Core.IO;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using System.Threading;
using System.Runtime;
using System.Diagnostics;

namespace NScumm.MonoGame
{
    public class ScummScreen : GameScreen
    {
        private readonly GameSettings info;
        private SpriteBatch spriteBatch;
        private IEngine engine;
        private XnaGraphicsManager gfx;
        private XnaInputManager inputManager;
        private Vector2 cursorPos;
        private IAudioOutput audioDriver;
        private Game game;
        private bool contentLoaded;
        private SpriteFont font;

        public ScummScreen(Game game, GameSettings info)
        {
            TransitionOnTime = TimeSpan.FromSeconds(1.0);
            TransitionOffTime = TimeSpan.FromSeconds(1.0);

            this.game = game;
            this.info = info;
        }

        public bool engineFaildToStart = false;
        public override void LoadContent()
        {
            if (!contentLoaded)
            {
                contentLoaded = true;
                spriteBatch = new SpriteBatch(ScreenManager.GraphicsDevice);

                font = ScreenManager.Content.Load<SpriteFont>("Fonts/MenuFont");
                inputManager = new XnaInputManager(ScreenManager.Game, info.Game);
                gfx = new XnaGraphicsManager(info.Game.Width, info.Game.Height, 
                    info.Game.PixelFormat, game.Window, ScreenManager.GraphicsDevice);
                
                ScreenManager.Game.Services.AddService<Core.Graphics.IGraphicsManager>(gfx);
                var saveFileManager = ServiceLocator.SaveFileManager;
#if WINDOWS_UWP
                audioDriver = new XAudio2Mixer();
#else
                audioDriver = new XnaAudioDriver();
#endif
                audioDriver.Play();

                //init engines
                for (int i = 0; i < 9; i++)
                {
                    try
                    {
                        engine = info.MetaEngine.Create(info, gfx, inputManager, audioDriver, saveFileManager);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[ex] ScummScreen : " + ex.Message);

                        engineFaildToStart = true;
                        break;
                        //Is it a good idea to test other version if the current faild?
                        /*info.Game.Version = i;
                        if (i == 8)
                        {
                            engineFaildToStart = true;
                        }*/
                    }
                }
                if (!engineFaildToStart)
                {
                    engine.ShowMenuDialogRequested += OnShowMenuDialogRequested;
                    game.Services.AddService(engine);

                    Task.Factory.StartNew(() =>
                    {
                        UpdateGame();
                    });
                }
                else
                {
                    GamePage.ShowTileHandler.Invoke(new string[]
                    { 
                        "Failed State", "Start failed!",
                        $"Failed to start the game", 
                        "Engine cannot start" }, 
                        EventArgs.Empty);
                }
                //callGCTimer(true);
            }
        }

        public override void EndRun()
        {
            engine.HasToQuit = true;
            audioDriver.Stop();
            base.EndRun();
        }

        public override void UnloadContent()
        {
            gfx.Dispose();
            audioDriver.Dispose();
        }

        public override void HandleInput(InputState input)
        {
            if (input.IsNewKeyPress(Keys.Enter) 
                && input.CurrentKeyboardState.IsKeyDown(Keys.LeftControl))
            {
                var gdm = ((ScummGame)game).GraphicsDeviceManager;
                gdm.ToggleFullScreen();
                gdm.ApplyChanges();
            }
            else if (input.IsNewKeyPress(Keys.Space))
            {
                engine.IsPaused = !engine.IsPaused;
            }
            else
            {
                inputManager.UpdateInput(input.CurrentKeyboardState);
                cursorPos = inputManager.RealPosition;
                base.HandleInput(input);
            }
        }

        private void UpdateFrameRate()
        {
#if WINDOWS_UWP
            GamePage.FPSHandler.Invoke(null, EventArgs.Empty);
#endif
        }
        public override void Draw(GameTime gameTime)
        {
            if (engineFaildToStart)
            {
                return;
            }
            spriteBatch.Begin();
            gfx.DrawScreen(spriteBatch);
            gfx.DrawCursor(spriteBatch, cursorPos);
            spriteBatch.End();
            UpdateFrameRate();
        }

        private void UpdateGame()
        {
            engine.Run();
            ScreenManager.Game.Exit();
        }

        private void OnShowMenuDialogRequested(object sender, EventArgs e)
        {
            if (!engine.IsPaused)
            {
                engine.IsPaused = true;
                var page = game.Services.GetService<IMenuService>();
                page.ShowMenu();
            }
        }

        private Timer GCTimer;
        bool NoGCRegionState = false;
        bool ReduceFreezesInProgress = false;
        //GCServices.GCService gcService = new GCServices.GCService();
        private void updateGCCaller()
        {
            ReduceFreezesInProgress = true;

            try
            {
                if (!NoGCRegionState)
                {
                    GC.WaitForPendingFinalizers();
                    //gcService.TryStartNoGCRegionCall();
                    NoGCRegionState = true;
                }
                else
                {
                    //gcService.EndNoGCRegionCall();
                    NoGCRegionState = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ex] ScummScreen : " + ex.Message);

                NoGCRegionState = false;
            }

            ReduceFreezesInProgress = false;
        }
        public async void UpdateGC(object sender, EventArgs e)
        {
            try
            {
                {
                    if (!ReduceFreezesInProgress)
                    {
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            CoreDispatcherPriority.High, async () =>
                        {
                            updateGCCaller();
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ex] ScummScreen : " + ex.Message);
            }
        }
        private void callGCTimer(bool startState = false)
        {
            try
            {
                GCTimer?.Dispose();
                if (startState)
                {
                    GCTimer = new Timer(delegate { UpdateGC(null, EventArgs.Empty); }, null, 0, 1500);
                }
                else
                {
                    if (NoGCRegionState)
                    {
                        NoGCRegionState = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ex] ScummScreen : " + ex.Message);
                NoGCRegionState = false;
            }
        }
    }
}

