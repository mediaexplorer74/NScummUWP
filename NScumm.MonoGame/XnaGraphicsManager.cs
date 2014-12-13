﻿/*
 * This file is part of NScumm.
 * 
 * NScumm is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * NScumm is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with NScumm.  If not, see <http://www.gnu.org/licenses/>.
 */

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace NScumm.MonoGame
{
    sealed class XnaGraphicsManager : NScumm.Core.Graphics.IGraphicsManager, IDisposable
    {
        #region Fields

        readonly Texture2D _texture;
        Texture2D _textureCursor;
        byte[] _pixels;
        Color[] _colors;
        bool _cursorVisible;
        Vector2 _hotspot;
        int _shakePos;
        GraphicsDevice _device;

        #endregion

        #region Constructor

        public XnaGraphicsManager(GraphicsDevice device)
        {
            if (device == null)
                throw new ArgumentNullException("device");

            _device = device;
            _pixels = new byte[320 * 200];
            _texture = new Texture2D(device, 320, 200);
            _textureCursor = new Texture2D(device, 16, 16);
            _colors = new Color[256];
            for (int i = 0; i < _colors.Length; i++)
            {
                _colors[i] = Color.White;               
            }
        }

        #endregion

        public void UpdateScreen()
        {
            var colors = new Color[320 * 200];
            for (int h = 0; h < 200; h++)
            {
                for (int w = 0; w < 320; w++)
                {
                    var color = _colors[_pixels[w + h * 320]];
                    colors[w + h * 320] = color;
                }
            }
            _texture.SetData(colors);
        }

        public void CopyRectToScreen(byte[] buffer, int sourceStride, int x, int y, int width, int height)
        {
            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    _pixels[x + w + (y + h) * 320] = buffer[w + h * sourceStride];
                }
            }
        }

        #region Palette Methods

        public void SetPalette(NScumm.Core.Graphics.Color[] colors)
        {
            if (colors.Length > 0)
            {
                SetPalette(colors, 0, colors.Length);
            }
        }

        public void SetPalette(NScumm.Core.Graphics.Color[] colors, int first, int num)
        {
            for (int i = 0; i < num; i++)
            {
                var color = colors[i + first];
                _colors[i + first] = new Color(color.R, color.G, color.B);
            }
        }

        #endregion

        #region Cursor Methods

        public void SetCursor(byte[] pixels, int width, int height, NScumm.Core.Graphics.Point hotspot)
        {
            if (_textureCursor.Width != width || _textureCursor.Height != height)
            {
                _textureCursor.Dispose();
                _textureCursor = new Texture2D(_device, width, height);
            }

            _hotspot = new Vector2(hotspot.X, hotspot.Y);
            var pixelsCursor = new Color[width * height];
            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    var palColor = pixels[w + h * width];
                    var color = palColor == 0xFF ? Color.Transparent : _colors[palColor];
                    pixelsCursor[w + h * width] = color;
                }
            }
            _textureCursor.SetData(pixelsCursor);
        }

        public void ShowCursor(bool show)
        {
            _cursorVisible = show;
        }

        #endregion

        #region Draw Methods

        public void DrawScreen(SpriteBatch spriteBatch)
        {
            var rect = spriteBatch.GraphicsDevice.Viewport.Bounds;
            rect.Offset(0, _shakePos);
            spriteBatch.Draw(_texture, rect, null, Color.White);
        }

        public void DrawCursor(SpriteBatch spriteBatch, Vector2 cursorPos)
        {
            if (_cursorVisible)
            {
                double scaleX = spriteBatch.GraphicsDevice.Viewport.Bounds.Width / 320.0;
                double scaleY = spriteBatch.GraphicsDevice.Viewport.Bounds.Height / 200.0;
                var rect = new Rectangle((int)(cursorPos.X - scaleX * _hotspot.X), (int)(cursorPos.Y - scaleY * _hotspot.Y), (int)(scaleX * _textureCursor.Width), (int)(scaleY * _textureCursor.Height));
                spriteBatch.Draw(_textureCursor, rect, null, Color.White);
            }
        }

        #endregion

        #region Misc

        public void SetShakePos(int pos)
        {
            _shakePos = pos;
        }

        #endregion

        #region Dispose

        ~XnaGraphicsManager ()
        {
            Dispose();
        }

        public void Dispose()
        {
            _texture.Dispose();
            _textureCursor.Dispose();
        }

        #endregion
    }
}
