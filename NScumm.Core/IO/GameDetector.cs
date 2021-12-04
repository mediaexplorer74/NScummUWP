//  GameDetector.cs
//  Author: Scemino <scemino74@gmail.com>

using System.Collections.Generic;
using System.Linq;

namespace NScumm.Core.IO
{
    public class GameDetector
    {
        private List<IMetaEngine> _engines;

        public void Add(IMetaEngine engine)
        {
            if (_engines == null) _engines = new List<IMetaEngine>();
            _engines.Add(engine);
        }

        public GameDetected DetectGame(string path)
        {
            return _engines.Select(e => e.DetectGame(path)).FirstOrDefault(o => o != null && o.Game != null);
        }
    }
}
