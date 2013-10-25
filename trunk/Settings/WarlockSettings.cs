using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlimAI.Settings
{
    class WarlockSettings : Styx.Helpers.Settings
    {
        public WarlockSettings() : base(Path.Combine(GeneralSettings.SlimAISettingsPath, "Warlock.xml")) { }
    }
}
