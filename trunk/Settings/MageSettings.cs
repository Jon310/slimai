using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlimAI.Settings
{
    class MageSettings : Styx.Helpers.Settings
    {
        public MageSettings() : base(Path.Combine(GeneralSettings.SlimAISettingsPath, "Mage.xml")) { }
    }
}
