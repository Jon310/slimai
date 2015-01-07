using System;
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

namespace SlimAI.Class.Hunter
{
    [UsedImplicitly]
    class MarksmanshipHunter
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        static WoWUnit Pet { get { return StyxWoW.Me.Pet; } }

        #region Coroutine Combat
        private static async Task<bool> CombatCoroutine()
        {
            //if (SlimAI.PvPRotation)
            //{
            //    await PvPCoroutine();
            //    return true;
            //}

            if (!Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive) return true;

            await Spell.CoCastMove("Kill Shot");
            await Spell.CoCastMove("Chimaera Shot");
            await Spell.CoCastMove("Rapid Fire", SlimAI.Burst);
            await Spell.CoCastMove("Stampede", SlimAI.Burst && (Me.HasAura("Rapid Fire") || PartyBuff.WeHaveBloodlust));
            await CarefulAim(Me.CurrentTarget.HealthPercent > 80 || Me.HasAura("Rapid Fire"));
            await Spell.CoCastMove("A Murder of Crows", SlimAI.Burst);
            await Spell.CoCastMove("Dire Beast");
            await Spell.CoCastMove("Glaive Toss");
            await Spell.CoCastMove("Powershot");
            await Spell.CoCastMove("Barrage", SlimAI.Weave);
            await Spell.CoCastMove("Aimed Shot");
            await Spell.CoCastMove("Steady Shot");

            return false;
         }

        [Behavior(BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterMarksmanship)]
        public static Composite CoMarksmanshipCombat()
        {
            return new ActionRunCoroutine(ctx => CombatCoroutine());
        }

#endregion


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Hunter, WoWSpec.HunterMarksmanship)]
        public static Composite MarksmanshipPreCombatBuffs()
        {
            return new PrioritySelector(

            );
        }

        #region Careful Aim
        private static async Task<bool> CarefulAim(bool reqs)
        {
            if (!reqs)
                return false;
            await Spell.CoCastMove("Glaive Toss");
            await Spell.CoCastMove("Powershot");
            await Spell.CoCastMove("Barrage", SlimAI.Weave);
            await Spell.CoCastMove("Aimed Shot");
            await Spell.CoCastMove("Glaive Toss");
            await Spell.CoCastMove("Steady Shot");

            return false;
        }
        #endregion

        //#region Dispells
        //private static bool Tranq
        //{
        //    get
        //    {
        //        return Me.CurrentTarget.HasAnyAura("Divine Plea", "Fear Ward", "Power Word: Shield", "Dark Soul: Instability", "Dark Soul: Knowledge",
        //                                           "Dark Soul: Misery", "Icy Veins", "Hand of Protection", "Innervate", "Incanter's Ward", "Alter Time",
        //                                           "Power Infusion", "Stay of Execution", "Eternal Flame", "Spiritwalker's Grace", "Ancestral Swiftness");
        //    }
        //}
        //#endregion

        //#region Best Tranq
        //public static WoWUnit BestTranq
        //{
        //    get
        //    {
        //        var bestTranq = (from unit in ObjectManager.GetObjectsOfType<WoWPlayer>(false)
        //                         where unit.IsAlive
        //                         where unit.IsPlayer
        //                         where !unit.IsInMyPartyOrRaid
        //                         where unit.InLineOfSight
        //                         where unit.Distance <= 40
        //                         where unit.HasAnyAura("Power Infusion", "Fear Ward", "Power Word: Shield",
        //                                               "Dark Soul: Instability", "Dark Soul: Knowledge", "Dark Soul: Misery",
        //                                               "Icy Veins", "Incanter's Ward", "Alter Time", "Innervate",
        //                                               "Hand of Protection", "Divine Plea", "Stay of Execution", "Eternal Flame",
        //                                               "Spiritwalker's Grace", "Ancestral Swiftness")
        //                         select unit).FirstOrDefault();
        //        return bestTranq;
        //    }
        //}
        //#endregion

        //#region Misdirect
        ///// <summary>
        ///// creates composite that buffs Misdirection on appropriate target.  always cast on Pet for Normal, never cast at all in PVP, 
        ///// conditionally cast in Instances based upon parameter value
        ///// </summary>
        ///// <param name="buffForPull">applies to Instances only.  true = call is for pull behavior so allow use in instances; 
        ///// false = disabled in instances</param>
        ///// <returns></returns>
        //public static Composite CreateMisdirectionBehavior()
        //{
        //    // Normal - misdirect onto Pet on cooldown
        //    if (!Me.IsInGroup())
        //    {
        //        return new Throttle(5,
        //            new Decorator(
        //                ret => Me.GotAlivePet && !Me.HasAura("Misdirection"),
        //                Spell.Cast("Misdirection", ctx => Me.Pet, req => Me.GotAlivePet && Pet.Distance < 100))
        //            );
        //    }

        //    // Instances - misdirect only if pullCheck == true
        //    if (Me.IsInGroup())
        //    {
        //        return new ThrottlePasses(5,
        //            new Decorator(
        //                ret => Me.GotAlivePet && !Me.HasAura("Misdirection"),
        //                Spell.Cast("Misdirection", on => Group.Tanks.FirstOrDefault(t => t.IsAlive && t.Distance < 100))
        //                )
        //            );
        //    }

        //    return new ActionAlwaysFail();
        //}
        //#endregion

        //#region Traps
        //public static Composite CreateHunterTrapBehavior(string trapName, bool useLauncher, UnitSelectionDelegate onUnit, SimpleBooleanDelegate require = null)
        //{
        //    return new PrioritySelector(
        //        new Decorator(
        //            ret => onUnit != null && onUnit(ret) != null
        //                && (require == null || require(ret))
        //                && onUnit(ret).DistanceSqr < (40 * 40)
        //                && SpellManager.HasSpell(trapName) && Spell.GetSpellCooldown(trapName) == TimeSpan.Zero,
        //            new Sequence(
        //        // add or remove trap launcher based upon parameter 
        //                new PrioritySelector(
        //                    new Decorator(ret => useLauncher && Me.HasAura("Trap Launcher"), new ActionAlwaysSucceed()),
        //                    Spell.BuffSelf("Trap Launcher", req => useLauncher),
        //                    new Decorator(ret => !useLauncher, new Action(ret => Me.CancelAura("Trap Launcher")))
        //                    ),

        //                // wait for launcher to appear (or dissappear) as required
        //                new PrioritySelector(
        //                    new Wait(TimeSpan.FromMilliseconds(500),
        //                        until => (!useLauncher && !Me.HasAura("Trap Launcher")) || (useLauncher && Me.HasAura("Trap Launcher")),
        //                        new ActionAlwaysSucceed()),
        //                    new Action(ret =>
        //                    {
        //                        return RunStatus.Failure;
        //                    })
        //                    ),

        //        // Spell.Cast( trapName, ctx => onUnit(ctx)),
        //                new Action(ret => SpellManager.Cast(trapName, onUnit(ret))),
        //                Common.CreateWaitForLagDuration(),
        //                new Action(ctx => SpellManager.ClickRemoteLocation(onUnit(ctx).Location))
        //                )
        //            )
        //        );
        //}

        //#endregion

        #region Reset
        private static RunStatus ResetVariables()
        {
            KeyboardPolling.IsKeyDown(Keys.Z);
            KeyboardPolling.IsKeyDown(Keys.C);
            KeyboardPolling.IsKeyDown(Keys.F);
            KeyboardPolling.IsKeyDown(Keys.R);
            return RunStatus.Failure;
        }
        #endregion


        #region HunterTalents
        public enum HunterTalents
        {
            Posthaste = 1,//Tier 1
            NarrowEscape,
            CrouchingTiger,
            SilencingShot,//Tier 2
            WyvernSting,
            Intimidation,
            Exhilaration,//Tier 3
            AspectoftheIronHawk,
            SpiritBond,
            Fervor,//Tier 4
            DireBeast,
            ThrilloftheHunt,
            AMurderofCrows,//Tier 5
            BlinkStrikes,
            LynxRush,
            GlaiveToss,//Tier 6
            Powershot,
            Barrage
        }
        #endregion
    }
}
