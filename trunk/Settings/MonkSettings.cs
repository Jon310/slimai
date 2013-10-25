using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlimAI.Settings
{
    class MonkSettings : Styx.Helpers.Settings
    {
        public MonkSettings() : base(Path.Combine(GeneralSettings.SlimAISettingsPath, "Monk.xml")) { }
    }
}
