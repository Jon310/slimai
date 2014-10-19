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
                //Spell.WaitForCastOrChannel(),
                new Decorator(ret => (Me.Combat || healtarget.Combat) && !Me.Mounted,
                    new PrioritySelector(

                        new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                        new Decorator(ret => Me.ManaPercent <= 85,
                            new Action(ret => { Item.UseTrinkets(); return RunStatus.Failure; })),
                Common.CreateInterruptBehavior(),
                Spell.Cast(DivinePlea,
                            ret => Me.ManaPercent < 85),
                new Decorator(ret => SlimAI.Dispell,
                    Dispelling.CreateDispelBehavior()),
                new Decorator(ret => SlimAI.Burst,
                    new PrioritySelector(
                        Spell.Cast(DivineFavor,
                                    ret => HealerManager.GetCountWithHealth(60) > 3 && !Me.HasAnyAura("Guardian of Ancient Kings", "Avenging Wrath")),
                        Spell.Cast(AvengingWrath,
                                    ret => HealerManager.GetCountWithHealth(50) > 3 && !Me.HasAnyAura("Guardian of Ancient Kings","Divine Favor")),
                        Spell.Cast(GuardianofAncientKings,
                                    ret => HealerManager.GetCountWithHealth(40) > 3 && !Me.HasAnyAura("Avenging Wrath", "Divine Favor")))),
                LightsHammerCast(),
                
                SSCast(),                
                Spell.Cast("Light of Dawn",
                           ret => HealerManager.GetCountWithHealth(90) > 3 && (Me.CurrentHolyPower >= 3 || Me.HasAura("Divine Purpose"))),
                Spell.Cast("Word of Glory",
                           on => healtarget,
                           ret => healtarget.HealthPercent < 85 && (Me.CurrentHolyPower >= 3 || Me.HasAura("Divine Purpose"))),
                Spell.Cast("Holy Shock",
                           on => healtarget,
                           ret => healtarget.HealthPercent < 93),
                Spell.Cast(HolyPrism,
                            on => PrismTar()),
                            //ret => HealerManager.GetCountWithHealth(90) > 2),
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
                        )
                    )
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

            if (t != null && t.Count >= 2)
            {
                return t.Player;
            }

            return null;

        }
        #endregion

        #region SS
        private static Composite SSCast()
        {
            return new Decorator(ret =>
            {
                int rollCount = HealerManager.Instance.TargetList.Count(u => u.IsAlive && u.HasMyAura("Sacred Shield"));
                // Logger.WriteDebug("GetBestRiptideTarget:  currently {0} group members have my Riptide", rollCount);
                return rollCount < 3;
            },
                new PrioritySelector(
                    Spell.Cast(SacredShield, on =>
                    {
                        // if tank needs SS, bail out on Rolling as they have priority
                        //WoWUnit unit = GetBestSSTankTarget();
                        //return unit;
                        //if (GetBestSSTankTarget() != null)
                        //    return null;
                        // get the best target from all wowunits in our group
                        WoWUnit unit = GetBestSSTarget();
                        return unit;
                    }, ret => !GetBestSSTarget().HasAura("Sacred Shield"))));
        }

        private static WoWUnit GetBestSSTarget()
        {
            WoWUnit SSTarget = HealerManager.Instance.TargetList.Where(u => u.IsAlive && u.Combat && u.DistanceSqr < 40 * 40 && !u.HasAura("Sacred Shield") && u.InLineOfSpellSight).OrderBy(u => u.HealthPercent).FirstOrDefault();
            return SSTarget;
        }

        private static WoWUnit GetBestSSTankTarget()
        {
            WoWUnit SSTarget = Group.Tanks.Where(u => u.IsAlive && u.Combat && u.DistanceSqr < 40 * 40 && !u.HasAura("Sacred Shield") && u.InLineOfSpellSight).OrderBy(u => u.HealthPercent).FirstOrDefault();
            return SSTarget;
        }
        #endregion

        #region Prism tar
        private static WoWUnit PrismTar()
        {
            var prismtarget = Unit.NearbyUnitsInCombatWithMe.FirstOrDefault(u => u.IsTargetingUs() && u.IsHostile && Me.IsSafelyFacing(u) && Clusters.GetClusterCount(u, Unit.NearbyFriendlyPlayers, ClusterType.Radius, 15f) >= 2);
            return prismtarget;
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
