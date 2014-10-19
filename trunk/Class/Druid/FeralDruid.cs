using System.Linq;
using System.Windows.Forms;
using SlimAI.Helpers;
using CommonBehaviors.Actions;
using SlimAI.Managers;
using SlimAI.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Class.Druid
{
    class FeralDruid
    {      
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DruidSettings Settings { get { return GeneralSettings.Instance.Druid(); } }
        private static double? _EnergyRegen;
        private static double? _timeToMax;
        private static double? _energy;

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidFeral)]
        public static Composite FeralCombat()
        {
            return new PrioritySelector(
                new Throttle(1,
                    new Action(context => ResetVariables())),
                new Decorator(ret => SlimAI.PvPRotation,
                    CreatePvP()),
                new Decorator(ret => !Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive,
                    new ActionAlwaysSucceed()),
                Tranq(),
                Common.CreateInterruptBehavior(),
                //Spell.WaitForCastOrChannel(),
                new Decorator(ret => Me.HasAura("Tiger's Fury"),
                    new PrioritySelector(
                        new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                        new Action(ret => { Item.UseTrinkets(); return RunStatus.Failure; }),
                        Spell.Cast("Incarnation: King of the Jungle", ret => SlimAI.Burst && SpellManager.HasSpell("Incarnation: King of the Jungle")),
                        Spell.Cast("Nature's Vigil", ret => SlimAI.Burst && SpellManager.HasSpell("Nature's Vigil")),
                        Spell.Cast("Berserk", ret => SlimAI.Burst),
                        Spell.Cast(FeralSpirit, ret => SlimAI.Burst)
                        )),
                Spell.Cast(FerociousBite, ret => Me.CurrentTarget.HasMyAura("Rip") && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds <= 3 && Me.CurrentTarget.HealthPercent <= 25),
                Spell.Cast(FaerieFire, ret => !Me.CurrentTarget.HasAura("Weakened Armor", 3)),
                Spell.Cast("Cenarion Ward",
                        on => WardTar,
                        ret => SpellManager.HasSpell("Cenarion Ward")),
                new Throttle(2,
                    Spell.Cast(HealingTouch,
                        on => HTtar,
                        ret => Me.HasAura("Predatory Swiftness") && !Me.HasAura(DreamofCenarius) && (Me.GetAuraTimeLeft("Predatory Swiftness").TotalSeconds <= 1.5 || Me.ComboPoints >= 4 || Me.CurrentTarget.GetAuraTimeLeft("Rake").TotalSeconds <= 4))),
                Spell.Cast("Savage Roar", ret => Me.GetAuraTimeLeft("Savage Roar").TotalSeconds <= 3 && Me.CurrentTarget.HealthPercent < 25 && Me.ComboPoints > 0 || !Me.HasAura("Savage Roar")),
                Spell.Cast("Tiger's Fury", ret => Me.CurrentEnergy <= 30 && !Me.HasAura(Clearcasting)),
                Spell.Cast("Force of Nature", ret => SpellManager.HasSpell("Force of Nature") && (Spell.GetCharges("Force of Nature") == 3 || Me.Agility > 32000)),
                Spell.Cast(FerociousBite, ret => Me.ComboPoints >= 5 && Me.CurrentTarget.HealthPercent <= 25 && Me.CurrentTarget.HasMyAura("Rip")),
                Spell.Cast(Rip, ret => Me.HasAura("Savage Roar") && Me.ComboPoints == 5 && (Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds <= 2 || !Me.CurrentTarget.HasMyAura("Rip"))),
                Spell.Cast("Savage Roar", ret => SavageRoarTimer),
                Spell.Cast("Thrash", ret => Me.HasAura(Clearcasting) && (Me.CurrentTarget.GetAuraTimeLeft("Thrash").TotalSeconds < 3 || !Me.CurrentTarget.HasMyAura("Thrash"))),
                Spell.Cast(Rake, ret => DebuffTimeLeft(Rake, Me.CurrentTarget) <= 3 &&  Me.HasAura("Savage Roar")),
                Spell.Cast("Thrash", ret => Me.CurrentTarget.GetAuraTimeLeft("Thrash").TotalSeconds < 3 && (Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds >= 8 &&
                                           (Me.GetAuraTimeLeft("Savage Roar").TotalSeconds >= 12 || Me.HasAura("Berserk") || Me.ComboPoints == 5))),
                Spell.Cast(FerociousBite, ret => Me.HasAura("Savage Roar") && Me.ComboPoints >= 5 && Me.CurrentTarget.HasMyAura("Rip") && Me.CurrentTarget.GetAuraTimeLeft("Rip").TotalSeconds > 7),
                //Spell.Cast("Swipe", ret => Unit.UnfriendlyUnits(8).Count() >= 2 && SlimAI.AOE),
                ////(Me.GetAuraTimeLeft("Savage Roar").TotalSeconds <= 5 || Me.ActiveAuras.ContainsKey("Clearcasting") ||
                ////Me.HasAura("Berserk") || Me.HasAura("Tiger's Fury") || Spell.GetSpellCooldown("Tiger's Fury").TotalSeconds <= 3)),
                //Spell.Cast(Shred, ret => Me.CurrentTarget.MeIsBehind || Me.HasAura(Clearcasting) || Me.HasAura("Berserk") || EnergyRegen >= 15),
                //Spell.Cast("Mangle"));    
                Spell.Cast("Ravage!"),
                CreateFiller()
            );
            

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
                          Catform = 768,
                          CenarionWard = 102351,
                          Clearcasting = 135700,
                          DreamofCenarius = 108381,
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

