using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlimAI.Helpers;
using SlimAI.Managers;
using SlimAI.Settings;
using Styx;
using SlimAI.Class;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;


namespace SlimAI.Class.Warrior
{
    class ArmsWarrior
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static bool HasTalent(WarriorTalents tal) { return TalentManager.IsSelected((int)tal); }
        private static WarriorSettings Settings { get { return GeneralSettings.Instance.Warrior(); } }


        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms)]
        public static Composite ArmsCombat()
        {
            return new PrioritySelector(
                new Decorator(ret => Me.HasAura("Dire Fixation"),
                    new PrioritySelector(
                        BossMechs.HorridonHeroic())),
                new Throttle(1,1,
                    Common.CreateInterruptBehavior()),
                Spell.Cast(VictoryRush, ret => Me.HealthPercent <= 90 && Me.HasAura("Victorious")),
                Spell.Cast(DieByTheSword, ret => Me.HealthPercent <= 20),
                Item.UsePotionAndHealthstone(50),
                DemoBanner(),
                HeroicLeap(),
                new Decorator(ret => Unit.UnfriendlyUnits(8).Count() >= 4,
                            CreateAoe()),
                new Decorator(ret => SlimAI.Burst && Me.CurrentTarget.HasMyAura("Colossus Smash"),
                    new PrioritySelector(
                        Spell.Cast(Recklessness),
                        Spell.Cast(Avatar),
                        Spell.Cast(SkullBanner))),
                Spell.Cast(BloodBath),
                new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                Spell.Cast(BerserkerRage, ret => !Me.HasAura(Enrage)),
                Spell.Cast(SweepingStrikes, ret => Unit.UnfriendlyUnits(8).Count() >= 2),
                Spell.Cast(HeroicStrike, ret => (Me.CurrentTarget.HasAura("Colossus Smash") && Me.CurrentRage >= 80 && Me.CurrentTarget.HealthPercent >= 20) || Me.CurrentRage >= 105, true),
                Spell.Cast(MortalStrike),
                Spell.Cast(DragonRoar, ret => !Me.CurrentTarget.HasAura("Colossus Smash") && Me.HasAura("Bloodbath") && Me.CurrentTarget.Distance <= 8),
                Spell.Cast(ColossusSmash, ret => Me.CurrentTarget.HasAuraExpired("Colossus Smash") || !Me.CurrentTarget.HasMyAura("Colossus Smash")),
                Spell.Cast(Execute, ret => Me.CurrentTarget.HasMyAura("Colossus Smash") || Me.HasAura("Recklessness") || Me.CurrentRage >= 95),
                Spell.Cast(DragonRoar, ret => (!Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.CurrentTarget.HealthPercent < 20) || (Me.HasAura("Bloodbath") && Me.CurrentTarget.HealthPercent >= 20) && Me.CurrentTarget.Distance <= 8),
                Spell.Cast(ThunderClap, ret => Unit.UnfriendlyUnits(8).Count() >= 3 && Clusters.GetCluster(Me, Unit.UnfriendlyUnits(8), ClusterType.Radius, 8).Any(u => !u.HasAura("Deep Wounds"))),
                Spell.Cast(Slam, ret => (Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.HasAura("Recklessness")) && Me.CurrentTarget.HealthPercent >= 20),
                Spell.Cast(Overpower, ret => Me.HasAura("Taste for Blood", 3) && Me.CurrentTarget.HealthPercent >= 20 || Me.HasAura("Sudden Execute")),
                Spell.Cast(Execute, ret => !Me.HasAura("Sudden Execute")),
                Spell.Cast(Slam, ret => Me.CurrentRage >= 40 && Me.CurrentTarget.HealthPercent >= 20),
                Spell.Cast(Overpower, ret => Me.CurrentTarget.HealthPercent >= 20),
                Spell.Cast(BattleShout),
                Spell.Cast(HeroicThrow),
                Spell.Cast(ImpendingVictory, ret => Me.HealthPercent < 50));
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms)]
        public static Composite ArmsPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.Cast(BattleShout, ret => !Me.HasPartyBuff(PartyBuffType.AttackPower)));
        }

        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorArms)]
        public static Composite ArmsPull()
        {
            return new PrioritySelector(
                Spell.Cast(Charge));
        }

        private static Composite CreateAoe()
        {
            return new PrioritySelector(
                new Decorator(ret => SlimAI.Burst && Me.CurrentTarget.HasAura("Colossus Smash"),
                    new PrioritySelector(
                        Spell.Cast(Recklessness),
                        Spell.Cast(Avatar),
                        Spell.Cast(SkullBanner))),
                Spell.Cast(BloodBath),
                new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                Spell.Cast(BerserkerRage, ret => !Me.HasAura(Enrage)),
                Spell.Cast(SweepingStrikes),
                Spell.Cast(Bladestorm),
                Spell.Cast(Whirlwind, ret => (Me.CurrentTarget.HasAura("Colossus Smash") && Me.CurrentRage >= 80 && Me.CurrentTarget.HealthPercent >= 20) || Me.CurrentRage >= 105),
                Spell.Cast(MortalStrike),
                Spell.Cast(DragonRoar, ret => !Me.CurrentTarget.HasAura("Colossus Smash") && Me.HasAura("Bloodbath") && Me.CurrentTarget.Distance <= 8),
                Spell.Cast(ColossusSmash, ret => Me.HasAuraExpired("Colossus Smash")),
                Spell.Cast(Execute, ret => Me.CurrentTarget.HasAura("Colossus Smash") || Me.HasAura("Recklessness") || Me.CurrentRage >= 95),
                Spell.Cast(DragonRoar, ret => (!Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.CurrentTarget.HealthPercent < 20) || (Me.HasAura("Bloodbath") && Me.CurrentTarget.HealthPercent >= 20) && Me.CurrentTarget.Distance <= 8),
                Spell.Cast(ThunderClap, ret => Unit.UnfriendlyUnits(8).Count() >= 3 && Clusters.GetCluster(Me, Unit.UnfriendlyUnits(8), ClusterType.Radius, 8).Any(u => !u.HasAura("Deep Wounds"))),
                Spell.Cast(Slam, ret => (Me.CurrentTarget.HasAura("Colossus Smash") && Me.HasAura("Recklessness")) && Me.CurrentTarget.HealthPercent >= 20),
                Spell.Cast(Overpower, ret => Me.HasAura("Taste for Blood", 3) && Me.CurrentTarget.HealthPercent >= 20),
                Spell.Cast(Execute, ret => !Me.HasAura("Sudden Execute")),
                Spell.Cast(Overpower, ret => Me.CurrentTarget.HealthPercent >= 20 || Me.HasAura("Sudden Execute")),
                Spell.Cast(Whirlwind, ret => Me.CurrentRage >= 40 && Me.CurrentTarget.HealthPercent >= 20),
                Spell.Cast(BattleShout),
                Spell.Cast(HeroicThrow),
                Spell.Cast(ImpendingVictory, ret => Me.HealthPercent < 50));
        }

        private static Composite HeroicLeap()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Heroic Leap") &&
                    Lua.GetReturnVal<bool>("return IsLeftAltKeyDown() and not GetCurrentKeyBoardFocus()", 0),
                    new Action(ret =>
                    {
                        SpellManager.Cast("Heroic Leap");
                        Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");
                    }));
        }

        private static Composite DemoBanner()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Demoralizing Banner") &&
                    KeyboardPolling.IsKeyDown(Keys.Z),
                    new Action(ret =>
                    {
                        SpellManager.Cast("Demoralizing Banner");
                        Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");
                    }));
        }

        #region WarriorTalents
        enum WarriorTalents
        {
            None = 0,
            Juggernaut,
            DoubleTime,
            Warbringer,
            EnragedRegeneration,
            SecondWind,
            ImpendingVictory,
            StaggeringShout,
            PiercingHowl,
            DisruptingShout,
            Bladestorm,
            Shockwave,
            DragonRoar,
            MassSpellReflection,
            Safeguard,
            Vigilance,
            Avatar,
            Bloodbath,
            StormBolt
        }
        #endregion

        #region Warrior Spells
        private const int Avatar = 107574,
                          BattleShout = 6673,
                          Bladestorm = 46924,
                          BloodBath = 12292,
                          BerserkerRage = 18499,
                          Charge = 100,
                          Cleave = 845,
                          ColossusSmash = 86346,
                          DieByTheSword = 118038,
                          DragonRoar = 118000,
                          Enrage = 12880,
                          Execute = 5308,
                          HeroicStrike = 78,
                          HeroicThrow = 57755,
                          ImpendingVictory = 103840,
                          MortalStrike = 12294,
                          Overpower = 7384,
                          Recklessness = 1719,
                          SkullBanner = 114207,
                          Slam = 1464,
                          SweepingStrikes = 12328,
                          ThunderClap = 6343,
                          VictoryRush = 34428,
                          Whirlwind = 1680;
        #endregion
    }
}
