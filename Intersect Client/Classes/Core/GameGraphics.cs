﻿using System;
using System.Collections.Generic;
using Intersect;
using Intersect.GameObjects;
using IntersectClientExtras.File_Management;
using IntersectClientExtras.GenericClasses;
using IntersectClientExtras.Graphics;
using Intersect_Client.Classes.Entities;
using Intersect_Client.Classes.General;
using Intersect_Client.Classes.Maps;
using Intersect_Client.Classes.UI;
using Color = IntersectClientExtras.GenericClasses.Color;

namespace Intersect_Client.Classes.Core
{
    public static class GameGraphics
    {
        //Game Renderer
        public static GameRenderer Renderer;
        public static GameShader DefaultShader;
        private static GameContentManager contentManager;

        //Resolution
        private static int _oldWidth = 0;
        private static int _oldHeight = 0;

        //Screen Values
        public static GameFont GameFont;
        public static FloatRect CurrentView;

        //Darkness Stuff
        public static float _brightnessLevel;
        private static GameRenderTexture _darknessTexture;

        //Overlay Stuff
        public static Color OverlayColor = Color.Transparent;
        private static long _overlayUpdate = 0;

        //Player Spotlight Values
        private static long _lightUpdate = 0;
        private static float _playerLightIntensity = 255;
        private static float _playerLightSize = 0;
        private static float _playerLightExpand = 0f;
        public static ColorF _playerLightColor = ColorF.White;
        private static List<LightBase> _lightQueue = new List<LightBase>();
        private static long _fadeTimer;

        //Grid Switched
        public static bool GridSwitched = false;

        //Rendering Variables
        public static int DrawCalls = 0;
        public static int EntitiesDrawn = 0;
        public static int LightsDrawn = 0;
        public static int MapsDrawn = 0;

        //Cache the Y based rendering
        public static List<Entity>[] Layer1Entities;
        public static List<Entity>[] Layer2Entities;

        public static bool PreRenderedMapLayer = false;
        public static object GFXLock = new Object();
        public static List<GameRenderTexture> MapReleaseQueue = new List<GameRenderTexture>();
        public static List<GameRenderTexture> FreeMapTextures = new List<GameRenderTexture>();

        //Animations
        public static List<AnimationInstance> LiveAnimations = new List<AnimationInstance>();
        public static object AnimationLock = new Object();

        //Init Functions
        public static void InitGraphics()
        {
            Renderer.Init();
            contentManager = Globals.ContentManager;
            contentManager.LoadAll();
            GameFont = contentManager.GetFont("arial", 8);
        }

        public static void InitInGame()
        {
            Layer1Entities = new List<Entity>[Options.MapHeight * 3];
            Layer2Entities = new List<Entity>[Options.MapHeight * 3];
            for (var i = 0; i < Options.MapHeight * 3; i++)
            {
                Layer1Entities[i] = new List<Entity>();
                Layer2Entities[i] = new List<Entity>();
            }
        }

        public static void DrawIntro()
        {
            GameTexture imageTex = contentManager.GetTexture(GameContentManager.TextureType.Image,
                Globals.Database.IntroBG[Globals.IntroIndex]);
            if (imageTex != null)
            {
                DrawFullScreenTextureStretched(imageTex);
            }
        }

        public static void DrawMenu()
        {
            GameTexture imageTex = contentManager.GetTexture(GameContentManager.TextureType.Gui,
                Globals.Database.MenuBG);
            if (imageTex != null)
            {
                DrawFullScreenTexture(imageTex);
            }
        }

        public static void DrawInGame()
        {
            var currentMap = MapInstance.GetMap(Globals.Me.CurrentMap);
            if (currentMap != null && Globals.NeedsMaps == false)
            {
                if (GridSwitched)
                {
                    var map = MapInstance.GetMap(Globals.Me.CurrentMap);
                    if (map != null)
                    {
                        //Brightness
                        byte brightnessTarget = (byte) ((map.Brightness / 100f) * 255);
                        _brightnessLevel = brightnessTarget;
                        _playerLightColor.R = map.PlayerLightColor.R;
                        _playerLightColor.G = map.PlayerLightColor.G;
                        _playerLightColor.B = map.PlayerLightColor.B;
                        _playerLightSize = map.PlayerLightSize;
                        _playerLightIntensity = map.PlayerLightIntensity;
                        _playerLightExpand = map.PlayerLightExpand;

                        //Overlay
                        OverlayColor.A = (byte) map.AHue;
                        OverlayColor.R = (byte) map.RHue;
                        OverlayColor.G = (byte) map.GHue;
                        OverlayColor.B = (byte) map.BHue;

                        //Fog && Panorama
                        map.GridSwitched();
                    }
                    GridSwitched = false;
                }
                ClearDarknessTexture();

                TryPreRendering();

                GenerateLightMap();

                var gridX = currentMap.MapGridX;
                var gridY = currentMap.MapGridY;
                //Draw Panoramas First...
                for (int x = gridX - 1; x <= gridX + 1; x++)
                {
                    for (int y = gridY - 1; y <= gridY + 1; y++)
                    {
                        if (x >= 0 && x < Globals.MapGridWidth && y >= 0 && y < Globals.MapGridHeight &&
                            Globals.MapGrid[x, y] != -1)
                        {
                            DrawMapPanorama(Globals.MapGrid[x, y]);
                        }
                    }
                }

                for (int x = gridX - 1; x <= gridX + 1; x++)
                {
                    for (int y = gridY - 1; y <= gridY + 1; y++)
                    {
                        if (x >= 0 && x < Globals.MapGridWidth && y >= 0 && y < Globals.MapGridHeight &&
                            Globals.MapGrid[x, y] != -1)
                        {
                            DrawMap(Globals.MapGrid[x, y], 0);
                        }
                    }
                }

                lock (AnimationLock)
                {
                    foreach (AnimationInstance animInstance in LiveAnimations)
                    {
                        animInstance.Draw(false);
                    }
                }

                for (int x = 0; x < Layer1Entities.Length; x++)
                {
                    for (int y = 0; y < Layer1Entities[x].Count; y++)
                    {
                        Layer1Entities[x][y].Draw();
                        EntitiesDrawn++;
                    }
                }

                for (int x = gridX - 1; x <= gridX + 1; x++)
                {
                    for (int y = gridY - 1; y <= gridY + 1; y++)
                    {
                        if (x >= 0 && x < Globals.MapGridWidth && y >= 0 && y < Globals.MapGridHeight &&
                            Globals.MapGrid[x, y] != -1)
                        {
                            DrawMap(Globals.MapGrid[x, y], 1);
                        }
                    }
                }

                for (int x = 0; x < Layer2Entities.Length; x++)
                {
                    for (int y = 0; y < Layer2Entities[x].Count; y++)
                    {
                        Layer2Entities[x][y].Draw();
                        EntitiesDrawn++;
                    }
                }

                for (int x = gridX - 1; x <= gridX + 1; x++)
                {
                    for (int y = gridY - 1; y <= gridY + 1; y++)
                    {
                        if (x >= 0 && x < Globals.MapGridWidth && y >= 0 && y < Globals.MapGridHeight &&
                            Globals.MapGrid[x, y] != -1)
                        {
                            DrawMap(Globals.MapGrid[x, y], 2);
                        }
                    }
                }

                lock (AnimationLock)
                {
                    foreach (AnimationInstance animInstance in LiveAnimations)
                    {
                        animInstance.Draw(true);
                    }
                }

                //Draw the players targets
                Globals.Me.DrawTargets();

                DrawOverlay();

                for (int x = 0; x < Layer1Entities.Length; x++)
                {
                    for (int y = 0; y < Layer1Entities[x].Count; y++)
                    {
                        Layer1Entities[x][y].DrawName(null);
                        if (Layer1Entities[x][y].GetType() != typeof(Event))
                        {
                            Layer1Entities[x][y].DrawHpBar();
                            Layer1Entities[x][y].DrawCastingBar();
                        }
                        Layer1Entities[x][y].DrawChatBubbles();
                    }
                }
                for (int x = 0; x < Layer2Entities.Length; x++)
                {
                    for (int y = 0; y < Layer2Entities[x].Count; y++)
                    {
                        Layer2Entities[x][y].DrawName(null);
                        if (Layer2Entities[x][y].GetType() != typeof(Event))
                        {
                            Layer2Entities[x][y].DrawHpBar();
                            Layer2Entities[x][y].DrawCastingBar();
                        }
                        Layer2Entities[x][y].DrawChatBubbles();
                    }
                }

                //Draw action msg's
                for (int x = gridX - 1; x <= gridX + 1; x++)
                {
                    for (int y = gridY - 1; y <= gridY + 1; y++)
                    {
                        if (x >= 0 && x < Globals.MapGridWidth && y >= 0 && y < Globals.MapGridHeight &&
                            Globals.MapGrid[x, y] != -1)
                        {
                            var map = MapInstance.GetMap(Globals.MapGrid[x, y]);
                            if (map != null) map.DrawActionMsgs();
                        }
                    }
                }

                DrawDarkness();
            }
        }

        //Game Rendering
        public static void Render()
        {
            if (Renderer.Begin())
            {
                if (Renderer.GetScreenWidth() != _oldWidth || Renderer.GetScreenHeight() != _oldHeight)
                {
                    _darknessTexture = null;
                    Gui.DestroyGwen();
                    Gui.InitGwen();
                    _oldWidth = Renderer.GetScreenWidth();
                    _oldHeight = Renderer.GetScreenHeight();
                }
                Renderer.Clear(Color.Black);
                DrawCalls = 0;
                MapsDrawn = 0;
                EntitiesDrawn = 0;
                LightsDrawn = 0;
                PreRenderedMapLayer = false;

                UpdateView();

                if (Globals.GameState == GameStates.Intro)
                {
                    DrawIntro();
                }
                else if (Globals.GameState == GameStates.Menu)
                {
                    DrawMenu();
                }
                else if (Globals.GameState == GameStates.Loading)
                {
                }
                if (Globals.GameState == GameStates.InGame)
                {
                    DrawInGame();
                }

                Gui.DrawGui();

                DrawGameTexture(Renderer.GetWhiteTexture(), new FloatRect(0, 0, 1, 1), CurrentView,
                    new Color((int) GameFade.GetFade(), 0, 0, 0), null, GameBlendModes.Alpha);
                Renderer.End();
            }
        }

        private static void TryPreRendering()
        {
            if (Globals.Database.RenderCaching && Globals.Me != null &&
                MapInstance.GetMap(Globals.Me.CurrentMap) != null)
            {
                var gridX = MapInstance.GetMap(Globals.Me.CurrentMap).MapGridX;
                var gridY = MapInstance.GetMap(Globals.Me.CurrentMap).MapGridY;
                for (int x = gridX - 1; x <= gridX + 1; x++)
                {
                    for (int y = gridY - 1; y <= gridY + 1; y++)
                    {
                        if (x >= 0 && x < Globals.MapGridWidth && y >= 0 && y < Globals.MapGridHeight &&
                            Globals.MapGrid[x, y] != -1)
                        {
                            var map = MapInstance.GetMap(Globals.MapGrid[x, y]);
                            if (map != null && !map.MapRendered)
                            {
                                if (!PreRenderedMapLayer)
                                {
                                    map.PreRenderMap();
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void DrawMap(int mapNum, int layer = 0)
        {
            var map = MapInstance.GetMap(mapNum);
            if (map != null)
            {
                if (
                    !new FloatRect(map.GetX(), map.GetY(), Options.TileWidth * Options.MapWidth,
                        Options.TileHeight * Options.MapHeight).IntersectsWith(CurrentView)) return;
                map.Draw(layer);
                if (layer == 0)
                {
                    MapsDrawn++;
                }
            }
        }

        private static void DrawMapPanorama(int mapNum)
        {
            var map = MapInstance.GetMap(mapNum);
            if (map != null)
            {
                if (
                    !new FloatRect(map.GetX(), map.GetY(), Options.TileWidth * Options.MapWidth,
                        Options.TileHeight * Options.MapHeight).IntersectsWith(CurrentView)) return;
                map.DrawPanorama();
            }
        }

        public static void DrawOverlay()
        {
            var map = MapInstance.GetMap(Globals.Me.CurrentMap);
            if (map != null)
            {
                float ecTime = Globals.System.GetTimeMS() - _overlayUpdate;

                if (OverlayColor.A != map.AHue ||
                    OverlayColor.R != map.RHue ||
                    OverlayColor.G != map.GHue ||
                    OverlayColor.B != map.BHue)
                {
                    if (OverlayColor.A < map.AHue)
                    {
                        if ((int) OverlayColor.A + (int) (255 * ecTime / 2000f) > map.AHue)
                        {
                            OverlayColor.A = (byte) map.AHue;
                        }
                        else
                        {
                            OverlayColor.A += (byte) (255 * ecTime / 2000f);
                        }
                    }

                    if (OverlayColor.A > map.AHue)
                    {
                        if ((int) OverlayColor.A - (int) (255 * ecTime / 2000f) < map.AHue)
                        {
                            OverlayColor.A = (byte) map.AHue;
                        }
                        else
                        {
                            OverlayColor.A -= (byte) (255 * ecTime / 2000f);
                        }
                    }

                    if (OverlayColor.R < map.RHue)
                    {
                        if ((int) OverlayColor.R + (int) (255 * ecTime / 2000f) > map.RHue)
                        {
                            OverlayColor.R = (byte) map.RHue;
                        }
                        else
                        {
                            OverlayColor.R += (byte) (255 * ecTime / 2000f);
                        }
                    }

                    if (OverlayColor.R > map.RHue)
                    {
                        if ((int) OverlayColor.R - (int) (255 * ecTime / 2000f) < map.RHue)
                        {
                            OverlayColor.R = (byte) map.RHue;
                        }
                        else
                        {
                            OverlayColor.R -= (byte) (255 * ecTime / 2000f);
                        }
                    }

                    if (OverlayColor.G < map.GHue)
                    {
                        if ((int) OverlayColor.G + (int) (255 * ecTime / 2000f) > map.GHue)
                        {
                            OverlayColor.G = (byte) map.GHue;
                        }
                        else
                        {
                            OverlayColor.G += (byte) (255 * ecTime / 2000f);
                        }
                    }

                    if (OverlayColor.G > map.GHue)
                    {
                        if ((int) OverlayColor.G - (int) (255 * ecTime / 2000f) < map.GHue)
                        {
                            OverlayColor.G = (byte) map.GHue;
                        }
                        else
                        {
                            OverlayColor.G -= (byte) (255 * ecTime / 2000f);
                        }
                    }

                    if (OverlayColor.B < map.BHue)
                    {
                        if ((int) OverlayColor.B + (int) (255 * ecTime / 2000f) > map.BHue)
                        {
                            OverlayColor.B = (byte) map.BHue;
                        }
                        else
                        {
                            OverlayColor.B += (byte) (255 * ecTime / 2000f);
                        }
                    }

                    if (OverlayColor.B > map.BHue)
                    {
                        if ((int) OverlayColor.B - (int) (255 * ecTime / 2000f) < map.BHue)
                        {
                            OverlayColor.B = (byte) map.BHue;
                        }
                        else
                        {
                            OverlayColor.B -= (byte) (255 * ecTime / 2000f);
                        }
                    }
                }
            }

            DrawGameTexture(Renderer.GetWhiteTexture(), new FloatRect(0, 0, 1, 1), CurrentView, OverlayColor, null);
            _overlayUpdate = Globals.System.GetTimeMS();
        }

        public static void DrawFullScreenTexture(GameTexture tex, float alpha = 1f)
        {
            int bgx = (int) (Renderer.GetScreenWidth() / 2 - tex.GetWidth() / 2);
            int bgy = (int) (Renderer.GetScreenHeight() / 2 - tex.GetHeight() / 2);
            int bgw = (int) tex.GetWidth();
            int bgh = (int) tex.GetHeight();
            int diff = 0;
            if (bgw < Renderer.GetScreenWidth())
            {
                diff = (int) (Renderer.GetScreenWidth() - bgw);
                bgx -= diff / 2;
                bgw += diff;
            }
            if (bgh < Renderer.GetScreenHeight())
            {
                diff = (int) (Renderer.GetScreenHeight() - bgh);
                bgy -= diff / 2;
                bgh += diff;
            }
            DrawGameTexture(tex, new FloatRect(0, 0, tex.GetWidth(), tex.GetHeight()),
                new FloatRect(bgx + Renderer.GetView().X, bgy + Renderer.GetView().Y, bgw, bgh),
                new Color((int) (alpha * 255f), 255, 255, 255));
        }

        public static void DrawFullScreenTextureStretched(GameTexture tex)
        {
            DrawGameTexture(tex, new FloatRect(0, 0, (int) tex.GetWidth(), (int) tex.GetHeight()),
                new FloatRect(Renderer.GetView().X, Renderer.GetView().Y, Renderer.GetScreenWidth(),
                    Renderer.GetScreenHeight()), Color.White);
        }

        private static void UpdateView()
        {
            if (Globals.Me == null)
            {
                CurrentView = new FloatRect(0, 0, Renderer.GetScreenWidth(), Renderer.GetScreenHeight());
                return;
            }
            var map = MapInstance.GetMap(Globals.Me.CurrentMap);
            if (Globals.GameState == GameStates.InGame && map != null)
            {
                Player en = Globals.Me;
                float x = map.GetX() - Options.MapWidth * Options.TileWidth;
                float y = map.GetY() - Options.MapHeight * Options.TileHeight;
                float x1 = map.GetX() + (Options.MapWidth * Options.TileWidth) * 2;
                float y1 = map.GetY() + (Options.MapHeight * Options.TileHeight) * 2;
                if (map.HoldUp == 1)
                {
                    y += Options.MapHeight * Options.TileHeight;
                }
                if (map.HoldLeft == 1)
                {
                    x += Options.MapWidth * Options.TileWidth;
                }
                if (map.HoldRight == 1)
                {
                    x1 -= Options.MapWidth * Options.TileWidth;
                }
                if (map.HoldDown == 1)
                {
                    y1 -= Options.MapHeight * Options.TileHeight;
                }
                float w = x1 - x;
                float h = y1 - y;
                var RestrictView = new FloatRect(x, y, w, h);
                CurrentView = new FloatRect((int) Math.Ceiling(en.GetCenterPos().X - Renderer.GetScreenWidth() / 2f),
                    (int) Math.Ceiling(en.GetCenterPos().Y - Renderer.GetScreenHeight() / 2f), Renderer.GetScreenWidth(),
                    Renderer.GetScreenHeight());
                if (RestrictView.Width >= CurrentView.Width)
                {
                    if (CurrentView.Left < RestrictView.Left)
                    {
                        CurrentView.X = RestrictView.Left;
                    }
                    if (CurrentView.Left + CurrentView.Width > RestrictView.Left + RestrictView.Width)
                    {
                        CurrentView.X -= (CurrentView.Left + CurrentView.Width) -
                                         (RestrictView.Left + RestrictView.Width);
                    }
                }
                if (RestrictView.Height >= CurrentView.Height)
                {
                    if (CurrentView.Top < RestrictView.Top)
                    {
                        CurrentView.Y = RestrictView.Top;
                    }
                    if (CurrentView.Top + CurrentView.Height > RestrictView.Top + RestrictView.Height)
                    {
                        CurrentView.Y -= (CurrentView.Top + CurrentView.Height) -
                                         (RestrictView.Top + RestrictView.Height);
                    }
                }
            }
            else
            {
                CurrentView = new FloatRect(0, 0, Renderer.GetScreenWidth(), Renderer.GetScreenHeight());
            }
            Renderer.SetView(CurrentView);
        }

        public static void CreateMapTextures(int count)
        {
            for (int i = 0; i < count; i++)
            {
                ReleaseMapTexture(Renderer.CreateRenderTexture(Options.TileWidth * Options.MapWidth,
                    Options.TileHeight * Options.MapHeight));
            }
        }

        public static bool GetMapTexture(ref GameRenderTexture replaceme)
        {
            if (FreeMapTextures.Count > 0)
            {
                replaceme = FreeMapTextures[0];
                FreeMapTextures.RemoveAt(0);
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void ReleaseMapTexture(GameRenderTexture releaseTex)
        {
            if (releaseTex.SetActive(false))
            {
                FreeMapTextures.Add(releaseTex);
            }
            else
            {
                //Debug.Print("Error!");
            }
        }

        //Lighting
        private static void ClearDarknessTexture()
        {
            if (_darknessTexture == null)
            {
                _darknessTexture = Renderer.CreateRenderTexture(Renderer.GetScreenWidth(), Renderer.GetScreenHeight());
            }
            _darknessTexture.Clear(Color.Black);
        }

        private static void GenerateLightMap()
        {
            var map = MapInstance.GetMap(Globals.Me.CurrentMap);
            if (map == null) return;
            if (_darknessTexture == null)
            {
                return;
            }

            if (map.IsIndoors)
            {
                DrawGameTexture(Renderer.GetWhiteTexture(), new FloatRect(0, 0, 1, 1),
                    new FloatRect(0, 0, _darknessTexture.GetWidth(), _darknessTexture.GetHeight()),
                    new Color((byte) (_brightnessLevel), 255, 255, 255), _darknessTexture, GameBlendModes.Add);
            }
            else
            {
                DrawGameTexture(Renderer.GetWhiteTexture(), new FloatRect(0, 0, 1, 1),
                    new FloatRect(0, 0, _darknessTexture.GetWidth(), _darknessTexture.GetHeight()),
                    new Color(255, 255, 255, 255), _darknessTexture, GameBlendModes.Add);
                DrawGameTexture(Renderer.GetWhiteTexture(), new FloatRect(0, 0, 1, 1),
                    new FloatRect(0, 0, _darknessTexture.GetWidth(), _darknessTexture.GetHeight()),
                    new Color((int) ClientTime.GetTintColor().A, (int) ClientTime.GetTintColor().R,
                        (int) ClientTime.GetTintColor().G, (int) ClientTime.GetTintColor().B), _darknessTexture,
                    GameBlendModes.None);
            }

            AddLight((int) Math.Ceiling(Globals.Me.GetCenterPos().X), (int) Math.Ceiling(Globals.Me.GetCenterPos().Y),
                (int) _playerLightSize, (byte) _playerLightIntensity, _playerLightExpand,
                Intersect.Color.FromArgb((int) _playerLightColor.A, (int) _playerLightColor.R, (int) _playerLightColor.G,
                    (int) _playerLightColor.B));

            DrawLights();
            _darknessTexture.End();
        }

        public static void DrawDarkness()
        {
            GameShader radialShader = Globals.ContentManager.GetShader("radialgradient");
            if (radialShader != null)
            {
                DrawGameTexture(_darknessTexture, CurrentView.Left, CurrentView.Top, null, GameBlendModes.Multiply);
            }
        }

        public static void AddLight(int x, int y, int size, byte intensity, float expand, Intersect.Color color)
        {
            _lightQueue.Add(new LightBase(0, 0, x, y, intensity, size, expand, color));
            LightsDrawn++;
        }

        private static void DrawLights()
        {
            GameShader radialShader = Globals.ContentManager.GetShader("radialgradient");
            if (radialShader != null)
            {
                foreach (LightBase l in _lightQueue)
                {
                    int x = l.OffsetX - ((int) CurrentView.Left + l.Size);
                    int y = l.OffsetY - ((int) CurrentView.Top + l.Size);

                    radialShader.SetColor("LightColor", new Color(l.Intensity, l.Color.R, l.Color.G, l.Color.B));
                    radialShader.SetFloat("Expand", l.Expand / 100f);

                    DrawGameTexture(Renderer.GetWhiteTexture(), new FloatRect(0, 0, 1, 1),
                        new FloatRect(x, y, l.Size * 2, l.Size * 2), new Color(255, 255, 255, 255),
                        _darknessTexture, GameBlendModes.Add, radialShader);
                }
            }
            _lightQueue.Clear();
        }

        public static void UpdatePlayerLight()
        {
            //Draw Light Around Player
            var map = MapInstance.GetMap(Globals.Me.CurrentMap);
            if (map != null)
            {
                float ecTime = Globals.System.GetTimeMS() - _lightUpdate;
                float valChange = (255 * ecTime / 2000f);
                byte brightnessTarget = (byte) ((map.Brightness / 100f) * 255);
                if (_brightnessLevel < brightnessTarget)
                {
                    if (_brightnessLevel + valChange > brightnessTarget)
                    {
                        _brightnessLevel = brightnessTarget;
                    }
                    else
                    {
                        _brightnessLevel += valChange;
                    }
                }

                if (_brightnessLevel > brightnessTarget)
                {
                    if (_brightnessLevel - valChange < brightnessTarget)
                    {
                        _brightnessLevel = brightnessTarget;
                    }
                    else
                    {
                        _brightnessLevel -= valChange;
                    }
                }

                if (_playerLightColor.R != map.PlayerLightColor.R ||
                    _playerLightColor.G != map.PlayerLightColor.G ||
                    _playerLightColor.B != map.PlayerLightColor.B)
                {
                    if (_playerLightColor.R < map.PlayerLightColor.R)
                    {
                        if (_playerLightColor.R + valChange > map.PlayerLightColor.R)
                        {
                            _playerLightColor.R = map.PlayerLightColor.R;
                        }
                        else
                        {
                            _playerLightColor.R += valChange;
                        }
                    }

                    if (_playerLightColor.R > map.PlayerLightColor.R)
                    {
                        if (_playerLightColor.R - valChange < map.PlayerLightColor.R)
                        {
                            _playerLightColor.R = map.PlayerLightColor.R;
                        }
                        else
                        {
                            _playerLightColor.R -= valChange;
                        }
                    }

                    if (_playerLightColor.G < map.PlayerLightColor.G)
                    {
                        if (_playerLightColor.G + valChange > map.PlayerLightColor.G)
                        {
                            _playerLightColor.G = map.PlayerLightColor.G;
                        }
                        else
                        {
                            _playerLightColor.G += valChange;
                        }
                    }

                    if (_playerLightColor.G > map.PlayerLightColor.G)
                    {
                        if (_playerLightColor.G - valChange < map.PlayerLightColor.G)
                        {
                            _playerLightColor.G = map.PlayerLightColor.G;
                        }
                        else
                        {
                            _playerLightColor.G -= valChange;
                        }
                    }

                    if (_playerLightColor.B < map.PlayerLightColor.B)
                    {
                        if (_playerLightColor.B + valChange > map.PlayerLightColor.B)
                        {
                            _playerLightColor.B = map.PlayerLightColor.B;
                        }
                        else
                        {
                            _playerLightColor.B += valChange;
                        }
                    }

                    if (_playerLightColor.B > map.PlayerLightColor.B)
                    {
                        if (_playerLightColor.B - valChange < map.PlayerLightColor.B)
                        {
                            _playerLightColor.B = map.PlayerLightColor.B;
                        }
                        else
                        {
                            _playerLightColor.B -= valChange;
                        }
                    }
                }

                if (_playerLightSize != map.PlayerLightSize)
                {
                    if (_playerLightSize < map.PlayerLightSize)
                    {
                        if (_playerLightSize + (500 * ecTime / 2000f) > map.PlayerLightSize)
                        {
                            _playerLightSize = map.PlayerLightSize;
                        }
                        else
                        {
                            _playerLightSize += (500 * ecTime / 2000f);
                        }
                    }

                    if (_playerLightSize > map.PlayerLightSize)
                    {
                        if (_playerLightSize - (500 * ecTime / 2000f) < map.PlayerLightSize)
                        {
                            _playerLightSize = map.PlayerLightSize;
                        }
                        else
                        {
                            _playerLightSize -= (500 * ecTime / 2000f);
                        }
                    }
                }

                if (_playerLightIntensity < map.PlayerLightIntensity)
                {
                    if (_playerLightIntensity + valChange > map.PlayerLightIntensity)
                    {
                        _playerLightIntensity = map.PlayerLightIntensity;
                    }
                    else
                    {
                        _playerLightIntensity += valChange;
                    }
                }

                if (_playerLightIntensity > map.AHue)
                {
                    if (_playerLightIntensity - valChange < map.PlayerLightIntensity)
                    {
                        _playerLightIntensity = map.PlayerLightIntensity;
                    }
                    else
                    {
                        _playerLightIntensity -= valChange;
                    }
                }

                if (_playerLightExpand < map.PlayerLightExpand)
                {
                    if (_playerLightExpand + (100f * ecTime / 2000f) > map.PlayerLightExpand)
                    {
                        _playerLightExpand = map.PlayerLightExpand;
                    }
                    else
                    {
                        _playerLightExpand += (100f * ecTime / 2000f);
                    }
                }

                if (_playerLightExpand > map.PlayerLightExpand)
                {
                    if (_playerLightExpand - (100f * ecTime / 2000f) < map.PlayerLightExpand)
                    {
                        _playerLightExpand = map.PlayerLightExpand;
                    }
                    else
                    {
                        _playerLightExpand -= (100f * ecTime / 2000f);
                    }
                }
                _lightUpdate = Globals.System.GetTimeMS();
            }
        }

        //Helper Functions

        //Rendering Functions

        /// <summary>
        ///     Renders a specified texture onto a RenderTexture or the GameScreen (if renderTarget is passed as null) at the
        ///     coordinates given using a specified blending mode.
        /// </summary>
        /// <param name="tex">The texture to draw</param>
        /// <param name="x">X coordinate on the render target to draw to</param>
        /// <param name="y">Y coordinate on the render target to draw to</param>
        /// <param name="renderTarget">Where to draw to. If null it this will draw to the game screen.</param>
        /// <param name="blendMode">Which blend mode to use when rendering</param>
        public static void DrawGameTexture(GameTexture tex, float x, float y, GameRenderTexture renderTarget = null,
            GameBlendModes blendMode = GameBlendModes.None, GameShader shader = null, float rotationDegrees = 0.0f)
        {
            var destRectangle = new FloatRect(x, y, (int) tex.GetWidth(), (int) tex.GetHeight());
            var srcRectangle = new FloatRect(0, 0, (int) tex.GetWidth(), (int) tex.GetHeight());
            DrawGameTexture(tex, srcRectangle, destRectangle, Color.White, renderTarget, blendMode, shader,
                rotationDegrees);
        }

        /// <summary>
        ///     Renders a specified texture onto a RenderTexture or the GameScreen (if renderTarget is passed as null) at the
        ///     coordinates given using a specified blending mode.
        /// </summary>
        /// <param name="tex">The texture to draw</param>
        /// <param name="x">X coordinate on the render target to draw to</param>
        /// <param name="y">Y coordinate on the render target to draw to</param>
        /// <param name="renderColor">Color mask to draw with. Default is Color.White</param>
        /// <param name="renderTarget">Where to draw to. If null it this will draw to the game screen.</param>
        /// <param name="blendMode">Which blend mode to use when rendering</param>
        public static void DrawGameTexture(GameTexture tex, float x, float y, Color renderColor,
            GameRenderTexture renderTarget = null, GameBlendModes blendMode = GameBlendModes.None,
            GameShader shader = null, float rotationDegrees = 0.0f)
        {
            var destRectangle = new FloatRect(x, y, (int) tex.GetWidth(), (int) tex.GetHeight());
            var srcRectangle = new FloatRect(0, 0, (int) tex.GetWidth(), (int) tex.GetHeight());
            DrawGameTexture(tex, srcRectangle, destRectangle, renderColor, renderTarget, blendMode, shader,
                rotationDegrees);
        }

        /// <summary>
        ///     Renders a specified texture onto a RenderTexture or the GameScreen (if renderTarget is passed as null) at the
        ///     coordinates given using a specified blending mode.
        /// </summary>
        /// <param name="tex">The texture to draw</param>
        /// <param name="dx">X coordinate on the renderTarget to draw to.</param>
        /// <param name="dy">Y coordinate on the renderTarget to draw to.</param>
        /// <param name="sx">X coordinate on the source texture to grab from.</param>
        /// <param name="sy">Y coordinate on the source texture to grab from.</param>
        /// <param name="w">Width of the texture part we are rendering.</param>
        /// <param name="h">Height of the texture part we are rendering.</param>
        /// <param name="renderTarget">>Where to draw to. If null it this will draw to the game screen.</param>
        /// <param name="blendMode">Which blend mode to use when rendering</param>
        public static void DrawGameTexture(GameTexture tex, float dx, float dy, float sx, float sy, float w, float h,
            GameRenderTexture renderTarget = null, GameBlendModes blendMode = GameBlendModes.None,
            GameShader shader = null, float rotationDegrees = 0.0f)
        {
            var destRectangle = new FloatRect(dx, dy, w, h);
            var srcRectangle = new FloatRect(sx, sy, w, h);
            DrawGameTexture(tex, srcRectangle, destRectangle, Color.White, renderTarget, blendMode, shader,
                rotationDegrees);
        }

        public static void DrawGameTexture(GameTexture tex, FloatRect srcRectangle, FloatRect targetRect,
            Color renderColor, GameRenderTexture renderTarget = null, GameBlendModes blendMode = GameBlendModes.None,
            GameShader shader = null, float rotationDegrees = 0.0f)
        {
            if (tex == null) return;
            Renderer.DrawTexture(tex, srcRectangle, targetRect, renderColor, renderTarget, blendMode, shader,
                rotationDegrees);
        }
    }
}