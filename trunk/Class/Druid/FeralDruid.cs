﻿using System.Linq;
using SlimAI.Helpers;
using SlimAI.Managers;
using CommonBehaviors.Actions;
using SlimAI.Settings;
using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Class.Druid
{
    class FeralDruid
    {      
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        static WoWUnit healtarget { get { return HealerManager.FindLowestHealthTarget(); } }
        private static DruidSettings Settings { get { return GeneralSettings.Instance.Druid(); } }
        private static double? _EnergyRegen;

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidFeral)]
        public static Composite FeralCombat()
        {
            HealerManager.NeedHealTargeting = true;
            return new PrioritySelector(
                Common.CreateInterruptBehavior(),
                Spell.WaitForCastOrChannel(),
                new Decorator(ret => Me.CurrentTarget != null && (!Me.CurrentTarget.IsWithinMeleeRange || Me.IsCasting),
                    new ActionAlwaysSucceed()),
                new Throttle(1,
                    new Action(context => ResetVariables())),
                new Decorator(ret => Me.HasAura("Tiger's Fury"),
                    new PrioritySelector(
                        new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                        new Action(ret => { Item.UseTrinkets(); return RunStatus.Failure; }),
                        Spell.Cast(FeralSpirit, ret => SlimAI.Burst))),
                Spell.Cast(FerociousBite, ret => Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds <= 3 && Me.CurrentTarget.HealthPercent <= 25),
                Spell.Cast(FaerieFire, ret => !Me.CurrentTarget.HasAura("Weakened Armor", 3)),
                new Throttle(2,
                    Spell.Cast(HealingTouch, 
                        on => healtarget, 
                        ret => Me.HasAura("Predatory Swiftness") && !Me.HasAura(DreamofCenarius) && (Me.GetAuraTimeLeft("Predatory Swiftness").TotalSeconds <= 1.5 || Me.ComboPoints >= 4))),
                Spell.Cast(SavageRoar, ret => Me.GetAuraTimeLeft("Savage Roar").TotalSeconds < 3 || !Me.HasAura("Savage Roar")),
                Spell.Cast(TigersFury, ret => Me.EnergyPercent <= 35 && !Me.ActiveAuras.ContainsKey("Clearcasting")),
                Spell.Cast(Berserk, ret => Me.HasAura("Tiger's Fury") && SlimAI.Burst),
                Spell.Cast(FerociousBite, ret => Me.ComboPoints >= 5 && Me.CurrentTarget.HealthPercent <= 25 && Me.CurrentTarget.HasAura("Rip")),
                Spell.Cast(Rip, ret => Me.ComboPoints == 5 && (Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds <= 2 || !Me.CurrentTarget.HasAura("Rip"))),
                Spell.Cast("Thrash", ret => Me.ActiveAuras.ContainsKey("Clearcasting") && (Me.CurrentTarget.GetAuraTimeLeft("Thrash").TotalSeconds < 3 || !Me.CurrentTarget.HasAura("Thrash"))),
                Spell.Cast(Rake, ret => Me.CurrentTarget.GetAuraTimeLeft("Rake").TotalSeconds <= 3 || !Me.CurrentTarget.HasAura("Rake")),
                Spell.Cast("Thrash", ret => Me.CurrentTarget.GetAuraTimeLeft("Thrash").TotalSeconds < 3 && (Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds >= 8 &&
                                           (Me.GetAuraTimeLeft("Savage Roar").TotalSeconds >= 12 || Me.HasAura("Berserk") || Me.ComboPoints == 5))),
                Spell.Cast(FerociousBite, ret => Me.ComboPoints >= 5 && Me.CurrentTarget.HasMyAura("Rip") && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds > 7 && Me.GetAuraTimeLeft("Savage Roar").TotalSeconds > 6),
                Spell.Cast("Swipe", ret => Unit.UnfriendlyUnits(8).Count() >= 2 &&
                                           (Me.GetAuraTimeLeft("Savage Roar").TotalSeconds <= 5 || Me.ActiveAuras.ContainsKey("Clearcasting") ||
                                           Me.HasAura("Berserk") || Me.HasAura("Tiger's Fury") || Spell.GetSpellCooldown("Tiger's Fury").TotalSeconds <= 3)),
                Spell.Cast(Shred, ret => Me.CurrentTarget.MeIsSafelyBehind || Me.ActiveAuras.ContainsKey("Clearcasting") || Me.HasAura("Berserk") || EnergyRegen >= 15),
                Spell.Cast("Mangle"));
#region Old dps
//                new Throttle(Spell.Cast("Nature's Vigil", ret => Me.CachedHasAura("Berserk"))),
//                Spell.Cast("Incarnation", ret => Me.CachedHasAura("Berserk")),
//                Spell.CastOnGround("Force of Nature",
//                                    u => (Me.CurrentTarget ?? Me).Location,
//                                    ret => StyxWoW.Me.CurrentTarget != null
//                                    && StyxWoW.Me.CurrentTarget.Distance < 40),
//                new Throttle(1,1,
//                Spell.Cast(HealingTouch, ret => (Me.CachedHasAura("Predatory Swiftness") && Me.GetAuraTimeLeft("Predatory Swiftness").TotalSeconds <= 1.5 && !Me.CachedHasAura(DreamofCenarius)) ||
//                                                (Me.CachedHasAura("Predatory Swiftness") && Me.ComboPoints >= 4 && (Me.CachedHasAura(DreamofCenarius) && Me.CachedStackCount(DreamofCenarius) <= 1 || !Me.CachedHasAura(DreamofCenarius))))),

//                Spell.Cast("Savage Roar", ret => !Me.CachedHasAura("Savage Roar")),
//                Spell.Cast("Faerie Fire", ret => !Me.CurrentTarget.CachedHasAura("Weakened Armor", 3)),
//                //healing_touch,if=buff.predatory_swiftness.up&(combo_points>=4|(set_bonus.tier15_2pc_melee&combo_points>=3))&buff.dream_of_cenarius_damage.stack<2
//                Spell.Cast(HealingTouch, ret => Me.CachedHasAura("Predatory Swiftness") && Me.ComboPoints >= 4 && (Me.CachedHasAura(DreamofCenarius) && Me.CachedStackCount(DreamofCenarius) <= 1 || !Me.CachedHasAura(DreamofCenarius))),
//                //Spell.Cast(HealingTouch, ret => Me.CachedHasAura("Nature's Swiftness")),
//                //use_item,name=eternal_blossom_grips,sync=tigers_fury
//                new Decorator(ret => Me.CachedHasAura("Tiger's Fury"),
//                    new PrioritySelector(
//                        new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
//                        new Action(ret => { Item.UseTrinkets(); return RunStatus.Failure; }))),
//                Spell.Cast("Tiger's Fury", ret => Me.EnergyPercent <= 35 && !Me.ActiveAuras.ContainsKey("Clearcasting") && !Me.CachedHasAura("Berserk")),
//                Spell.Cast("Berserk", ret => Me.CachedHasAura("Tiger's Fury") && AdvancedAI.Burst),
//                Spell.Cast("Ferocious Bite", ret => Me.ComboPoints >= 1 && Me.CurrentTarget.CachedHasAura("Rip") && (Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds <= 3 && Me.CurrentTarget.HealthPercent <= 25)),
//                Spell.Cast("Thrash", ret => Me.CurrentTarget.TimeToDeath() >= 6 && Me.ActiveAuras.ContainsKey("Clearcasting") && Me.CurrentTarget.GetAuraTimeLeft("Thrash").TotalSeconds <= 3),
//                Spell.Cast("Ferocious Bite", ret => Me.ComboPoints >= 5 && Me.CurrentTarget.TimeToDeath() <= 4 || Me.CurrentTarget.TimeToDeath() <= 1 && Me.ComboPoints >= 3),
//                Spell.Cast("Savage Roar", ret => Me.HasAuraExpired("Savage Roar", 3) && Me.ComboPoints == 0 && Me.CurrentTarget.HealthPercent <= 25),
//                //Spell.Cast(NaturesSwiftness, ret => !Me.CachedHasAura(DreamofCenarius) && !Me.CachedHasAura("Predatory Swiftness") && Me.ComboPoints >= 4 && Me.CurrentTarget.HealthPercent <= 25),
//                Spell.Cast("Rip", ret => Me.ComboPoints == 5 && Me.CachedHasAura(DreamofCenarius) && Me.CurrentTarget.HealthPercent <= 25 && Me.CurrentTarget.TimeToDeath() >= 30),
//                //pool_resource,wait=0.25,if=combo_points>=5&dot.rip.ticking&target.health.pct<=25&((energy<50&buff.berserk.down)|(energy<25&buff.berserk.remains>1))
//                //PoolinResources(),
//                // Spell.Cast("Rip", ret => Me.ComboPoints == 5 && !Me.CurrentTarget.HasMyAura("Rip")),
//                Spell.Cast("Ferocius Bite", ret => Me.ComboPoints >= 5 && Me.CurrentTarget.HasMyAura("Rip") && Me.CurrentTarget.HealthPercent <= 25 && (Me.ComboPoints >= 5 && Me.CurrentTarget.HasMyAura("Rip") && Me.CurrentTarget.HealthPercent <= 25 && ((Me.CurrentEnergy < 50 && !Me.CachedHasAura("Berserk")) || (Me.CurrentEnergy < 25 && Me.GetAuraTimeLeft("Berserk").TotalSeconds > 1)))),
//                Spell.Cast("Rip", ret => Me.ComboPoints == 5 && (Me.CurrentTarget.HasMyAura("Rip") && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds <= 3 || !Me.CurrentTarget.HasMyAura("Rip")) && Me.CachedHasAura(DreamofCenarius)),
//                //Spell.Cast(NaturesSwiftness, ret => !Me.CachedHasAura(DreamofCenarius) && !Me.CachedHasAura("Predatory Swiftness") && Me.ComboPoints >= 4 && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds <= 3),
//                Spell.Cast("Rip", ret => Me.ComboPoints == 5 && Me.CurrentTarget.TimeToDeath() >= 6 && Me.CurrentTarget.HasAuraExpired("Rip", 2) && (Me.CachedHasAura("Berserk") || Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds + 1.9 <= SpellManager.Spells["Tiger's Fury"].CooldownTimeLeft.TotalSeconds)),
//                Spell.Cast("Savage Roar", ret => Me.HasAuraExpired("Savage Roar", 3) && Me.ComboPoints == 0 && Me.GetAuraTimeLeft("Savage Roar").TotalSeconds + 2 <= Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds),
//                Spell.Cast("Savage Roar", ret => Me.HasAuraExpired("Savage Roar", 6) && Me.ComboPoints >= 5 && Me.GetAuraTimeLeft("Savage Roar").TotalSeconds + 2 <= Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds),
//                //pool_resource,wait=0.25,if=combo_points>=5&((energy<50&buff.berserk.down)|(energy<25&buff.berserk.remains>1))&dot.rip.remains>=6.5
//                //PoolResources(),
//                Spell.Cast("Ferocious Bite", ret => Me.ComboPoints >= 5 && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds >= 6 && (Me.ComboPoints >= 5 && ((Me.CurrentEnergy < 50 && !Me.CachedHasAura("Berserk")) || (Me.CurrentEnergy < 25 && Me.GetAuraTimeLeft("Berserk").TotalSeconds > 1)) && Me.CurrentTarget.GetAuraTimeLeft("Rip", true).TotalSeconds >= 6.5)),
//                Spell.Cast("Rake", ret => Me.CurrentTarget.GetAuraTimeLeft("Rake").TotalSeconds <= 9 && Me.CachedHasAura(DreamofCenarius)),
//                Spell.Cast("Rake", ret => Me.CurrentTarget.GetAuraTimeLeft("Rake").TotalSeconds <= 3),
//                //pool_resource,wait=0.25,for_next=1
//                Spell.Cast("Thrash", ret => Me.CurrentTarget.GetAuraTimeLeft("Rake").TotalSeconds <= 3 && Me.CurrentTarget.TimeToDeath() >= 6 && (Me.CurrentTarget.GetAuraTimeLeft("Rake").TotalSeconds >= 4 || Me.CachedHasAura("Berserk"))),
//                Spell.Cast("Thrash", ret => Me.CurrentTarget.GetAuraTimeLeft("Rake").TotalSeconds <= 3 && Me.CurrentTarget.TimeToDeath() >= 6 && Me.ComboPoints == 5),
//                Spell.Cast("Shred", ret => Me.ActiveAuras.ContainsKey("Clearcasting") && Me.CurrentTarget.MeIsSafelyBehind || Me.ActiveAuras.ContainsKey("Clearcasting") && Me.HasAnyAura("Tiger's Fury", "Berserk")),
//                Spell.Cast("Shred", ret => Me.CachedHasAura("Berserk")),
//                Spell.Cast("Mangle", ret => Me.ComboPoints <= 5 && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds <= 3 || Me.ComboPoints == 0 && Me.HasAuraExpired("Savage Roar", 2)),
//                Spell.Cast("Shred", ret => (Me.CurrentTarget.MeIsSafelyBehind || (TalentManager.HasGlyph("Shred") && (Me.HasAnyAura("Tiger's Fury", "Berserk"))))),
//                Spell.Cast("Mangle", ret => !Me.CurrentTarget.MeIsBehind));
#endregion
        }

        private static double EnergyRegen
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

        private static RunStatus ResetVariables()
        {
            _EnergyRegen = null;
            return RunStatus.Failure;
        }

        #region DruidTalents
        public enum DruidTalents
        {
            FelineSwiftness = 1,//Tier 1
            DisplacerBeast,
            WildCharge,
            NaturesSwiftness,//Tier 2
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
                          CenarionWard = 102351,
                          DreamofCenarius = 108381,
                          Enrage = 5229,
                          FaerieFire = 770,
                          FeralSpirit = 51533,
                          FerociousBite = 22568,
                          FrenziedRegeneration = 22842,
                          HealingTouch = 5185,
                          Lacerate = 33745,
                          MarkoftheWild = 1126,
                          Maul = 6807,
                          MightofUrsoc = 106922,
                          NaturesSwiftness = 132158,
                          Rake = 1822,
                          Renewal = 108238,
                          Rip = 1079,
                          SavageDefense = 62606,
                          SavageRoar = 52610,
                          Shred = 5221,
                          SkullBash = 106839,
                          SurvivalInstincts = 61336,
                          TigersFury = 5217;
        #endregion
    }
}

