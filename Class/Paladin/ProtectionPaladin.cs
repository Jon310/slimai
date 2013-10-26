using System;
using System.Linq;
using SlimAI.Helpers;
using SlimAI.Managers;
using CommonBehaviors.Actions;
using SlimAI.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Class.Paladin
{
    class ProtectionPaladin
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PaladinSettings Settings { get { return GeneralSettings.Instance.Paladin(); } }
        
        [Behavior(BehaviorType.Combat, WoWClass.Paladin, WoWSpec.PaladinProtection)]
        public static Composite ProtectionCombat()
        {
            return new PrioritySelector(
                new Decorator(ret => !Me.Combat && !Me.CurrentTarget.IsAlive && Me.IsCasting,
                    new ActionAlwaysSucceed()),
                Common.CreateInterruptBehavior(),
                new Throttle( TimeSpan.FromMilliseconds(500),
                    new Sequence(
                        new Action( ret => _seal = GetBestSeal()),
                        new Decorator(ret => !Me.HasMyAura(SealSpell(_seal)) && Spell.CanCastHack(SealSpell(_seal), Me),
                            Spell.Cast( s => SealSpell(_seal), on => Me, ret => !Me.HasAura(SealSpell(_seal)))))),

                //Staying alive
                Spell.Cast(SacredShield, on => Me, ret => !Me.HasAura("Sacred Shield") && SpellManager.HasSpell("Sacred Shield")),
                Spell.Cast(LayonHands, on => Me, ret => Me.HealthPercent <= 10 && !Me.HasAura("Forbearance")),
                Spell.Cast(ArdentDefender, ret => Me.HealthPercent <= 15 && Me.HasAura("Forbearance")),
                Spell.Cast(DivineProtection, ret => Me.HealthPercent <= 80 && !Me.HasAura("Shield of the Righteous")),

                Spell.Cast(WordofGlory, ret => Me.HealthPercent < 50 && (Me.CurrentHolyPower >= 3 || Me.HasAura("Divine Purpose"))),
                Spell.Cast(WordofGlory, ret => Me.HealthPercent < 25 && (Me.CurrentHolyPower >= 2 || Me.HasAura("Divine Purpose"))),
                Spell.Cast(WordofGlory, ret => Me.HealthPercent < 15 && (Me.CurrentHolyPower >= 1 || Me.HasAura("Divine Purpose"))),

                //Prot T15 2pc 
                new Decorator(ret => SlimAI.AFK && !Me.HasAura("Shield of Glory"),
                    new PrioritySelector(
                        Spell.Cast(WordofGlory, ret => Me.HealthPercent < 90 && Me.CurrentHolyPower == 1),
                        Spell.Cast(WordofGlory, ret => Me.HealthPercent < 75 && Me.CurrentHolyPower <= 2),
                        Spell.Cast(WordofGlory, ret => Me.HealthPercent < 50 && (Me.CurrentHolyPower >= 3 || Me.HasAura("Divine Purpose"))))),

                CreateDispelBehavior(),

                new Decorator(ret => Unit.UnfriendlyUnits(8).Count() >= 2,
                    CreateAoe()),

                Spell.Cast(ShieldoftheRighteous, ret => (Me.CurrentHolyPower == 5 || Me.HasAura("Divine Purpose")) && SlimAI.Burst),
                Spell.Cast(HammeroftheRighteous, ret => !Me.CurrentTarget.HasAura("Weakened Blows")),
                Spell.Cast(Judgment, ret => SpellManager.HasSpell("Sanctified Wrath") && Me.HasAura("Avenging Wrath")),
                Spell.Cast(AvengersShield, ret => Me.HasAura(GrandCrusader)),
                Spell.Cast(CrusaderStrike),
                Spell.Cast(Judgment),
                new Decorator(ret => Spell.GetSpellCooldown("Judgment").TotalSeconds >= 1 && Spell.GetSpellCooldown("Crusader Strike").TotalSeconds >= 1 && !Me.HasAura(GrandCrusader),
                    new PrioritySelector(
                        LightsHammer(),
                        Spell.Cast(HolyPrism, on => Unit.UnfriendlyUnits(15).Count() >= 2 ? Me : Me.CurrentTarget),
                        Spell.Cast(ExecutionSentence),
                        Spell.Cast(HammerofWrath),
                        Spell.Cast(ShieldoftheRighteous, ret => Me.CurrentHolyPower >= 3 && SlimAI.Burst),
                        Spell.Cast(AvengersShield),
                        Spell.Cast(Consecration, ret => !Me.IsMoving && Me.CurrentTarget.IsWithinMeleeRange),
                        Spell.Cast(HolyWrath, ret => Me.CurrentTarget.IsWithinMeleeRange))));
        }
        
        private static Composite CreateAoe()
        {
            return new PrioritySelector(
                Spell.Cast(ShieldoftheRighteous, ret => (Me.CurrentHolyPower == 5 || Me.HasAura("Divine Purpose")) && SlimAI.Burst),
                Spell.Cast(Judgment, ret => SpellManager.HasSpell("Sanctified Wrath") && Me.HasAura("Avenging Wrath")),
                Spell.Cast(HammeroftheRighteous),
                Spell.Cast(Judgment),
                Spell.Cast(AvengersShield, ret => Me.HasAura(GrandCrusader)),
                new Decorator(ret => Spell.GetSpellCooldown("Judgment").TotalSeconds >= 1 && Spell.GetSpellCooldown("Crusader Strike").TotalSeconds >= 1 && !Me.HasAura(GrandCrusader),
                    new PrioritySelector(
                        LightsHammer(),
                        Spell.Cast(HolyPrism, on => Me, ret => Me.HealthPercent <= 90),
                        Spell.Cast(ExecutionSentence),
                        Spell.Cast(HammerofWrath),
                        Spell.Cast(ShieldoftheRighteous, ret => Me.CurrentHolyPower >= 3 && SlimAI.Burst),
                        Spell.Cast(Consecration, ret => !Me.IsMoving && Me.CurrentTarget.IsWithinMeleeRange),
                        Spell.Cast(AvengersShield),
                        Spell.Cast(HolyWrath, ret => Me.CurrentTarget.IsWithinMeleeRange),
                        new ActionAlwaysSucceed())));
        }

        private static WoWUnit dispeltar
        {
            get
            {
                var dispelothers = (from unit in ObjectManager.GetObjectsOfType<WoWPlayer>(false)
                                    where unit.IsAlive
                                    where Dispelling.CanDispel(unit)
                                    select unit).OrderByDescending(u => u.HealthPercent).LastOrDefault();
                return dispelothers;
            }
        }

        private static Composite CreateDispelBehavior()
        {
            return new PrioritySelector(
                Spell.Cast(Cleanse, on => Me, ret => Dispelling.CanDispel(Me)),
                Spell.Cast(Cleanse, on => dispeltar, ret => Dispelling.CanDispel(dispeltar)));
        }

        private static PaladinSeal GetBestSeal()
        {
            if (StyxWoW.Me.Specialization == WoWSpec.None)
                return SpellManager.HasSpell("Seal of Command") ? PaladinSeal.Command : PaladinSeal.None;

            var bestSeal = PaladinSeal.Truth;

            if (Me.ManaPercent < 30  && Me.HealthPercent > 50)
                bestSeal = PaladinSeal.Insight;
            else if (Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 4)
                bestSeal = PaladinSeal.Righteousness;
            else if (bestSeal == PaladinSeal.Command && SpellManager.HasSpell("Seal of Truth"))
                bestSeal = PaladinSeal.Truth;

            return bestSeal;
        }

        static PaladinSeal _seal;

        static string SealSpell(PaladinSeal s)
        {
            return "Seal of " + s.ToString();
        }

        private enum PaladinSeal
        {
            None = 0,
            Auto = 1,
            Command,
            Truth,
            Insight,
            Righteousness,
            Justice
        }

        #region Light's Hammer
        private static Composite LightsHammer()
        {
            return new Decorator(ret => SpellManager.HasSpell("Light's Hammer") && Unit.UnfriendlyUnits(10).Any(),
                new Action(ret =>
                {
                    var tpos = Me.CurrentTarget.Location;
                    var mpos = Me.Location;

                    SpellManager.Cast("Light's Hammer");
                    SpellManager.ClickRemoteLocation(mpos);
                }));
        }
        #endregion

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
            AvengingWrath =31884,
            BlessingofKings = 20217,
            BlessingofMight = 19740,
            BlindingLight = 115750,
            Cleanse = 4987,
            Consecration = 26573,
            CrusaderStrike = 35395,
            DevotionAura = 31821,
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
            //LightsHammer = 114158,
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
