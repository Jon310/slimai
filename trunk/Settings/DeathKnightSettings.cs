using System;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using Styx.Helpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace SlimAI.Settings
{
    class DeathKnightSettings : Styx.Helpers.Settings
    {
        public DeathKnightSettings() : base(Path.Combine(GeneralSettings.SlimAISettingsPath, "DeathKnight.xml")) {}

        [Setting]
        [DefaultValue(false)]
        [Category("Common")]
        [DisplayName("Dark Command Always")]
        public bool UseDarkCommand { get; set; }
    }
}
