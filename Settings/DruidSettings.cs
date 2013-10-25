using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlimAI.Settings
{
    class DruidSettings : Styx.Helpers.Settings
    {
        public DruidSettings() :  base(Path.Combine(GeneralSettings.SlimAISettingsPath, "Druid.xml")) {}
    }
}
