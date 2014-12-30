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

            await Spell.CoCast(SavageDefense, IsCurrentTank());

            await Spell.CoCast(Maul, (Me.HasAura(ToothandClaw) && Me.CurrentRage >= 75 && IsCurrentTank()) || Me.CurrentRage >= Me.MaxRage - 15/*Me.HasAura(ToothandClaw) && !IsCurrentTank()*/);

            await Spell.CoCast(CenarionWard, Me.HealthPercent <= 75);

            await Spell.CoCast(HealingTouch, Me, Me.HasAura(145162) && Me.HealthPercent <= 50);

            await Spell.CoCast(Pulverize, Me.HasAuraExpired("Pulverize", 3));

            await Spell.CoCast(Mangle);

            await Spell.CoCast(Lacerate, !Me.HasAura(Berserk) && ((Me.HasAuraExpired("Pulverize", 4) && buffStackCount(Lacerate, Me.CurrentTarget) < 3) || !Me.CurrentTarget.HasAura(Lacerate)));

            //await Spell.CoCast("Thrash", !Me.CurrentTarget.HasAura("Thrash"));

            await Spell.CoCast("Thrash", Unit.EnemyUnitsSub8.Count(u => u.HasAuraExpired("Thrash", 4)) >= 1 || SlimAI.AOE && Unit.UnfriendlyUnits(8).Count() >= 2);

            await Spell.CoCast(Lacerate);


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

        #region Stack Count
        public static uint buffStackCount(int Buff, WoWUnit onTarget)
        {
            if (onTarget != null)
            {
                var Results = onTarget.GetAuraById(Buff);
                if (Results != null)
                    return Results.StackCount;
            }
            return 0;
        }
        #endregion

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
                          Pulverize = 80313,
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
