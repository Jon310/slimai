using System;
using System.Linq;
using System.Windows.Forms;
using SlimAI.Helpers;
using SlimAI.Managers;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Class.Hunter
{
    class BeastmasterHunter
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        static WoWUnit Pet { get { return StyxWoW.Me.Pet; } }

        [Behavior(BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterBeastMastery)]
        public static Composite BeastMasterCombat()
            {
                return new PrioritySelector(

                    new Throttle(1,
                        new Action(context => ResetVariables())),
                    new Decorator(ret => SlimAI.PvPRotation,
                        CreatePvP()),

                new Decorator(ret => !Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive || Me.HasAura("Feign Death"),
                    new ActionAlwaysSucceed()),
                        Common.CreateInterruptBehavior(),
                        CreateMisdirectionBehavior(),
                        CreateHunterTrapBehavior("Explosive Trap", true, ret => Me.CurrentTarget, ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2 && SlimAI.AOE),

                        Spell.Cast("Focus Fire", ctx => Me.HasAura("Frenzy", 5) && !Me.HasAura("The Beast Within")),
                        Spell.Cast("Serpent Sting", ret => !Me.CurrentTarget.HasMyAura("Serpent Sting")),
                        new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                        //Burst
                        new Decorator(ret => SlimAI.Burst,
                            new PrioritySelector(
                                Spell.Cast("Rapid Fire"),
                                Spell.Cast("Stampede"),
                                Spell.Cast("Bestial Wrath", ret => Me.CurrentFocus > 60),
                                Spell.Cast("A Murder of Crows"))),

                        Spell.Cast("Fervor", ctx => Me.CurrentFocus < 65),
                        Spell.Cast("Dire Beast"),
                        Spell.Cast("Rabid", ret => Me.HasAura("The Beast Within")),

                        Spell.Cast("Exhilaration", ret => Me.HealthPercent < 35 || (Pet != null && Pet.HealthPercent < 25)),
                        Spell.Cast("Tranquilizing Shot", ctx => Me.CurrentTarget.ActiveAuras.ContainsKey("Enraged") || Me.CurrentTarget.ActiveAuras.ContainsKey("Magic")),

                        //Spell.Buff("Concussive Shot",
                        //    ret => Me.CurrentTarget.CurrentTargetGuid == Me.Guid 
                        //        && Me.CurrentTarget.Distance > Spell.MeleeRange),

                        // AoE Rotation
                        new Decorator(ret => Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 5 && SlimAI.AOE, 
                            new PrioritySelector(
                                Spell.Cast( "Multi-Shot", ctx => Clusters.GetBestUnitForCluster( Unit.NearbyUnfriendlyUnits.Where( u => u.Distance < 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u)), ClusterType.Radius, 8f)),
                                Spell.Cast( "Kill Shot", onUnit => Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 20 && u.Distance < 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u))),
                                Spell.Cast("Cobra Shot", ret => !Me.HasAura("The Beast Within")))),

                        Spell.Cast("Mend Pet", onUnit => Pet, ret => Me.GotAlivePet && Pet.HealthPercent < 60 && !Pet.HasAura("Mend Pet")),
                        Spell.Cast("Kill Command", ctx => Me.GotAlivePet && Pet.GotTarget && Pet.Location.Distance(Pet.CurrentTarget.Location) < 25f),
                        Spell.Cast("Kill Shot", ctx => Me.CurrentTarget.HealthPercent <= 20),
                        Spell.Cast("Glaive Toss"),
                        Spell.Cast("Lynx Rush", ret => Pet != null && Unit.NearbyUnfriendlyUnits.Any(u => Pet.Location.Distance(u.Location) <= 10)),
                        Spell.Cast("Barrage"),
                        Spell.Cast("Powershot"),
                        Spell.Cast("Arcane Shot", ret => Me.CurrentFocus >= 61 || Me.HasAura("Thrill of the Hunt") || Me.HasAura("The Beast Within")),  
                        Spell.Cast("Cobra Shot", ret => !Me.HasAura("The Beast Within"))
                    );
            }
        

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Hunter, WoWSpec.HunterBeastMastery)]
        public static Composite BeastMasterPreCombatBuffs()
            {
                return new PrioritySelector(

                );
            }

        #region PvP
        private static Composite CreatePvP()
        {
            return new PrioritySelector(
                new Decorator(ret => !Me.Combat || Me.Mounted || Me.HasAura("Feign Death"),
                    new ActionAlwaysSucceed()),
                //CC
                CreateHunterTrapBehavior("Explosive Trap", true, on => Me.CurrentTarget, ret => KeyboardPolling.IsKeyDown(Keys.Z)),
                Spell.Cast("Scatter Shot", on => Me.FocusedUnit, ret => KeyboardPolling.IsKeyDown(Keys.C)),
                CreateHunterTrapBehavior("Freezing Trap", true, on => Me.FocusedUnit, ret => KeyboardPolling.IsKeyDown(Keys.F)),
                Spell.Cast("Concussive Shot", ret => KeyboardPolling.IsKeyDown(Keys.R)),

                new Throttle(1,
                Spell.Cast("Tranquilizing Shot", on => BestTranq)),

                Spell.Cast("Focus Fire", ctx => Me.HasAura("Frenzy", 5) && !Me.HasAura("The Beast Within")),
                Spell.Cast("Serpent Sting", ret => !Me.CurrentTarget.HasMyAura("Serpent Sting")),

                //Burst
                new Decorator(ret => SlimAI.Burst,
                    new PrioritySelector(
                        Spell.Cast("Rapid Fire"),
                        Spell.Cast("Stampede"),
                        Spell.Cast("Bestial Wrath", ret => Me.CurrentFocus > 60),
                        Spell.Cast("A Murder of Crows"))),

                Spell.Cast("Fervor", ctx => Me.CurrentFocus <= 50),
                Spell.Cast("Dire Beast"),
                Spell.Cast("Lynx Rush", ret => Pet != null && Unit.NearbyUnfriendlyUnits.Any(u => Pet.Location.Distance(u.Location) <= 10)),
                Spell.Cast("Rabid", ret => Me.HasAura("The Beast Within")),

                Spell.Cast("Exhilaration", ret => Me.HealthPercent < 35 || (Pet != null && Pet.HealthPercent < 25)),
                Spell.Cast("Tranquilizing Shot", on => BestTranq),

                Spell.Cast("Mend Pet", onUnit => Pet, ret => Me.GotAlivePet && Pet.HealthPercent < 60 && !Pet.HasAura("Mend Pet")),
                Spell.Cast("Kill Command", ctx => Me.GotAlivePet && Pet.GotTarget && Pet.Location.Distance(Pet.CurrentTarget.Location) < 25f),
                Spell.Cast("Kill Shot", ctx => Me.CurrentTarget.HealthPercent <= 20),
                Spell.Cast("Glaive Toss"),
                Spell.Cast("Barrage"),
                Spell.Cast("Powershot"),
                Spell.Cast("Arcane Shot", ret => Me.CurrentFocus >= 61 || Me.HasAura("Thrill of the Hunt") || Me.HasAura("The Beast Within")),
                Spell.Cast("Cobra Shot", ret => !Me.HasAura("The Beast Within")),
                 Spell.Cast("Focus Fire", ctx => Me.HasAura("Frenzy", 1) && Me.GetAuraTimeLeft("Frenzy").TotalSeconds <= 1),
                new ActionAlwaysSucceed()
                );
        }
        #endregion

        #region Dispells
        private static bool Tranq
        {
            get
                {
                    return Me.CurrentTarget.HasAnyAura("Divine Plea", "Fear Ward", "Power Word: Shield", "Dark Soul: Instability", "Dark Soul: Knowledge",
                                                       "Dark Soul: Misery", "Icy Veins","Hand of Protection", "Innervate", "Incanter's Ward", "Alter Time",
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
                                                       "Dark Soul: Instability", "Dark Soul: Knowledge","Dark Soul: Misery",
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
                            new Decorator(ret => !useLauncher, new Action(ret => Me.CancelAura("Trap Launcher")))
                            ),

                        // wait for launcher to appear (or dissappear) as required
                        new PrioritySelector(
                            new Wait(TimeSpan.FromMilliseconds(500),
                                until => (!useLauncher && !Me.HasAura("Trap Launcher")) || (useLauncher && Me.HasAura("Trap Launcher")),
                                new ActionAlwaysSucceed()),
                            new Action(ret =>
                            {
                                return RunStatus.Failure;
                            })
                            ),

                // Spell.Cast( trapName, ctx => onUnit(ctx)),
                        new Action(ret => SpellManager.Cast(trapName, onUnit(ret))),
                        Common.CreateWaitForLagDuration(),
                        new Action(ctx => SpellManager.ClickRemoteLocation(onUnit(ctx).Location))
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
