using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlimAI.Settings
{
    class PriestSettings : Styx.Helpers.Settings
    {
        public PriestSettings() : base(Path.Combine(GeneralSettings.SlimAISettingsPath, "Priest.xml")) { }
    }
}
