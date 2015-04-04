//
//  TrsFile.cs
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
using System.IO;
using NScumm.Core.Audio;
using System.Threading;
using NScumm.Core.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

namespace NScumm.Core.Smush
{
    class TrsFile
    {
        static readonly Regex DefRegex = new Regex(@"^#.*?\s+(\d+)", RegexOptions.Singleline);
        static readonly Regex TextRegex = new Regex(@"(?://)?(.+)", RegexOptions.Singleline);
        IDictionary<int, string> _texts;

        private TrsFile(IDictionary<int, string> texts)
        {
            if (texts == null)
                throw new ArgumentNullException("texts");
            _texts = texts;
        }

        class XorStream:Stream
        {
            public Stream Stream { get; private set; }

            public byte EncodedByte { get; private set; }

            public XorStream(Stream stream, byte encodedByte)
            {
                Stream = stream;
                EncodedByte = encodedByte;
            }

            #region implemented abstract members of Stream

            public override void Flush()
            {
                Stream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = Stream.Read(buffer, offset, count);
                for (int i = 0; i < read; i++)
                {
                    buffer[i] ^= EncodedByte;
                }
                return read;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return Stream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                Stream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    buffer[offset + i] ^= EncodedByte;
                }
                Stream.Write(buffer, offset, count);
            }

            public override bool CanRead
            {
                get
                {
                    return Stream.CanRead;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return Stream.CanSeek;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return Stream.CanWrite;
                }
            }

            public override long Length
            {
                get
                {
                    return Stream.Length;
                }
            }

            public override long Position
            {
                get
                {
                    return Stream.Position;
                }
                set
                {
                    Stream.Position = value;
                }
            }

            #endregion
        }

        public static TrsFile Load(string filename)
        {
            TrsFile file;
            using (var f = ServiceLocator.FileStorage.OpenFileRead(filename))
            using (var sr = new StreamReader(f, Encoding.GetEncoding("iso-8859-1")))
            {
                file = Load(sr);
            }
            return file;
        }

        public static TrsFile LoadEncoded(string filename)
        {
            TrsFile file;
            using (var f = ServiceLocator.FileStorage.OpenFileRead(filename))
            {
                var sig = new byte[4];
                f.Read(sig, 0, 4);
                if (System.Text.Encoding.UTF8.GetString(sig) == "ETRS")
                {
                    f.Seek(16, SeekOrigin.Begin);
                    file = Load(new StreamReader(new XorStream(f, 0xCC)));
                }
                else
                {
                    f.Position = 0;
                    file = Load(new StreamReader(f));
                }
            }
            return file;
        }

        public static TrsFile Load(StreamReader reader)
        {
            string line;
            var texts = new Dictionary<int, string>();
            while ((line = reader.ReadLine()) != null)
            {
                var m = TrsFile.DefRegex.Match(line);
                if (m.Success)
                {
                    var id = int.Parse(m.Groups[1].Value);
                    line = reader.ReadLine();
                    var m2 = TrsFile.TextRegex.Match(line);
                    texts.Add(id, m2.Groups[1].Value);
                }
            }

            return new TrsFile(texts);
        }

        public string this [int id]
        {
            get
            {
                if (_texts.ContainsKey(id))
                {
                    return _texts[id];
                }
                return string.Empty;
            }
        }
    }
}
