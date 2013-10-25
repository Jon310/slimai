using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlimAI.Settings
{
    class ShamanSettings : Styx.Helpers.Settings
    {
        public ShamanSettings() : base(Path.Combine(GeneralSettings.SlimAISettingsPath, "Shaman.xml")) { }
    }
}
