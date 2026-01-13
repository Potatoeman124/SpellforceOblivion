using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpellforceDataEditor.OblivionScripts
{
    public class ProgressInfo
    {
        public static int ProgressUpdateInterval = 25;
        public string Phase { get; set; } = "";
        public int Current { get; set; }
        public int Total { get; set; }
        public string Detail { get; set; } = "";

        public int Percent => Total <= 0 ? 0 : (int)(100.0 * Current / Total);
    }
}
