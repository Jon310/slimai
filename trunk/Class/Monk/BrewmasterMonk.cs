using System;
using System.Collections.Generic;
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

namespace SlimAI.Class.Monk
{
    static class BrewmasterMonk
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static MonkSettings Settings { get { return GeneralSettings.Instance.Monk(); } }
        private static double? _timeToMax;
        private static double? _energyRegen;
        private static double? _energy;

        #region Coroutine Combat

        private static async Task<bool> CombatCoroutine()
        {
            new Throttle(1,
                         new Action(context => ResetVariables()));
                /*Things to fix
                 * using glyph of expel harm to heal ppl dont want to have to page heal manger if i dont have to to keep it faster i guess
                */
            if (!Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive) return true;

            //ZenMed();
                //Spell.WaitForCastOrChannel(),
                //CreateInterruptSpellCast(),
                //Common.CreateInterruptBehavior(),
                //Spell.Cast(SpearHandStrike, on => Unit.NearbyUnitsInCombatWithMe.FirstOrDefault(u => u.IsCasting && u.CanInterruptCurrentSpellCast && u.IsWithinMeleeRange && Me.IsSafelyFacing(u))),
                //Item.UsePotionAndHealthstone(40),
                //new Action(ret => { Item.UseWaist(); return RunStatus.Failure; }),

                // Execute if we can
            await Spell.CoCast(TouchofDeath, Me.CurrentChi >= 3 && Me.HasAura("Death Note"));

            //await Spell.CoCastOnGround(SummonBlackOxStatue, Me.Location, !Me.HasAura("Sanctuary of the Ox") /*&& Me.IsInGroup()*/ && SlimAI.Weave);
            
                //PB, EB, and Guard are off the GCD
                //!!!!!!!Purifying Brew !!!!!!!
            await Spell.CoCast(PurifyingBrew, Me.HasAura("Heavy Stagger") ||
                                              (((HasShuffle() || Me.CurrentChi >= 2) && !Me.HasAura(ElusiveBrew)) && Me.HasAura("Moderate Stagger") && Me.HealthPercent <= 70));


                //Elusive Brew will made auto at lower stacks when I can keep up 80 to 90% up time this is just to keep from capping
            await Spell.CoCast(ElusiveBrew, Me.HasAura("Elusive Brew", 12) && !Me.HasAura(ElusiveBrew) && IsCurrentTank());
            await Spell.CoCast(BlackoutKick, !Me.HasAura("Shuffle") || Me.HasAura("Shuffle") && Me.GetAuraTimeLeft("Shuffle").TotalSeconds <= 6);
            await Spell.CoCast(Guard, Me.CurrentChi >= 2 && Me.HealthPercent <= 80 && IsCurrentTank());
                
                //Spell.Cast(ExpelHarm, ret => Me.HealthPercent <= 80),
                //Spell.Cast(ExpelHarm, on => EHtar, ret => Me.HealthPercent > 70 && TalentManager.HasGlyph("Targeted Expulsion")),
            await Spell.CoCast(ExpelHarm, Me.HealthPercent <= 70 && Me.CurrentEnergy > 40/*&& TalentManager.HasGlyph("Targeted Expulsion") || Me.HealthPercent < 80 && !TalentManager.HasGlyph("Targeted Expulsion")*/);
                //Detox
            //CreateDispelBehavior();

            await Spell.CoCast(TigerPalm, !Me.HasAura("Tiger Power"));
                //Spell.Cast(BreathofFire, ret => Me.CurrentChi >= 3 && HasShuffle() && Me.CurrentTarget.HasAura("Dizzying Haze") && SlimAI.AOE),
            await Spell.CoCast(BlackoutKick, Me.CurrentChi >= 3 && Spell.GetSpellCooldown("Keg Smash").TotalSeconds <= 1 || Me.CurrentChi >= 4 || Me.CurrentChi >= 3 && Spell.GetSpellCooldown("Guard").TotalSeconds >= 3);
            await Spell.CoCast(KegSmash);

                //Chi Talents
            await Spell.CoCast(ChiWave);
            await Spell.CoCast(ZenSphere, !Me.HasAura(ZenSphere));
                
            await Spell.CoCast(RushingJadeWind, Unit.UnfriendlyUnits(8).Count() >= 3 && Spell.GetSpellCooldown("Keg Smash").TotalSeconds >= 3 && SlimAI.AOE);
                        //Spell.Cast(RushingJadeWind, ret => Unit.UnfriendlyUnits(8).Count() >= 3 && ((Spell.GetSpellCooldown("Keg Smash").TotalSeconds >= (((40 - 0) * (1.0 / EnergyRegen)) / 1.6)) || Me.CurrentEnergy >= 80)),
            await Spell.CoCast(SpinningCraneKick, Unit.UnfriendlyUnits(8).Count() >= 5 && Spell.GetSpellCooldown("Keg Smash").TotalSeconds >= 3 && SlimAI.AOE);

                //Spell.Cast(Jab, ret => ((Me.CurrentEnergy - 40) + (Spell.GetSpellCooldown("Keg Smash").TotalSeconds * EnergyRegen)) > 40),
                //Spell.Cast("Jab", ret => (Spell.GetSpellCooldown("Keg Smash").TotalSeconds >= (((40 - 0) * (1.0 / EnergyRegen)) / 1.6)) || Me.CurrentEnergy >= 80),
            await Spell.CoCast("Jab", Me.CurrentEnergy >= 80 || Spell.GetSpellCooldown("Keg Smash").TotalSeconds >= 3);

            await Spell.CoCast(TigerPalm/*, Spell.GetSpellCooldown("Keg Smash").TotalSeconds >= 1 && Me.CurrentChi < 3 && Me.CurrentEnergy < 80*/);
            return false;
        }

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        public static Composite CoBrewmasterCombat()
        {
            return new ActionRunCoroutine(ctx => CombatCoroutine());
        }

#endregion

        #region Buffs
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        public static Composite BrewmasterPreCombatBuffs()
        {
            return new PrioritySelector(
                new Decorator(ret => Me.Mounted,
                    new ActionAlwaysSucceed())
                //PartyBuff.BuffGroup("Legacy of the Emperor")
                );
        }
        #endregion

        #region Zen Heals

        private static WoWUnit Tanking
        {
            get
            {
                var tank = Group.Tanks.FirstOrDefault(u => StyxWoW.Me.CurrentTarget.ThreatInfo.TargetGuid == u.Guid && u.Distance < 40);
                return tank;
            }
        }
        #endregion

        #region Energy Crap

        private static double EnergyRegen
        {
            get
            {
                if (!_energyRegen.HasValue)
                {
                    _energyRegen = Lua.GetReturnVal<float>("return GetPowerRegen()", 1);
                    return _energyRegen.Value;
                }
                return _energyRegen.Value;
            }
        }

        private static double Energy
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
        private static RunStatus ResetVariables()
        {
            _timeToMax = null;
            _energy = null;
            _energyRegen = null;
            KeyboardPolling.IsKeyDown(Keys.Z);
            KeyboardPolling.IsKeyDown(Keys.C);
            return RunStatus.Failure;
        }

        private static double TimeToMax
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

        #region OxStatue
        private static Composite OxStatue()
        {
            return new Decorator(ret => !Me.HasAura("Sanctuary of the Ox") && Me.IsInGroup() && SlimAI.Weave,
                new Action(ret =>
                {
                    var tpos = Me.CurrentTarget.Location;
                    var mpos = Me.Location;

                    SpellManager.Cast("Summon Black Ox Statue");
                    SpellManager.ClickRemoteLocation(mpos);
                }));
        }
        #endregion

        #region Healing Sphere
        private static Composite HealingSphere()
        {
           return new Decorator(ret => Me.HealthPercent <= 50 && Me.CurrentEnergy >= 45 && (!Me.CurrentTarget.IsWithinMeleeRange) && Spell.GetSpellCooldown("Expel Harm").TotalSeconds >= 1/*Me.GetAuraTimeLeft("Shuffle").TotalSeconds >= 6 && Spell.GetSpellCooldown("Keg Smash").TotalSeconds >= 3*/,
                new Action(ret =>
                {
                    var mpos = Me.Location;
                    
                    SpellManager.Cast("Healing Sphere");
                    SpellManager.ClickRemoteLocation(mpos);
                }));
        }
        #endregion

        #region Healing Sphere Other tank
        private static Composite HealingSphereTank()
        {
            return new Decorator(ret => !IsCurrentTank() && Tanking.HealthPercent <= 50 && SlimAI.AFK,
                new Action(ret =>
                {
                    var otpos = Tanking.Location;

                    SpellManager.Cast("Healing Sphere");
                    SpellManager.ClickRemoteLocation(otpos);
                }));
        }
        #endregion

        #region Zen Med
        private static Composite ZenMed()
        {
            return
                new Decorator(ret => SpellManager.CanCast(ZenMeditation) && !Me.IsMoving &&
                    KeyboardPolling.IsKeyDown(Keys.Z),
                    new Action(ret =>
                    {
                        SpellManager.Cast(ZenMeditation);
                        return;
                    }));
        }
        #endregion

        #region Is Tank
        static bool IsCurrentTank()
        {
            return StyxWoW.Me.CurrentTarget.CurrentTargetGuid == StyxWoW.Me.Guid;
        }
        #endregion

        #region Shuffle
        static bool HasShuffle()
        {
            return Me.HasAura("Shuffle") && Me.GetAuraTimeLeft("Shuffle").TotalSeconds > 6;
        }
        #endregion
        
        #region Dispelling

        private static WoWUnit Dispeltar
        {
            get
            {
                var dispelothers = (from unit in ObjectManager.GetObjectsOfType<WoWPlayer>()
                                    where unit.IsAlive
                                    where Dispelling.CanDispel(unit)
                                    select unit).OrderByDescending(u => u.HealthPercent).LastOrDefault();
                return dispelothers;
            }
        }
        #endregion

        #region Expel Harm

        private static WoWUnit EHtar
        {
            get
            {
                var eHheal = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWPlayer>()
                                    where unit.IsAlive
                                    where unit.Distance < 40
                                    where unit.HealthPercent < 80
                                    select unit).OrderByDescending(u => u.HealthPercent).LastOrDefault();
                return eHheal;
            }
        }

        private static Composite CreateDispelBehavior()
        {
            return new PrioritySelector(
                Spell.Cast("Detox", on => Me, ret => Dispelling.CanDispel(Me)),
                Spell.Cast("Detox", on => Dispeltar, ret => Dispelling.CanDispel(Dispeltar)));
        }
        #endregion

        #region y'shaarj
        public static WoWUnit TouchedTar
        {
            get
            {
                var Touched = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWPlayer>()
                                    where unit.IsAlive
                                    where unit.HasAnyAura("Touch of Y'Shaarj", "Empowered Touch of Y'Shaarj")
                                    where unit.IsCasting
                                    select unit).FirstOrDefault();
                return Touched;
            }
        }

        public static Composite CreateInterruptSpellCast()
        {
            return new Decorator(
                // If the target is casting, and can actually be interrupted, AND we've waited out the double-interrupt timer, then find something to interrupt with.
                new PrioritySelector(
                Spell.Cast("Spear Hand Strike", on => TouchedTar),
                // AOE interrupt
                //need to add ring here
                // Racials last.
                // Don't waste stomp on bosses. They can't be stunned 99% of the time!
                Spell.Cast("Paralysis", on => TouchedTar, ret => !TouchedTar.HasAura("Empowered Touch of Y'Shaarj") && (SpellManager.Spells["Spear Hand Strike"].Cooldown || TouchedTar.Distance >= 8))
                    ));
        }
        #endregion

        #region MonkTalents
        enum MonkTalents
        {
            None = 0,
            Celerity,
            TigersLust,
            Momentum,
            ChiWave,
            ZenSphere,
            ChiBurst,
            PowerStrikes,
            Ascention,
            ChiBrew,
            RingofPeace,
            ChargingOxWave,
            LegSweep,
            HealingElixirs,
            DampenHarm,
            DiffuseMagic,
            RushingJadeWind,
            InvokeXuentheWhiteTiger,
            ChiTorpedo
        }
        #endregion

        #region Monk Spells
        private const int BlackoutKick = 100784,
                          BreathofFire = 115181,
                          ChiWave = 115098,
                          ElusiveBrew = 115308,
                          ExpelHarm = 115072,
                          Guard = 115295,
                          Jab = 100780,
                          KegSmash = 121253,
                          PurifyingBrew = 119582,
                          RushingJadeWind = 116847,
                          SpearHandStrike = 116705,
                          SpinningCraneKick = 101546,
                          StanceoftheSturdyOx = 115069,
                          SummonBlackOxStatue = 115315,
                          TigerPalm = 100787,
                          TouchofDeath = 115080,
                          ZenMeditation = 115176,
                          ZenSphere = 124081;
        #endregion
    }
}
