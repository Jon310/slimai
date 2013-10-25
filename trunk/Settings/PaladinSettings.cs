using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlimAI.Settings
{
    class PaladinSettings : Styx.Helpers.Settings
    {
        public PaladinSettings() : base(Path.Combine(GeneralSettings.SlimAISettingsPath, "Paladin.xml")) { }
    }
}
