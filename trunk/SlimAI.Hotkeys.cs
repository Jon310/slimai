using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Styx.Common;
using Styx.WoWInternals;

namespace SlimAI
{
    partial class SlimAI
    {
        public static bool PvPRotation { get; set; }
        public static bool PvERotation { get; set; }
        public static bool AFK { get; set; }
        public static bool Trace { get; set; }
        public static bool Burst { get; set; }
        public static bool AOE { get; set; }
        public static bool Weave { get; set; }
        public static bool Dispell { get; set; }

        protected virtual void UnregisterHotkeys()
        {
            HotkeysManager.Unregister("PvP Toggle");
            HotkeysManager.Unregister("PvE Toggle");
            HotkeysManager.Unregister("AFK Toggle");
            HotkeysManager.Unregister("Trace Toggle");
            HotkeysManager.Unregister("Burst Toggle");
            HotkeysManager.Unregister("AOE Toggle");
            HotkeysManager.Unregister("Weave Toggle");
            HotkeysManager.Unregister("Dispell Toggle");
        }

        protected virtual void RegisterHotkeys()
        {
            HotkeysManager.Register("PvP Toggle",
            Keys.P,
            ModifierKeys.Alt,
            o =>
            {
                PvPRotation = !PvPRotation;
                Logging.Write("PvP enabled: " + PvPRotation);
                Lua.DoString("print('PvP Enabled: " + PvPRotation + "')");
            });
            PvPRotation = false;

            HotkeysManager.Register("PvE Toggle",
            Keys.O,
            ModifierKeys.Alt,
            o =>
            {
                PvERotation = !PvERotation;
                Logging.Write("PvE enabled: " + PvERotation);
                Lua.DoString("print('PvE Enabled: " + PvERotation + "')");
            });
            PvERotation = false;

            HotkeysManager.Register("AFK Toggle",
            Keys.NumPad7,
            ModifierKeys.Control,
            o =>
            {
                AFK = !AFK;
                Logging.Write("AFK enabled: " + AFK);
                Lua.DoString("print('AFK Enabled: " + AFK + "')");
            });
            AFK = false;

            HotkeysManager.Register("Trace Toggle",
            Keys.NumPad8,
            ModifierKeys.Control,
            o =>
            {
                Trace = !Trace;
                Logging.Write("Trace enabled: " + Trace);
                Lua.DoString("print('Trace Enabled: " + Trace + "')");
            });
            Trace = false;

            HotkeysManager.Register("Burst Toggle",
            Keys.NumPad1,
            ModifierKeys.Control,
            o =>
            {
                Burst = !Burst;
                Logging.Write("Burst enabled: " + Burst);
                Lua.DoString("print('Burst Enabled: " + Burst + "')");
            });
            Burst = true;

            HotkeysManager.Register("AOE Toggle",
            Keys.NumPad4,
            ModifierKeys.Control,
            o =>
            {
                AOE = !AOE;
                Logging.Write("AOE enabled: " + AOE);
                Lua.DoString("print('AOE Enabled: " + AOE + "')");
            });
            AOE = true;

            HotkeysManager.Register("Weave Toggle",
            Keys.NumPad3,
            ModifierKeys.Control,
            o =>
            {
                Weave = !Weave;
                Logging.Write("Weave enabled: " + Weave);
                Lua.DoString("print('Weave Enabled: " + Weave + "')");
            });
            Weave = true;

            HotkeysManager.Register("Dispell Toggle",
            Keys.NumPad5,
            ModifierKeys.Control,
            o =>
            {
                Dispell = !Dispell;
                Logging.Write("Dispelling enabled: " + Dispell);
                Lua.DoString("print('Dispelling Enabled: " + Dispell + "')");
            });
            Dispell = true;
        }

    }
}
