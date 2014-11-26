using System;
using System.Collections.Generic;
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
    class ProtectionPaladin
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static PaladinSettings Settings { get { return GeneralSettings.Instance.Paladin(); } }

        #region Coroutine Combat

        private static async Task<bool> CombatCoroutine()
        {

            if (!Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive) return true;
                //Common.CreateInterruptBehavior(),
                //new Throttle(TimeSpan.FromMilliseconds(500),
                //    new Sequence(
                //        new Action(ret => _seal = GetBestSeal()),
                //        new Decorator(ret => !Me.HasMyAura(SealSpell(_seal)) && Spell.CanCastHack(SealSpell(_seal), Me),
                //            Spell.Cast(s => SealSpell(_seal), on => Me, ret => !Me.HasAura(SealSpell(_seal)))))),

                //Common.CreateInterruptBehavior(),
                //Staying alive
                //Item.UsePotionAndHealthstone(40),
            await Spell.CoCast(LayonHands, Me, Me.HealthPercent <= 10 && !Me.HasAura("Forbearance"));
            await Spell.CoCast(ArdentDefender,  Me.HealthPercent <= 15 && Me.HasAura("Forbearance"));
            await Spell.CoCast(GuardianofAncientKings, Me.HealthPercent < 50 && !Me.HasAnyAura("ArdentDefender") && SlimAI.AFK);
            await Spell.CoCast(DivineProtection, Me.HealthPercent <= 80 && !Me.HasAura("Shield of the Righteous") && IsCurrentTank() && SlimAI.AFK);

            await Spell.CoCast(WordofGlory, Me, Me.HealthPercent < 30 && (Me.CurrentHolyPower >= 3 || Me.HasAura("Divine Purpose")));
            await Spell.CoCast(WordofGlory, Me, Me.HealthPercent < 25 && (Me.CurrentHolyPower >= 2 || Me.HasAura("Divine Purpose")));
            await Spell.CoCast(WordofGlory, Me, Me.HealthPercent < 15 && (Me.CurrentHolyPower >= 1 || Me.HasAura("Divine Purpose")));

            await Spell.CoCast(WordofGlory, Me, SpellManager.HasSpell(EternalFlame) && !Me.ActiveAuras.ContainsKey("Eternal Flame") && (Me.CurrentHolyPower >= 3 || Me.HasAura("Divine Purpose")) && Me.HasAura("Bastion of Glory", 5));

                //CreateDispelBehavior(),

            await CoAOE(Unit.UnfriendlyUnits(8).Count() >= 2 && SlimAI.AOE);

            await Spell.CoCast(ShieldoftheRighteous, MaxHolyPower);
            await Spell.CoCast(HolyWrath, SpellManager.HasSpell("Sanctified Wrath"));
            await Spell.CoCast(AvengersShield, Me.HasAura(GrandCrusader));
            await Spell.CoCast(CrusaderStrike);
            await Spell.CoCast(Judgment);
            await Spell.CoCast(SacredShield, Me, Me.HasAuraExpired("Sacred Shield", 9) && SpellManager.HasSpell("Sacred Shield"));
            await Spell.CoCastOnGround(LightsHammer, Me.Location, Unit.UnfriendlyUnits(10).Any() && SlimAI.Weave);
            await Spell.CoCast(HolyPrism);
            await Spell.CoCast(ExecutionSentence, SlimAI.Weave);
            await Spell.CoCast(HammerofWrath);
            await Spell.CoCast(ShieldoftheRighteous, SlimAI.Burst);
            await Spell.CoCast(AvengersShield);
            await Spell.CoCast(Consecration, !Me.IsMoving && Me.CurrentTarget.IsWithinMeleeRange);
            await Spell.CoCast(HolyWrath, Me.CurrentTarget.IsWithinMeleeRange);
                        
            return false;
        }

        [Behavior(BehaviorType.Combat, WoWClass.Paladin, WoWSpec.PaladinProtection)]
        public static Composite CoProtectionCombat()
        {
            return new ActionRunCoroutine(ctx => CombatCoroutine());
        }
        #endregion

        #region Coroutine AOE
        private static async Task<bool> CoAOE(bool reqs)
        {
            if (!reqs)
                return false;

            await Spell.CoCast(ShieldoftheRighteous, MaxHolyPower);
            await Spell.CoCast(AvengersShield, Me.HasAura(GrandCrusader));
            await Spell.CoCast(HolyWrath, SpellManager.HasSpell("Sanctified Wrath"));
            await Spell.CoCast(HammeroftheRighteous);
            await Spell.CoCast(Judgment);
            await Spell.CoCast(SacredShield, Me, Me.HasAuraExpired("Sacred Shield", 9) && SpellManager.HasSpell("Sacred Shield"));
            await Spell.CoCast(AvengersShield);
            await Spell.CoCastOnGround(LightsHammer, Me.Location, Unit.UnfriendlyUnits(10).Any());
            await Spell.CoCast(HolyPrism, Me, Me.HealthPercent <= 90);
            await Spell.CoCast(Consecration, !Me.IsMoving && Me.CurrentTarget.IsWithinMeleeRange);
            await Spell.CoCast(ExecutionSentence);
            await Spell.CoCast(HammerofWrath);
            await Spell.CoCast(ShieldoftheRighteous, SlimAI.Burst);
            await Spell.CoCast(HolyWrath, Me.CurrentTarget.IsWithinMeleeRange);
            
            return false;
        }
        #endregion

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

        #region Is Tank
        static bool IsCurrentTank()
        {
            return StyxWoW.Me.CurrentTarget.CurrentTargetGuid == StyxWoW.Me.Guid;
        }
        #endregion

        private static bool MaxHolyPower
        {
            get
            {
                return ((Spell.GetSpellCooldown("Judgment").TotalSeconds <= 1 || Spell.GetSpellCooldown("Crusader Strike").TotalSeconds <= 1 || Me.HasAura(GrandCrusader)) && Me.CurrentHolyPower == 5) ||
                    Me.HasAura("Divine Purpose");
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

        //#region Light's Hammer
        //private static async Task<bool> LightsHammer()
        //{
        //    if (!SpellManager.HasSpell("Light's Hammer"))
        //        return false;
        //    if (!Unit.UnfriendlyUnits(10).Any())
        //        return false;
        //    await Spell.CoCastOnGround()
        //    //return new Decorator(ret => SpellManager.HasSpell("Light's Hammer") && Unit.UnfriendlyUnits(10).Any(),
        //    //    new Action(ret =>
        //    //    {
        //    //        var tpos = Me.CurrentTarget.Location;
        //    //        var mpos = Me.Location;

        //    //        SpellManager.Cast("Light's Hammer");
        //    //        SpellManager.ClickRemoteLocation(mpos);
        //    //    }));
        //}
        //#endregion

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
