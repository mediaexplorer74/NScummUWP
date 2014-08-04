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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NScumm.Core.Graphics;

namespace NScumm.Core.IO
{
	public abstract class ResourceFile
	{
		#region Fields

		protected readonly XorReader _reader;

		#endregion

		#region Chunk Class

		sealed class Chunk
		{
			public long Size { get; set; }

			public ushort Tag { get; set; }

			public long Offset { get; set; }
		}

		#endregion

		#region ChunkIterator Class

		sealed class ChunkIterator : IEnumerator<Chunk>
		{
			readonly XorReader _reader;
			readonly long _position;
			readonly long _size;

			public ChunkIterator (XorReader reader, long size)
			{
				_reader = reader;
				_position = reader.BaseStream.Position;
				_size = size;
			}

			public Chunk Current {
				get;
				private set;
			}

			object System.Collections.IEnumerator.Current {
				get { return Current; }
			}

			public void Dispose ()
			{
			}

			public bool MoveNext ()
			{
				if (Current != null) {
					var offset = Current.Offset + Current.Size - 6;
					_reader.BaseStream.Seek (offset, SeekOrigin.Begin);
				}
				Current = null;
				if (_reader.BaseStream.Position < (_position + _size - 6) && _reader.BaseStream.Position < _reader.BaseStream.Length) {
					var size = _reader.ReadUInt32 ();
					var tag = _reader.ReadUInt16 ();
					Current = new Chunk { Offset = _reader.BaseStream.Position, Size = size, Tag = tag };
				}
				return Current != null;
			}

			public void Reset ()
			{
				_reader.BaseStream.Seek (_position, SeekOrigin.Begin);
				Current = null;
			}
		}

		#endregion

		#region Constructor

		protected ResourceFile (string path, byte encByte)
		{
			var dir = Path.GetDirectoryName (path);
			var realPath = (from file in Directory.EnumerateFiles (dir)
			                where string.Equals (file, path, StringComparison.OrdinalIgnoreCase)
			                select file).FirstOrDefault ();
			var fs = File.OpenRead (realPath);
			var br2 = new BinaryReader (fs);
			_reader = new XorReader (br2, encByte);
		}

		#endregion

		#region Public Methods

		public abstract Dictionary<byte, long> ReadRoomOffsets ();

		internal Room ReadRoom (long roomOffset)
		{
			var stripsDic = new Dictionary<ushort, byte[]> ();
			var its = new Stack<ChunkIterator> ();
			var room = new Room ();
			_reader.BaseStream.Seek (roomOffset, SeekOrigin.Begin);
			var it = new ChunkIterator (_reader, _reader.BaseStream.Length - _reader.BaseStream.Position);
			do {
				while (it.MoveNext ()) {
					switch (it.Current.Tag) {
					case 0x464C:
                            // *LFLF* disk block
                            // room number
						_reader.ReadUInt16 ();
                            //its.Push(it);
						it = new ChunkIterator (_reader, it.Current.Size - 2);
						break;
					case 0x4F52:
                            // ROOM
						its.Push (it);
						it = new ChunkIterator (_reader, it.Current.Size);
						break;
					case 0x4448:
                            // ROOM Header
						room.Header = ReadRMHD ();
						break;
					case 0x4343:
                            // CYCL
						room.ColorCycle = ReadCYCL ();
						break;
					case 0x5053:
                            // EPAL
						ReadEPAL ();
						break;
					case 0x5842:
                            // BOXD
						{
							int size = (int)(it.Current.Size - 6);
							var numBoxes = _reader.ReadByte ();
							for (int i = 0; i < numBoxes; i++) {
								var box = new Box ();
								box.Ulx = _reader.ReadInt16 ();
								box.Uly = _reader.ReadInt16 ();
								box.Urx = _reader.ReadInt16 ();
								box.Ury = _reader.ReadInt16 ();
								box.Lrx = _reader.ReadInt16 ();
								box.Lry = _reader.ReadInt16 ();
								box.Llx = _reader.ReadInt16 ();
								box.Lly = _reader.ReadInt16 ();
								box.Mask = _reader.ReadByte ();
								box.Flags = (BoxFlags)_reader.ReadByte ();
								box.Scale = _reader.ReadUInt16 ();
								room.Boxes.Add (box);
								size -= 20;
							}

							if (size > 0) {
								room.BoxMatrix.Clear ();
								room.BoxMatrix.AddRange (_reader.ReadBytes (size));
							}
						}
						break;
					case 0x4150:
						{
							// CLUT
							var colors = ReadCLUT ();
							room.HasPalette = true;
							Array.Copy (colors, room.Palette.Colors, colors.Length);
						}
						break;
					case 0x4153:
                            // SCAL
						if (it.Current.Size > 6) {
							room.Scales = ReadSCAL ();
						}
						break;
					case 0x4D42:
                            // BM (IM00)
						if (it.Current.Size > 8) {
							room.Data = _reader.ReadBytes ((int)(it.Current.Size - 6));
						}
						break;
					case 0x4E45:
						{
							// Entry script
							byte[] entryScript = _reader.ReadBytes ((int)(it.Current.Size - 6));
							if (room.EntryScript.Data == null) {
								room.EntryScript.Data = entryScript;
							} else {
								throw new NotSupportedException ("Entry script has already been defined.");
							}
						}
						break;
					case 0x5845:
						{
							// Exit script
							byte[] exitScript = _reader.ReadBytes ((int)(it.Current.Size - 6));
							if (room.ExitScript.Data == null) {
								room.ExitScript.Data = exitScript;
							} else {
								throw new NotSupportedException ("Exit script has already been defined.");
							}
						}
						break;
					case 0x4C53:
						{
							// *SL* 
							_reader.ReadByte ();
						}
						break;
					case 0x434C: //LC
						{
							// *NLSC* number of local scripts
							_reader.ReadUInt16 ();
						}
						break;
					case 0x534C:
						{
							// local scripts
							var index = _reader.ReadByte ();
							var pos = _reader.BaseStream.Position;
							room.LocalScripts [index - 0xC8] = new ScriptData {
								Offset = pos - roomOffset - 8,
								Data = _reader.ReadBytes ((int)(it.Current.Size - 7))
							};
						}
						break;
					case 0x494F:
						{
							// Object Image
							var objId = _reader.ReadUInt16 ();
							if (it.Current.Size > 8) {
								stripsDic.Add (objId, _reader.ReadBytes ((int)(it.Current.Size - 6)));
							}
						}
						break;
					case 0x434F:
						{
							// Object script
							var objId = _reader.ReadUInt16 ();
							_reader.ReadByte ();
							var x = _reader.ReadByte ();
							var tmp = _reader.ReadByte ();
							var y = tmp & 0x7F;
							byte parentState = (byte)(((tmp & 0x80) != 0) ? 1 : 0);
							var width = _reader.ReadByte ();
							var parent = _reader.ReadByte ();
							var walk_x = _reader.ReadInt16 ();
							var walk_y = _reader.ReadInt16 ();
							tmp = _reader.ReadByte ();
							byte height = (byte)(tmp & 0xF8);
							byte actordir = (byte)(tmp & 0x07);

							var data = new ObjectData ();
							data.Number = objId;
							data.Position = new Point ((short)(8 * x), (short)(8 * y));
							data.Width = (ushort)(8 * width);
							data.Height = height;
							data.Parent = parent;
							data.ParentState = parentState;
							data.Walk = new Point (walk_x, walk_y);
							data.ActorDir = actordir;
							room.Objects.Add (data);

							var nameOffset = _reader.ReadByte ();
							var size = nameOffset - 6 - 13;
							ReadVerbTable (data, size);
							data.Name = ReadObjectName (it, nameOffset);
							// read script
							size = (int)(it.Current.Offset + it.Current.Size - 6 - _reader.BaseStream.Position);
							data.Script.Data = _reader.ReadBytes (size);
							data.Script.Offset = nameOffset + data.Name.Length + 1;

							SetObjectImage (stripsDic, data);
						}
						break;
					//case 0x4F53:
					//    {
					//        // SO
					//        its.Push(it);
					//        it = new ChunkIterator(_reader, it.Current.Size);
					//    }
					//    break;
					default:
						System.Diagnostics.Debug.WriteLine ("Ignoring Resource Tag: {0:X2} ({2}{3}), Size: {1:X4}",
							it.Current.Tag, it.Current.Size, (char)(it.Current.Tag & 0x00FF), (char)(it.Current.Tag >> 8));
						break;
					}
				}
				it = its.Pop ();
			} while (its.Count > 0);

			return room;
		}

		protected virtual void GotoResourceHeader (long offset)
		{
			_reader.BaseStream.Seek (offset, SeekOrigin.Begin);
		}

		public XorReader ReadCostume (long costOffset)
		{
			GotoResourceHeader (costOffset + 4);
			var tag = _reader.ReadInt16 ();
			if (tag != 0x4F43)
				throw new NotSupportedException ("Invalid costume.");
			return _reader;
		}

		public byte[] ReadScript (long roomOffset)
		{
			GotoResourceHeader (roomOffset);
			long size = _reader.ReadUInt32 ();
			var tag = _reader.ReadInt16 ();
			if (tag != 0x4353)
				throw new NotSupportedException ("Expected SC block.");
			var data = _reader.ReadBytes ((int)(size - 6));
			return data;
		}

		public byte[] ReadSound (long roomOffset)
		{
			GotoResourceHeader (roomOffset);
			long size = _reader.ReadUInt32 ();
			var tag = _reader.ReadInt16 ();
			if (tag != 0x4F53)
				throw new NotSupportedException ("Expected SO block.");
			var totalSize = size - 6;
			while (totalSize > 0) {
				size = _reader.ReadUInt32 ();
				tag = _reader.ReadInt16 ();
				if (tag == 0x4F53) {
					totalSize -= 6;
				} else if (tag == 0x4441) {
					_reader.BaseStream.Seek (-6, SeekOrigin.Current);
					return _reader.ReadBytes ((int)size);
				} else {
					totalSize -= size;
					_reader.BaseStream.Seek (size - 6, SeekOrigin.Current);
				}

			}
			return null;
		}

		#endregion

		#region Private Methods

		byte[] ReadObjectName (IEnumerator<Chunk> it, byte nameOffset)
		{
			_reader.BaseStream.Seek (it.Current.Offset + nameOffset - 6, SeekOrigin.Begin);
			var name = new List<byte> ();
			var c = _reader.ReadByte ();
			while (c != 0) {
				name.Add (c);
				c = _reader.ReadByte ();
			}
			return name.ToArray ();
		}

		void ReadVerbTable (ObjectData data, int size)
		{
			var tableLength = (size - 1) / 3;
			for (int i = 0; i < tableLength; i++) {
				var id = _reader.ReadByte ();
				var offset = _reader.ReadUInt16 ();
				data.ScriptOffsets.Add (id, offset);
			}
			_reader.ReadByte ();
		}

		static void SetObjectImage (IDictionary<ushort, byte[]> stripsDic, ObjectData obj)
		{
			if (stripsDic.ContainsKey (obj.Number)) {
				var stripData = stripsDic [obj.Number];
				obj.Image = stripData;
			} else {
				obj.Image = new byte[0];
			}
		}

		RoomHeader ReadRMHD ()
		{
			var header = new RoomHeader {
				Width = _reader.ReadUInt16 (),
				Height = _reader.ReadUInt16 (),
				NumObjects = _reader.ReadUInt16 ()
			};
			return header;
		}

		ColorCycle[] ReadCYCL ()
		{
			var colorCycle = new ColorCycle[16];
			for (int i = 0; i < 16; i++) {
				var delay = ScummHelper.SwapBytes (_reader.ReadUInt16 ());
				var start = _reader.ReadByte ();
				var end = _reader.ReadByte ();

				colorCycle [i] = new ColorCycle ();

				if (delay == 0 || delay == 0x0aaa || start >= end)
					continue;

				colorCycle [i].Counter = 0;
				colorCycle [i].Delay = (ushort)(16384 / delay);
				colorCycle [i].Flags = 2;
				colorCycle [i].Start = start;
				colorCycle [i].End = end;
			}

			return colorCycle;
		}

		ScaleSlot[] ReadSCAL ()
		{
			var scales = new ScaleSlot[4];
			for (int i = 0; i < 4; i++) {
				var scale1 = _reader.ReadUInt16 ();
				var y1 = _reader.ReadUInt16 ();
				var scale2 = _reader.ReadUInt16 ();
				var y2 = _reader.ReadUInt16 ();
				scales [i] = new ScaleSlot { Scale1 = scale1, Y1 = y1, Y2 = y2, Scale2 = scale2 };
			}
			return scales;
		}

		byte[] ReadEPAL ()
		{
			return _reader.ReadBytes (256);
		}

		Color[] ReadCLUT ()
		{
			var numColors = _reader.ReadUInt16 () / 3;
			var colors = new Color[numColors];
			for (int i = 0; i < numColors; i++) {
				colors [i] = Color.FromRgb (_reader.ReadByte (), _reader.ReadByte (), _reader.ReadByte ());
			}
			return colors;
		}

		#endregion
	}
}
