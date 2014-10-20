using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Buddy.Coroutines;
using JetBrains.Annotations;
using SlimAI.Helpers;
using CommonBehaviors.Actions;
using SlimAI.Settings;
using Styx;
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
    public class GuardianDruid
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static DruidSettings Settings { get { return GeneralSettings.Instance.Druid(); } }

        #region Coroutine Section

        public static async Task<bool> CombatCoroutine()
        {
            // Pause for Casting
            if (Me.IsCasting || Me.IsChanneling || !Me.GotTarget || Me.Mounted);

            await Spell.CoCast(Rejuvenation, Me, Me.HasAura(HeartoftheWildBuff) && !Me.HasAura(Rejuvenation));
            await Spell.CoCast(CenarionWard, Me, Me.HealthPercent <= 75);
            await Spell.CoCast(HealingTouch, Me, Me.HasAura(145162) && Me.HealthPercent <= 90 || Me.HasAura(145162) && Me.GetAuraTimeLeft(145162).TotalSeconds < 2 && Me.GetAuraTimeLeft(145162).TotalSeconds > 1);
            await Spell.CoCast(FrenziedRegeneration, Me.CurrentRage >= 60 && Me.HealthPercent <= 65 && !Me.HasAura("Frenzied Regeneration"));
            await Spell.CoCast(SavageDefense, IsCurrentTank() && Me.HealthPercent <= 80);

            await Spell.CoCast(Maul, (Me.RagePercent > 90 || Me.HasAura(ToothandClaw) && Me.HealthPercent > 55));
            await Spell.CoCast(Mangle);
            await Spell.CoCast("Thrash", Me.CurrentTarget.HasAuraExpired("Thrash", 4) || (Unit.UnfriendlyUnits(8).Count() >= 2 && SlimAI.AOE));
            await Spell.CoCast(Lacerate);
            await Spell.CoCast(Maul, !IsCurrentTank());

            return false;

        }

        [Behavior(BehaviorType.Combat, WoWClass.Druid, WoWSpec.DruidGuardian)]
        public static Composite CoGuardianCombat()
        {
            return new ActionRunCoroutine(ctx => CombatCoroutine());
        }

        #endregion



        
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Druid, WoWSpec.DruidGuardian)]
        public static Composite GuardianPreCombatBuffs()
        {
            return new PrioritySelector(
                new Decorator(ret => Me.Mounted || Me.Combat,
                    new ActionAlwaysSucceed())
                //PartyBuff.BuffGroup("Mark of the Wild")
                );
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
                          Thrash = 77758,
                          ToothandClaw = 135286;
        #endregion
    }
}
