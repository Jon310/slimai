using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlimAI.Settings
{
    class HotkeySettings : Styx.Helpers.Settings
    {
        public HotkeySettings() :  base(Path.Combine(GeneralSettings.SlimAISettingsPath, "Hotkey.xml")) {}
    }
}
