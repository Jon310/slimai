using System.Linq;
using SlimAI.Helpers;
using SlimAI.Managers;
using CommonBehaviors.Actions;
using SlimAI.Settings;
using Styx;
using Styx.CommonBot;
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

        [Behavior(BehaviorType.Combat, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        public static Composite BrewmasterCombat()
        {
            return new PrioritySelector(
                new Throttle(1,
                    new Action(context => ResetVariables())),
                /*Things to fix
                 * using glyph of expel harm to heal ppl dont want to have to page heal manger if i dont have to to keep it faster i guess
                */
                new Decorator(ret => !Me.Combat,
                    new ActionAlwaysSucceed()),
                Spell.Cast(SpearHandStrike, ret => StyxWoW.Me.CurrentTarget.IsCasting && StyxWoW.Me.CurrentTarget.CanInterruptCurrentSpellCast),
                Spell.WaitForCastOrChannel(),
                Item.UsePotionAndHealthstone(40),
                //new Action(ret => { Item.UseWaist(); return RunStatus.Failure; }),
                new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),

                // Execute if we can
                Spell.Cast(TouchofDeath, ret => Me.CurrentChi >= 3 && Me.HasAura("Death Note")),
                //stance stuff need to work on it more
                Spell.Cast(StanceoftheSturdyOx, ret => IsCurrentTank() && !Me.HasAura("Stance of the Sturdy Ox")),

                new Decorator(ret => Me.HasAura("Stance of the Fierce Tiger"),
                    new PrioritySelector(
                        HealingSphereTank(),
                        Spell.Cast(TigerPalm, ret => !Me.HasAura("Tiger Power")),
                        Spell.Cast(ChiWave),
                        Spell.Cast(BlackoutKick),
                        Spell.Cast(RushingJadeWind, ret => Unit.UnfriendlyUnits(8).Count() >= 3),
                        Spell.Cast(SpinningCraneKick, ret => Unit.UnfriendlyUnits(8).Count() >= 3),
                        Spell.Cast(ExpelHarm, ret => Me.HealthPercent <= 35),
                        Spell.Cast(Jab, ret => Me.CurrentChi <= 4),
                        Spell.Cast(TigerPalm),
                        new ActionAlwaysSucceed())),

                //// apply the Weakened Blows debuff. Keg Smash also generates allot of threat 
                Spell.Cast(KegSmash, ret => Me.CurrentChi <= 3 && Clusters.GetCluster(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8).Any(u => !u.HasAura("Weakened Blows"))),

                OxStatue(),
                //Spell.CastOnGround("Summon Black Ox Statue", on => Me.Location, ret => !Me.HasAura("Sanctuary of the Ox") && AdvancedAI.UsefulStuff),

                //PB, EB, and Guard are off the GCD
                //!!!!!!!Purifying Brew !!!!!!!
                Spell.Cast(PurifyingBrew, ret => Me.HasAura("Purifier") && (Me.GetAuraTimeLeft("Purifier").TotalSeconds <= 1) || Me.HasAura("Moderate Stagger") || Me.HasAura("Heavy Stagger")),
                new Decorator(ret => Me.CurrentChi > 0,
                    new PrioritySelector(
                        Spell.Cast(PurifyingBrew, ret => Me.HasAura("Heavy Stagger")),
                        new Decorator(ret => (Me.GetAuraTimeLeft("Shuffle").TotalSeconds >= 6 || Me.CurrentChi > 2),
                            new PrioritySelector(
                                Spell.Cast(PurifyingBrew, ret => Me.HasAura("Moderate Stagger") && Me.HealthPercent <= 70),
                                Spell.Cast(PurifyingBrew, ret => Me.HasAura("Light Stagger") && Me.HealthPercent < 40))))),

                Item.UsePotionAndHealthstone(40),

                //Elusive Brew will made auto at lower stacks when I can keep up 80 to 90% up time this is just to keep from capping
                Spell.Cast(ElusiveBrew, ret => Me.HasAura("Elusive Brew", 12) && !Me.HasAura(ElusiveBrew)),

                //Guard
                Spell.Cast(Guard, ret => Me.CurrentChi >= 2 && Me.HasAura("Power Guard")),
                //Blackout Kick might have to add guard back but i think its better to open with BK and get shuffle to build AP for Guard
                Spell.Cast(BlackoutKick, ret => Me.CurrentChi >= 2 && !Me.HasAura("Shuffle") || Me.HasAura("Shuffle") && Me.GetAuraTimeLeft("Shuffle").TotalSeconds < 6),
                Spell.Cast(TigerPalm, ret => Me.CurrentChi >= 2 && !Me.HasAura("Power Guard") || !Me.HasAura("Tiger Power")),
                Spell.Cast(ExpelHarm, ret => Me.HealthPercent <= 35),
                Spell.Cast(BreathofFire, ret => Me.CurrentChi >= 3 && Me.HasAura("Shuffle") && Me.GetAuraTimeLeft("Shuffle").TotalSeconds > 6.5 && Me.CurrentTarget.HasAura("Dizzying Haze") && SlimAI.AOE),

                //Detox
                CreateDispelBehavior(),
                Spell.Cast(BlackoutKick, ret => Me.CurrentChi >= 3),
                Spell.Cast(KegSmash),

                //Chi Talents
                //need to do math here and make it use 2 if im going to use it
                Spell.Cast(ChiWave),
                //Spell.Cast("Chi Wave", on => Me, ret => Me.HealthPercent <= 85),
                Spell.Cast(ZenSphere, on => Tanking),
                
                Spell.Cast(ExpelHarm, on => EHtar, ret => Me.HealthPercent > 70 && TalentManager.HasGlyph("Targeted Expulsion")),
                Spell.Cast(ExpelHarm, ret => Me.HealthPercent <= 70 && TalentManager.HasGlyph("Targeted Expulsion") || Me.HealthPercent < 85 && !TalentManager.HasGlyph("Targeted Expulsion")),

                //Healing Spheres need to work on not happy with this atm
                //HealingSphere(),
                //HealingSphereTank(),
                //Spell.CastOnGround("Healing Sphere", on => Me.Location, ret => Me.HealthPercent <= 50 && Me.CurrentEnergy >= 60),

                new Decorator(ret => SlimAI.AOE && Spell.GetSpellCooldown("Keg Smash").TotalSeconds >= 2,
                    new PrioritySelector(
                        Spell.Cast(RushingJadeWind, ret => Unit.UnfriendlyUnits(8).Count() >= 3),
                        Spell.Cast(SpinningCraneKick, ret => Unit.UnfriendlyUnits(8).Count() >= 5))),

                Spell.Cast(Jab, ret => ((Me.CurrentEnergy - 40) + (Spell.GetSpellCooldown("Keg Smash").TotalSeconds * EnergyRegen)) > 40),

                //Spell.Cast("Jab", ret => Spell.GetSpellCooldown("Keg Smash").TotalSeconds >= (((40 - 0) * (1.0 / EnergyRegen)) / 1.6)),
                //Spell.Cast("Jab", ret => Me.CurrentEnergy >= 80 || Spell.GetSpellCooldown("Keg Smash").TotalSeconds >= 3),

                //dont like using this in auto to many probs with it
                //Spell.Cast("Invoke Xuen, the White Tiger", ret => Me.CurrentTarget.IsBoss && IsCurrentTank()),
                new Throttle(
                Spell.Cast(TigerPalm, ret => Spell.GetSpellCooldown("Keg Smash").TotalSeconds >= 1 && Me.CurrentChi < 3 && Me.CurrentEnergy < 80)));
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Monk, WoWSpec.MonkBrewmaster)]
        public static Composite BrewmasterPreCombatBuffs()
        {
            return new PrioritySelector(
                PartyBuff.BuffGroup("Legacy of the Emperor"));
        }

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
            return new Decorator(ret => Me.HealthPercent <= 50 && Me.CurrentEnergy >= 60,
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

        #region Is Tank
        static bool IsCurrentTank()
        {
            return StyxWoW.Me.CurrentTarget.CurrentTargetGuid == StyxWoW.Me.Guid;
        }
        #endregion

        #region Dispelling

        private static WoWUnit Dispeltar
        {
            get
            {
                var dispelothers = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWPlayer>()
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
                          ZenSphere = 124081;
        #endregion
    }
}
