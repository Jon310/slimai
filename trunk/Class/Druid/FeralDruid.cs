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

namespace SlimAI.Class.Druid
{
    [UsedImplicitly]
    class FeralDruid
    {      
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DruidSettings Settings { get { return GeneralSettings.Instance.Druid(); } }
        private static double? _EnergyRegen;
        private static double? _timeToMax;
        private static double? _energy;

        #region Coroutine Combat

        private static async Task<bool> CombatCoroutine()
        {
            if (SlimAI.PvPRotation)
            //{
            //    await PvPCoroutine();
            //    return true;
            //}

            if (!Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive) return true;

            await Spell.CoCast("Incarnation: King of the Jungle", Me.HasAura("Tiger's Fury") && SlimAI.Burst && SpellManager.HasSpell("Incarnation: King of the Jungle"));
            await Spell.CoCast("Nature's Vigil", SlimAI.Burst && SpellManager.HasSpell("Nature's Vigil") && Me.HasAura("Tiger's Fury"));
            await Spell.CoCast("Berserk", Me.HasAura("Tiger's Fury") && SlimAI.Burst);

            await Spell.CoCast(FerociousBite, Me.CurrentTarget.HasMyAura("Rip") && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds <= 3 && Me.CurrentTarget.HealthPercent <= 25);
            await Spell.CoCast("Cenarion Ward", WardTar, SpellManager.HasSpell("Cenarion Ward"));
            await Spell.CoCast(HealingTouch, HTtar, Me.HasAura("Predatory Swiftness") && !Me.HasAura(Bloodtalons) && (Me.GetAuraTimeLeft("Predatory Swiftness").TotalSeconds <= 1.5 || Me.ComboPoints >= 4 || Me.CurrentTarget.GetAuraTimeLeft("Rake").TotalSeconds <= 4));
            await Spell.CoCast("Savage Roar", Me.GetAuraTimeLeft("Savage Roar").TotalSeconds <= 3 || ((Me.HasAura(Berserk) || Spell.GetSpellCooldown("Tiger's Fury").TotalSeconds <= 3) && Me.HasAuraExpired("Savage Roar", 12)) && Me.ComboPoints == 5 || !Me.HasAura("Savage Roar"));
            await Spell.CoCast("Tiger's Fury", Me.CurrentEnergy <= 30 && !Me.HasAura(Clearcasting));
            await Spell.CoCast("Force of Nature", SpellManager.HasSpell("Force of Nature") && (Spell.GetCharges("Force of Nature") == 3));
            await Spell.CoCast(FerociousBite, Me.ComboPoints >= 5 && Me.CurrentTarget.HealthPercent <= 25 && Me.CurrentTarget.HasMyAura("Rip"));
            await Spell.CoCast(Rip, Me.HasAura("Savage Roar") && Me.ComboPoints == 5 && (Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds <= 7 || !Me.CurrentTarget.HasMyAura("Rip")));
            await Spell.CoCast("Savage Roar", SavageRoarTimer);
            await Spell.CoCast("Thrash", Unit.UnfriendlyUnits(8).Count() >= 2 && SlimAI.AOE && Clusters.GetCluster(Me, Unit.UnfriendlyUnits(8), ClusterType.Radius, 8).Any(u => !u.HasAura("Thrash")));
            await Spell.CoCast(Rake, Me.CurrentTarget.HasAuraExpired("Rake", 4) &&  Me.HasAura("Savage Roar"));
            //await Spell.CoCast("Thrash", Me.CurrentTarget.GetAuraTimeLeft("Thrash").TotalSeconds < 3 && (Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds >= 8 &&
            //                               (Me.GetAuraTimeLeft("Savage Roar").TotalSeconds >= 12 || Me.HasAura("Berserk") || Me.ComboPoints == 5)));
            await Spell.CoCast(FerociousBite, Me.HasAura("Savage Roar") && Me.ComboPoints == 5 && Me.CurrentTarget.HasMyAura("Rip") && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds > 7 && Me.CurrentEnergy > 50);
            await Spell.CoCast("Swipe", Unit.UnfriendlyUnits(8).Count() >= 3 && SlimAI.AOE && Me.ComboPoints < 5);
            await Spell.CoCast(Shred, Unit.UnfriendlyUnits(8).Count() < 3 && Me.ComboPoints < 5);
            await Spell.CoCast(Rejuvenation, RejuvTar, Me.ManaPercent >= 40);
            return false;

        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidFeral)]
        public static Composite CoFeralCombat()
        {
            return new ActionRunCoroutine(ctx => CombatCoroutine());
        }

        #endregion

        #region PvP
        private static Composite CreatePvP()
        {
            return new PrioritySelector(
                //Spell.WaitForCastOrChannel(),
                CloneFocus(),
                Tranq(),
                new Decorator(ret => !Me.Combat || Me.Mounted,
                    new ActionAlwaysSucceed()),
                Spell.Cast(Catform, ret => SlimAI.AFK && Me.Shapeshift != ShapeshiftForm.Cat),
                new Decorator(ret => Me.HasAura("Tiger's Fury") && SlimAI.Burst,
                    new PrioritySelector(
                        Spell.Cast("Incarnation: King of the Jungle", ret => SpellManager.HasSpell("Incarnation: King of the Jungle")),
                        Spell.Cast("Berserk"),
                        Spell.Cast("Nature's Vigil", ret => SpellManager.HasSpell("Nature's Vigil")),
                        new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                        //new Action(ret => { Item.UseTrinkets(); return RunStatus.Failure; }),
                        Spell.Cast(FeralSpirit)
                        )),
                Spell.Cast("Nature's Vigil", ret => SpellManager.HasSpell("Nature's Vigil") && Me.HealthPercent <= 30),
                Spell.Cast("Cenarion Ward",
                        on => WardTar,
                        ret => SpellManager.HasSpell("Cenarion Ward")),
                new Throttle(2,
                    Spell.Cast(HealingTouch,
                        on => HTtarPvP,
                        ret => Me.HasAura("Predatory Swiftness"))),
                new Decorator(ret => Me.HasAura("Bear Form"),
                    new PrioritySelector(
                        Spell.Cast(FrenziedRegeneration, ret => Me.HealthPercent <= 90),
                        Spell.Cast("Mangle"),
                        Spell.Cast("Thrash"),
                        Spell.Cast(Lacerate),
                        Spell.Cast("Maul", ret => Me.RagePercent >= 90),
                        Spell.Cast(FaerieFire),
                        new ActionAlwaysSucceed())),
                Spell.Cast("Savage Roar", ret => !Me.HasAura("Savage Roar")),
                Spell.Cast(FaerieFire, ret => !Me.CurrentTarget.HasAura("Weakened Armor", 3) || !Me.CurrentTarget.HasAura("Faerie Fire")),
                Spell.Cast("Tiger's Fury", ret => Me.CurrentEnergy <= 30 && !Me.HasAura(Clearcasting) && SlimAI.Burst),
                Spell.Cast(FerociousBite, ret => FerociousBitePvP),
                Spell.Cast(Rip, ret => Me.HasAura("Savage Roar") && Me.ComboPoints == 5 && (Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds <= 2 || !Me.CurrentTarget.HasMyAura("Rip"))),
                Spell.Cast("Savage Roar", ret => SavageRoarTimer),
                Spell.Cast(Rake, ret => DebuffTimeLeft(Rake, Me.CurrentTarget) <= 3 && Me.HasAura("Savage Roar")),
                Spell.Cast("Ravage!"),
                new Decorator(ret => (Me.CurrentEnergy >= 70 || Me.HasAura(Clearcasting) || SlimAI.Weave || Me.HasAura("Tiger's Fury")) && !Me.HasAura("Incarnation: King of the Jungle"),
                    new PrioritySelector(
                        Spell.Cast(Shred, ret => Me.CurrentTarget.MeIsBehind),
                        Spell.Cast("Mangle"))),
                new ActionAlwaysSucceed()
                );
        }
        #endregion

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidFeral)]
        public static Composite FeralPreCombatBuffs()
        {
            return new PrioritySelector(
                new Decorator(ret => Me.Mounted || Me.Combat,
                    new ActionAlwaysSucceed())
                //PartyBuff.BuffGroup("Mark of the Wild")
                );
        }

        private static Composite CreateFiller()
        {
            return new PrioritySelector(
                new Decorator(ret => !Me.HasAura("Incarnation: King of the Jungle") || (Me.HasAura(Clearcasting) || Me.HasAura("Berserk") || Me.HasAura(TigersFury) ||
                                     Me.CurrentEnergy >= 80 || Me.ComboPoints < 5 && (Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds <= 5 || !Me.CurrentTarget.HasAura(Rip)) ||
                                     Me.ComboPoints == 0 && Me.GetAuraTimeLeft("Savage Roar").TotalSeconds < 2 || Spell.GetSpellCooldown("Tiger's Fury").TotalSeconds <= 3),
                    new PrioritySelector(
                Spell.Cast("Swipe", ret => Unit.UnfriendlyUnits(8).Count() >= 2 && SlimAI.AOE),
                                           //(Me.GetAuraTimeLeft("Savage Roar").TotalSeconds <= 5 || Me.ActiveAuras.ContainsKey("Clearcasting") ||
                                           //Me.HasAura("Berserk") || Me.HasAura("Tiger's Fury") || Spell.GetSpellCooldown("Tiger's Fury").TotalSeconds <= 3)),
                Spell.Cast(Shred, ret => Me.CurrentTarget.MeIsBehind || EnergyRegen >= 15),
                Spell.Cast("Mangle"))));
        }

        #region Uility
        private static Composite CloneFocus()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Cyclone") &&
                    KeyboardPolling.IsKeyDown(Keys.C),
                    new PrioritySelector(
                        Spell.Cast("Cyclone", on => Me.FocusedUnit))
                
                    );
        }

        private static Composite Tranq()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Tranquility") &&
                    KeyboardPolling.IsKeyDown(Keys.Z),
                    new PrioritySelector(
                        Spell.Cast("Heart of the Wild", ret => SpellManager.HasSpell("Heart of the Wild") && SpellManager.CanCast("Heart of the Wild")),
                        Spell.Cast("Tranquility"))
                //new Action(ret => Spell.Cast(Paralysis, on => Me.FocusedUnit))
                    );
        }
        #endregion

        #region Savage Roar
        private static bool SavageRoarTimer
        {
            get
            {
                return
                       Me.HasAuraExpired("Savage Roar", 3) && Me.ComboPoints > 0 && Me.GetAuraTimeLeft("Savage Roar").TotalSeconds + 2 > Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds ||
                       Me.HasAuraExpired("Savage Roar", 6) && Me.ComboPoints >= 5 && Me.GetAuraTimeLeft("Savage Roar").TotalSeconds + 2 <= Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds ||
                       Me.HasAuraExpired("Savage Roar", 12) && Me.ComboPoints >= 5 && Me.GetAuraTimeLeft("Savage Roar").TotalSeconds <= Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds + 6
            ;
            }
         }
        #endregion

        #region Ferocious Bite PvP
        private static bool FerociousBitePvP
        {
            get
            {
                return
                       Me.CurrentTarget.HasMyAura("Rip") && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds <= 4 && Me.CurrentTarget.HealthPercent <= 25 ||
                       Me.ComboPoints >= 5 && Me.CurrentTarget.HasMyAura("Rip") && (Me.CurrentTarget.HealthPercent < 25 ||
                       Me.HasAura("Savage Roar") && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds > 7 && Me.CurrentEnergy >= 50)
            ;
            }
        }
        #endregion

        private static double DebuffTimeLeft(int debuff, WoWUnit onTarget)
        {
            if (onTarget != null)
            {
                var results = onTarget.GetAuraById(debuff);
                if (results != null)
                {
                    if (results.TimeLeft.TotalSeconds > 0)
                        return results.TimeLeft.TotalSeconds;
                }
            }
            return 0;
        }

        #region Engery
        protected static double EnergyRegen
        {
            get
            {
                if (!_EnergyRegen.HasValue)
                {
                    _EnergyRegen = Lua.GetReturnVal<float>("return GetPowerRegen()", 1);
                    return _EnergyRegen.Value;
                }
                return _EnergyRegen.Value;
            }
        }

        protected static double Energy
        {
            get
            {
                if (!_energy.HasValue)
                {
                    _energy = Lua.GetReturnVal<int>("return UnitPower(\"player\");", 0);
                    return _energy.Value;
                }
                return _energy.Value;
            }
        }

        protected static double TimeToMax
        {
            get
            {
                if (!_timeToMax.HasValue)
                {
                    _timeToMax = (100 - Energy) * (1.0 / EnergyRegen);
                    return _timeToMax.Value;
                }
                return _timeToMax.Value;
            }
        }
        #endregion

        private static RunStatus ResetVariables()
        {
            _EnergyRegen = null;
            KeyboardPolling.IsKeyDown(Keys.Z);
            KeyboardPolling.IsKeyDown(Keys.C);
            return RunStatus.Failure;
        }

        #region Healing Proc's
        private static WoWUnit HTtar
        {
            get
            {
                var eHheal = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWPlayer>()
                              where unit.IsAlive
                              where unit.IsInMyPartyOrRaid
                              where unit.Distance < 40
                              where unit.InLineOfSight
                              where unit.HealthPercent <= 100
                              select unit).OrderByDescending(u => u.HealthPercent).LastOrDefault();
                return eHheal;
            }
        }

        private static WoWUnit HTtarPvP
        {
            get
            {
                var eHheal = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWPlayer>()
                              where unit.IsAlive
                              where unit.IsInMyPartyOrRaid
                              where unit.Distance < 40
                              where unit.InLineOfSight
                              where unit.HealthPercent <= 80
                              select unit).OrderByDescending(u => u.HealthPercent).LastOrDefault();
                return eHheal;
            }
        }

        private static WoWUnit WardTar
        {
            get
            {
                var eHheal = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWPlayer>()
                              where unit.IsAlive
                              where unit.IsInMyPartyOrRaid
                              where unit.Distance < 40
                              where unit.InLineOfSight
                              where unit.HealthPercent <= 70
                              select unit).OrderByDescending(u => u.HealthPercent).LastOrDefault();
                return eHheal;
            }
        }

        private static WoWUnit RejuvTar
        {
            get
            {
                var eHheal = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWPlayer>()
                              where unit.IsAlive
                              where !unit.HasMyAura("Rejuvenation")
                              where unit.IsInMyPartyOrRaid
                              where unit.Distance < 40
                              where unit.InLineOfSight
                              where unit.HealthPercent <= 75
                              select unit).OrderByDescending(u => u.HealthPercent).LastOrDefault();
                return eHheal;
            }
        }
        #endregion

        #region DruidTalents
        public enum DruidTalents
        {
            FelineSwiftness = 1,//Tier 1
            DisplacerBeast,
            WildCharge,
            YserasGift,//Tier 2
            Renewal,
            CenarionWard,
            FaerieSwarm,//Tier 3
            MassEntanglement,
            Typhoon,
            SouloftheForest,//Tier 4
            Incarnation,
            ForceofNature,
            DisorientingRoar,//Tier 5
            UrsolsVortex,
            MightyBash,
            HeartoftheWild,//Tier 6
            DreamofCenarius,
            NaturesVigil
        }
        #endregion

        #region Druid Spells
        private const int BarkSkin = 22812,
                          BearForm = 5487,
                          Berserk = 106952,
                          Bloodtalons = 145152,
                          Catform = 768,
                          CenarionWard = 102351,
                          Clearcasting = 135700,
                          Enrage = 5229,
                          FaerieFire = 770,
                          FeralSpirit = 110807,
                          FerociousBite = 22568,
                          FrenziedRegeneration = 22842,
                          ForceofNature = 102703,
                          HealingTouch = 5185,
                          Lacerate = 33745,
                          MarkoftheWild = 1126,
                          Maul = 6807,
                          MightofUrsoc = 106922,
                          NaturesSwiftness = 132158,
                          Rake = 1822,
                          Renewal = 108238,
                          Rejuvenation = 774,
                          Rip = 1079,
                          SavageDefense = 62606,
                          SavageRoar = 127538,
                          Shred = 5221,
                          SkullBash = 106839,
                          SurvivalInstincts = 61336,
                          TigersFury = 5217;
        #endregion
    }
}

