using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimAI.Helpers;
using SlimAI.Managers;
using SlimAI.Settings;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace SlimAI.Class.Monk
{
    class MistweaverMonk
    {
        private static readonly LocalPlayer Me = StyxWoW.Me;
        static WoWUnit healtarget { get { return HealerManager.FindLowestHealthTarget(); } }
        private static MonkSettings Settings { get { return GeneralSettings.Instance.Monk(); } }

        [Behavior(BehaviorType.Combat | BehaviorType.Heal, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        public static Composite MistweaverCombat()
        {
            HealerManager.NeedHealTargeting = true;
            return new PrioritySelector(
                Spell.Cast(ExpelHarm, ret => Me.MaxChi - Me.CurrentChi > 1),
                Spell.Cast(ManaTea, ret => Me.GetAuraStacks("Mana Tea") > 2 && Me.ManaPercent < 90),

                new Throttle(1, 15,
                    Spell.CastOnGround("Summon Jade Serpent Statue", on => Me.Location, ret => NeedSerpentStatue())),

                Spell.Cast(LifeCocoon, 
                    on => healtarget, 
                    ret => healtarget.HealthPercent < 30),

                Spell.Cast(Revival, ret => HealerManager.GetCountWithHealth(60) > 3),

                Spell.Cast(RenewingMist, 
                    on => healtarget, 
                    ret => !healtarget.HasAura("Renewing Mist")),

                new Sequence(ret => HealerManager.GetCountWithBuffAndHealth("Rnewing Mist", 85) > 3,
                    Spell.Cast(ThunderFocusTea),
                    Spell.Cast(Uplift)),

                Spell.Cast(Uplift, ret => HealerManager.GetCountWithBuffAndHealth("Renewing Mist", 90) > 2),
                    
                Spell.Cast(SurgingMist, 
                    on => healtarget, 
                    ret => Me.HasAura("Vital Mists") && healtarget.HealthPercent < 40),

                new Sequence(ret => healtarget.HealthPercent < 40,
                    Spell.Cast(SoothingMist, on => healtarget),
                    Spell.Cast(SurgingMist, on => healtarget)),

                new Sequence(ret => healtarget.HealthPercent < 60,
                    Spell.Cast(SoothingMist, on => healtarget),
                    Spell.Cast(EnvelopingMist, on => healtarget),
                    Spell.Cast(SurgingMist, on => healtarget)),

                new Sequence(ret => healtarget.HealthPercent < 70,
                    Spell.Cast(SoothingMist, on => healtarget),
                    Spell.Cast(EnvelopingMist, on => healtarget)),

                Spell.Cast(SoothingMist,
                    on => healtarget.HealthPercent < 95),

                Spell.Cast(BlackoutKick, ret => Me.HasAura("Muscle Memory") && !Me.HasAura("Serpent's Zeal")),
                Spell.Cast(TigerPalm, ret => Me.HasAura("Muscle Memory") && !Me.HasAura("Tiger Power")),
                Spell.Cast(Jab, ret => healtarget.HealthPercent > 95),
                Spell.Cast(CracklingJadeLightning, ret => healtarget.HealthPercent > 98),
                Spell.CastOnGround("Healing Sphere", on => healtarget.Location, ret => Me.IsMoving && healtarget.HealthPercent < 75));
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Monk, WoWSpec.MonkMistweaver)]
        public static Composite MistweaverPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.Cast(StanceoftheWiseSerpent, ret => !Me.HasAura("Stance of the Wise Serpent")));
        }

        #region SerpentStatue
        private static readonly WaitTimer SerpentStatueTimer = new WaitTimer(TimeSpan.FromSeconds(45));
        private static bool NeedSerpentStatue()
        {
            // Dont do shit if its not been x seconds.
            if (!SerpentStatueTimer.IsFinished)
                return false;

            // derp..
            if (!Me.Combat || Me.IsMoving)
                return false;

            // Spell on cooldown derp..
            if (SpellManager.Spells["Summon Jade Serpent Statue"].Cooldown)
                return false;

            // Grab dat..fast!
            var serpentStatue = ObjectManager.GetObjectsOfTypeFast<WoWUnit>().FirstOrDefault(p => p.CreatedByUnitGuid == Me.Guid && p.Entry == 60849);

            // if we dont have one..then get dat shit.
            if (serpentStatue == null)
            {
                SerpentStatueTimer.Reset();
                return true;
            }

            // Distance...
            if (serpentStatue.Distance > 60)
            {
                SerpentStatueTimer.Reset();
                return true;
            }

            return false;
        }
        #endregion SerpentStatue

        #region MonkTalents
        enum MonkTalents
        {
            None = 0,
            Celerity,
            TigersLust,
            Momentum,
            ChiWave,
            ZenSphere,
            ChiBurst,
            PowerStrikes,
            Ascention,
            ChiBrew,
            RingofPeace,
            ChargingOxWave,
            LegSweep,
            HealingElixirs,
            DampenHarm,
            DiffuseMagic,
            RushingJadeWind,
            InvokeXuentheWhiteTiger,
            ChiTorpedo
        }
        #endregion

        #region Monk Spells
        private const int BlackoutKick = 100784,
                          BreathofFire = 115181,
                          ChiWave = 115098,
                          CracklingJadeLightning = 117952,
                          ElusiveBrew = 115308,
                          EnergizingBrew = 115288,
                          EnvelopingMist = 124682,
                          ExpelHarm = 115072,
                          FistsofFury = 113656,
                          Guard = 115295,
                          InvokeXuentheWhiteTiger = 123904,
                          Jab = 100780,
                          KegSmash = 121253,
                          LegacyoftheEmperor = 115921,
                          LegacyoftheWhiteTiger = 116781,
                          LifeCocoon = 116849,
                          ManaTea = 115294,
                          PurifyingBrew = 119582,
                          RenewingMist = 115151,
                          Revival = 115310,
                          RisingSunKick = 107428,
                          RushingJadeWind = 116847,
                          SoothingMist = 115175,
                          SpearHandStrike = 116705,
                          SpinningCraneKick = 101546,
                          StanceoftheSturdyOx = 115069,
                          StanceoftheWiseSerpent = 115070,
                          SummonBlackOxStatue = 115315,
                          SurgingMist = 116694,
                          ThunderFocusTea = 116680,
                          TigereyeBrew = 116740,
                          TigerPalm = 100787,
                          TouchofDeath = 115080,
                          Uplift = 116670,
                          ZenSphere = 124081;
        #endregion
    }
}
