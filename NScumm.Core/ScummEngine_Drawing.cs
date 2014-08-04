﻿//
//  ScummEngin_Drawing.cs
//
//  Author:
//       scemino <scemino74@gmail.com>
//
//  Copyright (c) 2014 
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using NScumm.Core.Graphics;
using System.IO;

namespace NScumm.Core
{
	partial class ScummEngine
	{
		VirtScreen _mainVirtScreen;
		VirtScreen _textVirtScreen;
		VirtScreen _verbVirtScreen;
		VirtScreen _unkVirtScreen;
		Surface _textSurface;
		Surface _composite;
		bool _bgNeedsRedraw;
		bool _fullRedraw;
		internal Gdi Gdi;
		bool _completeScreenRedraw;
		IGraphicsManager _gfxManager;
		byte[] _shadowPalette = new byte[256];
		int _palDirtyMin, _palDirtyMax;
		int _textSurfaceMultiplier = 1;
		int _screenStartStrip;
		int _screenEndStrip;

		static byte[] tableEGAPalette = new byte[] {
			0x00, 0x00, 0x00,   0x00, 0x00, 0xAA,   0x00, 0xAA, 0x00,   0x00, 0xAA, 0xAA,
			0xAA, 0x00, 0x00,   0xAA, 0x00, 0xAA,   0xAA, 0x55, 0x00,   0xAA, 0xAA, 0xAA,
			0x55, 0x55, 0x55,   0x55, 0x55, 0xFF,   0x55, 0xFF, 0x55,   0x55, 0xFF, 0xFF,
			0xFF, 0x55, 0x55,   0xFF, 0x55, 0xFF,   0xFF, 0xFF, 0x55,   0xFF, 0xFF, 0xFF
		};
		Palette _currentPalette = new Palette ();

		internal Surface TextSurface { get { return _textSurface; } }

		void DrawBox ()
		{
			int x, y, x2, y2, color;

			x = GetVarOrDirectWord (OpCodeParameter.Param1);
			y = GetVarOrDirectWord (OpCodeParameter.Param2);

			_opCode = ReadByte ();
			x2 = GetVarOrDirectWord (OpCodeParameter.Param1);
			y2 = GetVarOrDirectWord (OpCodeParameter.Param2);
			color = GetVarOrDirectByte (OpCodeParameter.Param3);

			DrawBox (x, y, x2, y2, color);
		}

		void DrawObject ()
		{
			byte state = 1;
			int obj = GetVarOrDirectWord (OpCodeParameter.Param1);

			int xpos = GetVarOrDirectWord (OpCodeParameter.Param2);
			int ypos = GetVarOrDirectWord (OpCodeParameter.Param3);

			int idx = GetObjectIndex (obj);
			if (idx == -1)
				return;

			if (xpos != 0xFF) {
				var wx = _objs [idx].Walk.X + (xpos * 8) - _objs [idx].Position.X;
				var wy = _objs [idx].Walk.Y + (ypos * 8) - _objs [idx].Position.Y;
				_objs [idx].Walk = new Point ((short)wx, (short)wy);
				_objs [idx].Position = new Point ((short)(xpos * 8), (short)(ypos * 8));
			}

			AddObjectToDrawQue ((byte)idx);

			var x = (ushort)_objs [idx].Position.X;
			var y = (ushort)_objs [idx].Position.Y;
			var w = _objs [idx].Width;
			var h = _objs [idx].Height;

			int i = _objs.Length - 1;
			do {
				if (_objs [i].Number != 0 &&
				    _objs [i].Position.X == x && _objs [i].Position.Y == y &&
				    _objs [i].Width == w && _objs [i].Height == h)
					PutState (_objs [i].Number, 0);
			} while ((--i) != 0);

			PutState (obj, state);
		}

		void RestoreBackground (Rect rect, byte backColor)
		{
			VirtScreen vs;

			if (rect.Top < 0)
				rect.Top = 0;
			if (rect.Left >= rect.Right || rect.Top >= rect.Bottom)
				return;

			if ((vs = FindVirtScreen (rect.Top)) == null)
				return;

			if (rect.Left > vs.Width)
				return;

			// Convert 'rect' to local (virtual screen) coordinates
			rect.Top -= vs.TopLine;
			rect.Bottom -= vs.TopLine;

			rect.Clip (vs.Width, vs.Height);

			int height = rect.Height;
			int width = rect.Width;

			MarkRectAsDirty (vs, rect.Left, rect.Right, rect.Top, rect.Bottom, Gdi.UsageBitRestored);

			var screenBuf = new PixelNavigator (vs.Surfaces [0]);
			screenBuf.GoTo (vs.XStart + rect.Left, rect.Top);

			if (height == 0)
				return;

			if (vs.HasTwoBuffers && _currentRoom != 0 && IsLightOn ()) {
				var back = new PixelNavigator (vs.Surfaces [1]);
				back.GoTo (vs.XStart + rect.Left, rect.Top);
				Gdi.Blit (screenBuf, back, width, height);
				if (vs == MainVirtScreen && _charset.HasMask) {
					var mask = new PixelNavigator (_textSurface);
					mask.GoTo (rect.Left, rect.Top - ScreenTop);
					Gdi.Fill (mask, CharsetMaskTransparency, width * _textSurfaceMultiplier, height * _textSurfaceMultiplier);
				}
			} else {
				Gdi.Fill (screenBuf, backColor, width, height);
			}
		}

		void DrawVerbBitmap (int verb, int x, int y)
		{
			var vst = _verbs [verb];
			var vs = FindVirtScreen (y);

			if (vs == null)
				return;

			Gdi.IsZBufferEnabled = false;

			var hasTwoBufs = vs.HasTwoBuffers;
			//vs.HasTwoBuffers=false;

			int xStrip = x / 8;
			int yDiff = y - vs.TopLine;

			for (int i = 0; i < vst.ImageWidth / 8; i++) {
				Gdi.DrawBitmap (vst.Image, vs, xStrip + i, yDiff,
					vst.ImageWidth, vst.ImageHeight,
					i, 1, DrawBitmaps.AllowMaskOr | DrawBitmaps.ObjectMode);
			}

			vst.CurRect.Right = vst.CurRect.Left + vst.ImageWidth;
			vst.CurRect.Bottom = vst.CurRect.Top + vst.ImageHeight;
			vst.OldRect = vst.CurRect;

			Gdi.IsZBufferEnabled = true;
			//vs.HasTwoBuffers=hasTwoBufs;
		}

		void DrawString (int a, byte[] msg)
		{
			var buf = new byte[270];
			int i, c;
			int fontHeight;
			uint color;

			ConvertMessageToString (msg, buf, 0);

			_charset.Top = _string [a].Position.Y + ScreenTop;
			_charset.StartLeft = _charset.Left = _string [a].Position.X;
			_charset.Right = _string [a].Right;
			_charset.Center = _string [a].Center;
			_charset.SetColor (_string [a].Color);
			_charset.DisableOffsX = _charset.FirstChar = true;
			_charset.SetCurID (_string [a].Charset);

			fontHeight = _charset.GetFontHeight ();

			// trim from the right
			int tmpPos = 0;
			int spacePos = 0;
			while (buf [tmpPos] != 0) {
				if (buf [tmpPos] == ' ') {
					if (spacePos == 0)
						spacePos = tmpPos;
				} else {
					spacePos = 0;
				}
				tmpPos++;
			}
			if (spacePos != 0) {
				buf [spacePos] = 0;
			}

			if (_charset.Center) {
				_charset.Left -= _charset.GetStringWidth (a, buf, 0) / 2;
			}

			if (buf [0] == 0) {
				_charset.Str.Left = _charset.Left;
				_charset.Str.Top = _charset.Top;
				_charset.Str.Right = _charset.Left;
				_charset.Str.Bottom = _charset.Top;
			}

			for (i = 0; (c = buf [i++]) != 0;) {
				if (c == 0xFF || (c == 0xFE)) {
					c = buf [i++];
					switch (c) {
					case 9:
					case 10:
					case 13:
					case 14:
						i += 2;
						break;

					case 1:
					case 8:
						if (_charset.Center) {
							_charset.Left = _charset.StartLeft - _charset.GetStringWidth (a, buf, i);
						} else {
							_charset.Left = _charset.StartLeft;
						}
						_charset.Top += fontHeight;
						break;

					case 12:
						color = (uint)(buf [i] + (buf [i + 1] << 8));
						i += 2;
						if (color == 0xFF)
							_charset.SetColor (_string [a].Color);
						else
							_charset.SetColor ((byte)color);
						break;
					}
				} else {
					//if ((c & 0x80) != 0 && _useCJKMode)
					//{
					//    if (checkSJISCode(c))
					//        c += buf[i++] * 256;
					//}
					_charset.PrintChar (c, true);
					_charset.BlitAlso = false;
				}
			}

			if (a == 0) {
				_nextLeft = _charset.Left;
				_nextTop = _charset.Top;
			}

			_string [a].Position = new Point ((short)_charset.Str.Right, _string [a].Position.Y);
		}

		void SetDirtyColors (int min, int max)
		{
			if (_palDirtyMin > min)
				_palDirtyMin = min;
			if (_palDirtyMax < max)
				_palDirtyMax = max;
		}

		void DrawBox (int x, int y, int x2, int y2, int color)
		{
			VirtScreen vs;

			if ((vs = FindVirtScreen (y)) == null)
				return;

			if (x > x2)
				ScummHelper.Swap (ref x, ref x2);

			if (y > y2)
				ScummHelper.Swap (ref y, ref y2);

			x2++;
			y2++;

			// Adjust for the topline of the VirtScreen
			y -= vs.TopLine;
			y2 -= vs.TopLine;

			// Clip the coordinates
			if (x < 0)
				x = 0;
			else if (x >= vs.Width)
				return;

			if (x2 < 0)
				return;
			if (x2 > vs.Width)
				x2 = vs.Width;

			if (y < 0)
				y = 0;
			else if (y > vs.Height)
				return;

			if (y2 < 0)
				return;

			if (y2 > vs.Height)
				y2 = vs.Height;

			int width = x2 - x;
			int height = y2 - y;

			// This will happen in the Sam & Max intro - see bug #1039162 - where
			// it would trigger an assertion in blit().

			if (width <= 0 || height <= 0)
				return;

			MarkRectAsDirty (vs, x, x2, y, y2);

			var backbuff = new PixelNavigator (vs.Surfaces [0]);
			backbuff.GoTo (vs.XStart + x, y);

			// A check for -1 might be wrong in all cases since o5_drawBox() in its current form
			// is definitely not capable of passing a parameter of -1 (color range is 0 - 255).
			// Just to make sure I don't break anything I restrict the code change to FM-Towns
			// version 5 games where this change is necessary to fix certain long standing bugs.
			if (color == -1) {
				if (vs != MainVirtScreen)
					Console.Error.WriteLine ("can only copy bg to main window");

				var bgbuff = new PixelNavigator (vs.Surfaces [1]);
				bgbuff.GoTo (vs.XStart + x, y);

				Gdi.Blit (backbuff, bgbuff, width, height);
				if (_charset.HasMask) {
					var mask = new PixelNavigator (_textSurface);
					mask.GoToIgnoreBytesByPixel (x * _textSurfaceMultiplier, (y - ScreenTop) * _textSurfaceMultiplier);
					Gdi.Fill (mask, CharsetMaskTransparency, width * _textSurfaceMultiplier, height * _textSurfaceMultiplier);
				}
			} else {
				Gdi.Fill (backbuff, (byte)color, width, height);
			}
		}

		void UpdatePalette ()
		{
			if (_palDirtyMax == -1)
				return;

			var colors = new Color[256];

			int first = _palDirtyMin;
			int num = _palDirtyMax - first + 1;

			for (int i = _palDirtyMin; i <= _palDirtyMax; i++) {
				var color = _currentPalette.Colors [_shadowPalette [i]];
				colors [i] = color;
			}

			_palDirtyMax = -1;
			_palDirtyMin = 256;

			_gfxManager.SetPalette (colors, first, num);
		}

		void HandleMouseOver ()
		{
			if (_completeScreenRedraw) {
				VerbMouseOver (0);
			} else {
				if (_cursor.State > 0) {
					var pos = _inputManager.GetMousePosition ();
					VerbMouseOver (FindVerbAtPos ((int)pos.X, (int)pos.Y));
				}
			}
		}

		void ClearTextSurface ()
		{
			Gdi.Fill (_textSurface.Pixels, _textSurface.Pitch, CharsetMaskTransparency, _textSurface.Width, _textSurface.Height);
		}

		void HandleDrawing ()
		{
			if (_camera.CurrentPosition != _camera.LastPosition || _bgNeedsRedraw || _fullRedraw) {
				RedrawBGAreas ();
			}

			ProcessDrawQueue ();

			_fullRedraw = false;
		}

		/// <summary>
		/// Redraw background as needed, i.e. the left/right sides if scrolling took place etc.
		/// Note that this only updated the virtual screen, not the actual display.
		/// </summary>
		void RedrawBGAreas ()
		{
			if (_game.Id != "pass" && _game.Version >= 4 && _game.Version <= 6) {
				// Starting with V4 games (with the exception of the PASS demo), text
				// is drawn over the game graphics (as  opposed to be drawn in a
				// separate region of the screen). So, when scrolling in one of these
				// games (pre-new camera system), if actor text is visible (as indicated
				// by the _hasMask flag), we first remove it before proceeding.
				if (_camera.CurrentPosition.X != _camera.LastPosition.X && _charset.HasMask) {
					StopTalk ();
				}
			}

			// Redraw parts of the background which are marked as dirty.
			if (!_fullRedraw && _bgNeedsRedraw) {
				for (int i = 0; i != Gdi.NumStrips; i++) {
					if (Gdi.TestGfxUsageBit (_screenStartStrip + i, Gdi.UsageBitDirty)) {
						RedrawBGStrip (i, 1);
					}
				}
			}

			int val = 0;
			var diff = _camera.CurrentPosition.X - _camera.LastPosition.X;
			if (!_fullRedraw && diff == 8) {
				val = -1;
				RedrawBGStrip (Gdi.NumStrips - 1, 1);
			} else if (!_fullRedraw && diff == -8) {
				val = +1;
				RedrawBGStrip (0, 1);
			} else if (_fullRedraw || diff != 0) {
				// TODO: ClearFlashlight
				//ClearFlashlight();
				_bgNeedsRedraw = false;
				RedrawBGStrip (0, Gdi.NumStrips);
			}
			DrawRoomObjects (val);
			_bgNeedsRedraw = false;
		}

		void RedrawBGStrip (int start, int num)
		{
			int s = _screenStartStrip + start;

			for (int i = 0; i < num; i++)
				Gdi.SetGfxUsageBit (s + i, Gdi.UsageBitDirty);

			Gdi.DrawBitmap (roomData.Data, _mainVirtScreen, s, 0, roomData.Header.Width, _mainVirtScreen.Height, s, num, 0);
		}

		void HandleShaking ()
		{
			if (_shakeEnabled) {
				_shakeFrame = (_shakeFrame + 1) % ShakePositions.Length;
				_gfxManager.SetShakePos (ShakePositions [_shakeFrame]);
			} else if (!_shakeEnabled && _shakeFrame != 0) {
				_shakeFrame = 0;
				_gfxManager.SetShakePos (0);
			}
		}

		void DrawDirtyScreenParts ()
		{
			// Update verbs
			UpdateDirtyScreen (_verbVirtScreen);

			// Update the conversation area (at the top of the screen)
			UpdateDirtyScreen (_textVirtScreen);

			// Update game area ("stage")
			if (_camera.LastPosition.X != _camera.CurrentPosition.X) {
				// Camera moved: redraw everything
				DrawStripToScreen (_mainVirtScreen, 0, _mainVirtScreen.Width, 0, _mainVirtScreen.Height);
				_mainVirtScreen.SetDirtyRange (_mainVirtScreen.Height, 0);
			} else {
				UpdateDirtyScreen (_mainVirtScreen);
			}

			// Handle shaking
			HandleShaking ();
		}

		void UpdateDirtyScreen (VirtScreen vs)
		{
			// Do nothing for unused virtual screens
			if (vs.Height == 0)
				return;

			int i;
			int w = 8;
			int start = 0;

			for (i = 0; i < Gdi.NumStrips; i++) {
				if (vs.BDirty [i] != 0) {
					int top = vs.TDirty [i];
					int bottom = vs.BDirty [i];
					vs.TDirty [i] = vs.Height;
					vs.BDirty [i] = 0;
					if (i != (Gdi.NumStrips - 1) && vs.BDirty [i + 1] == bottom && vs.TDirty [i + 1] == top) {
						// Simple optimizations: if two or more neighboring strips
						// form one bigger rectangle, coalesce them.
						w += 8;
						continue;
					}
					DrawStripToScreen (vs, start * 8, w, top, bottom);
					w = 8;
				}
				start = i + 1;
			}
		}

		/// <summary>
		/// Blit the specified rectangle from the given virtual screen to the display.
		/// Note: t and b are in *virtual screen* coordinates, while x is relative to
		/// the *real screen*. This is due to the way tdirty/vdirty work: they are
		/// arrays which map 'strips' (sections of the real screen) to dirty areas as
		/// specified by top/bottom coordinate in the virtual screen.
		/// </summary>
		/// <param name="vs"></param>
		/// <param name="x"></param>
		/// <param name="width"></param>
		/// <param name="top"></param>
		/// <param name="bottom"></param>
		void DrawStripToScreen (VirtScreen vs, int x, int width, int top, int bottom)
		{
			// Short-circuit if nothing has to be drawn
			if (bottom <= top || top >= vs.Height)
				return;

			// Perform some clipping
			if (width > vs.Width - x)
				width = vs.Width - x;
			if (top < ScreenTop)
				top = ScreenTop;
			if (bottom > ScreenTop + ScreenHeight)
				bottom = ScreenTop + ScreenHeight;

			// Convert the vertical coordinates to real screen coords
			int y = vs.TopLine + top - ScreenTop;
			int height = bottom - top;

			if (width <= 0 || height <= 0)
				return;

			var srcNav = new PixelNavigator (vs.Surfaces [0]);
			srcNav.GoTo (vs.XStart + x, top);

			var compNav = new PixelNavigator (_composite);
			var txtNav = new PixelNavigator (_textSurface);
			int m = _textSurfaceMultiplier;
			txtNav.GoTo (x * m, y * m);

			var vsPitch = vs.Pitch - width * vs.BytesPerPixel;
			var textPitch = _textSurface.Pitch - width * m;

			for (int h = height * m; h > 0; --h) {
				for (int w = width * m; w > 0; w--) {
					var temp = txtNav.Read ();
					int mask = temp ^ CharsetMaskTransparency;
					mask = (((mask & 0x7f) + 0x7f) | mask) & 0x80;
					mask = ((mask >> 7) + 0x7f) ^ 0x80;

					var dst = ((temp ^ srcNav.Read ()) & mask) ^ temp;
					compNav.Write ((byte)dst);

					srcNav.OffsetX (1);
					txtNav.OffsetX (1);
					compNav.OffsetX (1);
				}

				srcNav.OffsetX (vsPitch);
				txtNav.OffsetX (textPitch);
			}

			var src = _composite.Pixels;

			// Finally blit the whole thing to the screen
			_gfxManager.CopyRectToScreen (src, width * vs.BytesPerPixel, x, y, width, height);
		}

		void MarkObjectRectAsDirty (int obj)
		{
			for (int i = 1; i < _objs.Length; i++) {
				if (_objs [i].Number == obj) {
					if (_objs [i].Width != 0) {
						int minStrip = Math.Max (_screenStartStrip, _objs [i].Position.X / 8);
						int maxStrip = Math.Min (_screenEndStrip + 1, _objs [i].Position.X / 8 + _objs [i].Width / 8);
						for (int strip = minStrip; strip < maxStrip; strip++) {
							Gdi.SetGfxUsageBit (strip, Gdi.UsageBitDirty);
						}
					}
					_bgNeedsRedraw = true;
					return;
				}
			}
		}

		internal void MarkRectAsDirty (VirtScreen vs, int left, int right, int top, int bottom, int dirtybit = 0)
		{
			int lp, rp;

			if (left > right || top > bottom)
				return;
			if (top > vs.Height || bottom < 0)
				return;

			if (top < 0)
				top = 0;
			if (bottom > vs.Height)
				bottom = vs.Height;

			if (vs == MainVirtScreen && dirtybit != 0) {
				lp = left / 8 + _screenStartStrip;
				if (lp < 0)
					lp = 0;

				rp = (right + vs.XStart) / 8;

				if (rp >= 200)
					rp = 200;

				for (; lp <= rp; lp++)
					Gdi.SetGfxUsageBit (lp, dirtybit);
			}

			// The following code used to be in the separate method setVirtscreenDirty
			lp = left / 8;
			rp = right / 8;

			if ((lp >= Gdi.NumStrips) || (rp < 0))
				return;
			if (lp < 0)
				lp = 0;
			if (rp >= Gdi.NumStrips)
				rp = Gdi.NumStrips - 1;

			while (lp <= rp) {
				if (top < vs.TDirty [lp])
					vs.TDirty [lp] = top;
				if (bottom > vs.BDirty [lp])
					vs.BDirty [lp] = bottom;
				lp++;
			}
		}

		internal PixelNavigator GetMaskBuffer (int x, int y, int z)
		{
			return Gdi.GetMaskBuffer ((x + _mainVirtScreen.XStart) / 8, y, z);
		}

		internal VirtScreen FindVirtScreen (int y)
		{
			if (VirtScreenContains (_mainVirtScreen, y))
				return _mainVirtScreen;
			if (VirtScreenContains (_textVirtScreen, y))
				return _textVirtScreen;
			if (VirtScreenContains (_verbVirtScreen, y))
				return _verbVirtScreen;
			if (VirtScreenContains (_unkVirtScreen, y))
				return _unkVirtScreen;

			return null;
		}

		static bool VirtScreenContains (VirtScreen vs, int y)
		{
			return (y >= vs.TopLine && y < vs.TopLine + vs.Height);
		}

		int GetNumZBuffers ()
		{
			var smapReader = new BinaryReader (new MemoryStream (roomData.Data));
			var numZBuffer = 0;
			int zOffset = 0;
			if (Game.Version == 3) {
				numZBuffer = 2;
			} else if (Game.Features.HasFlag (GameFeatures.SixteenColors)) {
				zOffset = smapReader.ReadInt16 ();
				smapReader.BaseStream.Seek (-2, SeekOrigin.Current);
			} else {
				zOffset = smapReader.ReadInt32 ();
				smapReader.BaseStream.Seek (-4, SeekOrigin.Current);
			}
			while (zOffset != 0 && numZBuffer < 4) {
				numZBuffer++;
				smapReader.BaseStream.Seek (zOffset, SeekOrigin.Current);
				zOffset = smapReader.ReadInt16 ();
				smapReader.BaseStream.Seek (-2, SeekOrigin.Current);
			}
			return numZBuffer;
		}
	}
}

