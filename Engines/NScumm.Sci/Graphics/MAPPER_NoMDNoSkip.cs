﻿//  Author:
//       scemino <scemino74@gmail.com>
//
//  Copyright (c) 2016
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

#if ENABLE_SCI32

using NScumm.Core;

namespace NScumm.Sci.Graphics
{
    /// <summary>
    /// Pixel mapper for a CelObj with no transparent pixels and
    /// no remapping data.
    /// </summary>
    internal class MAPPER_NoMDNoSkip : IMapper
    {
        public void Draw(BytePtr target, byte pixel, byte tmp)
        {
            target.Value = pixel;
        }
    }
}

#endif
