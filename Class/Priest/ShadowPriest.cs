using System;
using System.Linq;
using System.Windows.Forms;
using SlimAI.Helpers;
using SlimAI.Managers;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Class.Priest
{
    class ShadowPriest
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private const int MindFlay = 15407;
        private const int Insanity = 129197;
        private static uint Orbs { get { return Me.GetCurrentPower(WoWPowerType.ShadowOrbs); } }

         [Behavior(BehaviorType.Combat, WoWClass.Priest, WoWSpec.PriestShadow)]
        public static Composite ShadowCombat()
        {
            return new PrioritySelector(

                //new Decorator(ret => Me.IsCasting && (Me.ChanneledSpell == null || Me.ChanneledSpell.Id != MindFlay), new Action(ret => { return RunStatus.Success; })),
                new Decorator(ret => !Me.Combat || Me.Mounted || !Me.CurrentTarget.IsAlive || !Me.GotTarget /*|| Me.ChanneledSpell.Name == "Hymn of Hope"*/,
                    new ActionAlwaysSucceed()),
                
                new Decorator(ret => Me.ChanneledSpell != null,
                    new PrioritySelector(
                        //new Decorator(ret => Me.ChanneledSpell.Name == "Hymn of Hope",
                            //Spell.WaitForCastOrChannel()))),
                
                Hymn(),
                MassDispel(),

                new Decorator(ret => SlimAI.Burst,
                    new PrioritySelector(
                        Spell.Cast("Lifeblood"),
                        new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                        new Action(ret => { Item.UseTrinkets(); return RunStatus.Failure; }),
                            new Decorator(ret => Me.CurrentTarget.IsBoss,
                                new PrioritySelector(
                                    Spell.Cast(Shadowfiend, ret => Spell.GetSpellCooldown("Shadowfiend").TotalMilliseconds < 10),
                                    Spell.Cast(Mindbender, ret => Spell.GetSpellCooldown("Mindbender").TotalMilliseconds < 10),
                                    Spell.Cast(PowerInfusion, ret => SpellManager.HasSpell("Power Infusion") && Spell.GetSpellCooldown("Power Infusion").TotalMilliseconds < 10))))),

                Spell.Cast(VoidShift, on => VoidTank),
                Spell.Cast(PrayerofMending, on => Me, ret => Me.HealthPercent <=85),
                new Throttle(1,
                    Spell.Cast(VampiricEmbrace, ret => HealerManager.GetCountWithHealth(55) > 4)),

                new Decorator(
                    ret => Me.GotTarget && Me.ChanneledSpell != null,
                    new PrioritySelector(
                        new Decorator(
                            ret => Me.ChanneledSpell.Name == "Mind Flay"
                                && CMF && !SpellManager.HasSpell("Solace and Insanity"),
                            new Sequence(
                                //new Action(ret => Logging.WriteDiagnostic("/cancel Mind Flay on {0} @ {1:F1}%", Me.CurrentTarget.SafeName(), Me.CurrentTarget.HealthPercent)),
                                new Action(ret => SpellManager.StopCasting()),
                                new WaitContinue(TimeSpan.FromMilliseconds(500), ret => Me.ChanneledSpell == null, new ActionAlwaysSucceed()))))),

                new Decorator(ret => Unit.UnfriendlyUnitsNearTarget(10f).Count() > 2 && SlimAI.AOE,
                    CreateAOE()),
                //Spell.Cast("Shadow Word: Pain", ret => Orbs == 3 && Me.CurrentTarget.CachedHasAura("Shadow Word: Pain") && Me.CurrentTarget.CachedGetAuraTimeLeft("Shadow Word: Pain") <= 6),
                //Spell.Cast("Vampiric Touch", ret => Orbs == 3 && Me.CurrentTarget.CachedHasAura("Vampiric Touch") && Me.CurrentTarget.CachedGetAuraTimeLeft("Shadow Word: Pain") <= 6),
                //Spell.Cast("Devouring Plague", ret => Orbs == 3 && Me.CurrentTarget.CachedGetAuraTimeLeft("Shadow Word: Pain") >= 6 && Me.CurrentTarget.CachedGetAuraTimeLeft("Vampiric Touch") >= 6),
                Spell.Cast(DevouringPlague, ret => Orbs == 3),

                Spell.Cast(MindBlast, ret => Orbs < 3),
                new Throttle(2,
                    new PrioritySelector(
                        Spell.Cast(ShadowWordDeath, ret => Orbs < 3))),
                //new Throttle(2,
                //    new PrioritySelector(
                //        Spell.Cast("Mind Flay", ret => Me.CurrentTarget.CachedHasAura("Devouring Plague")))),
                Spell.Cast(MindFlay, on => Me.CurrentTarget, ret => SpellManager.HasSpell("Solace and Insanity") && Me.CurrentTarget.HasAura("Devouring Plague")),
                new Throttle(2,
                    Spell.Cast(ShadowWordPain, ret => !Me.CurrentTarget.HasAura("Shadow Word: Pain") || Me.CurrentTarget.HasAuraExpired("Shadow Word: Pain", 3))),
                new Throttle(2,
                    Spell.Cast(VampiricTouch, ret => !Me.CurrentTarget.HasAura("Vampiric Touch") || Me.CurrentTarget.HasAuraExpired("Vampiric Touch", 4))),
                Spell.Cast(MindSpike, ret => Me.HasAura(87160)),
                Spell.Cast(Halo, ret => Me.CurrentTarget.Distance <= 30),
                Spell.Cast(Cascade),
                Spell.Cast(DivineStar, ret => Me.CurrentTarget.Distance <= 24),
                //new Throttle(1,
                //    new PrioritySelector(
                Spell.Cast(MindFlay, on => Me.CurrentTarget, ret => !CMF),
                Spell.Cast(ShadowWordDeath, ret => Me.IsMoving),
                Spell.Cast(ShadowWordPain, ret => Me.IsMoving))));
        }

         [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Priest, WoWSpec.PriestShadow)]
         public static Composite ShadowPreCombatBuffs()
        {
            return new PrioritySelector(
                //new Decorator(ret => AdvancedAI.PvPRot,
                //    ShadowPriestPvP.CreateSPPvPBuffs),
                //PartyBuff.BuffGroup("Power Word: Fortitude"),
                Spell.Cast(Shadowform, ret => !Me.HasAura("Shadowform")),
                Spell.Cast(InnerFire, ret => !Me.HasAura("Inner Fire")));
        }

        private static Composite CreateAOE()
        {
            return new PrioritySelector(
                Spell.Cast(MindBlast, ret => Orbs < 3),
                Spell.Cast(DevouringPlague, ret => Orbs == 3),
                Spell.Cast(MindSpike, ret => Me.HasAura(87160)),
                Spell.Cast(MindSear, on => SearTarget),
                Spell.Cast(Cascade),
                Spell.Cast(DivineStar),
                Spell.Cast(Halo, ret => Me.CurrentTarget.Distance < 30),
                Spell.Cast(ShadowWordPain, on => PainMobs),
                Spell.Cast(VampiricTouch, on => TouchMobs),
                Spell.Cast(MindFlay, ret => Me.CurrentTarget.HasAura("Devouring Plague") && SpellManager.HasSpell("Solace and Insanity")),
                Spell.Cast(MindFlay, on => Me.CurrentTarget, ret => !CMF));
        }

        #region Cancel Mind Flay

        private static bool CMF
        {
            get { return Me.CurrentTarget.HasAuraExpired("Shadow Word: Pain", 3) || Me.CurrentTarget.HasAuraExpired("Vampiric Touch", 4) || 
                Orbs == 3 || !SpellManager.Spells["Mind Blast"].Cooldown || Me.HasAura(87160); }
        }

        #endregion

        #region Uility
        private static Composite Hymn()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Hymn of Hope") &&
                    KeyboardPolling.IsKeyDown(Keys.Z),
                    new Action(ret =>
                    {
                        SpellManager.Cast("Hymn of Hope");
                        return;
                    }));
        }

        private static Composite MassDispel()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Mass Dispel") &&
                    KeyboardPolling.IsKeyDown(Keys.C),
                    new Action(ret =>
                    {
                        SpellManager.Cast("Mass Dispel");
                        Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");
                        return;
                    }));
        }

        public static WoWUnit VoidTank
        {
            get
            {
                var voidOn = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWPlayer>()
                              where unit.IsAlive
                              where Group.Tanks.Any()
                              where unit.HealthPercent <= 30 && Me.HealthPercent > 70
                              where unit.IsPlayer
                              where !unit.IsHostile
                              where unit.InLineOfSight
                              select unit).FirstOrDefault();
                return voidOn;
            }
        }
        #endregion

        #region Muliti Dot & AoE
        public static WoWUnit PainMobs
        {
            get
            {
                var painOn = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
                              where unit.IsAlive
                              where unit.IsHostile
                              where unit.InLineOfSight
                              where !unit.HasAura("Shadow Word: Pain")
                              where unit.Distance < 40
                              where unit.IsTargetingUs() || unit.IsTargetingMyRaidMember
                              select unit).FirstOrDefault();
                return painOn;
            }
        }

        public static WoWUnit TouchMobs
        {
            get
            {
                var painOn = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
                              where unit.IsAlive
                              where unit.IsHostile
                              where unit.InLineOfSight
                              where !unit.HasAura("Vampiric Touch")
                              where unit.Distance < 40
                              where unit.IsTargetingUs() || unit.IsTargetingMyRaidMember
                              select unit).FirstOrDefault();
                return painOn;
            }
        }
        
        public static WoWUnit SearTarget
        {
            get
            {
                var bestTank = Group.Tanks.FirstOrDefault(t => t.IsAlive && Clusters.GetClusterCount(t, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 10f) >= 5);
                if (bestTank != null)
                    return bestTank;
                var searMob = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
                               where unit.IsAlive
                               where !unit.IsHostile
                               where unit.InLineOfSight
                               where Clusters.GetClusterCount(Me.CurrentTarget, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 10f) >= 5
                               select unit).FirstOrDefault();
                return searMob;
            }
        }
        #endregion

        #region PriestTalents
        public enum PriestTalents
        {
            VoidTendrils = 1,
            Psyfiend,
            DominateMind,
            BodyAndSoul,
            AngelicFeather,
            Phantasm,
            FromDarknessComesLight,
            Mindbender,
            SolaceAndInsanity,
            DesperatePrayer,
            SpectralGuise,
            AngelicBulwark,
            TwistOfFate,
            PowerInfusion,
            DivineInsight,
            Cascade,
            DivineStar,
            Halo
        }
        #endregion

        #region Priest Spells

        private const int
            AngelicFeather = 121536,
            Archangel = 81700,
            BindingHeal = 32546,
            Cascade = 121135,
            ChakraChastise = 81209,
            ChakraSanctuary = 81206,
            ChakraSerenity = 81208,
            CircleofHealing = 34861,
            DesperatePrayer = 19236,
            DevouringPlague = 2944,
            DispelMagic = 528,
            DivineHymn = 94843,
            DivineStar = 110744,
            DominateMind = 605,
            Fade = 586,
            FearWard = 6346,
            FlashHeal = 2061,
            GreaterHeal = 2060,
            GuardianSpirit = 47788,
            Halo = 120517,
            Heal = 2050,
            HolyFire = 14914,
            HolyWordChastise = 88625,
            HymnofHope = 64901,
            InnerFire = 588,
            InnerFocus = 89485,
            InnerWill = 73413,
            LeapofFaith = 73325,
            Levitate = 1706,
            Lightwell = 126135,
            //MassDispel = 32375,
            Mindbender = 123040,
            MindBlast = 8092,
            MindSear = 48045,
            MindSpike = 73510,
            MindVision = 2096,
            PainSuppression = 33206,
            Penance = 47540,
            PowerInfusion = 10060,
            PowerWordBarrier = 62618,
            PowerWordFortitude = 21562,
            PowerWordShield = 17,
            PowerWordSolace = 129250,
            PrayerofHealing = 596,
            PrayerofMending = 33076,
            PsychicScream = 8122,
            Psyfiend = 108921,
            Purify = 527,
            Renew = 139,
            Resurrection = 2006,
            ShackleUndead = 9484,
            Shadowfiend = 34433,
            Shadowform = 15473,
            ShadowWordDeath = 32379,
            ShadowWordPain = 589,
            Smite = 585,
            SpectralGuise = 112833,
            SpiritShell = 109964,
            VampiricEmbrace = 15286,
            VampiricTouch = 34914,
            VoidShift = 108968,
            VoidTendrils = 108920;

        #endregion

    }
}
