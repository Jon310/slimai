using System.Linq;
using SlimAI.Helpers;
using CommonBehaviors.Actions;
using SlimAI.Settings;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Class.Warrior
{
    public class FuryWarrior
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings Settings { get { return GeneralSettings.Instance.Warrior(); } }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite FuryCombat()
        {
            return new PrioritySelector(
                Common.CreateInterruptBehavior(),
                Leap(),
                DemoBanner(),
                new Decorator(ret => !Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive,
                    new ActionAlwaysSucceed()),
                new Decorator(ret => Me.HasAura("Dire Fixation"),
                    new PrioritySelector(
                        BossMechs.HorridonHeroic())),
                Spell.Cast(ShatteringThrow, ret => Me.CurrentTarget.IsBoss() && PartyBuff.WeHaveBloodlust),
                Spell.Cast(VictoryRush, ret => Me.HealthPercent <= 90),
                Spell.Cast(BerserkerRage, ret => !Me.HasAura(Enrage) && Me.CurrentTarget.HasMyAura("Colossus Smash")),
                Spell.Cast(ColossusSmash, ret => Me.CurrentRage > 80 && Me.HasAura("Raging Blow!") && Me.HasAura(Enrage)),
                new Decorator(ret => Unit.UnfriendlyUnits(8).Count() > 2,
                    CreateAoe()),
                new Decorator(ret => Me.CurrentTarget.HealthPercent <= 20,
                    CreateExecuteRange()),
                new Decorator(ret => Me.CurrentTarget.HealthPercent > 20,
                    new PrioritySelector(
                        Item.UsePotionAndHealthstone(40),
                        new Decorator(ret => SlimAI.Burst,
                            new PrioritySelector(
                                Spell.Cast("Blood Fury"),
                                Spell.Cast(Recklessness),
                                Spell.Cast(Avatar),
                                Spell.Cast(SkullBanner))),
                        Spell.Cast(BloodBath),
                        new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                        new Decorator(ret => !Me.CurrentTarget.HasAura("Colossus Smash"),
                            new PrioritySelector(
                                Spell.Cast(Bloodthirst),
                                Spell.Cast(HeroicStrike, ctx => Me.CurrentRage > 105 && ColossusSmashCheck()),
                                Spell.Cast(RagingBlow, ret => Me.HasAura("Raging Blow!", 2) && ColossusSmashCheck()),
                                Spell.Cast(WildStrike, ret => Me.HasAura("Bloodsurge")),
                                Spell.Cast(DragonRoar, ret => Me.CurrentTarget.Distance <= 8),
                                Spell.Cast(RagingBlow, ret => Me.HasAura("Raging Blow!", 1) && ColossusSmashCheck()),
                                Spell.Cast(BattleShout, ret => Me.RagePercent < 30 && Spell.GetSpellCooldown("Colossus Smash").TotalSeconds <= 2),
                                Spell.Cast(Shockwave),
                                Spell.Cast(WildStrike, ret => Me.CurrentRage >= 115 && ColossusSmashCheck()))),
                        Spell.Cast(HeroicStrike, ret => Me.CurrentRage > 30, true),
                        Spell.Cast(Bloodthirst),
                        Spell.Cast(RagingBlow),
                        Spell.Cast(WildStrike, ret => Me.HasAura("Bloodsurge")))));
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite FuryPreCombatBuffs()
        {
            return new PrioritySelector(
                new Decorator(ret => Me.Mounted,
                    new ActionAlwaysSucceed()),
                Spell.Cast(BattleShout, ret => !Me.HasPartyBuff(PartyBuffType.AttackPower)),
                FuryPull());

        }

        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorFury)]
        private static Composite FuryPull()
        {
            return new PrioritySelector(
                    new Decorator(ret => SlimAI.AFK,
                        new PrioritySelector(
                Spell.CastOnGround("Heroic Leap", on => Me.CurrentTarget.Location, ret => SpellManager.Spells["Charge"].Cooldown),
                Spell.Cast(Charge))));
        }

        private static Composite CreateAoe()
        {
            return new PrioritySelector(
                new Decorator(ret => Unit.UnfriendlyUnits(8).Count() >= 5,
                    new PrioritySelector(
                        Spell.Cast(Whirlwind),
                        Spell.Cast(Bloodthirst),
                        Spell.Cast(RagingBlow))),
                Spell.Cast(Whirlwind, ret => !Me.HasAura("Meat Cleaver", (int)MathEx.Clamp(1, 3, Unit.UnfriendlyUnits(8).Count() - 1))),
                Spell.Cast(Bloodthirst),
                Spell.Cast(RagingBlow, ret => Me.HasAura("Meat Cleaver", (int)MathEx.Clamp(1, 3, Unit.UnfriendlyUnits(8).Count() - 1))),
                Spell.Cast(Cleave, ret => Me.CurrentRage >= 105 && Spell.GetSpellCooldown("Colossus Smash").TotalSeconds >= 3, true));
        }

        private static Composite CreateExecuteRange()
        {
            return new PrioritySelector(
                new Decorator(ret => !Me.CurrentTarget.HasAura("Colossus Smash"),
                    new PrioritySelector(
                        Spell.Cast(Bloodthirst),
                        Spell.Cast(RagingBlow),
                        new Decorator(ret => Me.RagePercent < 85,
                            new Action(ret => RunStatus.Success)))),
                new Decorator(ret => Me.CurrentTarget.HasAura("Colossus Smash"),
                    new PrioritySelector(
                        Spell.Cast(Execute))));
        }

        private static Composite Leap()
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
                    Lua.GetReturnVal<bool>("return IsLeftShiftKeyDown() and not GetCurrentKeyBoardFocus()", 0),
                    new Action(ret =>
                    {
                        SpellManager.Cast(DemoralizingBanner);
                        Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");
                    }));
        }

        private static bool ColossusSmashCheck()
        {
            return (Spell.GetSpellCooldown("Colossus Smash").TotalSeconds >= 3 ||
                    !SpellManager.Spells["Colossus Smash"].Cooldown);
        }

        #region WarriorTalents
        private enum WarriorTalents
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
                          DemoralizingBanner = 114203,
                          DieByTheSword = 118038,
                          DragonRoar = 118000,
                          Enrage = 12880,
                          Execute = 5308,
                          HeroicLeap = 6544,
                          HeroicStrike = 78,
                          HeroicThrow = 57755,
                          ImpendingVictory = 103840,
                          MortalStrike = 12294,
                          Overpower = 7384,
                          RagingBlow = 85288,
                          Recklessness = 1719,
                          ShatteringThrow = 64382,
                          Shockwave = 46968,
                          SkullBanner = 114207,
                          Slam = 1464,
                          SweepingStrikes = 12328,
                          ThunderClap = 6343,
                          VictoryRush = 34428,
                          Whirlwind = 1680,
                          WildStrike = 100130;
        #endregion
    }
}
