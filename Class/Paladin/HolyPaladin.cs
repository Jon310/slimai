using SlimAI.Managers;
using CommonBehaviors.Actions;
using SlimAI.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using SlimAI.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Class.Paladin
{
    class HolyPaladin
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        static WoWUnit healtarget { get { return HealerManager.FindLowestHealthTarget(); } }
        private static PaladinSettings Settings { get { return GeneralSettings.Instance.Paladin(); } }
        
        [Behavior(BehaviorType.Combat, WoWClass.Paladin, WoWSpec.PaladinHoly)]
        public static Composite HolyCombat()
        {
            HealerManager.NeedHealTargeting = true;

            var cancelHeal = Math.Max(95, Math.Max(93, Math.Max(55, 25)));//95,93,55,25
            return new PrioritySelector(
                new Decorator(ret => !Me.Combat && !Me.CurrentTarget.IsAlive && Me.IsCasting,
                              new ActionAlwaysSucceed()),
                Common.CreateInterruptBehavior(),
                Spell.Cast(DivinePlea,
                            ret => Me.ManaPercent < 85),
                new Decorator(ret => SlimAI.Burst,
                    new PrioritySelector(
                        Spell.Cast(DivineFavor,
                                    ret => HealerManager.GetCountWithHealth(60) > 3),
                        Spell.Cast(AvengingWrath,
                                    ret => HealerManager.GetCountWithHealth(50) > 3),
                        Spell.Cast(GuardianofAncientKings,
                                    ret => HealerManager.GetCountWithHealth(40) > 3))),
                LightsHammerCast(),

                Spell.Cast(SacredShield,
                           on => Tanking,
                           ret => healtarget.HealthPercent < 100),                
                Spell.Cast("Light of Dawn",
                           on => GetBestHolyRadianceTarget(),
                           ret => healtarget.HealthPercent < 93 && Me.CurrentHolyPower >= 3,
                           cancel => healtarget.HealthPercent > cancelHeal),
                Spell.Cast("Word of Glory",
                           on => healtarget,
                           ret => healtarget.HealthPercent < 85 && Me.CurrentHolyPower >= 3,
                           cancel => healtarget.HealthPercent > cancelHeal),
                Spell.Cast("Holy Shock",
                           on => healtarget,
                           ret => healtarget.HealthPercent < 93,
                           cancel => healtarget.HealthPercent > cancelHeal),
                HolyRadianceCast(),
                //Spell.Cast("Holy Radiance",
                //           on => healtarget,
                //           ret => healtarget.HealthPercent < 93,
                //           cancel => healtarget.HealthPercent > cancelHeal),
                Spell.Cast("Divine Light",
                           on => healtarget,
                           ret => healtarget.HealthPercent < 55,
                           cancel => healtarget.HealthPercent > cancelHeal),
                Spell.Cast("Flash of Light",
                           on => healtarget,
                           ret => healtarget.HealthPercent < 25,
                           cancel => healtarget.HealthPercent > cancelHeal),
                Spell.Cast("Holy Light",
                           on => healtarget,
                           ret => healtarget.HealthPercent < 93,
                           cancel => healtarget.HealthPercent > cancelHeal)

                );
        }

        #region AoE Heals
        private static Composite LightsHammerCast()
        {
            return new PrioritySelector(
                context => GetBestHolyRadianceTarget(),
                new Decorator(
                    ret => ret != null,
                    new PrioritySelector(
                        new Sequence(
                            
                            Common.CreateWaitForLagDuration(ret => Spell.IsGlobalCooldown()),
                            new WaitContinue(TimeSpan.FromMilliseconds(1500),
                                             until => !Spell.IsGlobalCooldown(LagTolerance.No),
                                             new ActionAlwaysSucceed()),
                            Spell.CastOnGround("Light's Hammer", on => (WoWUnit)on, req => true, false)))));
        }        

        private static Composite HolyRadianceCast()
        {
            return new PrioritySelector(
                context => GetBestHolyRadianceTarget(),
                new Decorator(
                    ret => ret != null,
                    new PrioritySelector(
                        new Sequence(
                            Common.CreateWaitForLagDuration(ret => Spell.IsGlobalCooldown()),
                            new WaitContinue(TimeSpan.FromMilliseconds(1500),
                                             until => !Spell.IsGlobalCooldown(LagTolerance.No),
                                             new ActionAlwaysSucceed()),
                            Spell.Cast("Holy Radiance", on => (WoWUnit)on)))));
        }

        private static WoWUnit GetBestHolyRadianceTarget()
        {
            if (!Me.IsInGroup() || !Me.Combat)
                return null;

            if (!Spell.CanCastHack("Holy Radiance", Me, skipWowCheck: true))
            {
                // Logger.WriteDebug("GetBestHealingRainTarget: CanCastHack says NO to Healing Rain");
                return null;
            }

            // note: expensive, but worth it to optimize placement of Healing Rain by
            // finding location with most heals, but if tied one with most living targets also
            // build temp list of targets that could use heal and are in range + radius
            List<WoWUnit> coveredTargets = HealerManager.Instance.TargetList
                .Where(u => u.IsAlive && u.DistanceSqr < 50 * 50)
                .ToList();
            List<WoWUnit> coveredRadianceTargets = coveredTargets
                .Where(u => u.HealthPercent < 95)
                .ToList();

            // search all targets to find best one in best location to use as anchor for cast on ground
            var t = coveredTargets
                .Where(u => u.DistanceSqr < 40 * 40)
                .Select(p => new
                {
                    Player = p,
                    Count = coveredRadianceTargets.Count(pp => pp.Location.DistanceSqr(p.Location) < 10 * 10),
                    Covered = coveredTargets.Count(pp => pp.Location.DistanceSqr(p.Location) < 10 * 10)
                })
                .OrderByDescending(v => v.Count)
                .ThenByDescending(v => v.Covered)
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            if (t != null && t.Count >= 3)
            {
                return t.Player;
            }

            return null;

        }
        #endregion

        #region Tank Finder

        private static WoWUnit Tanking
        {
            get
            {
                var tank = Group.Tanks.FirstOrDefault(u => StyxWoW.Me.CurrentTarget.ThreatInfo.TargetGuid == u.Guid && u.Distance < 40);
                return tank;
            }
        }
        #endregion

        #region Paladian Spells
        private const int
            ArdentDefender = 31850,
            AvengersShield = 31935,
            AvengingWrath = 31884,
            BlessingofKings = 20217,
            BlessingofMight = 19740,
            BlindingLight = 115750,
            Cleanse = 4987,
            Consecration = 26573,
            CrusaderStrike = 35395,
            DevotionAura = 31821,
            DivineFavor = 31842,
            DivinePlea = 54428,
            DivineProtection = 498,
            DivineShield = 642,
            EternalFlame = 114163,
            ExecutionSentence = 114157,
            FistofJustice = 105593,
            FlashofLight = 19750,
            GrandCrusader = 85416,
            GuardianofAncientKings = 86659,
            HammeroftheRighteous = 53595,
            HammerofWrath = 24275,
            HandofFreedom = 1044,
            HandofProtection = 1022,
            HandofPurity = 114039,
            HandofSacrifice = 6940,
            HandofSalvation = 1038,
            HolyAvenger = 105809,
            HolyPrism = 114165,
            HolyWrath = 119072,
            Judgment = 20271,
            LayonHands = 633,
            LightsHammer = 114158,
            Rebuke = 96231,
            Reckoning = 62124,
            Redemption = 7328,
            Repentance = 20066,
            RighteousFury = 25780,
            SacredShield = 20925,
            SealofInsight = 20165,
            SealofRighteousness = 20154,
            SealofTruth = 31801,
            ShieldoftheRighteous = 53600,
            SpeedofLight = 85499,
            TurnEvil = 10326,
            WordofGlory = 85673;
        #endregion

    }
}
