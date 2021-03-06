﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Buddy.Coroutines;
using JetBrains.Annotations;
using CommonBehaviors.Actions;
using SlimAI.Helpers;
using SlimAI.Lists;
using SlimAI.Managers;
using SlimAI.Settings;
using Styx;
using SlimAI.Class;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Class.Paladin
{
        [UsedImplicitly]
        class RetributionPaladin
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PaladinSettings Settings { get { return GeneralSettings.Instance.Paladin(); } }

        #region Coroutine Combat

        private static async Task<bool> CombatCoroutine()
        {

            if (SlimAI.PvPRotation)
            {
                await PvPCoroutine();
                return true;
            }

            if (!Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive) return true;
            //Common.CreateInterruptBehavior(),
            //Dispelling.CreateDispelBehavior(),

            await Spell.CoCast(AvengingWrath, SlimAI.Burst);
            await Spell.CoCast(HolyAvenger, Me.HasAura(AvengingWrath) && SlimAI.Burst);
            await Spell.CoCast(DivineShield, Me.HealthPercent <= 20 && SlimAI.Weave);

            //new Action(ret => { Item.UseTrinkets(); return RunStatus.Failure; }))),
            await Spell.CoCast(FlashofLight, FlashTarclutch, Me.HasAura("Selfless Healer", 3));
            await Spell.CoCast(SealofRighteousness, SlimAI.AOE && !Me.HasAura("Seal of Righteousness") && Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius,8f) >= 4);
            await Spell.CoCast(SealofTruth, !Me.HasAura("Seal of Truth") && Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius,8f) < 4);
            await Spell.CoCast(ExecutionSentence, SlimAI.Burst);
            await Spell.CoCastOnGround(LightsHammer, Me.Location, Me.CurrentTarget.IsBoss && SlimAI.AOE);
            //await Spell.CoCast(DivineStorm, (Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f) >= 2 && (Me.CurrentHolyPower == 5 || Me.HasAura("Divine Purpose"))) && SlimAI.AOE && Me.CurrentTarget.Distance <= 8);
            await Spell.CoCast(TemplarsVerdict, Me.CurrentHolyPower == 5 || Me.HasAura("Divine Purpose"));
            await Spell.CoCast(HammerofWrath);
            await Spell.CoCast(DivineStorm, SlimAI.AOE && Me.HasAura("Divine Crusader") && Me.HasAura("Final Verdict") && Me.CurrentTarget.Distance <= 8 && (Me.HasAura(AvengingWrath) || Me.CurrentTarget.HealthPercent < 35));
            await Spell.CoCast(HammeroftheRighteous, Me.CurrentHolyPower <= 4 && Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f) >= 2 && SlimAI.AOE);
            await Spell.CoCast(CrusaderStrike, Me.CurrentHolyPower <= 4);
            await Spell.CoCast(DivineStorm, SlimAI.AOE && Me.HasAura("Divine Crusader") && Me.HasAura("Final Verdict") && Me.CurrentTarget.Distance <= 8);
            await Spell.CoCast(Judgment, SecTar, Me.CurrentHolyPower <= 4 && Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 15f) >= 2 && Me.HasAura("Glyph of Double Jeopardy"));
            await Spell.CoCast(Judgment, Me.CurrentHolyPower <= 4);
            //await Spell.CoCast(DivineStorm, Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f) >= 2 && SlimAI.AOE && Me.CurrentTarget.Distance <= 8);
            await Spell.CoCast(TemplarsVerdict);
            await Spell.CoCast(Exorcism, Me.CurrentHolyPower <= 4);
            await Spell.CoCast(HolyPrism);
            //D	5.46	execution_sentence
            //await Spell.CoCast(ExecutionSentence, SlimAI.Burst);
            ////t	0.14	divine_storm,if=buff.divine_crusader.react&holy_power=5&buff.final_verdict.up
            //await Spell.CoCast(DivineStorm, Me.HasAura("Divine Crusader") && Me.CurrentHolyPower == 5 && Me.HasAura("Final Verdict"));
            ////v	0.00	divine_storm,if=holy_power=5&active_enemies=2&buff.final_verdict.up
            //await Spell.CoCast(DivineStorm, Me.CurrentHolyPower == 5 && Unit.EnemyUnitsSub8.Count() == 2 && Me.HasAura("Final Verdict"));
            ////x	0.00	templars_verdict,if=holy_power=5|buff.holy_avenger.up&holy_power>=3&(!talent.seraphim.enabled|cooldown.seraphim.remains>5)
            //await Spell.CoCast(TemplarsVerdict, Me.CurrentHolyPower == 5 || (Me.HasAura("Holy Avenger") && Me.CurrentHolyPower >=3));
            ////y	0.00	templars_verdict,if=buff.divine_purpose.react&buff.divine_purpose.remains<3
            //await Spell.CoCast(TemplarsVerdict, Me.HasAura("Divine Purpose") && Me.HasAuraExpired("Divine Purpose"));
            ////{	2.92	final_verdict,if=holy_power=5|buff.holy_avenger.up&holy_power>=3
            //await Spell.CoCast(FinalVerdict, Me.CurrentHolyPower == 5 || Me.HasAura("Holy Avenger") && Me.CurrentHolyPower >= 3);
            ////|	0.27	final_verdict,if=buff.divine_purpose.react&buff.divine_purpose.remains<4
            //await Spell.CoCast(FinalVerdict, Me.HasAura("Divine Purpose") && Me.HasAuraExpired("Divine Purpose", 4));
            ////}	30.15	hammer_of_wrath
            //await Spell.CoCast(HammerofWrath);
            ////"	0.00	exorcism,if=buff.blazing_contempt.up&holy_power<=2&buff.holy_avenger.down
            //await Spell.CoCast(Exorcism, Me.HasAura("Blazing Contempt") && Me.CurrentHolyPower <= 2 && !Me.HasAura("Holy Avenger"));
            ////%	10.76	divine_storm,if=buff.divine_crusader.react&buff.final_verdict.up&(buff.avenging_wrath.up|target.health.pct<35)
            //await Spell.CoCast(DivineStorm, Me.HasAura("Divine Crusader") && Me.HasAura("Final Verdict") && (Me.HasAura("Avenging Wrath") || Me.CurrentTarget.HealthPercent < 35));
            ////&	34.86	final_verdict,if=buff.avenging_wrath.up|target.health.pct<35
            //await Spell.CoCast(FinalVerdict, Me.HasAura("Avenging Wrath") || Me.CurrentTarget.HealthPercent < 35);
            ////'	0.00	templars_verdict,if=buff.avenging_wrath.up|target.health.pct<35&(!talent.seraphim.enabled|cooldown.seraphim.remains>6)
            //await Spell.CoCast(TemplarsVerdict, Me.HasAura("Avenging Wrath") || Me.CurrentTarget.HealthPercent < 35);
            ////(	66.35	crusader_strike,if=holy_power<5
            //await Spell.CoCast(CrusaderStrike, Me.CurrentHolyPower < 5);
            ////*	10.89	divine_storm,if=buff.divine_crusader.react&buff.final_verdict.up
            //await Spell.CoCast(DivineStorm, Me.HasAura("Divine Crusader") && Me.HasAura("Final Verdict"));
            ////+	10.70	final_verdict,if=buff.divine_purpose.react
            //await Spell.CoCast(FinalVerdict, Me.HasAura("Divine Purpose"));
            ////,	11.63	final_verdict,if=holy_power>=4
            //await Spell.CoCast(FinalVerdict, Me.CurrentHolyPower >= 4);
            ////-	1.00	judgment,cycle_targets=1,if=last_judgment_target!=target&glyph.double_jeopardy.enabled&holy_power<5&cooldown.seraphim.remains<=3

            ////:	0.00	exorcism,if=glyph.mass_exorcism.enabled&active_enemies>=2&holy_power<5
            //await Spell.CoCast(Exorcism, Unit.EnemyUnitsSub8.Count() >= 2 && Me.CurrentHolyPower < 5);
            ////;	39.79	judgment,,if=holy_power<5
            //await Spell.CoCast(Judgment, Me.CurrentHolyPower < 5);
            ////<	11.21	final_verdict,if=holy_power>=3
            //await Spell.CoCast(FinalVerdict, Me.CurrentHolyPower >= 3);
            ////>	0.00	templars_verdict,if=buff.divine_purpose.react
            //await Spell.CoCast(TemplarsVerdict, Me.HasAura("Divine Purpose"));
            ////@	0.00	templars_verdict,if=holy_power>=4&(!talent.seraphim.enabled|cooldown.seraphim.remains>7)
            //await Spell.CoCast(TemplarsVerdict, Me.CurrentHolyPower >= 4);
            ////.	10.77	exorcism,if=holy_power<5
            //await Spell.CoCast(Exorcism, Me.CurrentHolyPower < 5);
            ////.	0.00	templars_verdict,if=holy_power>=3&(!talent.seraphim.enabled|cooldown.seraphim.remains>9)
            //await Spell.CoCast(TemplarsVerdict, Me.CurrentHolyPower >= 3);
            ////.	0.00	holy_prism
            //await Spell.CoCast(HolyPrism);







            return false;
        }

        [Behavior(BehaviorType.Combat, WoWClass.Paladin, WoWSpec.PaladinRetribution)]
        public static Composite CoRetributionCombat()
        {
            return new ActionRunCoroutine(ctx => CombatCoroutine());
        }
        #endregion


            #region PvP

            private static async Task<bool> PvPCoroutine()
            {

                if (Me.CurrentTarget.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield") || !Me.Combat || Me.Mounted) return true;


                if (StyxWoW.Me.CurrentTarget != null && (!StyxWoW.Me.CurrentTarget.IsWithinMeleeRange || StyxWoW.Me.IsCasting || SpellManager.GlobalCooldown)) return true;

                await Spell.CoCast(HandofFreedom, Me, (Me.IsSlowed() && Me.HasAura(AvengingWrath)) || Me.CurrentTarget.HealthPercent <= 30);
                await Spell.CoCast(AvengingWrath, SlimAI.Burst);
                await Spell.CoCast(HolyAvenger, Me.HasAura(AvengingWrath) && SlimAI.Burst);
                await Spell.CoCast(DivineShield, Me.HealthPercent <= 20 && SlimAI.Weave);

                //new Action(ret => { Item.UseTrinkets(); return RunStatus.Failure; }))),
                await Spell.CoCast(FlashofLight, FlashTarclutch, Me.HasAura("Selfless Healer", 3));
                await Spell.CoCast(ExecutionSentence, SlimAI.Burst);
                //await Spell.CoCastOnGround(LightsHammer, Me.Location, Me.CurrentTarget.IsBoss);
                await Spell.CoCast(DivineStorm, (Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f) >= 2 && (Me.CurrentHolyPower == 5 || Me.HasAura("Divine Purpose"))) && SlimAI.AOE && Me.CurrentTarget.Distance <= 8);
                await Spell.CoCast(TemplarsVerdict, Me.CurrentHolyPower == 5 || Me.HasAura("Divine Purpose"));
                await Spell.CoCast(HammerofWrath);
                await Spell.CoCast(DivineStorm, Me.HasAura("Divine Crusader") && Me.HasAura("Final Verdict") && Me.CurrentTarget.Distance <= 8 && (Me.HasAura(AvengingWrath) || Me.CurrentTarget.HealthPercent < 35));
                await Spell.CoCast(HammeroftheRighteous, Me.CurrentHolyPower <= 4 && Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f) >= 2 && SlimAI.AOE);
                await Spell.CoCast(CrusaderStrike);
                await Spell.CoCast(DivineStorm, Me.HasAura("Divine Crusader") && Me.HasAura("Final Verdict") && Me.CurrentTarget.Distance <= 8);
                //await Spell.CoCast("Judgment", on => SecTar, ret => Me.CurrentHolyPower <= 4 && Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius,15f) >= 2 && Me.HasAura("Glyph of Double Jeopardy"));
                await Spell.CoCast(Judgment);
                await Spell.CoCast(DivineStorm, Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f) >= 2 && SlimAI.AOE && Me.CurrentTarget.Distance <= 8);
                await Spell.CoCast(TemplarsVerdict);
                await Spell.CoCast(Exorcism);
                await Spell.CoCast(HolyPrism);
                await Spell.CoCast(FlashofLight, FlashTarLow, Me.HasAura("Selfless Healer", 3));

                return true;
            }


            #endregion


            #region SecTar
            public static WoWUnit SecTar
            {
                get
                {
                    if (!StyxWoW.Me.GroupInfo.IsInParty)
                        return null;
                    if (StyxWoW.Me.GroupInfo.IsInParty)
                    {
                        var secondTarget = (from unit in ObjectManager.GetObjectsOfType<WoWUnit>(false)
                                            where unit.IsAlive
                                            where unit.IsHostile
                                            where unit.Distance < 30
                                            where unit.IsTargetingMyPartyMember || unit.IsTargetingMyRaidMember
                                            where unit.InLineOfSight
                                            where unit.Guid != Me.CurrentTarget.Guid
                                            select unit).FirstOrDefault();
                        return secondTarget;
                    }
                    return null;
                }
            }
            #endregion

            private static WoWUnit FlashTarclutch
            {
                get
                {
                    var eHheal = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWPlayer>()
                                  where unit.IsAlive
                                  where unit.IsInMyPartyOrRaid
                                  where unit.Distance < 40
                                  where unit.InLineOfSight
                                  where unit.HealthPercent <= 35
                                  select unit).OrderByDescending(u => u.HealthPercent).LastOrDefault();
                    return eHheal;
                }
            }

            private static WoWUnit FlashTarLow
            {
                get
                {
                    var eHheal = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWPlayer>()
                                  where unit.IsAlive
                                  where unit.IsInMyPartyOrRaid
                                  where unit.Distance < 40
                                  where unit.InLineOfSight
                                  where unit.HealthPercent <= 60
                                  select unit).OrderByDescending(u => u.HealthPercent).LastOrDefault();
                    return eHheal;
                }
            }

        #region PaladinTalents
        public enum PaladinTalents
        {
            SpeedofLight = 1,//Tier 1
            LongArmoftheLaw,
            PersuitofJustice,
            FistofJustice,//Tier 2
            Repentance,
            BurdenofGuilt,
            SelflessHealer,//Tier 3
            EternalFlame,
            SacredShield,
            HandofPurity,//Tier 4
            UnbreakableSpirit,
            Clemency,
            HolyAvenger,//Tier 5
            SanctifiedWrath,
            DivinePurpose,
            HolyPrism,//Tier 6
            LightsHammer,
            ExecutionSentence
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
            DivineProtection = 498,
            DivineShield = 642,
            DivineStorm = 53385,
            EternalFlame = 114163,
            ExecutionSentence = 114157,
            Exorcism = 879,
            FinalVerdict = 157048,
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
            TemplarsVerdict = 85256,
            TurnEvil = 10326,
            WordofGlory = 85673;
        #endregion
    }
}

