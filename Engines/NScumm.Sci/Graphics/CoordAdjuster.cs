﻿//  Author:
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
using NScumm.Core.Graphics;

namespace NScumm.Sci.Graphics
{
    /// <summary>
    /// CoordAdjuster class, does coordinate adjustment as need by various functions
    ///  most of the time sci32 doesn't do any coordinate adjustment at all
    ///  sci16 does a lot of port adjustment on given coordinates
    /// </summary>
    internal class GfxCoordAdjuster
    {
        public virtual Rect OnControl(Rect rect)
        {
            return rect;
        }
    }

    internal class GfxCoordAdjuster16 : GfxCoordAdjuster
    {
        private GfxPorts _ports;

        public GfxCoordAdjuster16(GfxPorts ports)
        {
            _ports = ports;
        }

        public override Rect OnControl(Rect rect)
        {
            Port oldPort = _ports.SetPort(_ports._picWind);
            Rect adjustedRect=new Rect(rect.Left, rect.Top, rect.Right, rect.Bottom);

            adjustedRect.Clip(_ports.Port.rect);
            _ports.OffsetRect(adjustedRect);
            _ports.SetPort(oldPort);
            return adjustedRect;
        }
    }
}
