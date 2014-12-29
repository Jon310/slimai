using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Buddy.Coroutines;
using SlimAI.Managers;
using CommonBehaviors.Actions;
using SlimAI.Helpers;
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
    class ProtectionWarrior
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static WarriorSettings Settings { get { return GeneralSettings.Instance.Warrior(); } }

        #region Coroutine Combat Section
        private static async Task<bool> CombatCoroutine()
        {
            if (SlimAI.PvPRotation)
            {
                await PvPCoroutine();
                return true;
            }

            if (Me.HasAura("Gladiator Stance"))
            {
                await GladCoroutine();
                return true;
            }
           
            // HealerManager.NeedHealTargeting = true;
            if (!Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive) return true;

            // Boss Mechanics Section
            //
            // End Boss Mechanics Section

            // Intrrupt Section
            await Coroutine.ExternalTask(Task.Run(() => Common.CreateInterruptBehavior()));


            await Spell.CoCast(BloodBath, SlimAI.Burst && Me.CurrentTarget.IsWithinMeleeRange);
            await Spell.CoCast(Avatar, SlimAI.Burst && Me.CurrentTarget.IsWithinMeleeRange);

            await CoLeap();
            await CoMockingBanner();

            await Spell.CoCast(VictoryRush, Me.HealthPercent <= 90 && Me.HasAura("Victorious"));
            await Spell.CoCast(EnragedRegeneration, Me.HealthPercent <= 50);
            await Spell.CoCast(LastStand, Me.HealthPercent <= 15 && !Me.HasAura("Shield Wall") && SlimAI.AFK);
            await Spell.CoCast(ShieldWall, Me.HealthPercent <= 30 && !Me.HasAura("Last Stand") && SlimAI.AFK);

            await Spell.CoCast(DemoralizingShout, Unit.UnfriendlyUnits(10).Any() && IsCurrentTank());

            await Spell.CoCast(ImpendingVictory, Me.HealthPercent <= 75);
            await Spell.CoCast(ShieldBlock, !Me.HasAura("Shield Block") && IsCurrentTank() && SlimAI.Weave);
            await Spell.CoCast("Shield Barrier", (Me.CurrentRage >= 60 && !Me.HasAura("Shield Barrier") && IsCurrentTank() && !SlimAI.Weave) || Me.CurrentRage > 30 && Me.HasAura("Shield Block") && Me.HealthPercent <= 70);


            await Spell.CoCast(HeroicStrike, Me.CurrentRage > 85 || Me.HasAura(122510) || Me.HasAura(122016) || Me.HasAura("Unyielding Strikes", 6) || (!IsCurrentTank() && Me.CurrentRage > 60 && Me.CurrentTarget.IsBoss));
            await Spell.CoCast(ShieldSlam);
            await Spell.CoCast(Revenge, Me.CurrentRage < 90);

            // Needs Testing, Nesting broke the cc last time I tried it, but it could have been other issues.
            // if (Spell.GetSpellCooldown("Shield Slam").TotalSeconds >= 1 && Spell.GetSpellCooldown("Revenge").TotalSeconds >= 1)
            // {
            await Spell.CoCast(StormBolt);
            await Spell.CoCast(DragonRoar, Me.CurrentTarget.Distance <= 8);
            await Spell.CoCast(Execute, SlimAI.Burst || Me.HasAura("Sudden Death"));
            await CoAOE(Unit.EnemyUnitsSub8.Count() >= 2 && SlimAI.AOE);
            await Spell.CoCast(HeroicThrow, Me.CurrentTarget.Distance >= 10);
            await Spell.CoCast(Devastate);
           // //}

            return false;
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorProtection)]
        public static Composite CoProtCombat()
        {
            return new ActionRunCoroutine(ctx => CombatCoroutine());
        }

        #endregion
        
        //[Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorProtection)]
        //public static Composite ProtCombat()
        //{
        //    HealerManager.NeedHealTargeting = true;
        //    return new PrioritySelector(
        //        new Decorator(ret => !Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive,
        //            new ActionAlwaysSucceed()),
        //        new Decorator(ret => Me.HasAura("Dire Fixation"),
        //            new PrioritySelector(
        //                BossMechs.HorridonHeroic())),
        //        new Throttle(1, 1,
        //            new PrioritySelector(
        //                Common.CreateInterruptBehavior())),
        //        new Decorator(ret => SlimAI.Burst && Me.CurrentTarget.IsWithinMeleeRange,
        //            new PrioritySelector(
        //                Spell.Cast(Recklessness),
        //                Spell.Cast(BloodBath),
        //                new Decorator(ret => Me.HasAura("Recklessness"),
        //                    new PrioritySelector(
        //                        Spell.Cast(Avatar),
        //                        Spell.Cast(SkullBanner))))),
        //        //new Throttle(1,
        //        //    Item.UsePotionAndHealthstone(40)),
        //        new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
        //        DemoBanner(),
        //        Leap(),
        //        Mocking(),
        //        //CD's all bout living
        //        Spell.Cast(VictoryRush, ret => Me.HealthPercent <= 90 && Me.HasAura("Victorious")),
        //        Spell.Cast(BerserkerRage, ret => NeedZerker()),
        //        Spell.Cast(EnragedRegeneration, ret => NeedEnrageRegen()),
        //        Spell.Cast(LastStand, ret => Me.HealthPercent <= 15 && !Me.HasAura("Shield Wall")),
        //        Spell.Cast(ShieldWall, ret => Me.HealthPercent <= 30 && !Me.HasAura("Last Stand")),

        //        //Might need some testing
        //        new Throttle(1, 1,
        //            new PrioritySelector(
        //                Spell.Cast(RallyingCry, ret => HealerManager.GetCountWithHealth(55) > 4),
        //                Spell.Cast(DemoralizingShout, ret => Unit.UnfriendlyUnits(10).Any() && IsCurrentTank()))),

        //        Spell.Cast(ShieldBlock, ret => !Me.HasAura("Shield Block") && IsCurrentTank() && SlimAI.Weave),
        //        Spell.Cast(ShieldBarrier, ret => Me.CurrentRage > 60 && !Me.HasAura("Shield Barrier") && IsCurrentTank() && !SlimAI.Weave),
        //        Spell.Cast(ShieldBarrier, ret => Me.CurrentRage > 30 && Me.HasAura("Shield Block") && Me.HealthPercent <= 70),

        //        Spell.Cast(ShatteringThrow, ret => Me.CurrentTarget.IsBoss && PartyBuff.WeHaveBloodlust && !Me.IsMoving),

        //        Spell.Cast(ShieldSlam),
        //        Spell.Cast(Revenge, ret => Me.CurrentRage < 90),

        //        new Decorator(ret => Spell.GetSpellCooldown("Shield Slam").TotalSeconds >= 1 && Spell.GetSpellCooldown("Revenge").TotalSeconds >= 1/*SpellManager.Spells["Shield Slam"].Cooldown && SpellManager.Spells["Revenge"].Cooldown*/,
        //            new PrioritySelector(
        //                Spell.Cast(StormBolt),
        //                Spell.Cast(DragonRoar, ret => Me.CurrentTarget.Distance <= 8),
        //                Spell.Cast(Execute),
        //                Spell.Cast(ThunderClap, ret => !Me.CurrentTarget.HasAura("Weakened Blows") && Me.CurrentTarget.Distance <= 8),
        //                new Decorator(ret => Unit.UnfriendlyUnits(8).Count() >=2 && SlimAI.AOE, CreateAoe()),
        //                Spell.Cast(CommandingShout, ret => Me.HasPartyBuff(PartyBuffType.AttackPower)),
        //                Spell.Cast(BattleShout),
        //                Spell.Cast(HeroicStrike, ret => Me.CurrentRage > 85 || Me.HasAura(122510) || Me.HasAura(122016) || (!IsCurrentTank() && Me.CurrentRage > 60 && Me.CurrentTarget.IsBoss)),
        //                Spell.Cast(HeroicThrow, ret => Me.CurrentTarget.Distance >= 10),
        //                Spell.Cast(Devastate))));
        //}

        #region Glad

        private static async Task<bool> GladCoroutine()
        {
            if (!Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive) return true;

            //await Spell.CoCast(MassSpellReflection, Me.CurrentTarget.IsCasting && Me.CurrentTarget.Distance > 10);
            //await Spell.CoCast(ShieldWall, Me.HealthPercent < 40);
            //await Spell.CoCast(LastStand, Me.CurrentTarget.HealthPercent > Me.HealthPercent && Me.HealthPercent < 60);
            //await Spell.CoCast(DemoralizingShout, Unit.EnemyUnitsSub10.Count() >= 3);
            await Spell.CoCast(ShieldBarrier, Me.HealthPercent < 40 && Me.CurrentRage >= 100);
            await Spell.CoCast(VictoryRush, Me.HealthPercent <= 90 && Me.HasAura("Victorious"));
            //await Spell.CoCast(BerserkerRage, Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing));
            await Spell.CoCast(EnragedRegeneration, Me.HealthPercent <= 50);

            await CoLeap();

            if (Me.CurrentTarget.IsWithinMeleeRange && SlimAI.Burst)
            {
                await Spell.CoCast(Avatar);
                await Spell.CoCast(BloodBath);
                await Spell.CoCast(Bladestorm);
            }

            await Spell.CoCast(ShieldCharge, (!Me.HasAura("Shield Charge") && !SpellManager.Spells["Shield Slam"].Cooldown) || Spell.GetCharges(ShieldCharge) == 2);
            //await Spell.CoCast(HeroicStrike, Me.HasAura("Shield Charge") || Me.HasAura("Ultimatum") || Me.CurrentRage >= 90 || Me.HasAura("Unyielding Strikes", 5));
            
            await Spell.CoCast(HeroicStrike, (Me.HasAura("Sheld Charge") || (Me.HasAura("Unyielding Strikes") && Me.CurrentRage >= 50 - Spell.StackCount(169686) * 5)) && Me.CurrentTarget.HealthPercent > 20);
            await Spell.CoCast(HeroicStrike, Me.HasAura("Ultimatum") || Me.CurrentRage >= Me.MaxRage - 20 || Me.HasAura("Unyielding Strikes", 5));

            await Spell.CoCast(ShieldSlam);
            await Spell.CoCast(Revenge);
            await Spell.CoCast(Execute, Me.HasAura("Sudden Death"));
            await Spell.CoCast(StormBolt);
            await Spell.CoCast(ThunderClap, SlimAI.AOE && Unit.EnemyUnitsSub8.Any(u => !u.HasAura("Deep Wounds")) && Unit.EnemyUnitsSub8.Count() >= 2);
            await Spell.CoCast(DragonRoar, Me.CurrentTarget.Distance <= 8);
            await Spell.CoCast(ThunderClap, SlimAI.AOE && Unit.EnemyUnitsSub8.Count() >= 6);
            await Spell.CoCast(Execute, Me.CurrentRage > 60 && Me.CurrentTarget.HealthPercent < 20);
            await Spell.CoCast(Devastate);

            return true;
        }


        #endregion

        #region PvP

        private static async Task<bool> PvPCoroutine()
        {

            await CoLeap();
            await CoMockingBanner();

            if (Me.CurrentTarget.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield", "Deterrence") || !Me.Combat || Me.Mounted) return true;

            if (StyxWoW.Me.CurrentTarget != null && (!StyxWoW.Me.CurrentTarget.IsWithinMeleeRange || StyxWoW.Me.IsCasting || SpellManager.GlobalCooldown)) return true;

            await Spell.CoCast(VictoryRush, Me.HealthPercent <= 90 && Me.HasAura("Victorious"));

            await Spell.CoCast("Intervene", BestBanner);

            await CoStormBoltFocus();

            await Spell.CoCast("Intervene", BestInterveneTarget);
            //await Spell.CoCast(MassSpellReflection, Me.CurrentTarget.IsCasting && Me.CurrentTarget.Distance > 10);
            //await Spell.CoCast(ShieldWall, Me.HealthPercent < 40);
            //await Spell.CoCast(LastStand, Me.CurrentTarget.HealthPercent > Me.HealthPercent && Me.HealthPercent < 60);
            //await Spell.CoCast(DemoralizingShout, Unit.EnemyUnitsSub10.Count() >= 3);
            await Spell.CoCast(ShieldBarrier, Me.HealthPercent < 40 && Me.CurrentRage >= 100);
            //await Spell.CoCast(BerserkerRage, Me.HasAuraWithMechanic(WoWSpellMechanic.Fleeing));
            await Spell.CoCast(EnragedRegeneration, Me.HealthPercent <= 35);

            if (Me.CurrentTarget.IsWithinMeleeRange && SlimAI.Burst)
            {
                await Spell.CoCast(Avatar);
                await Spell.CoCast(BloodBath);
                await Spell.CoCast(Bladestorm);
            }

            await Spell.CoCast(ShieldCharge, (!Me.HasAura("Shield Charge") && SpellManager.Spells["Shield Slam"].Cooldown) || Spell.GetCharges(ShieldCharge) > 1);
            //await Spell.CoCast(HeroicStrike, Me.HasAura("Shield Charge") || Me.HasAura("Ultimatum") || Me.CurrentRage >= 90 || Me.HasAura("Unyielding Strikes", 5));

            await Spell.CoCast(HeroicStrike, (Me.HasAura("Sheld Charge") || (Me.HasAura("Unyielding Strikes") && Me.CurrentRage >= 50 - Spell.StackCount(169686) * 5)) && Me.CurrentTarget.HealthPercent > 20);
            await Spell.CoCast(HeroicStrike, Me.HasAura("Ultimatum") || Me.CurrentRage >= Me.MaxRage - 20 || Me.HasAura("Unyielding Strikes", 5));

            await Spell.CoCast(ShieldSlam);
            await Spell.CoCast(Revenge);
            await Spell.CoCast(Execute, Me.HasAura("Sudden Death"));
            await Spell.CoCast(ThunderClap, SlimAI.AOE && Unit.EnemyUnitsSub8.Count(u => !u.HasAura("Deep Wounds")) >= 1 && Unit.UnfriendlyUnits(8).Count() >= 2);
            await Spell.CoCast(DragonRoar, Me.CurrentTarget.Distance <= 8);
            await Spell.CoCast(Execute, Me.CurrentRage > 60 && Me.CurrentTarget.HealthPercent < 20);
            await Spell.CoCast(Devastate);

            return true;
        }
        #endregion

        private static Composite CreateAoe()
        {
            return new PrioritySelector(
                Spell.Cast(Shockwave, ret => Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 9) >= 3),
                Spell.Cast(Bladestorm),
                Spell.Cast(ThunderClap),
                Spell.Cast(Cleave, ret => (Me.CurrentRage > 85 || Me.HasAura(122510) || Me.HasAura(122016)) && Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 5) >= 2)
                );
        }

        private static async Task<bool> CoAOE(bool reqs)
        {
            if (!reqs)
                return false;

            await Spell.CoCast(Shockwave, Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Cone, 9) >= 3);
            await Spell.CoCast(Bladestorm);
            await Spell.CoCast(ThunderClap);
            
            return false;
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
                    KeyboardPolling.IsKeyDown(Keys.Z),
                    new Action(ret =>
                    {
                        SpellManager.Cast(DemoralizingBanner);
                        Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");
                        return;
                    }));
        }

        #region Coroutine Demo Banner
        private static async Task<bool> CoDemoBanner()
        {
            if (!SpellManager.CanCast(DemoralizingBanner))
                return false;

            if (!KeyboardPolling.IsKeyDown(Keys.Z))
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

        #region Coroutine Mocking Banner
        private static async Task<bool> CoMockingBanner()
        {
            if (!SpellManager.CanCast(MockingBanner))
                return false;

            if (!KeyboardPolling.IsKeyDown(Keys.G))
                return false;

            if (!SpellManager.Cast(MockingBanner))
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


        #region Pvp Stuff
        private static bool Freedoms
        {
            get
            {
                return Me.CurrentTarget.HasAnyAura("Hand of Freedom", "Ice Block", "Hand of Protection", "Divine Shield", "Cyclone", "Deterrence", "Phantasm", "Windwalk Totem");
            }
        }
        private static Composite StormBoltFocus()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Storm Bolt") &&
                    KeyboardPolling.IsKeyDown(Keys.C),
                    new PrioritySelector(
                        Spell.Cast("Storm Bolt", on => Me.FocusedUnit))

                    );
        }

        #region Coroutine Stormbolt Focus

        private static async Task<bool> CoStormBoltFocus()
        {
            if (SpellManager.CanCast("Storm Bolt") && KeyboardPolling.IsKeyDown(Keys.C))
            {
                await Spell.CoCast(StormBolt, Me.FocusedUnit);
            }

            return false;
        }

        private static void ResetVariables()
        {
            KeyboardPolling.IsKeyDown(Keys.G);
            KeyboardPolling.IsKeyDown(Keys.Z);
            KeyboardPolling.IsKeyDown(Keys.C);
        }
        #endregion

        #region Best Banner
        public static WoWUnit BestBanner//WoWUnit
        {
            get
            {
                if (!StyxWoW.Me.GroupInfo.IsInParty)
                    return null;
                if (StyxWoW.Me.GroupInfo.IsInParty)
                {
                    var closePlayer = FriendlyUnitsNearTarget(6f).OrderBy(t => t.DistanceSqr).FirstOrDefault(t => t.IsAlive);
                    if (closePlayer != null)
                        return closePlayer;
                    var bestBan = (from unit in ObjectManager.GetObjectsOfType<WoWUnit>(false)
                                   //where (unit.Equals(59390) || unit.Equals(59398))
                                   //where unit.Guid.Equals(59390) || unit.Guid.Equals(59398)
                                   where unit.Entry.Equals(59390) || unit.Entry.Equals(59398)
                                   //where (unit.Guid == 59390 || unit.Guid == 59398) 
                                   where unit.InLineOfSight
                                   select unit).FirstOrDefault();
                    return bestBan;
                }
                return null;
            }
        }
        #endregion

        #region BestInterrupt
        public static WoWUnit BestInterrupt
        {
            get
            {
                var bestInt = (from unit in ObjectManager.GetObjectsOfType<WoWPlayer>(false)
                               where unit.IsAlive
                               where unit.IsPlayer
                               where !unit.IsInMyPartyOrRaid
                               where unit.InLineOfSight
                               where unit.Distance <= 10
                               where unit.IsCasting
                               where unit.CanInterruptCurrentSpellCast
                               where unit.CurrentCastTimeLeft.TotalMilliseconds <
                                     MyLatency + 1000 &&
                                     InterruptCastNoChannel(unit) > MyLatency ||
                                     unit.IsChanneling &&
                                     InterruptCastChannel(unit) > MyLatency
                               select unit).FirstOrDefault();
                return bestInt;
            }
        }



        public static bool Interuptdelay(WoWUnit inttar)
        {
            var totaltime = inttar.CastingSpell.CastTime / 1000;
            var timeleft = inttar.CurrentCastTimeLeft.TotalSeconds;
            //Logging.Write((totaltime / 1000).ToString());
            //Logging.Write(timeleft.ToString());

            return (timeleft / totaltime) < MathEx.Random(.10, .50);

        }

        private static int InteruptMiss = 0;

        private static void addone()
        {
            var add = InteruptMiss + 1;
            InteruptMiss = add;
        }

        private static void resetIntMiss()
        {
            InteruptMiss = 0;
        }

        #endregion

        #region Best Intervene
        public static WoWUnit BestInterveneTarget
        {
            get
            {
                if (!StyxWoW.Me.GroupInfo.IsInParty)
                    return null;
                if (StyxWoW.Me.GroupInfo.IsInParty)
                {
                    var bestTank = Group.Tanks.OrderBy(t => t.DistanceSqr).FirstOrDefault(t => t.IsAlive);
                    if (bestTank != null)
                        return bestTank;
                    var bestInt = (from unit in ObjectManager.GetObjectsOfType<WoWPlayer>(false)
                                   where unit.IsAlive
                                   where unit.HealthPercent <= 30
                                   where unit.IsInMyPartyOrRaid
                                   where unit.IsPlayer
                                   where !unit.IsHostile
                                   where unit.InLineOfSight
                                   select unit).FirstOrDefault();
                    return bestInt;
                }
                return null;
            }
        }
        #endregion

        #region ChargeInterupt
        public static WoWUnit ChargeInt
        {
            get
            {
                if (!StyxWoW.Me.GroupInfo.IsInParty)
                    return null;
                if (StyxWoW.Me.GroupInfo.IsInParty)
                {
                    var bestInt = (from unit in ObjectManager.GetObjectsOfType<WoWPlayer>(false)
                                   where unit.IsAlive
                                   where unit.IsCasting
                                   where unit.CanInterruptCurrentSpellCast
                                   where unit.IsPlayer
                                   where unit.IsHostile
                                   where unit.InLineOfSight
                                   where unit.Distance <= 25
                                   where unit.Distance >= 8
                                   where unit.CurrentCastTimeLeft.TotalMilliseconds <
                                     MyLatency + 1000 &&
                                     InterruptCastNoChannel(unit) > MyLatency ||
                                     unit.IsChanneling &&
                                     InterruptCastChannel(unit) > MyLatency
                                   select unit).FirstOrDefault();
                    return bestInt;
                }
                return null;
            }
        }

        #endregion

        #region CreateChargeBehavior
        static Composite CreateChargeBehavior()
        {
            return new Decorator(
                    ret => StyxWoW.Me.CurrentTarget != null && !IsGlobalCooldown()/*&& PreventDoubleCharge*/,

                    new PrioritySelector(
                        Spell.Cast("Charge",
                            ret => StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance < (TalentManager.HasGlyph("Long Charge") ? 30f : 25f)),

                        Spell.CastOnGround("Heroic Leap",
                            ret => StyxWoW.Me.CurrentTarget.Location,
                            ret => StyxWoW.Me.CurrentTarget.Distance > 13 && StyxWoW.Me.CurrentTarget.Distance < 40 && SpellManager.Spells["Charge"].Cooldown)));
        }
        #endregion

        #region CreateInterruptSpellCast
        public static Composite CreateInterruptSpellCast(UnitSelectionDelegate onUnit)
        {
            return new Decorator(
                // If the target is casting, and can actually be interrupted, AND we've waited out the double-interrupt timer, then find something to interrupt with.
                ret => onUnit != null && onUnit(ret) != null/*Interuptdelay(onUnit(ret))&& PreventDoubleInterrupt*/,
                new PrioritySelector(
                //Spell.Cast("Pummel", onUnit),
                // AOE interrupt
                    Spell.Cast("Disrupting Shout", onUnit, ret => onUnit(ret).Distance < 10)
                //Spell.Cast("Mass Spell Reflection", onUnit, ret => onUnit(ret).IsCasting),
                //Spell.Cast("Shockwave", onUnit, ret => onUnit(ret).Distance < 10 && Me.IsFacing(onUnit(ret))),
                //Spell.Cast("Indimidating Shout", onUnit, ret => onUnit(ret).Distance < 8),
                   ));
        }
        #endregion

        #region Demo Banner
        private static Composite DemoBannerAuto()
        {
            return new Decorator(ret => SpellManager.Spells["Charge"].Cooldown &&
                                        SpellManager.Spells["Heroic Leap"].Cooldown &&
                                       !SpellManager.Spells["Demoralizing Banner"].Cooldown &&
                                       !SpellManager.Spells["Intervene"].Cooldown &&
                                       !FriendlyUnitsNearTarget(6f).Any() &&
                                        StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25,
                            new Action(ret =>
                            {
                                SpellManager.Cast("Demoralizing Banner");
                                SpellManager.ClickRemoteLocation(StyxWoW.Me.CurrentTarget.Location);
                            }));
        }
        #endregion

        #region FriendlyUnitsNearTarget
        public static IEnumerable<WoWUnit> FriendlyUnitsNearTarget(float distance)
        {
            var dist = distance * distance;
            var curTarLocation = StyxWoW.Me.CurrentTarget.Location;
            return ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Where(
                        p => ValidUnit(p) && p.IsFriendly && p.Location.DistanceSqr(curTarLocation) <= dist).ToList();
        }
        #endregion

        #region IsGlobalCooldown
        public static bool IsGlobalCooldown(bool faceDuring = false, bool allowLagTollerance = true)
        {
            uint latency = allowLagTollerance ? StyxWoW.WoWClient.Latency : 0;
            TimeSpan gcdTimeLeft = SpellManager.GlobalCooldownLeft;
            return gcdTimeLeft.TotalMilliseconds > latency;
        }
        #endregion

        #region Mocking Banner
        private static Composite MockingBannerAuto()
        {
            return new Decorator(ret => SpellManager.Spells["Demoralizing Banner"].Cooldown &&
                                        SpellManager.Spells["Demoralizing Banner"].CooldownTimeLeft.TotalSeconds <= 165 &&
                                        SpellManager.Spells["Charge"].Cooldown &&
                                        SpellManager.Spells["Heroic Leap"].Cooldown &&
                                       !SpellManager.Spells["Mocking Banner"].Cooldown &&
                                       !SpellManager.Spells["Intervene"].Cooldown &&
                                       !FriendlyUnitsNearTarget(6f).Any() &&
                                        StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25,
                            new Action(ret =>
                            {
                                SpellManager.Cast("Mocking Banner");
                                SpellManager.ClickRemoteLocation(StyxWoW.Me.CurrentTarget.Location);
                            }));
        }
        #endregion

        #region Coroutine Mocking Banner Auto

        private static async Task<bool> CoMockingBannerAuto()
        {
            if (SpellManager.Spells["Demoralizing Banner"].Cooldown &&
                SpellManager.Spells["Demoralizing Banner"].CooldownTimeLeft.TotalSeconds <= 165 &&
                SpellManager.Spells["Charge"].Cooldown &&
                SpellManager.Spells["Heroic Leap"].Cooldown &&
                !SpellManager.Spells["Mocking Banner"].Cooldown &&
                !SpellManager.Spells["Intervene"].Cooldown &&
                !FriendlyUnitsNearTarget(6f).Any() &&
                StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25)
            {
                await Spell.CoCastOnGround(MockingBanner);
            }

            return false;
        }

        #endregion

        #region ShatterBubbles
        static Composite ShatterBubbles()
        {
            return new Decorator(
                    ret => Me.CurrentTarget.IsPlayer &&
                          Me.CurrentTarget.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield") && Me.CurrentTarget.InLineOfSight,
                //Me.CurrentTarget.ActiveAuras.ContainsKey("Ice Block") ||
                //Me.CurrentTarget.ActiveAuras.ContainsKey("Hand of Protection") ||
                //Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield")),
                    new PrioritySelector(
                        Spell.Cast("Shattering Throw")));
        }
        #endregion

        #region Coroutine Shatter Bubbles
        private static Task<bool> CoShatterBubbles()
        {
            return Spell.CoCast(ShatteringThrow,
                        Me.CurrentTarget.IsPlayer &&
                        Me.CurrentTarget.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield") &&
                        Me.CurrentTarget.InLineOfSight);
        }
        #endregion

        #region InterruptCastNoChannel

        private static double InterruptCastNoChannel(WoWUnit target)
        {
            if (target == null || !target.IsPlayer)
            {
                return 0;
            }
            double timeLeft = 0;

            if (target.IsCasting && (//target.CastingSpell.Name == "Arcane Blast" ||
                ////target.CastingSpell.Name == "Banish" ||
                //target.CastingSpell.Name == "Binding Heal" ||
                                     target.CastingSpell.Name == "Cyclone" ||
                //target.CastingSpell.Name == "Chain Heal" ||
                //target.CastingSpell.Name == "Chain Lightning" ||
                //target.CastingSpell.Name == "Chi Burst" ||
                                     target.CastingSpell.Name == "Chaos Bolt" ||
                //target.CastingSpell.Name == "Demonic Circle: Summon" ||
                //target.CastingSpell.Name == "Denounce" ||
                //target.CastingSpell.Name == "Divine Light" ||
                //target.CastingSpell.Name == "Divine Plea" ||
                                     target.CastingSpell.Name == "Dominated Mind" ||
                                     target.CastingSpell.Name == "Elemental Blast" ||
                                     target.CastingSpell.Name == "Entangling Roots" ||
                //target.CastingSpell.Name == "Enveloping Mist" ||
                                     target.CastingSpell.Name == "Fear" ||
                //target.CastingSpell.Name == "Fireball" ||
                //target.CastingSpell.Name == "Flash Heal" ||
                //target.CastingSpell.Name == "Flash of Light" ||
                //target.CastingSpell.Name == "Frost Bomb" ||
                //target.CastingSpell.Name == "Frostjaw" ||
                //target.CastingSpell.Name == "Frostbolt" ||
                //target.CastingSpell.Name == "Frostfire Bolt" ||
                //target.CastingSpell.Name == "Greater Heal" ||
                //target.CastingSpell.Name == "Greater Healing Wave" ||
                //target.CastingSpell.Name == "Haunt" ||
                //target.CastingSpell.Name == "Heal" ||
                //target.CastingSpell.Name == "Healing Surge" ||
                //target.CastingSpell.Name == "Healing Touch" ||
                //target.CastingSpell.Name == "Healing Wave" ||
                                     target.CastingSpell.Name == "Hex" ||
                //target.CastingSpell.Name == "Holy Fire" ||
                //target.CastingSpell.Name == "Holy Light" ||
                //target.CastingSpell.Name == "Holy Radiance" ||
                //target.CastingSpell.Name == "Hibernate" ||
                                     target.CastingSpell.Name == "Mass Dispel" ||
                //target.CastingSpell.Name == "Mind Spike" ||
                //target.CastingSpell.Name == "Immolate" ||
                //target.CastingSpell.Name == "Incinerate" ||
                                     target.CastingSpell.Name == "Lava Burst" ||
                //target.CastingSpell.Name == "Mind Blast" ||
                //target.CastingSpell.Name == "Mind Spike" ||
                //target.CastingSpell.Name == "Nourish" ||
                                     target.CastingSpell.Name == "Polymorph" ||
                //target.CastingSpell.Name == "Prayer of Healing" ||
                //target.CastingSpell.Name == "Pyroblast" ||
                //target.CastingSpell.Name == "Rebirth" ||
                //target.CastingSpell.Name == "Regrowth" ||
                                     target.CastingSpell.Name == "Repentance" ||
                //target.CastingSpell.Name == "Scorch" ||
                //target.CastingSpell.Name == "Shadow Bolt" ||
                //target.CastingSpell.Name == "Shackle Undead"
                //target.CastingSpell.Name == "Smite" ||
                //target.CastingSpell.Name == "Soul Fire" ||
                //target.CastingSpell.Name == "Starfire" ||
                //target.CastingSpell.Name == "Starsurge" ||
                //target.CastingSpell.Name == "Surging Mist" ||
                //target.CastingSpell.Name == "Transcendence" ||
                //target.CastingSpell.Name == "Transcendence: Transfer" ||
                                    target.CastingSpell.Name == "Unstable Affliction"
                //target.CastingSpell.Name == "Vampiric Touch" ||
                //target.CastingSpell.Name == "Wrath")
                ))
            {
                timeLeft = target.CurrentCastTimeLeft.TotalMilliseconds;
            }
            return timeLeft;
        }

        #endregion

        #region InterruptCastChannel

        private static double InterruptCastChannel(WoWUnit target)
        {
            if (target == null || !target.IsPlayer)
            {
                return 0;
            }
            double timeLeft = 0;

            if (target.IsChanneling && (target.ChanneledSpell.Name == "Hymn of Hope" ||
                //target.ChanneledSpell.Name == "Arcane Barrage" ||
                                        target.ChanneledSpell.Name == "Evocation" ||
                //target.ChanneledSpell.Name == "Mana Tea" ||
                //target.ChanneledSpell.Name == "Crackling Jade Lightning" ||
                //target.ChanneledSpell.Name == "Malefic Grasp" ||
                //target.ChanneledSpell.Name == "Hellfire" ||
                                        target.ChanneledSpell.Name == "Harvest Life" ||
                                        target.ChanneledSpell.Name == "Health Funnel" ||
                                        target.ChanneledSpell.Name == "Drain Soul" ||
                //target.ChanneledSpell.Name == "Arcane Missiles" ||
                //target.ChanneledSpell.Name == "Mind Flay" ||
                //target.ChanneledSpell.Name == "Penance" ||
                //target.ChanneledSpell.Name == "Soothing Mist" ||
                                        target.ChanneledSpell.Name == "Tranquility" ||
                                        target.ChanneledSpell.Name == "Drain Life"))
            {
                timeLeft = target.CurrentChannelTimeLeft.TotalMilliseconds;
            }

            return timeLeft;
        }

        #endregion

        #region UpdateMyLatency

        public static readonly double MyLatency = 65;

        public static void UpdateMyLatency()
        {
            //if (THSettings.Instance.LagTolerance)
            //{
            //    //If SLagTolerance enabled, start casting next spell MyLatency Millisecond before GlobalCooldown ready.

            //    MyLatency = (StyxWoW.WoWClient.Latency);
            //    //MyLatency = 0;
            //    //Use here because Lag Tolerance cap at 400
            //    //Logging.Write("----------------------------------");
            //    //Logging.Write("MyLatency: " + MyLatency);
            //    //Logging.Write("----------------------------------");

            //    if (MyLatency > 400)
            //    {
            //        //Lag Tolerance cap at 400
            //        MyLatency = 400;
            //    }
            //}
            //else
            //{
            //    //MyLatency = 400;
            //    MyLatency = 0;
            //}
        }

        #endregion

        #region ValidUnit
        public static bool ValidUnit(WoWUnit p)
        {
            // Ignore shit we can't select/attack
            if (!p.CanSelect || !p.Attackable)
                return false;

            // Duh
            if (p.IsDead)
                return false;

            // check for players
            if (p.IsPlayer)
                return true;

            // Dummies/bosses are valid by default. Period.
            if (p.IsTrainingDummy())
                return true;

            // If its a pet, lets ignore it please.
            if (p.IsPet || p.OwnedByRoot != null)
                return false;

            // And ignore critters/non-combat pets
            if (p.IsNonCombatPet || p.IsCritter)
                return false;

            if (p.CreatedByUnitGuid != WoWGuid.Empty || p.SummonedByUnitGuid != WoWGuid.Empty)
                return false;

            return true;
        }
        #endregion
        #endregion


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
                          MassSpellReflection = 114028,
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
                          ShieldCharge = 156321,
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
