﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlimAI.Helpers;
using CommonBehaviors.Actions;
using SlimAI.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Class.Monk
{
    class WindwalkerMonk
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MonkSettings Settings { get { return GeneralSettings.Instance.Monk(); } }

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkWindwalker)]
        public static Composite WindwalkerCombat()
        {
            return new PrioritySelector(
                    /*Things to fix
                     * energy capping
                     * need to check healing spheres 
                     * chi capping? need to do more checking
                    */
                    new Throttle(1,
                        new Action(context => ResetVariables())),
                    new Decorator(ret => SlimAI.PvPRotation, 
                        CreatePvP()),
                    new Decorator(ret => !Me.Combat || Me.Mounted || !Me.GotTarget,
                        new ActionAlwaysSucceed()),
                Spell.Cast(SpearHandStrike, on => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.IsCasting && Me.CurrentTarget.CanInterruptCurrentSpellCast && u.IsWithinMeleeRange && Me.IsSafelyFacing(u))),

                    Spell.WaitForCastOrChannel(),

                    //Detox
                    new Decorator(ret => SlimAI.Dispell,
                        Dispelling.CreateDispelBehavior()),
                    //CreateDispelBehavior(),
                    //Healing Spheres need to work on
                    //Spell.CastOnGround("Healing Sphere", on => Me.Location, ret => Me.HealthPercent <= 50),

                    //new Action(ret => { Item.UseWaist(); return RunStatus.Failure; }),
                    new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),

                    //Tigerseye
                    Spell.Cast(TigereyeBrew, ret => Me.HasAura("Tigereye Brew", 18)),

                    new Throttle(2,
                    Spell.Cast(ChiBrew, ret => Me.CurrentChi < 2 && SlimAI.Weave/*&& Me.stackcount("Tigereye Brew") <= 1*/)),

                    Spell.Cast(EnergizingBrew, ret => Me.CurrentEnergy < 25),

                    // Execute if we can
                    Spell.Cast(TouchofDeath, ret => Me.HasAura("Death Note")),

                    Spell.Cast(TigerPalm, ret => Me.CurrentChi > 0 &&
                              (!Me.HasAura("Tiger Power") || Me.HasAura("Tiger Power") && Me.GetAuraTimeLeft("Tiger Power").TotalSeconds <= 3)),

                    //need to do some more work on this
                    //Spell.Cast(InvokeXuentheWhiteTiger, ret => SlimAI.Burst),

                    Spell.Cast(RisingSunKick),

                    //Spell ID 116740 = Tigerseye Brew the dmg buff part not the brewing
                    Spell.Cast(FistsofFury, ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u)) >= 1 &&
                              !Me.HasAura("Energizing Brew") && Me.EnergyPercent <= 50 && !PartyBuff.WeHaveBloodlust && !Me.IsMoving &&
                              SpellManager.Spells["Rising Sun Kick"].CooldownTimeLeft.TotalSeconds > 2.5 &&SlimAI.Burst),

                    //Chi Talents
                    Spell.Cast(ChiWave, ret => Me.EnergyPercent < 40),
                    //need to do math here and make it use 2 if im going to use it
                    //Spell.Cast("Zen Sphere", ret => !Me.HasAura("Zen Sphere")),

                    // free Tiger Palm or Blackout Kick... do before Jab
                    Spell.Cast(BlackoutKick, ret => Me.HasAura("Combo Breaker: Blackout Kick")),

                    Spell.Cast(TigerPalm, ret => Me.HasAura("Combo Breaker: Tiger Palm")),

                    new Decorator(ret => SlimAI.AOE,
                        new PrioritySelector(
                            Spell.Cast(SpinningCraneKick, ret => Unit.UnfriendlyUnits(8).Count() >= 4 && Me.CurrentChi < 3),
                            Spell.Cast(RushingJadeWind, ret => Unit.UnfriendlyUnits(8).Count() >= 3))),

                    Spell.Cast(ExpelHarm, ret => Me.CurrentChi <= 3 && Me.HealthPercent < 80),

                    //Spell.Cast(Jab, ret => Me.CurrentChi < 3 || (Me.CurrentChi <= 3 && SpellManager.HasSpell("Ascension"))),
                    Spell.Cast(Jab, ret => Me.CurrentChi < 3),

                    // chi dump
                    Spell.Cast(BlackoutKick, ret => Me.CurrentChi >= 3 && SpellManager.Spells["Rising Sun Kick"].CooldownTimeLeft.TotalSeconds > 1));
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Monk, WoWSpec.MonkWindwalker)]
        public static Composite WindwalkerPreCombatBuffs()
        {
            return new PrioritySelector(
                PartyBuff.BuffGroup("Legacy of the Emperor"),
                PartyBuff.BuffGroup("Legacy of the White Tiger"));
        }

        #region PvP
        private static Composite CreatePvP()
        {
            return new PrioritySelector(

                Spell.WaitForCastOrChannel(),
                ZenMed(),
                ParalysisFocus(),

                new Decorator(ret => !Me.Combat || Me.Mounted,
                    new ActionAlwaysSucceed()),

                //new Decorator(ret => SpellManager.CanCast(Paralysis) && SlimAI.AFK,
                //    new PrioritySelector(
                //        Spell.Cast(Paralysis, on => Me.FocusedUnit, ret => Me.FocusedUnit.InLineOfSight))),

                Spell.Cast(Disable, ret => !Me.CurrentTarget.HasAura(Disable)),

                new Throttle(5,
                //Spell.Cast(SpinningFireBlossom, ret => Clusters.GetCluster(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Path, 40).Count(u => !u.HasAura(123407) && !u.HasAura(Disable) &&
                //                            u.Distance > 10 && Me.IsSafelyFacing(u)) >=1 )),
                //Spell.Cast(SpinningFireBlossom, ret => Unit.NearbyUnfriendlyUnits.Count(u => !Me.CurrentTarget.HasAura(Disable) && !Me.CurrentTarget.HasAura(123407) && Me.CurrentTarget.Distance > 10 && Me.IsSafelyFacing(u)) >=1)),
                Spell.Cast(SpinningFireBlossom, ret => !Me.CurrentTarget.HasAuraWithEffect(WoWApplyAuraType.ModDecreaseSpeed) && !Me.CurrentTarget.HasAura(123407) && Me.CurrentTarget.Distance > 10 && Me.CurrentTarget.InLineOfSight)),
                
                //Detox
                //new Decorator(ret => SlimAI.Dispell,
                //    Dispelling.CreateDispelBehavior()),

                //Healing Spheres need to work on
                //Spell.CastOnGround("Healing Sphere", on => Me.Location, ret => Me.HealthPercent <= 50),

                new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),

                //Tigerseye
                //Spell.Cast(TigereyeBrew, ret => Me.HasAura("Tigereye Brew", 18)),

                new Throttle(2,
                Spell.Cast(ChiBrew, ret => Me.CurrentChi < 2 && SlimAI.Weave/*&& Me.stackcount("Tigereye Brew") <= 1*/)),

                Spell.Cast(EnergizingBrew, ret => Me.CurrentEnergy < 25),

                // Execute if we can
                Spell.Cast(TouchofDeath, ret => Me.HasAura("Death Note")),

                Spell.Cast(TigerPalm, ret => Me.CurrentChi > 0 &&
                            (!Me.HasAura("Tiger Power") || Me.HasAura("Tiger Power") && Me.GetAuraTimeLeft("Tiger Power").TotalSeconds <= 3)),

                //need to do some more work on this
                Spell.Cast(InvokeXuentheWhiteTiger, ret => SlimAI.Burst),

                Spell.Cast(RisingSunKick),

                //Spell ID 116740 = Tigerseye Brew the dmg buff part not the brewing
                //Spell.Cast(FistsofFury, ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u)) >= 1 &&
                //            !Me.HasAura("Energizing Brew") && Me.EnergyPercent <= 50 && !PartyBuff.WeHaveBloodlust && !Me.IsMoving &&
                //            SpellManager.Spells["Rising Sun Kick"].CooldownTimeLeft.TotalSeconds > 2.5 && SlimAI.Burst),

                //Chi Talents
                Spell.Cast(ChiWave, ret =>  Me.HealthPercent < 85),
                //need to do math here and make it use 2 if im going to use it
                //Spell.Cast("Zen Sphere", ret => !Me.HasAura("Zen Sphere")),

                // free Tiger Palm or Blackout Kick... do before Jab
                Spell.Cast(BlackoutKick, ret => Me.HasAura("Combo Breaker: Blackout Kick")),

                Spell.Cast(TigerPalm, ret => Me.HasAura("Combo Breaker: Tiger Palm")),

                Spell.Cast(ExpelHarm, ret => Me.CurrentChi <= 3 && Me.HealthPercent < 80),

                Spell.Cast(Jab, ret => Me.CurrentChi < 4),

                // chi dump
                Spell.Cast(BlackoutKick, ret => Me.CurrentChi >= 4 || Me.CurrentChi >= 2 && SpellManager.Spells["Rising Sun Kick"].CooldownTimeLeft.TotalSeconds > 3),
                new ActionAlwaysSucceed()
            );
        }
        #endregion

        #region Dispelling
        public static WoWUnit dispeltar
        {
            get
            {
                var dispelothers = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWPlayer>()
                                    where unit.IsAlive
                                    where Dispelling.CanDispel(unit)
                                    select unit).OrderByDescending(u => u.HealthPercent).LastOrDefault();
                return dispelothers;
            }
        }
        
        public static Composite CreateDispelBehavior()
        {
            return new PrioritySelector(
                Spell.Cast("Detox", on => Me, ret => Dispelling.CanDispel(Me)),
                Spell.Cast("Detox", on => dispeltar, ret => Dispelling.CanDispel(dispeltar)));
        }
        #endregion

        #region Uility
        private static Composite ParalysisFocus()
        {
            return
                new Decorator(ret => SpellManager.CanCast(Paralysis) &&
                    KeyboardPolling.IsKeyDown(Keys.C),
                    new PrioritySelector(
                        Spell.Cast(Paralysis, on => Me.FocusedUnit))  
                    //new Action(ret => Spell.Cast(Paralysis, on => Me.FocusedUnit))
                    );
        }

        private static Composite ZenMed()
        {
            return
                new Decorator(ret => SpellManager.CanCast(ZenMeditation) &&
                    KeyboardPolling.IsKeyDown(Keys.Z),
                    new Action(ret =>
                    {
                        SpellManager.Cast(ZenMeditation);
                        return;
                    }));
        }

        private static RunStatus ResetVariables()
        {
            KeyboardPolling.IsKeyDown(Keys.Z);
            KeyboardPolling.IsKeyDown(Keys.C);
            return RunStatus.Failure;
        }
        #endregion

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
                          ChiBrew = 115399,
                          ChiWave = 115098,
                          Disable = 116095,
                          ElusiveBrew = 115308,
                          EnergizingBrew = 115288,
                          ExpelHarm = 115072,
                          FistsofFury = 113656,
                          Guard = 115295,
                          InvokeXuentheWhiteTiger = 123904,
                          Jab = 100780,
                          KegSmash = 121253,
                          LegacyoftheEmperor = 115921,
                          LegacyoftheWhiteTiger = 116781,
                          Paralysis = 115078,
                          PurifyingBrew = 119582,
                          RisingSunKick = 107428,
                          RushingJadeWind = 116847,
                          SpearHandStrike = 116705,
                          SpinningFireBlossom = 115073,
                          SpinningCraneKick = 101546,
                          StanceoftheSturdyOx = 115069,
                          SummonBlackOxStatue = 115315,
                          TigereyeBrew = 116740,
                          TigerPalm = 100787,
                          TouchofDeath = 115080,
                          ZenMeditation = 115176,
                          ZenSphere = 124081;
        #endregion

    }
}
