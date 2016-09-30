﻿//
//  TownsScreen.cs
//
//  Author:
//       scemino <scemino74@gmail.com>
//
//  Copyright (c) 2015 
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NScumm.Core;
using NScumm.Core.Graphics;

namespace NScumm.Scumm.Graphics
{
    public class TownsScreen
    {
        const int DIRTY_RECTS_MAX = 20;
        const int FULL_REDRAW = (DIRTY_RECTS_MAX + 1);

        public TownsScreen(IGraphicsManager gfx, int width, int height, PixelFormat format)
        {
            _gfx = gfx;
            _width = width;
            _height = height;
            _pixelFormat = format;
            _pitch = width * Surface.GetBytesPerPixel(format);

            _outBuffer = new byte[_pitch * _height];
            for (int i = 0; i < _layers.Length; i++)
            {
                _layers[i] = new TownsScreenLayer();
            }
            SetupLayer(0, width, height, 256);
        }

        public void ToggleLayers(int flag)
        {
            if (flag < 0 || flag > 3)
                return;

            _layers[0].enabled = (flag & 1) != 0;
            _layers[0].onBottom = true;
            _layers[1].enabled = (flag & 2) != 0;
            _layers[1].onBottom = !_layers[0].enabled;

            _dirtyRects.Clear();
            _dirtyRects.Add(new Rect((short) (_width - 1), (short) (_height - 1)));
            _numDirtyRects = FULL_REDRAW;

            Array.Clear(_outBuffer, 0, _pitch * _height);
            Update();

            _gfx.UpdateScreen();
        }

        public PixelNavigator? GetLayerPixels(int layer, int x, int y)
        {
            if (layer < 0 || layer > 1)
                return null;

            var l = _layers[layer];
            if (!l.ready)
                return null;

            var pn = new PixelNavigator(l.pixels, l.pitch / l.bpp, l.bpp);
            pn.GoTo(x, y);
            return pn;
        }

        public int GetLayerPitch(int layer)
        {
            if (layer >= 0 && layer < 2)
                return _layers[layer].pitch;
            return 0;
        }

        public int GetLayerHeight(int layer)
        {
            if (layer >= 0 && layer < 2)
                return _layers[layer].height;
            return 0;
        }

        public int GetLayerBpp(int layer)
        {
            if (layer >= 0 && layer < 2)
                return _layers[layer].bpp;
            return 0;
        }

        public int GetLayerScaleW(int layer)
        {
            if (layer >= 0 && layer < 2)
                return _layers[layer].scaleW;
            return 0;
        }

        public int GetLayerScaleH(int layer)
        {
            if (layer >= 0 && layer < 2)
                return _layers[layer].scaleH;
            return 0;
        }

        public void SetupLayer(int layer, int width, int height, int numCol, byte[] pal = null)
        {
            if (layer < 0 || layer > 1)
                return;

            var l = _layers[layer];

            if ((numCol >> 15) != 0)
                throw new InvalidOperationException("TownsScreen::setupLayer(): No more than 32767 colors supported.");

            if (width > _width || height > _height)
                throw new InvalidOperationException("TownsScreen::setupLayer(): Layer width/height must be equal or less than screen width/height");

            l.scaleW = (byte)(_width / width);
            l.scaleH = (byte)(_height / height);

            if ((float)l.scaleW != ((float)_width / (float)width) || (float)l.scaleH != ((float)_height / (float)height))
                throw new InvalidOperationException("TownsScreen::setupLayer(): Layer width/height must be equal or an EXACT half, third, etc. of screen width/height.\n More complex aspect ratio scaling is not supported.");

            if (width <= 0 || height <= 0 || numCol < 16)
                throw new InvalidOperationException("TownsScreen::setupLayer(): Invalid width/height/number of colors setting.");

            l.height = height;
            l.numCol = numCol;
            l.bpp = (((numCol - 1) & 0xff00) != 0) ? 2 : 1;
            l.pitch = width * l.bpp;
            l.palette = pal;

            if ((l.palette != null) && Surface.GetBytesPerPixel(_pixelFormat) == 1)
                Debug.WriteLine("TownsScreen::setupLayer(): Layer palette usage requires 16 bit graphics setting.\nLayer palette will be ignored.");

            l.pixels = new byte[l.pitch * l.height];

            // build offset tables to speed up merging/scaling layers
            l.bltInternX = new ushort[_width];
            for (int i = 0; i < _width; ++i)
                l.bltInternX[i] = (ushort)((i / l.scaleW) * l.bpp);

            l.bltInternY = new int[_height];
            for (int i = 0; i < _height; ++i)
                l.bltInternY[i] = (i / l.scaleH) * l.pitch;

            l.bltTmpPal = (l.bpp == 1 && Surface.GetBytesPerPixel(_pixelFormat) == 2) ? new ushort[l.numCol] : null;

            l.enabled = true;
            _layers[0].onBottom = true;
            _layers[1].onBottom = !_layers[0].enabled;
            l.ready = true;
        }

        public void FillLayerRect(int layer, Point p, int w, int h, int col)
        {
            if (layer < 0 || layer > 1 || w <= 0 || h <= 0)
                return;

            var l = _layers[layer];
            if (!l.ready)
                return;

            Debug.Assert(p.X >= 0 && p.Y >= 0 && ((p.X + w) * l.bpp) <= (l.pitch) && (p.Y + h) <= (l.height));

            int pos = p.Y * l.pitch + p.X * l.bpp;

            for (int i = 0; i < h; ++i)
            {
                if (l.bpp == 2)
                {
                    for (int ii = 0; ii < w; ++ii)
                    {
                        l.pixels.WriteUInt16(pos, (ushort)col);
                        pos += 2;
                    }
                    pos += (l.pitch - w * 2);
                }
                else
                {
                    l.pixels.Set(pos, (byte)col, w);
                    pos += l.pitch;
                }
            }
            AddDirtyRect(p.X * l.scaleW, p.Y * l.scaleH, w * l.scaleW, h * l.scaleH);
        }

        public void ClearLayer(int layer)
        {
            if (layer < 0 || layer > 1)
                return;

            var l = _layers[layer];
            if (!l.ready)
                return;

            Array.Clear(l.pixels, 0, l.pitch * l.height);
            _dirtyRects.Add(new Rect((short) (_width - 1), (short) (_height - 1)));
            _numDirtyRects = FULL_REDRAW;
        }

        public void AddDirtyRect(int x, int y, int w, int h)
        {
            if (w <= 0 || h <= 0 || _numDirtyRects > DIRTY_RECTS_MAX)
                return;

            if (_numDirtyRects == DIRTY_RECTS_MAX)
            {
                // full redraw
                _dirtyRects.Clear();
                _dirtyRects.Add(new Rect((short) (_width - 1), (short) (_height - 1)));
                _numDirtyRects++;
                return;
            }

            int x2 = x + w - 1;
            int y2 = y + h - 1;

            Debug.Assert(x >= 0 && y >= 0 && x2 <= _width && y2 <= _height);

            bool skip = false;
            for (int i = 0; i < _dirtyRects.Count; i++)
            {
                var r = _dirtyRects[i];
                // Try to merge new rect with an existing rect (only once, since trying to merge
                // more than one overlapping rect would be causing more overhead than doing any good).
                if (x > r.Left && x < r.Right && y > r.Top && y < r.Bottom)
                {
                    x = r.Left;
                    y = r.Top;
                    skip = true;
                }

                if (x2 > r.Left && x2 < r.Right && y > r.Top && y < r.Bottom)
                {
                    x2 = r.Right;
                    y = r.Top;
                    skip = true;
                }

                if (x2 > r.Left && x2 < r.Right && y2 > r.Top && y2 < r.Bottom)
                {
                    x2 = r.Right;
                    y2 = r.Bottom;
                    skip = true;
                }

                if (x > r.Left && x < r.Right && y2 > r.Top && y2 < r.Bottom)
                {
                    x = r.Left;
                    y2 = r.Bottom;
                    skip = true;
                }

                if (skip)
                {
                    _dirtyRects[i] = new Rect((short) x, (short) y, (short) x2, (short) y2);
                    break;
                }
            }

            if (!skip)
            {
                _dirtyRects.Add(new Rect((short) x, (short) y, (short) x2, (short) y2));
                _numDirtyRects++;
            }
        }

        public void Update()
        {
            UpdateOutputBuffer();
            OutputToScreen();
        }

        void UpdateOutputBuffer()
        {
            for (var j = 0; j < _dirtyRects.Count; j++)
            {
                var r = _dirtyRects[j];
                for (int i = 0; i < 2; i++)
                {
                    var l = _layers[i];
                    if (!l.enabled || !l.ready)
                        continue;

                    var dst = r.Top * _pitch + r.Left * Surface.GetBytesPerPixel(_pixelFormat);
                    int ptch = _pitch - (r.Right - r.Left + 1) * Surface.GetBytesPerPixel(_pixelFormat);

                    if (Surface.GetBytesPerPixel(_pixelFormat) == 2 && l.bpp == 1)
                    {
                        if (l.palette == null)
                            throw new InvalidOperationException(string.Format("void TownsScreen::updateOutputBuffer(): No palette assigned to 8 bit layer {0}", i));
                        for (int ic = 0; ic < l.numCol; ic++)
                            l.bltTmpPal[ic] = Calc16BitColor(l.palette, ic * 3);
                    }

                    for (int y = r.Top; y <= r.Bottom; ++y)
                    {
                        if (l.bpp == Surface.GetBytesPerPixel(_pixelFormat) && l.scaleW == 1 && l.onBottom && ((l.numCol & 0xff00) != 0))
                        {
                            Array.Copy(l.pixels, l.bltInternY[y] + l.bltInternX[r.Left], _outBuffer, dst, (r.Right + 1 - r.Left) * Surface.GetBytesPerPixel(_pixelFormat));
                            dst += _pitch;

                        }
                        else if (Surface.GetBytesPerPixel(_pixelFormat) == 2)
                        {
                            for (int x = r.Left; x <= r.Right; ++x)
                            {
                                var src = l.bltInternY[y] + l.bltInternX[x];
                                if (l.bpp == 1)
                                {
                                    var col = l.pixels[src];
                                    if (col != 0 || l.onBottom)
                                    {
                                        if (l.numCol == 16)
                                            col = (byte)((col >> 4) & (col & 0x0f));
                                        _outBuffer.WriteUInt16(dst, l.bltTmpPal[col]);
                                    }
                                }
                                else
                                {
                                    _outBuffer.WriteUInt16(dst, l.pixels.ToUInt16(src));
                                }
                                dst += 2;
                            }
                            dst += ptch;

                        }
                        else
                        {
                            for (int x = r.Left; x <= r.Right; ++x)
                            {
                                var col = l.bltInternY[y] + l.bltInternX[x];
                                if (col != 0 || l.onBottom)
                                {
                                    if (l.numCol == 16)
                                        col = (col >> 4) & (col & 0x0f);
                                    _outBuffer[dst] = (byte)col;
                                }
                                dst++;
                            }
                            dst += ptch;
                        }
                    }
                }
            }
        }

        void OutputToScreen()
        {
            for (var j = 0; j < _dirtyRects.Count; j++)
            {
                var r = _dirtyRects[j];
                _gfx.CopyRectToScreen(_outBuffer, _pitch, r.Left, r.Top, r.Left, r.Top, r.Right - r.Left + 1, r.Bottom - r.Top + 1);
            }
            _dirtyRects.Clear();
            _numDirtyRects = 0;
        }

        ushort Calc16BitColor(byte[] palEntry, int offset)
        {
            return ColorHelper.RGBToColor(palEntry[offset + 0], palEntry[offset + 1], palEntry[offset + 2]);
        }

        class TownsScreenLayer
        {
            public byte[] pixels;
            public byte[] palette;
            public int pitch;
            public int height;
            public int bpp;
            public int numCol;
            public byte scaleW;
            public byte scaleH;
            public bool onBottom;
            public bool enabled;
            public bool ready;

            public ushort[] bltInternX;
            public int[] bltInternY;
            public ushort[] bltTmpPal;
        }

        TownsScreenLayer[] _layers = new TownsScreenLayer[2];

        IGraphicsManager _gfx;

        byte[] _outBuffer;

        int _height;
        int _width;
        int _pitch;
        PixelFormat _pixelFormat;

        int _numDirtyRects;
        List<Rect> _dirtyRects = new List<Rect>();
    }
}

