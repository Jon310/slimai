using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Buddy.Coroutines;
using JetBrains.Annotations;
using SlimAI.Helpers;
using CommonBehaviors.Actions;
using SlimAI.Settings;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Class.Warrior
{
    [UsedImplicitly]
    public class FuryWarrior
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings Settings { get { return GeneralSettings.Instance.Warrior(); } }

        #region Coroutine Combat Section

        private static async Task<bool> CombatCoroutine()
        {
            // Pause for Casting
            if (Me.IsCasting || Me.IsChanneling) return true;

            //Still Need to re-write (Testing Composites from Coroutines)
            //One of them is corect (maybe), Unknown what one works and is correct format
            //await Interrupt();
            //await Task.Run(() => Common.CreateInterruptBehavior());
            //await Coroutine.ExternalTask(Task.Run(() => Common.CreateInterruptBehavior()));

            await CoLeap();
            await CoDemoBanner();

            // Pause if Not in Combat, or mounted, or no target, or Target is dead
            if (!Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive) return true;

            // Boss Mechanics
            //
            // End Boss Mechanics

            await Spell.CoCast(ShatteringThrow, Me.CurrentTarget.IsBoss && PartyBuff.WeHaveBloodlust);
            await Spell.CoCast(VictoryRush, Me.HealthPercent <= 90);
            await Spell.CoCast(BerserkerRage, !Me.HasAura(Enrage) && Me.CurrentTarget.HasMyAura("Colossus Smash"));
            await Spell.CoCast(ColossusSmash, Me.CurrentRage > 80 && Me.HasAura("Raging Blow!") && Me.HasAura(Enrage));

            if (Unit.UnfriendlyUnits(8).Count() > 2)
            {
                return await CoAoe();
            }

            await CoExecute();

            //less that 20 % stop the bot here
            if (Me.CurrentTarget.HealthPercent < 20) return true;

            await Item.CoUseHS(40);

            await Spell.CoCast("Blood Fury", SlimAI.Burst);
            await Spell.CoCast(Recklessness, SlimAI.Burst);
            await Spell.CoCast(Avatar, SlimAI.Burst);
            await Spell.CoCast(SkullBanner, SlimAI.Burst);

            await Spell.CoCast(BloodBath);
            await Item.CoUseHands();

            await Spell.CoCast(Bloodthirst, !Me.CurrentTarget.HasAura("Colossus Smash"));
            await Spell.CoCast(HeroicStrike, Me.CurrentRage > 105 && ColossusSmashCheck() && !Me.CurrentTarget.HasAura("Colossus Smash"));
            await Spell.CoCast(RagingBlow, Me.HasAura("Raging Blow!", 2) && ColossusSmashCheck() && !Me.CurrentTarget.HasAura("Colossus Smash"));
            await Spell.CoCast(WildStrike, Me.HasAura("Bloodsurge") && !Me.CurrentTarget.HasAura("Colossus Smash"));
            await Spell.CoCast(DragonRoar, Me.CurrentTarget.Distance <= 8 && !Me.CurrentTarget.HasAura("Colossus Smash"));
            await Spell.CoCast(RagingBlow, Me.HasAura("Raging Blow!", 1) && ColossusSmashCheck() && !Me.CurrentTarget.HasAura("Colossus Smash"));
            await Spell.CoCast(BattleShout, Me.RagePercent < 30 && Spell.GetSpellCooldown("Colossus Smash").TotalSeconds <= 2 && !Me.CurrentTarget.HasAura("Colossus Smash"));
            await Spell.CoCast(Shockwave, !Me.CurrentTarget.HasAura("Colossus Smash"));
            await Spell.CoCast(WildStrike, Me.CurrentRage >= 115 && ColossusSmashCheck() && !Me.CurrentTarget.HasAura("Colossus Smash"));

            await Spell.CoCast(HeroicStrike, Me.CurrentRage > 30 && Me.CurrentTarget.HasAura("Colossus Smash"));

            await Spell.CoCast(Bloodthirst, Me.CurrentTarget.HasAura("Colossus Smash"));
            await Spell.CoCast(RagingBlow, Me.CurrentTarget.HasAura("Colossus Smash"));
            await Spell.CoCast(WildStrike, Me.HasAura("Bloodsurge") && Me.CurrentTarget.HasAura("Colossus Smash"));

            return false;
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite CoFuryCombat()
        {
            return new ActionRunCoroutine(ctx => CombatCoroutine());
        }

        #endregion

        //[Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorFury)]
        //public static Composite FuryCombat()
        //{
        //    return new PrioritySelector(
        //        Common.CreateInterruptBehavior(),
        //        Leap(),
        //        DemoBanner(),
        //        new Decorator(ret => !Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive,
        //            new ActionAlwaysSucceed()),
        //        new Decorator(ret => Me.HasAura("Dire Fixation"),
        //            new PrioritySelector(
        //                BossMechs.HorridonHeroic())),
        //        Spell.Cast(ShatteringThrow, ret => Me.CurrentTarget.IsBoss() && PartyBuff.WeHaveBloodlust),
        //        Spell.Cast(VictoryRush, ret => Me.HealthPercent <= 90),
        //        Spell.Cast(BerserkerRage, ret => !Me.HasAura(Enrage) && Me.CurrentTarget.HasMyAura("Colossus Smash")),
        //        Spell.Cast(ColossusSmash, ret => Me.CurrentRage > 80 && Me.HasAura("Raging Blow!") && Me.HasAura(Enrage)),
        //        new Decorator(ret => Unit.UnfriendlyUnits(8).Count() > 2,
        //            CreateAoe()),
        //        new Decorator(ret => Me.CurrentTarget.HealthPercent <= 20,
        //            CreateExecuteRange()),
        //        new Decorator(ret => Me.CurrentTarget.HealthPercent > 20,
        //            new PrioritySelector(
        //                Item.UsePotionAndHealthstone(40),
        //                new Decorator(ret => SlimAI.Burst,
        //                    new PrioritySelector(
        //                        Spell.Cast("Blood Fury"),
        //                        Spell.Cast(Recklessness),
        //                        Spell.Cast(Avatar),
        //                        Spell.Cast(SkullBanner))),
        //                Spell.Cast(BloodBath),
        //                new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
        //                new Decorator(ret => !Me.CurrentTarget.HasAura("Colossus Smash"),
        //                    new PrioritySelector(
        //                        Spell.Cast(Bloodthirst),
        //                        Spell.Cast(HeroicStrike, ctx => Me.CurrentRage > 105 && ColossusSmashCheck()),
        //                        Spell.Cast(RagingBlow, ret => Me.HasAura("Raging Blow!", 2) && ColossusSmashCheck()),
        //                        Spell.Cast(WildStrike, ret => Me.HasAura("Bloodsurge")),
        //                        Spell.Cast(DragonRoar, ret => Me.CurrentTarget.Distance <= 8),
        //                        Spell.Cast(RagingBlow, ret => Me.HasAura("Raging Blow!", 1) && ColossusSmashCheck()),
        //                        Spell.Cast(BattleShout, ret => Me.RagePercent < 30 && Spell.GetSpellCooldown("Colossus Smash").TotalSeconds <= 2),
        //                        Spell.Cast(Shockwave),
        //                        Spell.Cast(WildStrike, ret => Me.CurrentRage >= 115 && ColossusSmashCheck()))),
        //                Spell.Cast(HeroicStrike, ret => Me.CurrentRage > 30, true),
        //                Spell.Cast(Bloodthirst),
        //                Spell.Cast(RagingBlow),
        //                Spell.Cast(WildStrike, ret => Me.HasAura("Bloodsurge")))));
        //}

        #region Coroutine Pre Combat Buffs

        private static async Task<bool> PreCombatCoroutine()
        {
            if (Me.Mounted) return true;
            if (await Spell.CoBuff(BattleShout, !Me.HasPartyBuff(PartyBuffType.AttackPower))) return true;

            return false;
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, WoWSpec.WarriorFury)]
        public static Composite CoroutinePreCombatBuffs()
        {
            return new ActionRunCoroutine(ctx => PreCombatCoroutine());
        }

        #endregion

        //[Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, WoWSpec.WarriorFury)]
        //public static Composite FuryPreCombatBuffs()
        //{
        //    return new PrioritySelector(
        //        new Decorator(ret => Me.Mounted,
        //            new ActionAlwaysSucceed()),
        //        Spell.Cast(BattleShout, ret => !Me.HasPartyBuff(PartyBuffType.AttackPower)),
        //        FuryPull());

        //}

        #region Coroutine Pull Section

        private static async Task<bool> PullCoroutine()
        {
            if (SlimAI.AFK) return true;
            if (await Spell.CoCastOnGround(HeroicLeap, SpellManager.Spells["Charge"].Cooldown)) return true;
            if (await Spell.CoCast(Charge)) return true;

            return false;
        }

        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorFury)]
        private static Composite CoFuryPull()
        {
            return new ActionRunCoroutine(ctx => PullCoroutine());
        }

        #endregion

        //[Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorFury)]
        //private static Composite FuryPull()
        //{
        //    return new PrioritySelector(
        //            new Decorator(ret => SlimAI.AFK,
        //                new PrioritySelector(
        //        Spell.CastOnGround("Heroic Leap", on => Me.CurrentTarget.Location, ret => SpellManager.Spells["Charge"].Cooldown),
        //        Spell.Cast(Charge))));
        //}

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

        private static async Task<bool> CoAoe()
        {
            if (Unit.UnfriendlyUnits(8).Count() >= 5)
            {
                if (await Spell.CoCast(Whirlwind)) return true;
                if (await Spell.CoCast(Bloodthirst)) return true;
                if (await Spell.CoCast(RagingBlow)) return true;
            }

            if (await Spell.CoCast(Whirlwind, !Me.HasAura("Meat Cleaver", (int)MathEx.Clamp(1, 3, Unit.UnfriendlyUnits(8).Count() - 1)))) return true;
            if (await Spell.CoCast(Bloodthirst)) return true;
            if (await Spell.CoCast(RagingBlow, Me.HasAura("Meat Cleaver", (int)MathEx.Clamp(1, 3, Unit.UnfriendlyUnits(8).Count() - 1)))) return true;
            if (await Spell.CoCast(Cleave, Me.CurrentRage >= 105 && Spell.GetSpellCooldown("Colossus Smash").TotalSeconds >= 3)) return true;
            return false;
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

        #region Coroutine Execute Range
        private static async Task<bool> CoExecute()
        {
            if (Me.CurrentTarget.HealthPercent >= 20)
                return false;

            if (Me.CurrentTarget.HasAura("Colossus Smash"))
            {
                if (await Spell.CoCast(Bloodthirst)) return true;
                if (await Spell.CoCast(RagingBlow)) return true;
                if (Me.RagePercent < 85) return true;
            }

            return Me.CurrentTarget.HasAura("Colossus Smash") && await Spell.CoCast(Execute);
        }
        #endregion

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

        #region Coroutine Leap
        private static async Task<bool> CoLeap()
        {
            if (!SpellManager.CanCast(HeroicLeap))
                return false;

            if (!Lua.GetReturnVal<bool>("return IsLeftAltKeyDown() and not GetCurrentKeyBoardFocus()", 0))
                return false;

            if (!SpellManager.Cast(HeroicLeap))
                    return false;

            if (!await Coroutine.Wait(1000, () => StyxWoW.Me.CurrentPendingCursorSpell != null))
            {
                Logging.Write("Cursor Spell Didnt happen");
                return false;
            }

            Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");

            await CommonCoroutines.SleepForLagDuration();
            return true;
        }
        #endregion

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

        #region Coroutine Demo Banner
        private static async Task<bool> CoDemoBanner()
        {
            if (!SpellManager.CanCast(DemoralizingBanner))
                return false;

            if (!Lua.GetReturnVal<bool>("return IsLeftShiftKeyDown() and not GetCurrentKeyBoardFocus()", 0))
                return false;

            if (!SpellManager.Cast(DemoralizingBanner))
                return false;

            if (!await Coroutine.Wait(1000, () => StyxWoW.Me.CurrentPendingCursorSpell != null))
            {
                Logging.Write("Cursor Spell Didnt happen");
                return false;
            }

            Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");

            await CommonCoroutines.SleepForLagDuration();
            return true;
        }
        #endregion

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
