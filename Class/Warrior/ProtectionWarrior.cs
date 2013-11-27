using System.Linq;
using System.Windows.Forms;
using SlimAI.Managers;
using CommonBehaviors.Actions;
using SlimAI.Helpers;
using SlimAI.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Class.Warrior
{
    class ProtectionWarrior
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings Settings { get { return GeneralSettings.Instance.Warrior(); } }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorProtection)]
        public static Composite ProtCombat()
        {
            HealerManager.NeedHealTargeting = true;
            return new PrioritySelector(
                    new Decorator(ret => Me.CurrentTarget != null && !Me.CurrentTarget.IsAlive && Me.IsCasting,
                        new ActionAlwaysSucceed()),
                    new Decorator(ret => Me.HasAura("Dire Fixation"),
                        new PrioritySelector(
                            BossMechs.HorridonHeroic())),
                    new Throttle(1, 1,
                        new PrioritySelector(
                            Common.CreateInterruptBehavior())),
                    new Decorator(ret => SlimAI.Burst && Me.CurrentTarget.IsWithinMeleeRange,
                        new PrioritySelector(
                            Spell.Cast(Recklessness),
                            Spell.Cast(BloodBath),
                            new Decorator(ret => Me.HasAura("Recklessness"),
                                new PrioritySelector(
                                    Spell.Cast(Avatar),
                                    Spell.Cast(SkullBanner))))),
                    //new Throttle(1,
                    //    Item.UsePotionAndHealthstone(40)),
                    new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                    DemoBanner(),
                    Leap(),
                    Mocking(),
                    //CD's all bout living
                    Spell.Cast(VictoryRush, ret => Me.HealthPercent <= 90 && Me.HasAura("Victorious")),
                    Spell.Cast(BerserkerRage, ret => NeedZerker()),
                    Spell.Cast(EnragedRegeneration, ret => NeedEnrageRegen()),
                    Spell.Cast(LastStand, ret => Me.HealthPercent <= 15 && !Me.HasAura("Shield Wall")),
                    Spell.Cast(ShieldWall, ret => Me.HealthPercent <= 30 && !Me.HasAura("Last Stand")),

                    //Might need some testing
                    new Throttle(1, 1,
                        new PrioritySelector(
                            Spell.Cast(RallyingCry, ret => HealerManager.GetCountWithHealth(55) > 4),
                            Spell.Cast(DemoralizingShout, ret => Unit.UnfriendlyUnits(10).Any() && IsCurrentTank()))),

                    Spell.Cast(ShieldBlock, ret => !Me.HasAura("Shield Block") && IsCurrentTank() && SlimAI.Weave),
                    Spell.Cast(ShieldBarrier, ret => Me.CurrentRage > 60 && !Me.HasAura("Shield Barrier") && IsCurrentTank() && !SlimAI.Weave),
                    Spell.Cast(ShieldBarrier, ret => Me.CurrentRage > 30 && Me.HasAura("Shield Block") && Me.HealthPercent <= 70),

                    Spell.Cast(ShatteringThrow, ret => Me.CurrentTarget.IsBoss && PartyBuff.WeHaveBloodlust && !Me.IsMoving),

                    Spell.Cast(ShieldSlam),
                    Spell.Cast(Revenge, ret => Me.CurrentRage < 90),

                    new Decorator(ret => Spell.GetSpellCooldown("Shield Slam").TotalSeconds >= 1 && Spell.GetSpellCooldown("Revenge").TotalSeconds >= 1/*SpellManager.Spells["Shield Slam"].Cooldown && SpellManager.Spells["Revenge"].Cooldown*/,
                        new PrioritySelector(
                            Spell.Cast(StormBolt),
                            Spell.Cast(DragonRoar, ret => Me.CurrentTarget.Distance <= 8),
                            Spell.Cast(Execute),
                            Spell.Cast(ThunderClap, ret => !Me.CurrentTarget.HasAura("Weakened Blows") && Me.CurrentTarget.Distance <= 8),
                            new Decorator(ret => Unit.UnfriendlyUnits(8).Count() >=2 && SlimAI.AOE, CreateAoe()),
                            Spell.Cast(CommandingShout, ret => Me.HasPartyBuff(PartyBuffType.AttackPower)),
                            Spell.Cast(BattleShout),
                            Spell.Cast(HeroicStrike, ret => Me.CurrentRage > 85 || Me.HasAura(122510) || Me.HasAura(122016) || (!IsCurrentTank() && Me.CurrentRage > 60 && Me.CurrentTarget.IsBoss)),
                            Spell.Cast(HeroicThrow, ret => Me.CurrentTarget.Distance >= 10),
                            Spell.Cast(Devastate))));
        }

        private static Composite CreateAoe()
        {
            return new PrioritySelector(
                Spell.Cast(Shockwave, ret => Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 9) >= 3),
                Spell.Cast(Bladestorm),
                Spell.Cast(ThunderClap),
                Spell.Cast(Cleave, ret => (Me.CurrentRage > 85 || Me.HasAura(122510) || Me.HasAura(122016)) && Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 5) >= 2)
                );
        }

        private static Composite Leap()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Heroic Leap") &&
                    Lua.GetReturnVal<bool>("return IsLeftAltKeyDown() and not GetCurrentKeyBoardFocus()", 0),
                    new Action(ret =>
                    {
                        SpellManager.Cast(HeroicLeap);
                        Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");
                        return;
                    }));
        }

        private static Composite DemoBanner()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Demoralizing Banner") &&
                    KeyboardPolling.IsKeyDown(Keys.Z),
                    new Action(ret =>
                    {
                        SpellManager.Cast(DemoralizingBanner);
                        Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");
                        return;
                    }));
        }

        private static Composite Mocking()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Mocking Banner") &&
                    KeyboardPolling.IsKeyDown(Keys.C),
                    new Action(ret =>
                    {
                        SpellManager.Cast(MockingBanner);
                        Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");
                        return;
                    }));
        }

        private static bool NeedZerker()
        {

            return (!Me.HasAura(Enrage) && !SpellManager.HasSpell("Enraged Regeneration") ||
                   (SpellManager.HasSpell("Enraged Regeneration") && (!Me.HasAura(Enrage) &&
                    Me.HealthPercent <= 80 && !SpellManager.Spells["Enraged Regeneration"].Cooldown ||
                    Spell.GetSpellCooldown("Enraged Regeneration").TotalSeconds > 30 && SpellManager.Spells["Enraged Regeneration"].Cooldown)));
        }

        private static bool NeedEnrageRegen()
        {

            return (Me.HealthPercent <= 80 && Me.HasAura(Enrage) || Me.HealthPercent <= 50 && 
                    Spell.GetSpellCooldown("Berserker Rage").TotalSeconds > 10) && SpellManager.HasSpell("Enraged Regeneration");
        }

        static bool IsCurrentTank()
        {
            return StyxWoW.Me.CurrentTarget.CurrentTargetGuid == StyxWoW.Me.Guid;
        }

        #region WarriorTalents
        public enum WarriorTalents
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
                          Bloodthirst = 23881,
                          BerserkerRage = 18499,
                          Charge = 100,
                          Cleave = 845,
                          ColossusSmash = 86346,
                          CommandingShout = 469,
                          DemoralizingBanner = 114203,
                          DemoralizingShout = 1160,
                          Devastate = 20243,
                          DieByTheSword = 118038,
                          DragonRoar = 118000,
                          Enrage = 12880,
                          EnragedRegeneration = 55694,
                          Execute = 5308,
                          HeroicLeap = 6544,
                          HeroicStrike = 78,
                          HeroicThrow = 57755,
                          ImpendingVictory = 103840,
                          LastStand = 12975,
                          MockingBanner = 114192,
                          MortalStrike = 12294,
                          Overpower = 7384,
                          RagingBlow = 85288,
                          RallyingCry = 97462,
                          Recklessness = 1719,
                          Revenge = 6572,
                          ShatteringThrow = 64382,
                          ShieldBarrier = 112048,
                          ShieldBlock = 2565,
                          ShieldSlam = 23922,
                          ShieldWall = 871,
                          Shockwave = 46968,
                          SkullBanner = 114207,
                          Slam = 1464,
                          StormBolt = 107570,
                          SweepingStrikes = 12328,
                          ThunderClap = 6343,
                          VictoryRush = 34428,
                          Whirlwind = 1680,
                          WildStrike = 100130;
        #endregion
    }
}
