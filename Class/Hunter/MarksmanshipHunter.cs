using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommonBehaviors.Actions;
using SlimAI.Helpers;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = System.Action;

namespace SlimAI.Class.Hunter
{
    class MarksmanshipHunter
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        static WoWUnit Pet { get { return StyxWoW.Me.Pet; } }

        [Behavior(BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterMarksmanship)]
        public static Composite MarksmanshipCombat()
        {
            return new PrioritySelector(

                new Throttle(1,
                    new Styx.TreeSharp.Action(context => ResetVariables())),

            new Decorator(ret => !Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive || Me.HasAura("Feign Death"),
                new ActionAlwaysSucceed()),
                    Common.CreateInterruptBehavior(),
                    CreateMisdirectionBehavior(),
                    CreateHunterTrapBehavior("Explosive Trap", true, ret => Me.CurrentTarget, ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2 && SlimAI.AOE),
                    Spell.Cast("Exhilaration", ret => Me.HealthPercent < 35 || (Pet != null && Pet.HealthPercent < 25)),
                    Spell.Cast("Tranquilizing Shot", ctx => Me.CurrentTarget.ActiveAuras.ContainsKey("Enraged") || Me.CurrentTarget.ActiveAuras.ContainsKey("Magic")),
                    Spell.Cast("Mend Pet", onUnit => Pet, ret => Me.GotAlivePet && Pet.HealthPercent < 60 && !Pet.HasAura("Mend Pet")),

                    Spell.Cast("Powershot"),
                    Spell.Cast("Lynx Rush", ret => Pet != null && Unit.NearbyUnfriendlyUnits.Any(u => Pet.Location.Distance(u.Location) <= 10)),
                    Spell.Cast("Aimed Shot", ret => Me.HasAura("Fire!")),
                    Spell.Cast("Fervor", ctx => Me.CurrentFocus < 50),

                    new Decorator(ret => SlimAI.Burst,
                        new PrioritySelector(
                            Spell.Cast("Rapid Fire"),
                            Spell.Cast("Stampede"),
                            Spell.Cast("A Murder of Crows"))),

                    Spell.Cast("Dire Beast"),

                    new Decorator(ret => Me.CurrentTarget.HealthPercent > 80,
                        CreateCarefulAim()),

                    Spell.Cast("Steady Shot", ret => !Me.HasAura("Steady Focus") || Me.GetAuraTimeLeft("Steady Focus").TotalSeconds < 4),
                    Spell.Cast("Glaive Toss"),
                    Spell.Cast("Barrage"),
                    Spell.Cast("Serpent Sting", ret => !Me.CurrentTarget.HasMyAura("Serpent Sting")),
                    Spell.Cast("Chimera Shot"),
                    Spell.Cast("Steady Shot", ret => !Me.HasAura("Steady Focus") || Me.GetAuraTimeLeft("Steady Focus").TotalSeconds < 3),
                    Spell.Cast("Kill Shot", ctx => Me.CurrentTarget.HealthPercent <= 20),
                    Spell.Cast("Multi-Shot", ctx => Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 4 && SlimAI.AOE),  
                    Spell.Cast("Arcane Shot", ret => Me.HasAura("Thrill of the Hunt")),
                    Spell.Cast("Aimed Shot", ret => Me.HasAura("Rapid Fire") || PartyBuff.WeHaveBloodlust),
                    Spell.Cast("Arcane Shot", ret => Me.CurrentFocus >= 60 || Me.CurrentFocus >= 43 && (Spell.GetSpellCooldown("Chimera Shot").TotalSeconds > 1.5) && (!Me.HasAura("Rapid Fire") || !PartyBuff.WeHaveBloodlust)),
                    Spell.Cast("Steady Shot")
                );
        }


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Hunter, WoWSpec.HunterSurvival)]
        public static Composite MarksmanshipPreCombatBuffs()
        {
            return new PrioritySelector(

            );
        }

        #region Careful Aim
        private static Composite CreateCarefulAim()
        {
            return new PrioritySelector(
                    Spell.Cast("Serpent Sting", ret => !Me.CurrentTarget.HasMyAura("Serpent Sting")),
                //X	6.28	chimera_shot
                    Spell.Cast("Chimera Shot"),
                //Y	1.70	steady_shot,if=buff.pre_steady_focus.up&buff.steady_focus.remains<6
                    Spell.Cast("Steady Shot", ret => !Me.HasAura("Steady Focus") || Me.GetAuraTimeLeft("Steady Focus").TotalSeconds < 6),
                    Spell.Cast("Aimed Shot"),
                    Spell.Cast("Glaive Toss"),

                    Spell.Cast("Steady Shot")

                );
        }
        #endregion

        #region Dispells
        private static bool Tranq
        {
            get
            {
                return Me.CurrentTarget.HasAnyAura("Divine Plea", "Fear Ward", "Power Word: Shield", "Dark Soul: Instability", "Dark Soul: Knowledge",
                                                   "Dark Soul: Misery", "Icy Veins", "Hand of Protection", "Innervate", "Incanter's Ward", "Alter Time",
                                                   "Power Infusion", "Stay of Execution", "Eternal Flame", "Spiritwalker's Grace", "Ancestral Swiftness");
            }
        }
        #endregion

        #region Best Tranq
        public static WoWUnit BestTranq
        {
            get
            {
                var bestTranq = (from unit in ObjectManager.GetObjectsOfType<WoWPlayer>(false)
                                 where unit.IsAlive
                                 where unit.IsPlayer
                                 where !unit.IsInMyPartyOrRaid
                                 where unit.InLineOfSight
                                 where unit.Distance <= 40
                                 where unit.HasAnyAura("Power Infusion", "Fear Ward", "Power Word: Shield",
                                                       "Dark Soul: Instability", "Dark Soul: Knowledge", "Dark Soul: Misery",
                                                       "Icy Veins", "Incanter's Ward", "Alter Time", "Innervate",
                                                       "Hand of Protection", "Divine Plea", "Stay of Execution", "Eternal Flame",
                                                       "Spiritwalker's Grace", "Ancestral Swiftness")
                                 select unit).FirstOrDefault();
                return bestTranq;
            }
        }
        #endregion

        #region Misdirect
        /// <summary>
        /// creates composite that buffs Misdirection on appropriate target.  always cast on Pet for Normal, never cast at all in PVP, 
        /// conditionally cast in Instances based upon parameter value
        /// </summary>
        /// <param name="buffForPull">applies to Instances only.  true = call is for pull behavior so allow use in instances; 
        /// false = disabled in instances</param>
        /// <returns></returns>
        public static Composite CreateMisdirectionBehavior()
        {
            // Normal - misdirect onto Pet on cooldown
            if (!Me.IsInGroup())
            {
                return new ThrottlePasses(5,
                    new Decorator(
                        ret => Me.GotAlivePet && !Me.HasAura("Misdirection"),
                        Spell.Cast("Misdirection", ctx => Me.Pet, req => Me.GotAlivePet && Pet.Distance < 100))
                    );
            }

            // Instances - misdirect only if pullCheck == true
            if (Me.IsInGroup())
            {
                return new ThrottlePasses(5,
                    new Decorator(
                        ret => Me.GotAlivePet && !Me.HasAura("Misdirection"),
                        Spell.Cast("Misdirection", on => Group.Tanks.FirstOrDefault(t => t.IsAlive && t.Distance < 100))
                        )
                    );
            }

            return new ActionAlwaysFail();
        }
        #endregion

        #region Traps
        public static Composite CreateHunterTrapBehavior(string trapName, bool useLauncher, UnitSelectionDelegate onUnit, SimpleBooleanDelegate require = null)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => onUnit != null && onUnit(ret) != null
                        && (require == null || require(ret))
                        && onUnit(ret).DistanceSqr < (40 * 40)
                        && SpellManager.HasSpell(trapName) && Spell.GetSpellCooldown(trapName) == TimeSpan.Zero,
                    new Sequence(
                // add or remove trap launcher based upon parameter 
                        new PrioritySelector(
                            new Decorator(ret => useLauncher && Me.HasAura("Trap Launcher"), new ActionAlwaysSucceed()),
                            Spell.BuffSelf("Trap Launcher", req => useLauncher),
                            new Decorator(ret => !useLauncher, new Styx.TreeSharp.Action(ret => Me.CancelAura("Trap Launcher")))
                            ),

                        // wait for launcher to appear (or dissappear) as required
                        new PrioritySelector(
                            new Wait(TimeSpan.FromMilliseconds(500),
                                until => (!useLauncher && !Me.HasAura("Trap Launcher")) || (useLauncher && Me.HasAura("Trap Launcher")),
                                new ActionAlwaysSucceed()),
                            new Styx.TreeSharp.Action(ret =>
                            {
                                return RunStatus.Failure;
                            })
                            ),

                // Spell.Cast( trapName, ctx => onUnit(ctx)),
                        new Styx.TreeSharp.Action(ret => SpellManager.Cast(trapName, onUnit(ret))),
                        Helpers.Common.CreateWaitForLagDuration(),
                        new Styx.TreeSharp.Action(ctx => SpellManager.ClickRemoteLocation(onUnit(ctx).Location))
                        )
                    )
                );
        }

        #endregion

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
