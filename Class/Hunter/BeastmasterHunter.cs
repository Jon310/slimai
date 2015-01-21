using System;
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

namespace SlimAI.Class.Hunter
{
    [UsedImplicitly]
    class BeastMasterHunter
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        static WoWUnit Pet { get { return StyxWoW.Me.Pet; } }

        #region Coroutine Combat
        private static async Task<bool> CombatCoroutine()
        {

            if (SlimAI.PvPRotation)
            {
                await PvPCoroutine();
                return true;
            }

            if (!Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive || Me.IsCasting || Me.IsChanneling) return true;

            await CreateMisdirectionBehavior().ExecuteCoroutine();
            await CreateHunterTrapBehavior("Explosive Trap", true, ret => Me.CurrentTarget, ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2 && SlimAI.AOE).ExecuteCoroutine();
            
            await Spell.CoCastMove("Mend Pet", Pet, Me.GotAlivePet && Pet.HealthPercent < 60 && !Pet.HasAura("Mend Pet"));
            await Spell.CoCastMove("Focus Fire", Me.HasAura("Frenzy", 5) && !Me.HasAura("The Beast Within"));
       
            await Spell.CoCastMove("Rapid Fire", SlimAI.Burst);
            await Spell.CoCastMove("Stampede", SlimAI.Burst);
            await Spell.CoCastMove("Bestial Wrath", Me.CurrentFocus > 60 && SlimAI.Burst);
            await Spell.CoCastMove("A Murder of Crows", SlimAI.Burst);

            await Spell.CoCastMove("Dire Beast");
            await Spell.CoCastMove("Exhilaration", Me.HealthPercent < 35 || (Pet != null && Pet.HealthPercent < 25));
            await Spell.CoCastMove("Tranquilizing Shot", Me.CurrentTarget.ActiveAuras.ContainsKey("Enraged") || Me.CurrentTarget.ActiveAuras.ContainsKey("Magic"));

            await CoAOE(Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 5 && SlimAI.AOE);
            
            await Spell.CoCastMove("Kill Command", Me.GotAlivePet && Pet.GotTarget && Pet.Location.Distance(Pet.CurrentTarget.Location) < 25f);
            await Spell.CoCastMove("Kill Shot", Me.CurrentTarget.HealthPercent <= 20);
            await Spell.CoCastMove("Glaive Toss");
            await Spell.CoCastMove("Barrage");
            await Spell.CoCastMove("Powershot");
            await Spell.CoCastMove("Arcane Shot", Me.CurrentFocus >= 64 || Me.HasAura("Thrill of the Hunt") && Me.CurrentFocus > 35 || Me.HasAura("The Beast Within"));
            await Spell.CoCastMove("Cobra Shot", !Me.HasAura("The Beast Within"));
                //    );
            return false;
        }

        [Behavior(BehaviorType.Combat, WoWClass.Hunter, WoWSpec.HunterBeastMastery)]
        public static Composite CoBeastMasterCombat()
        {
            return new ActionRunCoroutine(ctx => CombatCoroutine());
        }
        #endregion


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Hunter, WoWSpec.HunterBeastMastery)]
        public static Composite BeastMasterPreCombatBuffs()
            {
                return new PrioritySelector(

                );
            }

        #region Coroutine AOE
        private static async Task<bool> CoAOE(bool reqs)
        {
            if (!reqs)
                return false;
            await Spell.CoCastMove("Multi-Shot", Clusters.GetBestUnitForCluster(Unit.NearbyUnfriendlyUnits.Where(u => u.Distance < 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u)), ClusterType.Radius, 8f));
            await Spell.CoCastMove("Kill Shot", Unit.NearbyUnfriendlyUnits.FirstOrDefault(u => u.HealthPercent < 20 && u.Distance < 40 && u.InLineOfSpellSight && Me.IsSafelyFacing(u)));
            await Spell.CoCastMove("Glaive Toss");
            await Spell.CoCastMove("Barrage");            
            await Spell.CoCastMove("Cobra Shot", !Me.HasAura("The Beast Within"));

            return false;
        }
        #endregion

        #region PvP

        private static async Task<bool> PvPCoroutine()
        {

            if (Me.CurrentTarget.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield", "Freezing Trap", "Cyclone", "Hex") || !Me.Combat || Me.Mounted) return true;

            await CoFreeze();
            await CoFire();
            await CoIce();
            await CoBind();
            
            if (!Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive || Me.IsCasting || Me.IsChanneling) return true;

            //await CreateMisdirectionBehavior().ExecuteCoroutine();
            //await CreateHunterTrapBehavior("Explosive Trap", true, ret => Me.CurrentTarget, ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2 && SlimAI.AOE).ExecuteCoroutine();

            await Spell.CoCast("Concussive Shot", !Freedoms && !Me.CurrentTarget.IsStunned() && !Me.CurrentTarget.IsCrowdControlled() && !Me.CurrentTarget.IsSlowed() && Me.CurrentTarget.IsPlayer);

            await Spell.CoCastMove("Mend Pet", Pet, Me.GotAlivePet && Pet.HealthPercent < 60 && !Pet.HasAura("Mend Pet"));
            await Spell.CoCastMove("Focus Fire", Me.HasAura("Frenzy", 5) && !Me.HasAura("The Beast Within"));

            await Spell.CoCastMove("Stampede", SlimAI.Burst);
            await Spell.CoCastMove("Bestial Wrath", Me.CurrentFocus > 60 && SlimAI.Burst);
            await Spell.CoCastMove("A Murder of Crows", SlimAI.Burst);

            await Spell.CoCastMove("Dire Beast");
            await Spell.CoCastMove("Exhilaration", Me.HealthPercent < 35 || (Pet != null && Pet.HealthPercent < 25));
            await Spell.CoCastMove("Tranquilizing Shot", BestTranq);

            //await CoAOE(Unit.UnfriendlyUnitsNearTarget(8f).Count() >= 5 && SlimAI.AOE);

            await Spell.CoCastMove("Kill Command", Me.GotAlivePet && Pet.GotTarget && Pet.Location.Distance(Pet.CurrentTarget.Location) < 25f);
            await Spell.CoCastMove("Kill Shot", Me.CurrentTarget.HealthPercent <= 20);
            await Spell.CoCastMove("Glaive Toss");
            await Spell.CoCastMove("Barrage");
            await Spell.CoCastMove("Powershot");
            await Spell.CoCastMove("Arcane Shot", Me.CurrentFocus >= 64 || Me.HasAura("Thrill of the Hunt") && Me.CurrentFocus > 35 || Me.HasAura("The Beast Within"));
            await Spell.CoCastMove("Cobra Shot", !Me.HasAura("The Beast Within"));

            return true;
        }


        #endregion

        //#region PvP
        //private static Composite CreatePvP()
        //{
        //    return new PrioritySelector(
        //        new Decorator(ret => !Me.Combat || Me.Mounted || Me.HasAura("Feign Death"),
        //            new ActionAlwaysSucceed()),
        //        //CC
        //        CreateHunterTrapBehavior("Explosive Trap", true, on => Me.CurrentTarget, ret => KeyboardPolling.IsKeyDown(Keys.Z)),
        //        Spell.Cast("Scatter Shot", on => Me.FocusedUnit, ret => KeyboardPolling.IsKeyDown(Keys.C)),
        //        CreateHunterTrapBehavior("Freezing Trap", true, on => Me.FocusedUnit, ret => KeyboardPolling.IsKeyDown(Keys.F)),
        //        Spell.Cast("Concussive Shot", ret => KeyboardPolling.IsKeyDown(Keys.R)),

        //        new Throttle(1,
        //        Spell.Cast("Tranquilizing Shot", on => BestTranq)),

        //        Spell.Cast("Focus Fire", ctx => Me.HasAura("Frenzy", 5) && !Me.HasAura("The Beast Within")),
        //        Spell.Cast("Serpent Sting", ret => !Me.CurrentTarget.HasMyAura("Serpent Sting")),

        //        //Burst
        //        new Decorator(ret => SlimAI.Burst,
        //            new PrioritySelector(
        //                Spell.Cast("Rapid Fire"),
        //                Spell.Cast("Stampede"),
        //                Spell.Cast("Bestial Wrath", ret => Me.CurrentFocus > 60),
        //                Spell.Cast("A Murder of Crows"))),

        //        Spell.Cast("Fervor", ctx => Me.CurrentFocus <= 50),
        //        Spell.Cast("Dire Beast"),
        //        Spell.Cast("Lynx Rush", ret => Pet != null && Unit.NearbyUnfriendlyUnits.Any(u => Pet.Location.Distance(u.Location) <= 10)),
        //        Spell.Cast("Rabid", ret => Me.HasAura("The Beast Within")),

        //        Spell.Cast("Exhilaration", ret => Me.HealthPercent < 35 || (Pet != null && Pet.HealthPercent < 25)),
        //        Spell.Cast("Tranquilizing Shot", on => BestTranq),

        //        Spell.Cast("Mend Pet", onUnit => Pet, ret => Me.GotAlivePet && Pet.HealthPercent < 60 && !Pet.HasAura("Mend Pet")),
        //        Spell.Cast("Kill Command", ctx => Me.GotAlivePet && Pet.GotTarget && Pet.Location.Distance(Pet.CurrentTarget.Location) < 25f),
        //        Spell.Cast("Kill Shot", ctx => Me.CurrentTarget.HealthPercent <= 20),
        //        Spell.Cast("Glaive Toss"),
        //        Spell.Cast("Barrage"),
        //        Spell.Cast("Powershot"),
        //        Spell.Cast("Arcane Shot", ret => Me.CurrentFocus >= 61 || Me.HasAura("Thrill of the Hunt") || Me.HasAura("The Beast Within")),
        //        Spell.Cast("Cobra Shot", ret => !Me.HasAura("The Beast Within")),
        //         Spell.Cast("Focus Fire", ctx => Me.HasAura("Frenzy", 1) && Me.GetAuraTimeLeft("Frenzy").TotalSeconds <= 1),
        //        new ActionAlwaysSucceed()
        //        );
        //}
        //#endregion

        #region Traps & Stuff
        private static async Task<bool> CoFreeze()
        {
            if (!SpellManager.CanCast("Freezing Trap"))
                return false;

            if (!Lua.GetReturnVal<bool>("return IsLeftAltKeyDown() and not GetCurrentKeyBoardFocus()", 0))
                return false;

            if (!SpellManager.Cast("Freezing Trap"))
                return false;

            if (!await Coroutine.Wait(1000, () => StyxWoW.Me.CurrentPendingCursorSpell != null))
            {
                Logging.Write("Cursor Spell Didnt happen");
                return false;
            }

            Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");

            await CommonCoroutines.SleepForLagDuration();
            return true;
        }

        private static async Task<bool> CoFire()
        {
            if (!SpellManager.CanCast("Explosive Trap"))
                return false;

            if (!KeyboardPolling.IsKeyDown(Keys.C))
                return false;

            if (!SpellManager.Cast("Explosive Trap"))
                return false;

            if (!await Coroutine.Wait(1000, () => StyxWoW.Me.CurrentPendingCursorSpell != null))
            {
                Logging.Write("Cursor Spell Didnt happen");
                return false;
            }

            Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");

            await CommonCoroutines.SleepForLagDuration();
            return true;
        }

        private static async Task<bool> CoIce()
        {
            if (!SpellManager.CanCast("Ice Trap"))
                return false;

            if (!KeyboardPolling.IsKeyDown(Keys.X))
                return false;

            if (!SpellManager.Cast("Ice Trap"))
                return false;

            if (!await Coroutine.Wait(1000, () => StyxWoW.Me.CurrentPendingCursorSpell != null))
            {
                Logging.Write("Cursor Spell Didnt happen");
                return false;
            }

            Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");

            await CommonCoroutines.SleepForLagDuration();
            return true;
        }

        private static async Task<bool> CoBind()
        {
            if (!SpellManager.CanCast("Binding Shot"))
                return false;

            if (!KeyboardPolling.IsKeyDown(Keys.R))
                return false;

            if (!SpellManager.Cast("Binding Shot"))
                return false;

            if (!await Coroutine.Wait(1000, () => StyxWoW.Me.CurrentPendingCursorSpell != null))
            {
                Logging.Write("Cursor Spell Didnt happen");
                return false;
            }

            Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");

            await CommonCoroutines.SleepForLagDuration();
            return true;
        }
        #endregion

        private static bool Freedoms
        {
            get
            {
                return Me.CurrentTarget.HasAnyAura("Hand of Freedom", "Ice Block", "Hand of Protection", "Divine Shield", "Cyclone", "Deterrence", "Phantasm", "Windwalk Totem");
            }
        }


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
