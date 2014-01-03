using System.Windows.Forms;
using CommonBehaviors.Actions;
using SlimAI.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using SlimAI.Helpers;
using System.Linq;

namespace SlimAI.Class.Druid
{
    class GuardianDruid
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DruidSettings Settings { get { return GeneralSettings.Instance.Druid(); } }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidGuardian)]
        public static Composite GuardianCombat()
        {
            return new PrioritySelector(
                new Decorator(ret => !Me.Combat || Me.IsCasting || !Me.GotTarget || Me.Mounted, 
                    new ActionAlwaysSucceed()),
                Spell.Cast(BearForm, ret => SlimAI.AFK && Me.Shapeshift != ShapeshiftForm.Bear),
                Common.CreateInterruptBehavior(),
                //Spell.Cast(SkullBash, on => Unit.NearbyUnitsInCombatWithMe.FirstOrDefault(u => u.IsCasting && u.CanInterruptCurrentSpellCast && u.IsWithinMeleeRange && Me.IsSafelyFacing(u))),
                CreateCooldowns(),
                Spell.Cast(Maul, ret => (Me.RagePercent > 90 || Me.GetAuraTimeLeft(SavageDefenseBuff).TotalSeconds >= 3) && Me.HealthPercent > 60),
                Spell.Cast(Mangle),
                Spell.Cast("Faerie Fire", ret => !Me.CurrentTarget.HasAura("Weakened Armor", 3)),
                new Decorator(ret => !SpellManager.CanCast("Mangle"),
                    new PrioritySelector(
                        Spell.Cast("Thrash"),
                        CreateAoe(),
                        Spell.Cast(Lacerate),
                        Spell.Cast("Faerie Fire"),
                        Spell.Cast(Maul, ret => !IsCurrentTank())
                    )
                )
            );
        }

        private static Composite CreateCooldowns()
        {
            return new PrioritySelector(
                Spell.Cast(Rejuvenation, on => Me, ret => Me.HasAura(HeartoftheWildBuff) && !Me.HasAura(Rejuvenation)),
                new Decorator(ret => IsCurrentTank(),
                    new PrioritySelector(
                        new Action(ret => { Item.UseTrinkets(); return RunStatus.Failure; }),
                        new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                Spell.Cast("Incarnation: Son of Ursoc", ret => SlimAI.Burst && Me.RagePercent < 60 && Me.HealthPercent < 60),
                Spell.Cast(CenarionWard, on => Me),
                Spell.Cast(Enrage, ret => Me.RagePercent < 40),
                Spell.Cast(HealingTouch, ret => Me.HasAura(145162) && Me.HealthPercent <= 90 || Me.HasAura(145162) && Me.GetAuraTimeLeft(145162).TotalSeconds < 2 && Me.GetAuraTimeLeft(145162).TotalSeconds > 1),
                Spell.Cast(BarkSkin),
                new Decorator(ret => SlimAI.Weave,
                    new PrioritySelector(
                        Spell.Cast(SurvivalInstincts, ret => Me.HealthPercent <= 50 && !Me.HasAura("Might of Ursoc")),
                        Spell.Cast(MightofUrsoc, ret => Me.HealthPercent <= 30 && !Me.HasAura("Survival Instincts")))),
                Spell.Cast(Renewal, ret => Me.HealthPercent <= 50 || Me.HasAura("Might of Ursoc")),
                Item.UsePotionAndHealthstone(40),
                Spell.Cast(FrenziedRegeneration, ret => Me.HealthPercent <= 65 && Me.CurrentRage >= 60 && !Me.HasAura("Frenzied Regeneration")),
                Spell.Cast(SavageDefense)
                    )
                )
            );
        }

        private static Composite CreateAoe()
        {
            return new Decorator(ret => Unit.UnfriendlyUnits(8).Count() >= 2 && SlimAI.AOE,
                new PrioritySelector(
                    Spell.Cast(Swipe)
                    ));
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian)]
        public static Composite GuardianPreCombatBuffs()
        {
            return new PrioritySelector(
                new Decorator(ret => Me.Mounted || Me.Combat,
                    new ActionAlwaysSucceed()),
                PartyBuff.BuffGroup("Mark of the Wild"));
        }

        static bool IsCurrentTank()
        {
            return Me.CurrentTarget.CurrentTargetGuid == Me.Guid;
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
                          Berserk = 50334,
                          CenarionWard = 102351,
                          DreamofCenarius = 108381,
                          Enrage = 5229,
                          FaerieFire = 770,
                          FeralSpirit = 51533,
                          FerociousBite = 22568,
                          FrenziedRegeneration = 22842,
                          HealingTouch = 5185,
                          HeartoftheWildBuff = 108293,
                          IncarnationSonofUrsoc = 106731,
                          Lacerate = 33745,
                          MarkoftheWild = 1126,
                          Mangle = 33917,
                          Maul = 6807,
                          MightofUrsoc = 106922,
                          NaturesSwiftness = 132158,
                          Rake = 1822,
                          Rejuvenation = 774,
                          Renewal = 108238,
                          Rip = 1079,
                          SavageDefense = 62606,
                          SavageDefenseBuff = 132402,
                          SavageRoar = 52610,
                          Shred = 5221,
                          SkullBash = 80964,
                          SurvivalInstincts = 61336,
                          Swipe = 106785,
                          Thrash = 77758;
        #endregion
    }
}
