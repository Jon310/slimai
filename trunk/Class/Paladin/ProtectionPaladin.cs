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
                Spell.Cast("Sacred Shield", on => Me, ret => !Me.HasAura("Sacred Shield") && SpellManager.HasSpell("Sacred Shield")),
                Spell.Cast("Lay on Hands", on => Me, ret => Me.HealthPercent <= 10 && !Me.HasAura("Forbearance")),
                Spell.Cast("Ardent Defender", ret => Me.HealthPercent <= 15 && Me.HasAura("Forbearance")),
                Spell.Cast("Divine Protection", ret => Me.HealthPercent <= 80 && !Me.HasAura("Shield of the Righteous")),

                Spell.Cast("Word of Glory", ret => Me.HealthPercent < 50 && (Me.CurrentHolyPower >= 3 || Me.HasAura("Divine Purpose"))),
                Spell.Cast("Word of Glory", ret => Me.HealthPercent < 25 && (Me.CurrentHolyPower >= 2 || Me.HasAura("Divine Purpose"))),
                Spell.Cast("Word of Glory", ret => Me.HealthPercent < 15 && (Me.CurrentHolyPower >= 1 || Me.HasAura("Divine Purpose"))),

                //Prot T15 2pc 
                new Decorator(ret => SlimAI.AFK && !Me.HasAura("Shield of Glory"),
                    new PrioritySelector(
                        Spell.Cast("Word of Glory", ret => Me.HealthPercent < 90 && Me.CurrentHolyPower == 1),
                        Spell.Cast("Word of Glory", ret => Me.HealthPercent < 75 && Me.CurrentHolyPower <= 2),
                        Spell.Cast("Word of Glory", ret => Me.HealthPercent < 50 && (Me.CurrentHolyPower >= 3 || Me.HasAura("Divine Purpose"))))),

                CreateDispelBehavior(),

                new Decorator(ret => Unit.UnfriendlyUnits(8).Count() >= 2,
                    CreateAoe()),

                Spell.Cast("Shield of the Righteous", ret => (Me.CurrentHolyPower == 5 || Me.HasAura("Divine Purpose")) && SlimAI.Burst),
                Spell.Cast("Hammer of the Righteous", ret => !Me.CurrentTarget.HasAura("Weakened Blows")),
                Spell.Cast("Judgment", ret => SpellManager.HasSpell("Sanctified Wrath") && Me.HasAura("Avenging Wrath")),
                Spell.Cast("Avenger's Shield", ret => Me.HasAura("Grand Crusader")),
                Spell.Cast("Crusader Strike"),
                Spell.Cast("Judgment"),
                LightsHammer(),
                Spell.Cast("Holy Prism", on => Unit.UnfriendlyUnits(15).Count() >= 2 ? Me : Me.CurrentTarget),
                Spell.Cast("Execution Sentence"),
                Spell.Cast("Hammer of Wrath"),
                Spell.Cast("Shield of the Righteous", ret => Me.CurrentHolyPower >= 3 && SlimAI.Burst),
                Spell.Cast("Avenger's Shield"),
                Spell.Cast("Consecration", ret => !Me.IsMoving && Me.CurrentTarget.IsWithinMeleeRange),
                Spell.Cast("Holy Wrath", ret => Me.CurrentTarget.IsWithinMeleeRange));
        }
        
        private static Composite CreateAoe()
        {
            return new PrioritySelector(
                Spell.Cast("Shield of the Righteous", ret => (Me.CurrentHolyPower == 5 || Me.HasAura("Divine Purpose")) && SlimAI.Burst),
                Spell.Cast("Judgment", ret => SpellManager.HasSpell("Sanctified Wrath") && Me.HasAura("Avenging Wrath")),
                Spell.Cast("Hammer of the Righteous"),
                Spell.Cast("Judgment"),
                Spell.Cast("Avenger's Shield", ret => Me.HasAura("Grand Crusader")),
                LightsHammer(),
                Spell.Cast("Holy Prism", on => Me, ret => Me.HealthPercent <= 90),
                Spell.Cast("Execution Sentence"),
                Spell.Cast("Hammer of Wrath"),
                Spell.Cast("Shield of the Righteous", ret => Me.CurrentHolyPower >= 3 && SlimAI.Burst),
                Spell.Cast("Consecration", ret => !Me.IsMoving && Me.CurrentTarget.IsWithinMeleeRange),
                Spell.Cast("Avenger's Shield"),
                Spell.Cast("Holy Wrath", ret => Me.CurrentTarget.IsWithinMeleeRange),
                new ActionAlwaysSucceed());
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
                Spell.Cast("Cleanse", on => Me, ret => Dispelling.CanDispel(Me)),
                Spell.Cast("Cleanse", on => dispeltar, ret => Dispelling.CanDispel(dispeltar)));
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
    }
}
