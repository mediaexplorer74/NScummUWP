//
//  ResourceFile4.cs
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

/*
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

using System;
using System.Collections.Generic;
using System.IO;

namespace NScumm.Core.IO
{
	public class ResourceFile4: ResourceFile
	{
		public ResourceFile4 (string path, byte encByte)
			: base (path, encByte)
		{

		}

		public override Dictionary<byte, long> ReadRoomOffsets ()
		{
			var roomOffsets = new Dictionary<byte, long> ();
			do {
				var size = _reader.ReadUInt32 ();
				var blockType = _reader.ReadUInt16 ();

				switch (blockType) {
				// *LECF* main container
				case 0x454C:
					break;
				// *LOFF* room offset table
				case 0x4F46:
					var numRooms = _reader.ReadByte ();
					while (numRooms-- != 0) {
						var room = _reader.ReadByte ();
						var offset = _reader.ReadUInt32 ();
						roomOffsets [room] = offset;
					}
					return roomOffsets;
				default:
					// skip
					Console.WriteLine ("Skip Block: 0x{0:X2}", blockType);
					_reader.BaseStream.Seek (size - 6, SeekOrigin.Current);
					break;
				}
			} while (_reader.BaseStream.Position < _reader.BaseStream.Length);
			return null;
		}

		protected override void GotoResourceHeader (long offset)
		{
			_reader.BaseStream.Seek (offset + 8, SeekOrigin.Begin);
		}
	}
	
}
