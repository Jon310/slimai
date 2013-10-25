using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlimAI.GUI;
using SlimAI.Helpers;
using SlimAI.Managers;
using SlimAI.Settings;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.WoWInternals.WoWObjects;

namespace SlimAI
{
    public partial class SlimAI : CombatRoutine
    {
        public override sealed string Name { get { return "SlimAI [" + Me.Specialization + "]"; } }
        public override WoWClass Class { get { return StyxWoW.Me.Class; } }
        private static readonly LocalPlayer Me = StyxWoW.Me;

        public override bool WantButton { get { return true; } }
        public static SlimAI Instance { get; set; }
        public SlimAI() { Instance = this; }

        public override void Initialize()
        {
            RegisterHotkeys();
            LuaCore.PopulateSecondryStats();
            TalentManager.Init();
            TalentManager.Update();
            UpdateContext();
            BotEvents.Player.OnMapChanged += e => UpdateContext();
            OnWoWContextChanged += (orig, ne) =>
            {
                Logging.Write("Context changed, re-creating behaviors");
                AssignBehaviors();
                Spell.GcdInitialize();
                Lists.BossList.Init();
            };
            Spell.GcdInitialize();
            Dispelling.Init();
            EventHandlers.Init();
            Lists.BossList.Init();
            Instance.AssignBehaviors();
            Logging.Write("Initialization Completed");
        }

        public override void Pulse()
        {
            if (!StyxWoW.IsInGame || !StyxWoW.IsInWorld)
                return;
            if (TalentManager.Pulse())
                return;
            UpdateContext();
            Spell.DoubleCastPreventionDict.RemoveAll(t => DateTime.UtcNow > t);
            switch (StyxWoW.Me.Class)
            {
                case WoWClass.Hunter:
                case WoWClass.DeathKnight:
                case WoWClass.Warlock:
                case WoWClass.Mage:
                    PetManager.Pulse();
                    break;
            }
            if (HealerManager.NeedHealTargeting)
                HealerManager.Instance.Pulse();
        }

        public override void ShutDown()
        {
            UnregisterHotkeys();
        }

        private SlimAIGUI _configForm;
        public override void OnButtonPress()
        {
            if (_configForm == null || _configForm.IsDisposed || _configForm.Disposing)
            {
                _configForm = new SlimAIGUI();
                _configForm.Height = GeneralSettings.Instance.FormHeight;
                _configForm.Width = GeneralSettings.Instance.FormWidth;
                TabControl tab = (TabControl)_configForm.Controls["tabControl1"];
                tab.SelectedIndex = GeneralSettings.Instance.FormTabIndex;
            }

            _configForm.Show();
        }

        static int countRentrancyStopBot = 0;
        private static void StopBot(string reason)
        {
            if (!TreeRoot.IsRunning)
                reason = "Bot Cannot Run: " + reason;
            else
            {
                reason = "Stopping Bot: " + reason;
                if (countRentrancyStopBot == 0)
                {
                    countRentrancyStopBot++;
                    if (TreeRoot.Current != null)
                        TreeRoot.Current.Stop();

                    TreeRoot.Stop();
                }
            }
            Logging.Write(reason);
        }
    }
}
