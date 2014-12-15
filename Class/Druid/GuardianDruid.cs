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

//8	37.42	savage_defense
            await Spell.CoCast(SavageDefense, IsCurrentTank());
//D	8.02	barkskin
//E	75.45	maul,if=buff.tooth_and_claw.react&incoming_damage_1s
            await Spell.CoCast(Maul, (Me.HasAura(ToothandClaw) && Me.CurrentRage >= 75 && IsCurrentTank()) || Me.HasAura(ToothandClaw) && !IsCurrentTank());
//F	2.88	berserk,if=buff.pulverize.remains>10
//G	8.69	frenzied_regeneration,if=rage>=80
//H	14.10	cenarion_ward
            await Spell.CoCast(CenarionWard, Me.HealthPercent <= 75);
//M	12.93	healing_touch,if=buff.dream_of_cenarius.react&health.pct<30
            await Spell.CoCast(HealingTouch, Me, Me.HasAura(145162) && Me.HealthPercent <= 30);
//N	33.84	pulverize,if=buff.pulverize.remains<0.5
            await Spell.CoCast(Pulverize, Me.HasAuraExpired("Pulverize", 3));
//O	13.50	lacerate,if=talent.pulverize.enabled&buff.pulverize.remains<=(3-dot.lacerate.stack)*gcd&buff.berserk.down
            await Spell.CoCast(Lacerate, !Me.HasAura(Berserk) && ((Me.HasAuraExpired("Pulverize", 3) && buffStackCount(Lacerate, Me.CurrentTarget) < 3) || !Me.CurrentTarget.HasAura(Lacerate)));
//R	6.96	thrash_bear,if=!ticking
            await Spell.CoCast("Thrash", !Me.CurrentTarget.HasAura("Thrash"));
//S	95.56	mangle
            await Spell.CoCast(Mangle);
//T	20.58	thrash_bear,if=remains<=0.3*duration
            await Spell.CoCast("Thrash", Me.CurrentTarget.HasAuraExpired("Thrash", 4));
//U	98.40	lacerate
            await Spell.CoCast(Lacerate);

            //await Spell.CoCast(Rejuvenation, Me, Me.HasAura(HeartoftheWildBuff) && !Me.HasAura(Rejuvenation));
            //await Spell.CoCast(CenarionWard, Me, Me.HealthPercent <= 75);
            //await Spell.CoCast(HealingTouch, Me, Me.HasAura(145162) && Me.HealthPercent <= 90 || Me.HasAura(145162) && Me.GetAuraTimeLeft(145162).TotalSeconds < 2 && Me.GetAuraTimeLeft(145162).TotalSeconds > 1);
            ////await Spell.CoCast(FrenziedRegeneration, Me.RagePercent >= 60 && Me.HealthPercent <= 65 && !Me.HasAura("Frenzied Regeneration"));
            //await Spell.CoCast(SavageDefense, IsCurrentTank() && Me.HealthPercent <= 80);

            //await Spell.CoCast(Maul, (Me.RagePercent > 90 || Me.HasAura(ToothandClaw) && Me.HealthPercent > 55 && Me.RagePercent >= 75));
            //await Spell.CoCast(Pulverize, Me.HasAuraExpired("Pulverize", 2));
            //await Spell.CoCast(Mangle);
            //await Spell.CoCast(Lacerate, !Me.HasAura("Pulverize"));
            //await Spell.CoCast("Thrash", Me.CurrentTarget.HasAuraExpired("Thrash", 4) || (Unit.UnfriendlyUnits(8).Count() >= 2 && SlimAI.AOE));
            //await Spell.CoCast(Lacerate);
            //await Spell.CoCast(Maul, !IsCurrentTank());

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
