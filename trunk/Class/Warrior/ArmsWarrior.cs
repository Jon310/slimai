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


namespace SlimAI.Class.Warrior
{
    [UsedImplicitly]
    class ArmsWarrior
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static bool HasTalent(WarriorTalents tal) { return TalentManager.IsSelected((int)tal); }
        private static WarriorSettings Settings { get { return GeneralSettings.Instance.Warrior(); } }
        #region Disarm
        public static string[] Disarm = new[] { //Pally
                                                 "Holy Avenger", "Avenging Wrath",
                                                 //Warrior need to make so it want disarm a warr if it has die by the sword buff
                                                 "Avatar", "Recklessness",
                                                 //Rogue
                                                 "Shadow Dance", "Shadow Blades",
                                                 //Kitty
                                                 "Berserk", "Incarnation", "Nature's Vigil",
                                                 //Hunter
                                                 "Rapid Fire","Bestial Wrath",
                                                 //DK
                                                 "Unholy Frenzy", "Pillar of Frost" };
        #endregion
        #region DontDisarm
        public static string[] DontDisarm = new[] { //Warrior 
                                                    "Die by the Sword", 
                                                    // Rogue
                                                    "Evasion", 
                                                    // Hunter
                                                    "Deterrence" };
        #endregion

        #region Coroutine Combat

        private static async Task<bool> CombatCoroutine()
        {
            await PvPCoroutine(SlimAI.PvPRotation);

            if (!Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive) return true;

            // Boss Mechanics Section
            //
            // End Boss Mechanics Section

            // Interupt Section
            await Coroutine.ExternalTask(Task.Run(() => Common.CreateInterruptBehavior()));
            // End Interupt Section

            await Spell.CoCast(VictoryRush, Me.HealthPercent <= 90 && Me.HasAura("Victorious"));
            await Spell.CoCast(DieByTheSword, Me.HealthPercent <= 20);

            await Item.CoUseHS(50);
            await CoLeap();
            await CoDemoBanner();

            await CoAOE(Unit.UnfriendlyUnits(8).Count() >= 4 && SlimAI.AOE);

            await Spell.CoCast(Recklessness, SlimAI.Burst && Me.CurrentTarget.HasMyAura("Colossus Smash"));
            await Spell.CoCast(Avatar, SlimAI.Burst && Me.CurrentTarget.HasMyAura("Colossus Smash"));
            await Spell.CoCast(SkullBanner, SlimAI.Burst && Me.CurrentTarget.HasMyAura("Colossus Smash"));

            await Spell.CoCast(BloodBath);
            await Item.CoUseHands();
            await Spell.CoCast(BerserkerRage, !Me.HasAura(Enrage));
            await Spell.CoCast(SweepingStrikes, Unit.UnfriendlyUnits(8).Count() >= 2 && SlimAI.AOE);
            await Spell.CoCast(HeroicStrike, (Me.CurrentTarget.HasAura("Colossus Smash") && Me.CurrentRage >= 80 && Me.CurrentTarget.HealthPercent >= 20) || Me.CurrentRage >= 105);
            await Spell.CoCast(MortalStrike);
            await Spell.CoCast(StormBolt, Me.CurrentTarget.HasMyAura("Colossus Smash"));
            await Spell.CoCast(DragonRoar, !Me.CurrentTarget.HasAura("Colossus Smash") && Me.HasAura("Bloodbath") && Me.CurrentTarget.Distance <= 8);
            await Spell.CoCast(ColossusSmash, Me.CurrentTarget.HasAuraExpired("Colossus Smash") || !Me.CurrentTarget.HasMyAura("Colossus Smash"));
            await Spell.CoCast(Execute, Me.CurrentTarget.HasMyAura("Colossus Smash") || Me.HasAura("Recklessness") || Me.CurrentRage >= 95);
            await Spell.CoCast(DragonRoar, (!Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.CurrentTarget.HealthPercent < 20) || (Me.HasAura("Bloodbath") && Me.CurrentTarget.HealthPercent >= 20) && Me.CurrentTarget.Distance <= 8);
            await Spell.CoCast(ThunderClap, Unit.UnfriendlyUnits(8).Count() >= 3 && Clusters.GetCluster(Me, Unit.UnfriendlyUnits(8), ClusterType.Radius, 8).Any(u => !u.HasAura("Deep Wounds")));
            await Spell.CoCast(Slam, (Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.HasAura("Recklessness")) && Me.CurrentTarget.HealthPercent >= 20);
            await Spell.CoCast(Overpower, Me.HasAura("Taste for Blood", 3) && Me.CurrentTarget.HealthPercent >= 20 || Me.HasAura("Sudden Execute"));
            await Spell.CoCast(Execute, !Me.HasAura("Sudden Execute"));
            await Spell.CoCast(Slam, Me.CurrentRage >= 40 && Me.CurrentTarget.HealthPercent >= 20);
            await Spell.CoCast(Overpower, Me.CurrentTarget.HealthPercent >= 20);
            await Spell.CoCast(BattleShout);
            await Spell.CoCast(HeroicThrow);
            await Spell.CoCast(ImpendingVictory, Me.HealthPercent < 50);

            return false;
        }

        [Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms)]
        public static Composite CoArmsCombat()
        {
            return new ActionRunCoroutine(ctx => CombatCoroutine());
        }

        #endregion


        //[Behavior(BehaviorType.Combat, WoWClass.Warrior, WoWSpec.WarriorArms)]
        //public static Composite ArmsCombat()
        //{
        //    return new PrioritySelector(
        //        new Throttle(1,
        //            new Action(context => ResetVariables())),
        //        new Decorator(ret => SlimAI.PvPRotation,
        //            CreatePvP()),
        //        new Decorator(ret => !Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive,
        //            new ActionAlwaysSucceed()),
        //        new Decorator(ret => Me.HasAura("Dire Fixation"),
        //            new PrioritySelector(
        //                BossMechs.HorridonHeroic())),
        //        new Throttle(1,1,
        //            Common.CreateInterruptBehavior()),
        //        Spell.Cast(VictoryRush, ret => Me.HealthPercent <= 90 && Me.HasAura("Victorious")),
        //        Spell.Cast(DieByTheSword, ret => Me.HealthPercent <= 20),
        //        Item.UsePotionAndHealthstone(50),
        //        DemoBanner(),
        //        Leap(),
        //        new Decorator(ret => Unit.UnfriendlyUnits(8).Count() >= 4 && SlimAI.AOE,
        //                    CreateAoe()),
        //        new Decorator(ret => SlimAI.Burst && Me.CurrentTarget.HasMyAura("Colossus Smash"),
        //            new PrioritySelector(
        //                Spell.Cast(Recklessness),
        //                Spell.Cast(Avatar),
        //                Spell.Cast(SkullBanner))),
        //        Spell.Cast(BloodBath),
        //        new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
        //        Spell.Cast(BerserkerRage, ret => !Me.HasAura(Enrage)),
        //        Spell.Cast(SweepingStrikes, ret => Unit.UnfriendlyUnits(8).Count() >= 2 && SlimAI.AOE),
        //        Spell.Cast(HeroicStrike, ret => (Me.CurrentTarget.HasAura("Colossus Smash") && Me.CurrentRage >= 80 && Me.CurrentTarget.HealthPercent >= 20) || Me.CurrentRage >= 105, true),
        //        Spell.Cast(MortalStrike),
        //        Spell.Cast(StormBolt, ret => Me.CurrentTarget.HasMyAura("Colossus Smash")),
        //        Spell.Cast(DragonRoar, ret => !Me.CurrentTarget.HasAura("Colossus Smash") && Me.HasAura("Bloodbath") && Me.CurrentTarget.Distance <= 8),
        //        Spell.Cast(ColossusSmash, ret => Me.CurrentTarget.HasAuraExpired("Colossus Smash") || !Me.CurrentTarget.HasMyAura("Colossus Smash")),
        //        Spell.Cast(Execute, ret => Me.CurrentTarget.HasMyAura("Colossus Smash") || Me.HasAura("Recklessness") || Me.CurrentRage >= 95),
        //        Spell.Cast(DragonRoar, ret => (!Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.CurrentTarget.HealthPercent < 20) || (Me.HasAura("Bloodbath") && Me.CurrentTarget.HealthPercent >= 20) && Me.CurrentTarget.Distance <= 8),
        //        Spell.Cast(ThunderClap, ret => Unit.UnfriendlyUnits(8).Count() >= 3 && Clusters.GetCluster(Me, Unit.UnfriendlyUnits(8), ClusterType.Radius, 8).Any(u => !u.HasAura("Deep Wounds"))),
        //        Spell.Cast(Slam, ret => (Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.HasAura("Recklessness")) && Me.CurrentTarget.HealthPercent >= 20),
        //        Spell.Cast(Overpower, ret => Me.HasAura("Taste for Blood", 3) && Me.CurrentTarget.HealthPercent >= 20 || Me.HasAura("Sudden Execute")),
        //        Spell.Cast(Execute, ret => !Me.HasAura("Sudden Execute")),
        //        Spell.Cast(Slam, ret => Me.CurrentRage >= 40 && Me.CurrentTarget.HealthPercent >= 20),
        //        Spell.Cast(Overpower, ret => Me.CurrentTarget.HealthPercent >= 20),
        //        Spell.Cast(BattleShout),
        //        Spell.Cast(HeroicThrow),
        //        Spell.Cast(ImpendingVictory, ret => Me.HealthPercent < 50));
        //}

        #region PvP

        private static async Task<bool> PvPCoroutine(bool reqs)
        {
            if (!reqs)
                return false;

            await CoShatterBubbles();
            await CoDemoBanner();
            await CoLeap();
            await CoMockingBanner();

            if (Me.CurrentTarget.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield") || !Me.Combat || Me.Mounted) return true;

            await Item.CoUseHS(40);
            await Spell.CoCast("Disarm", Me.CurrentTarget.HasAnyAura(Disarm) && !Me.CurrentTarget.HasAnyAura(DontDisarm));

            if (StyxWoW.Me.CurrentTarget != null && (!StyxWoW.Me.CurrentTarget.IsWithinMeleeRange || StyxWoW.Me.IsCasting || SpellManager.GlobalCooldown)) return true;

            await Spell.CoCast(VictoryRush, Me.HealthPercent <= 90 && Me.HasAura("Victorious"));
            await Spell.CoCast("Piercing Howl", SpellManager.HasSpell("Piercing Howl") && !Freedoms && !Me.CurrentTarget.IsStunned() && !Me.CurrentTarget.IsCrowdControlled() && !Me.CurrentTarget.IsSlowed() && Me.CurrentTarget.IsPlayer);
            await Spell.CoCast("Hamstring", !SpellManager.HasSpell("Piercing Howl") && !Freedoms && !Me.CurrentTarget.IsStunned() && !Me.CurrentTarget.IsCrowdControlled() && !Me.CurrentTarget.IsSlowed() && Me.CurrentTarget.IsPlayer);
            
            await Spell.CoCast("Intervene", BestBanner);

            await CoStormBoltFocus();

            await Spell.CoCast(SweepingStrikes, Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) >= 2 && SlimAI.AOE);

            await Spell.CoCast(Recklessness, SlimAI.Burst && Me.CurrentTarget.IsWithinMeleeRange);
            await Spell.CoCast(Avatar, Me.HasAura("Recklessness") && SlimAI.Burst && Me.CurrentTarget.IsWithinMeleeRange);
            await Spell.CoCast(SkullBanner, Me.HasAura("Recklessness") && SlimAI.Burst && Me.CurrentTarget.IsWithinMeleeRange);
            
            await Spell.CoCast(BloodBath, Me.CurrentTarget.IsWithinMeleeRange && SlimAI.AOE);
            await Spell.CoCast(Bladestorm, Me.CurrentTarget.IsWithinMeleeRange && SlimAI.AOE);
            await Item.CoUseHands();

            await Spell.CoCast("Intervene", BestInterveneTarget);
            await Spell.CoCast(Charge, ChargeInt);
            await Spell.CoCast(MortalStrike);
            await Spell.CoCast(HeroicStrike, (Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.CurrentRage >= 70) || Me.CurrentRage >= 75);
            await Spell.CoCast(ColossusSmash, !Me.CurrentTarget.HasAura("Colossus Smash"));
            await Spell.CoCast(Execute);
            await Spell.CoCast(ThunderClap, Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f) >= 2 && Clusters.GetCluster(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8).Any(u => !u.HasMyAura("Weakened Blows")));
            await Spell.CoCast(Slam, (Me.CurrentTarget.HasMyAura("Colossus Smash") || Me.HasAura("Recklessness") || Me.CurrentRage >= 40) && Me.CurrentTarget.HealthPercent >= 20);
            await Spell.CoCast(Overpower, Me.CurrentTarget.HealthPercent >= 20 || Me.HasAura("Sudden Execute"));
            await Spell.CoCast(BattleShout, !SlimAI.Weave);
            await Spell.CoCast(CommandingShout, SlimAI.Weave);
            await Spell.CoCast(HeroicThrow);
            await Spell.CoCast(ImpendingVictory, Me.CurrentTarget.HealthPercent > 20 || Me.HealthPercent < 50);

            return true;
        }

        //private static Composite CreatePvP()
        //{
        //    return new PrioritySelector(
        //            ShatterBubbles(),
        //            DemoBanner(),
        //            Leap(),
        //            MockBanner(),
        //            new Decorator(ret => Me.CurrentTarget.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield") || !Me.Combat || Me.Mounted,
        //                new ActionAlwaysSucceed()),

        //            //Spell.Cast("Rallying Cry", ret => HealerManager.GetCountWithHealth(25) >= 1),
        //        //new Throttle(1, 1,
        //        //    new Sequence(
        //        //        CreateInterruptSpellCast(on => BestInterrupt))),
        //        //CreateInterruptSpellCast(on => BestInterrupt),
        //            Item.UsePotionAndHealthstone(40),
        //            Spell.Cast("Disarm", ret => Me.CurrentTarget.HasAnyAura(Disarm) && !Me.CurrentTarget.HasAnyAura(DontDisarm)),
        //            //Spell.Cast("Die by the Sword", ret => Me.HealthPercent <= 20 /*&& Me.CurrentTarget.IsMelee()*/),
        //            //Spell.Cast("Shield Wall", ret => Me.HealthPercent <= 20 && !Me.HasAura("Die by the Sword")),

        //            new Decorator(ret => StyxWoW.Me.CurrentTarget != null && (!StyxWoW.Me.CurrentTarget.IsWithinMeleeRange || StyxWoW.Me.IsCasting || SpellManager.GlobalCooldown),
        //                new ActionAlwaysSucceed()),

        //            Spell.Cast(VictoryRush, ret => Me.HealthPercent <= 90 && Me.HasAura("Victorious")),
                    
        //            new Decorator(ret => !Freedoms && !Me.CurrentTarget.IsStunned() && !Me.CurrentTarget.IsCrowdControlled() && !Me.CurrentTarget.IsSlowed() && Me.CurrentTarget.IsPlayer /*!Me.CurrentTarget.HasAuraWithEffectsing(WoWApplyAuraType.ModDecreaseSpeed) && !Me.CurrentTarget.HasAnyAura("Piercing Howl", "Hamsting")*/,
        //                new PrioritySelector(
        //            Spell.Cast("Piercing Howl", ret => SpellManager.HasSpell("Piercing Howl")),
        //            Spell.Cast("Hamstring",  ret => !SpellManager.HasSpell("Piercing Howl")))),

        //            Spell.Cast("Intervene", on => BestBanner),

        //            StormBoltFocus(),

        //            Spell.Cast(SweepingStrikes, ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange) >= 2 && SlimAI.AOE),
        //            new Decorator(ret => SlimAI.Burst && Me.CurrentTarget.IsWithinMeleeRange,
        //                new PrioritySelector(
        //                    Spell.Cast(Recklessness),
        //                    Spell.Cast(Avatar, ret => Me.HasAura("Recklessness")),
        //                    Spell.Cast(SkullBanner, ret => Me.HasAura("Recklessness")))),
        //            new Decorator(ret => Me.CurrentTarget.IsWithinMeleeRange && SlimAI.AOE,
        //                new PrioritySelector(
        //                    Spell.Cast(BloodBath),
        //                    Spell.Cast(Bladestorm))),
        //            new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
        //            Spell.Cast("Intervene", on => BestInterveneTarget),
        //            Spell.Cast(Charge, on => ChargeInt),
        //            Spell.Cast(MortalStrike),
        //            Spell.Cast(HeroicStrike, ret => (Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.CurrentRage >= 70) || Me.CurrentRage >= 75),
        //            Spell.Cast(ColossusSmash, ret => !Me.CurrentTarget.HasAura("Colossus Smash")),
        //            Spell.Cast(Execute),
        //            Spell.Cast(ThunderClap, ret => Clusters.GetClusterCount(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8f) >= 2 && Clusters.GetCluster(Me, Unit.NearbyUnfriendlyUnits, ClusterType.Radius, 8).Any(u => !u.HasMyAura("Weakened Blows"))),
        //            Spell.Cast(Slam, ret => (Me.CurrentTarget.HasMyAura("Colossus Smash") || Me.HasAura("Recklessness") || Me.CurrentRage >= 40) && Me.CurrentTarget.HealthPercent >= 20),
        //            Spell.Cast(Overpower, ret => Me.CurrentTarget.HealthPercent >= 20 || Me.HasAura("Sudden Execute")),
        //            Spell.Cast(BattleShout, ret => !SlimAI.Weave ),
        //            Spell.Cast("Commanding Shout", ret => SlimAI.Weave),
        //            Spell.Cast(HeroicThrow),
        //            Spell.Cast(ImpendingVictory, ret => Me.CurrentTarget.HealthPercent > 20 || Me.HealthPercent < 50),
        //            new ActionAlwaysSucceed()
        //        );
        //}
        #endregion

        //[Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms)]
        //public static Composite ArmsPreCombatBuffs()
        //{
        //    return new PrioritySelector(
        //        new Decorator(ret => Me.Mounted,
        //            new ActionAlwaysSucceed()),
        //        Spell.Cast(BattleShout, ret => !Me.HasPartyBuff(PartyBuffType.AttackPower) && !SlimAI.Weave),
        //        Spell.Cast("Commanding Shout", ret => !Me.HasPartyBuff(PartyBuffType.Stamina) && SlimAI.Weave));
        //}


        #region Precombat Buff Coroutine
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Warrior, WoWSpec.WarriorArms)]
        public static Composite CoArmsPreCombatBuffs()
        {
            return new ActionRunCoroutine(ctx => PreCombatBuffCoroutine());
        }

        private static async Task<bool> PreCombatBuffCoroutine()
        {
            if (Me.Mounted)
                return false;

            await Spell.CoCast(BattleShout, !Me.HasPartyBuff(PartyBuffType.AttackPower) && !SlimAI.Weave);
            await Spell.CoCast(CommandingShout, !Me.HasPartyBuff(PartyBuffType.Stamina) && SlimAI.Weave);

            return false;
        }
        #endregion

        //[Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorArms)]
        //public static Composite ArmsPull()
        //{
        //    return new PrioritySelector(
        //        Spell.Cast(Charge));
        //}

        #region Coroutine Pull Section

        private static async Task<bool> PullCoroutine()
        {
            if (SlimAI.AFK) return false;
            await Spell.CoCastOnGround(HeroicLeap, SpellManager.Spells["Charge"].Cooldown);
            await Spell.CoCast(Charge);

            return false;
        }

        [Behavior(BehaviorType.Pull, WoWClass.Warrior, WoWSpec.WarriorArms)]
        private static Composite CoArmsPull()
        {
            return new ActionRunCoroutine(ctx => PullCoroutine());
        }

        #endregion
        
        //private static Composite CreateAoe()
        //{
        //    return new PrioritySelector(
        //        new Decorator(ret => SlimAI.Burst && Me.CurrentTarget.HasAura("Colossus Smash"),
        //            new PrioritySelector(
        //                Spell.Cast(Recklessness),
        //                Spell.Cast(Avatar),
        //                Spell.Cast(SkullBanner))),
        //        Spell.Cast(BloodBath),
        //        new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
        //        Spell.Cast(BerserkerRage, ret => !Me.HasAura(Enrage)),
        //        Spell.Cast(SweepingStrikes),
        //        Spell.Cast(Bladestorm, ret => Me.HasAura(SweepingStrikes)),
        //        Spell.Cast(Whirlwind, ret => (Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.CurrentRage >= 80 && Me.CurrentTarget.HealthPercent >= 20) || Me.CurrentRage >= 105),
        //        Spell.Cast(MortalStrike),
        //        Spell.Cast(StormBolt, ret => Me.CurrentTarget.HasMyAura("Colossus Smash")),
        //        Spell.Cast(DragonRoar, ret => !Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.HasAura("Bloodbath") && Me.CurrentTarget.Distance <= 8),
        //        Spell.Cast(ColossusSmash, ret => Me.CurrentTarget.HasAuraExpired("Colossus Smash")),
        //        Spell.Cast(Execute, ret => Me.CurrentTarget.HasAura("Colossus Smash") || Me.HasAura("Recklessness") || Me.CurrentRage >= 95),
        //        Spell.Cast(DragonRoar, ret => (!Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.CurrentTarget.HealthPercent < 20) || (Me.HasAura("Bloodbath") && Me.CurrentTarget.HealthPercent >= 20) && Me.CurrentTarget.Distance <= 8),
        //        Spell.Cast(ThunderClap, ret => Unit.UnfriendlyUnits(8).Count() >= 3 && Clusters.GetCluster(Me, Unit.UnfriendlyUnits(8), ClusterType.Radius, 8).Any(u => !u.HasAura("Deep Wounds"))),
        //        Spell.Cast(Slam, ret => (Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.HasAura("Recklessness")) && Me.CurrentTarget.HealthPercent >= 20),
        //        Spell.Cast(Overpower, ret => Me.HasAura("Taste for Blood", 3) && Me.CurrentTarget.HealthPercent >= 20),
        //        Spell.Cast(Execute, ret => !Me.HasAura("Sudden Execute")),
        //        Spell.Cast(Overpower, ret => Me.CurrentTarget.HealthPercent >= 20 || Me.HasAura("Sudden Execute")),
        //        Spell.Cast(Whirlwind, ret => Me.CurrentRage >= 40 && Me.CurrentTarget.HealthPercent >= 20),
        //        Spell.Cast(BattleShout),
        //        Spell.Cast(HeroicThrow),
        //        Spell.Cast(ImpendingVictory, ret => Me.HealthPercent < 50));
        //}

        #region Coroutine AOE
        private static async Task<bool> CoAOE(bool reqs)
        {
            if (!reqs)
                return false;

            if (await Spell.CoCast(Recklessness, SlimAI.Burst && Me.CurrentTarget.HasAura("Colossus Smash"))) return true;
            if (await Spell.CoCast(Avatar, SlimAI.Burst && Me.CurrentTarget.HasAura("Colossus Smash"))) return true;
            if (await Spell.CoCast(SkullBanner, SlimAI.Burst && Me.CurrentTarget.HasAura("Colossus Smash"))) return true;

            if (await Spell.CoCast(BloodBath)) return true;
            if (await Item.CoUseHands()) return true;

            if (await Spell.CoCast(BerserkerRage, !Me.HasAura(Enrage))) return true;
            if (await Spell.CoCast(SweepingStrikes)) return true;
            if (await Spell.CoCast(Bladestorm, Me.HasAura(SweepingStrikes))) return true;
            if (await Spell.CoCast(Whirlwind, (Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.CurrentRage >= 80 && Me.CurrentTarget.HealthPercent >= 20) || Me.CurrentRage >= 105)) return true;
            if (await Spell.CoCast(MortalStrike)) return true;
            if (await Spell.CoCast(StormBolt, Me.CurrentTarget.HasMyAura("Colossus Smash"))) return true;
            if (await Spell.CoCast(DragonRoar, !Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.HasAura("Bloodbath") && Me.CurrentTarget.Distance <= 8)) return true;
            if (await Spell.CoCast(ColossusSmash, Me.CurrentTarget.HasAuraExpired("Colossus Smash"))) return true;
            if (await Spell.CoCast(Execute, Me.CurrentTarget.HasAura("Colossus Smash") || Me.HasAura("Recklessness") || Me.CurrentRage >= 95)) return true;
            if (await Spell.CoCast(DragonRoar, (!Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.CurrentTarget.HealthPercent < 20) || (Me.HasAura("Bloodbath") && Me.CurrentTarget.HealthPercent >= 20) && Me.CurrentTarget.Distance <= 8)) return true;
            if (await Spell.CoCast(ThunderClap, Unit.UnfriendlyUnits(8).Count() >= 3 && Clusters.GetCluster(Me, Unit.UnfriendlyUnits(8), ClusterType.Radius, 8).Any(u => !u.HasAura("Deep Wounds")))) return true;
            if (await Spell.CoCast(Slam, (Me.CurrentTarget.HasMyAura("Colossus Smash") && Me.HasAura("Recklessness")) && Me.CurrentTarget.HealthPercent >= 20)) return true;
            if (await Spell.CoCast(Overpower, Me.HasAura("Taste for Blood", 3) && Me.CurrentTarget.HealthPercent >= 20)) return true;
            if (await Spell.CoCast(Execute, !Me.HasAura("Sudden Execute"))) return true;
            if (await Spell.CoCast(Overpower, Me.CurrentTarget.HealthPercent >= 20 || Me.HasAura("Sudden Execute"))) return true;
            if (await Spell.CoCast(Whirlwind, Me.CurrentRage >= 40 && Me.CurrentTarget.HealthPercent >= 20)) return true;
            if (await Spell.CoCast(BattleShout)) return true;
            if (await Spell.CoCast(HeroicThrow)) return true;
            if (await Spell.CoCast(ImpendingVictory, Me.HealthPercent < 50)) return true;

            return false;
        }
        #endregion

        #region Pvp Stuff
        private static bool Freedoms
        {
            get
            {
                return Me.CurrentTarget.HasAnyAura("Hand of Freedom", "Ice Block", "Hand of Protection", "Divine Shield", "Cyclone", "Deterrence", "Phantasm", "Windwalk Totem");
            }
        }
        private static Composite StormBoltFocus()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Storm Bolt") &&
                    KeyboardPolling.IsKeyDown(Keys.C),
                    new PrioritySelector(
                        Spell.Cast("Storm Bolt", on => Me.FocusedUnit))

                    );
        }

        #region Coroutine Stormbolt Focus

        private static async Task<bool> CoStormBoltFocus()
        {
            if (!SpellManager.CanCast("Storm Bolt") && !KeyboardPolling.IsKeyDown(Keys.C))
                return false;
            if (await Spell.CoCast(StormBolt, Me.FocusedUnit))
                return true;
            return true;
        }
        #endregion

        #region Best Banner
        public static WoWUnit BestBanner//WoWUnit
        {
            get
            {
                if (!StyxWoW.Me.GroupInfo.IsInParty)
                    return null;
                if (StyxWoW.Me.GroupInfo.IsInParty)
                {
                    var closePlayer = FriendlyUnitsNearTarget(6f).OrderBy(t => t.DistanceSqr).FirstOrDefault(t => t.IsAlive);
                    if (closePlayer != null)
                        return closePlayer;
                    var bestBan = (from unit in ObjectManager.GetObjectsOfType<WoWUnit>(false)
                                   //where (unit.Equals(59390) || unit.Equals(59398))
                                   //where unit.Guid.Equals(59390) || unit.Guid.Equals(59398)
                                   where unit.Entry.Equals(59390) || unit.Entry.Equals(59398)
                                   //where (unit.Guid == 59390 || unit.Guid == 59398) 
                                   where unit.InLineOfSight
                                   select unit).FirstOrDefault();
                    return bestBan;
                }
                return null;
            }
        }
        #endregion

        #region BestInterrupt
        public static WoWUnit BestInterrupt
        {
            get
            {
                var bestInt = (from unit in ObjectManager.GetObjectsOfType<WoWPlayer>(false)
                               where unit.IsAlive
                               where unit.IsPlayer
                               where !unit.IsInMyPartyOrRaid
                               where unit.InLineOfSight
                               where unit.Distance <= 10
                               where unit.IsCasting
                               where unit.CanInterruptCurrentSpellCast
                               where unit.CurrentCastTimeLeft.TotalMilliseconds <
                                     MyLatency + 1000 &&
                                     InterruptCastNoChannel(unit) > MyLatency ||
                                     unit.IsChanneling &&
                                     InterruptCastChannel(unit) > MyLatency
                               select unit).FirstOrDefault();
                return bestInt;
            }
        }



        public static bool Interuptdelay(WoWUnit inttar)
        {
            var totaltime = inttar.CastingSpell.CastTime / 1000;
            var timeleft = inttar.CurrentCastTimeLeft.TotalSeconds;
            //Logging.Write((totaltime / 1000).ToString());
            //Logging.Write(timeleft.ToString());

            return (timeleft / totaltime) < MathEx.Random(.10, .50);

        }

        private static int InteruptMiss = 0;

        private static void addone()
        {
            var add = InteruptMiss + 1;
            InteruptMiss = add;
        }

        private static void resetIntMiss()
        {
            InteruptMiss = 0;
        }

        #endregion

        #region Best Intervene
        public static WoWUnit BestInterveneTarget
        {
            get
            {
                if (!StyxWoW.Me.GroupInfo.IsInParty)
                    return null;
                if (StyxWoW.Me.GroupInfo.IsInParty)
                {
                    var bestTank = Group.Tanks.OrderBy(t => t.DistanceSqr).FirstOrDefault(t => t.IsAlive);
                    if (bestTank != null)
                        return bestTank;
                    var bestInt = (from unit in ObjectManager.GetObjectsOfType<WoWPlayer>(false)
                                   where unit.IsAlive
                                   where unit.HealthPercent <= 30
                                   where unit.IsInMyPartyOrRaid
                                   where unit.IsPlayer
                                   where !unit.IsHostile
                                   where unit.InLineOfSight
                                   select unit).FirstOrDefault();
                    return bestInt;
                }
                return null;
            }
        }
        #endregion

        #region ChargeInterupt
        public static WoWUnit ChargeInt
        {
            get
            {
                if (!StyxWoW.Me.GroupInfo.IsInParty)
                    return null;
                if (StyxWoW.Me.GroupInfo.IsInParty)
                {
                    var bestInt = (from unit in ObjectManager.GetObjectsOfType<WoWPlayer>(false)
                                   where unit.IsAlive
                                   where unit.IsCasting
                                   where unit.CanInterruptCurrentSpellCast
                                   where unit.IsPlayer
                                   where unit.IsHostile
                                   where unit.InLineOfSight
                                   where unit.Distance <= 25
                                   where unit.Distance >= 8
                                   where unit.CurrentCastTimeLeft.TotalMilliseconds <
                                     MyLatency + 1000 &&
                                     InterruptCastNoChannel(unit) > MyLatency ||
                                     unit.IsChanneling &&
                                     InterruptCastChannel(unit) > MyLatency
                                   select unit).FirstOrDefault();
                    return bestInt;
                }
                return null;
            }
        }

        #endregion

        #region CreateChargeBehavior
        static Composite CreateChargeBehavior()
        {
            return new Decorator(
                    ret => StyxWoW.Me.CurrentTarget != null && !IsGlobalCooldown()/*&& PreventDoubleCharge*/,

                    new PrioritySelector(
                        Spell.Cast("Charge",
                            ret => StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance < (TalentManager.HasGlyph("Long Charge") ? 30f : 25f))

                        //Spell.CastOnGround("Heroic Leap",
                //    ret => StyxWoW.Me.CurrentTarget.Location,
                //    ret => StyxWoW.Me.CurrentTarget.Distance > 13 && StyxWoW.Me.CurrentTarget.Distance < 40 && SpellManager.Spells["Charge"].Cooldown)
                ));
        }
        #endregion

        #region CreateInterruptSpellCast
        public static Composite CreateInterruptSpellCast(UnitSelectionDelegate onUnit)
        {
            return new Decorator(
                // If the target is casting, and can actually be interrupted, AND we've waited out the double-interrupt timer, then find something to interrupt with.
                ret => onUnit != null && onUnit(ret) != null/*Interuptdelay(onUnit(ret))&& PreventDoubleInterrupt*/,
                new PrioritySelector(
                //Spell.Cast("Pummel", onUnit),
                // AOE interrupt
                    Spell.Cast("Disrupting Shout", onUnit, ret => onUnit(ret).Distance < 10)
                //Spell.Cast("Mass Spell Reflection", onUnit, ret => onUnit(ret).IsCasting),
                //Spell.Cast("Shockwave", onUnit, ret => onUnit(ret).Distance < 10 && Me.IsFacing(onUnit(ret))),
                //Spell.Cast("Indimidating Shout", onUnit, ret => onUnit(ret).Distance < 8),
                   ));
        }
        #endregion

        #region Demo Banner
        private static Composite DemoBannerAuto()
        {
            return new Decorator(ret => SpellManager.Spells["Charge"].Cooldown &&
                                        SpellManager.Spells["Heroic Leap"].Cooldown &&
                                       !SpellManager.Spells["Demoralizing Banner"].Cooldown &&
                                       !SpellManager.Spells["Intervene"].Cooldown &&
                                       !FriendlyUnitsNearTarget(6f).Any() &&
                                        StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25,
                            new Action(ret =>
                            {
                                SpellManager.Cast("Demoralizing Banner");
                                SpellManager.ClickRemoteLocation(StyxWoW.Me.CurrentTarget.Location);
                            }));
        }
        #endregion

        #region FriendlyUnitsNearTarget
        public static IEnumerable<WoWUnit> FriendlyUnitsNearTarget(float distance)
        {
            var dist = distance * distance;
            var curTarLocation = StyxWoW.Me.CurrentTarget.Location;
            return ObjectManager.GetObjectsOfType<WoWUnit>(false, false).Where(
                        p => ValidUnit(p) && p.IsFriendly && p.Location.DistanceSqr(curTarLocation) <= dist).ToList();
        }
        #endregion

        #region IsGlobalCooldown
        public static bool IsGlobalCooldown(bool faceDuring = false, bool allowLagTollerance = true)
        {
            uint latency = allowLagTollerance ? StyxWoW.WoWClient.Latency : 0;
            TimeSpan gcdTimeLeft = SpellManager.GlobalCooldownLeft;
            return gcdTimeLeft.TotalMilliseconds > latency;
        }
        #endregion

        #region Mocking Banner
        private static Composite MockingBannerAuto()
        {
            return new Decorator(ret => SpellManager.Spells["Demoralizing Banner"].Cooldown &&
                                        SpellManager.Spells["Demoralizing Banner"].CooldownTimeLeft.TotalSeconds <= 165 &&
                                        SpellManager.Spells["Charge"].Cooldown &&
                                        SpellManager.Spells["Heroic Leap"].Cooldown &&
                                       !SpellManager.Spells["Mocking Banner"].Cooldown &&
                                       !SpellManager.Spells["Intervene"].Cooldown &&
                                       !FriendlyUnitsNearTarget(6f).Any() &&
                                        StyxWoW.Me.CurrentTarget.Distance >= 10 && StyxWoW.Me.CurrentTarget.Distance <= 25,
                            new Action(ret =>
                            {
                                SpellManager.Cast("Mocking Banner");
                                SpellManager.ClickRemoteLocation(StyxWoW.Me.CurrentTarget.Location);
                            }));
        }
        #endregion

        #region ShatterBubbles
        static Composite ShatterBubbles()
        {
            return new Decorator(
                    ret => Me.CurrentTarget.IsPlayer &&
                          Me.CurrentTarget.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield") && Me.CurrentTarget.InLineOfSight,
                //Me.CurrentTarget.ActiveAuras.ContainsKey("Ice Block") ||
                //Me.CurrentTarget.ActiveAuras.ContainsKey("Hand of Protection") ||
                //Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield")),
                    new PrioritySelector(
                        Spell.Cast("Shattering Throw")));
        }
        #endregion

        #region Coroutine Shatter Bubbles
        private static Task<bool> CoShatterBubbles()
        {
            return Spell.CoCast(ShatteringThrow,
                        Me.CurrentTarget.IsPlayer &&
                        Me.CurrentTarget.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield") &&
                        Me.CurrentTarget.InLineOfSight);
        } 
        #endregion

        #region InterruptCastNoChannel

        private static double InterruptCastNoChannel(WoWUnit target)
        {
            if (target == null || !target.IsPlayer)
            {
                return 0;
            }
            double timeLeft = 0;

            if (target.IsCasting && (//target.CastingSpell.Name == "Arcane Blast" ||
                ////target.CastingSpell.Name == "Banish" ||
                //target.CastingSpell.Name == "Binding Heal" ||
                                     target.CastingSpell.Name == "Cyclone" ||
                //target.CastingSpell.Name == "Chain Heal" ||
                //target.CastingSpell.Name == "Chain Lightning" ||
                //target.CastingSpell.Name == "Chi Burst" ||
                                     target.CastingSpell.Name == "Chaos Bolt" ||
                //target.CastingSpell.Name == "Demonic Circle: Summon" ||
                //target.CastingSpell.Name == "Denounce" ||
                //target.CastingSpell.Name == "Divine Light" ||
                //target.CastingSpell.Name == "Divine Plea" ||
                                     target.CastingSpell.Name == "Dominated Mind" ||
                                     target.CastingSpell.Name == "Elemental Blast" ||
                                     target.CastingSpell.Name == "Entangling Roots" ||
                //target.CastingSpell.Name == "Enveloping Mist" ||
                                     target.CastingSpell.Name == "Fear" ||
                //target.CastingSpell.Name == "Fireball" ||
                //target.CastingSpell.Name == "Flash Heal" ||
                //target.CastingSpell.Name == "Flash of Light" ||
                //target.CastingSpell.Name == "Frost Bomb" ||
                //target.CastingSpell.Name == "Frostjaw" ||
                //target.CastingSpell.Name == "Frostbolt" ||
                //target.CastingSpell.Name == "Frostfire Bolt" ||
                //target.CastingSpell.Name == "Greater Heal" ||
                //target.CastingSpell.Name == "Greater Healing Wave" ||
                //target.CastingSpell.Name == "Haunt" ||
                //target.CastingSpell.Name == "Heal" ||
                //target.CastingSpell.Name == "Healing Surge" ||
                //target.CastingSpell.Name == "Healing Touch" ||
                //target.CastingSpell.Name == "Healing Wave" ||
                                     target.CastingSpell.Name == "Hex" ||
                //target.CastingSpell.Name == "Holy Fire" ||
                //target.CastingSpell.Name == "Holy Light" ||
                //target.CastingSpell.Name == "Holy Radiance" ||
                //target.CastingSpell.Name == "Hibernate" ||
                                     target.CastingSpell.Name == "Mass Dispel" ||
                //target.CastingSpell.Name == "Mind Spike" ||
                //target.CastingSpell.Name == "Immolate" ||
                //target.CastingSpell.Name == "Incinerate" ||
                                     target.CastingSpell.Name == "Lava Burst" ||
                //target.CastingSpell.Name == "Mind Blast" ||
                //target.CastingSpell.Name == "Mind Spike" ||
                //target.CastingSpell.Name == "Nourish" ||
                                     target.CastingSpell.Name == "Polymorph" ||
                //target.CastingSpell.Name == "Prayer of Healing" ||
                //target.CastingSpell.Name == "Pyroblast" ||
                //target.CastingSpell.Name == "Rebirth" ||
                //target.CastingSpell.Name == "Regrowth" ||
                                     target.CastingSpell.Name == "Repentance" ||
                //target.CastingSpell.Name == "Scorch" ||
                //target.CastingSpell.Name == "Shadow Bolt" ||
                //target.CastingSpell.Name == "Shackle Undead"
                //target.CastingSpell.Name == "Smite" ||
                //target.CastingSpell.Name == "Soul Fire" ||
                //target.CastingSpell.Name == "Starfire" ||
                //target.CastingSpell.Name == "Starsurge" ||
                //target.CastingSpell.Name == "Surging Mist" ||
                //target.CastingSpell.Name == "Transcendence" ||
                //target.CastingSpell.Name == "Transcendence: Transfer" ||
                                    target.CastingSpell.Name == "Unstable Affliction" 
                //target.CastingSpell.Name == "Vampiric Touch" ||
                //target.CastingSpell.Name == "Wrath")
                ))
            {
                timeLeft = target.CurrentCastTimeLeft.TotalMilliseconds;
            }
            return timeLeft;
        }

        #endregion

        #region InterruptCastChannel

        private static double InterruptCastChannel(WoWUnit target)
        {
            if (target == null || !target.IsPlayer)
            {
                return 0;
            }
            double timeLeft = 0;

            if (target.IsChanneling && (target.ChanneledSpell.Name == "Hymn of Hope" ||
                //target.ChanneledSpell.Name == "Arcane Barrage" ||
                                        target.ChanneledSpell.Name == "Evocation" ||
                //target.ChanneledSpell.Name == "Mana Tea" ||
                //target.ChanneledSpell.Name == "Crackling Jade Lightning" ||
                //target.ChanneledSpell.Name == "Malefic Grasp" ||
                //target.ChanneledSpell.Name == "Hellfire" ||
                                        target.ChanneledSpell.Name == "Harvest Life" ||
                                        target.ChanneledSpell.Name == "Health Funnel" ||
                                        target.ChanneledSpell.Name == "Drain Soul" ||
                //target.ChanneledSpell.Name == "Arcane Missiles" ||
                //target.ChanneledSpell.Name == "Mind Flay" ||
                //target.ChanneledSpell.Name == "Penance" ||
                //target.ChanneledSpell.Name == "Soothing Mist" ||
                                        target.ChanneledSpell.Name == "Tranquility" ||
                                        target.ChanneledSpell.Name == "Drain Life"))
            {
                timeLeft = target.CurrentChannelTimeLeft.TotalMilliseconds;
            }

            return timeLeft;
        }

        #endregion

        #region UpdateMyLatency

        public static readonly double MyLatency = 65;

        public static void UpdateMyLatency()
        {
            //if (THSettings.Instance.LagTolerance)
            //{
            //    //If SLagTolerance enabled, start casting next spell MyLatency Millisecond before GlobalCooldown ready.

            //    MyLatency = (StyxWoW.WoWClient.Latency);
            //    //MyLatency = 0;
            //    //Use here because Lag Tolerance cap at 400
            //    //Logging.Write("----------------------------------");
            //    //Logging.Write("MyLatency: " + MyLatency);
            //    //Logging.Write("----------------------------------");

            //    if (MyLatency > 400)
            //    {
            //        //Lag Tolerance cap at 400
            //        MyLatency = 400;
            //    }
            //}
            //else
            //{
            //    //MyLatency = 400;
            //    MyLatency = 0;
            //}
        }

        #endregion

        #region ValidUnit
        public static bool ValidUnit(WoWUnit p)
        {
            // Ignore shit we can't select/attack
            if (!p.CanSelect || !p.Attackable)
                return false;

            // Duh
            if (p.IsDead)
                return false;

            // check for players
            if (p.IsPlayer)
                return true;

            // Dummies/bosses are valid by default. Period.
            if (p.IsTrainingDummy() || p.IsBoss())
                return true;

            // If its a pet, lets ignore it please.
            if (p.IsPet || p.OwnedByRoot != null)
                return false;

            // And ignore critters/non-combat pets
            if (p.IsNonCombatPet || p.IsCritter)
                return false;

            if (p.CreatedByUnitGuid != 0 || p.SummonedByUnitGuid != 0)
                return false;

            return true;
        }
        #endregion
        #endregion

        private static void ResetVariables()
        {
            KeyboardPolling.IsKeyDown(Keys.G);
            KeyboardPolling.IsKeyDown(Keys.Z);
            KeyboardPolling.IsKeyDown(Keys.C);
        }

        private static Composite Leap()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Heroic Leap") &&
                    Lua.GetReturnVal<bool>("return IsLeftAltKeyDown() and not GetCurrentKeyBoardFocus()", 0),
                    new Action(ret =>
                    {
                        SpellManager.Cast("Heroic Leap");
                        Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");
                    }));
        }

        #region Coroutine Leap
        private static async Task<bool> CoLeap()
        {
            if (!SpellManager.CanCast(HeroicLeap))
                return false;

            if (!Lua.GetReturnVal<bool>("return IsLeftAltKeyDown() and not GetCurrentKeyBoardFocus()", 0))
                return false;

            if (!SpellManager.Cast(HeroicLeap))
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

        private static Composite DemoBanner()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Demoralizing Banner") &&
                    KeyboardPolling.IsKeyDown(Keys.Z),
                    new Action(ret =>
                    {
                        SpellManager.Cast("Demoralizing Banner");
                        Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");
                    }));
        }

        #region Coroutine Demo Banner
        private static async Task<bool> CoDemoBanner()
        {
            if (!SpellManager.CanCast(DemoralizingBanner))
                return false;

            if (!KeyboardPolling.IsKeyDown(Keys.Z))
                return false;

            if (!SpellManager.Cast(DemoralizingBanner))
                return false;

            if (!await Coroutine.Wait(1000, () => StyxWoW.Me.CurrentPendingCursorSpell != null))
            {
                Logging.Write("Cursor Spell Didnt happen");
                return false;
            }

            Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");

            await CommonCoroutines.SleepForLagDuration();
            return true;
            return true;
        }
        #endregion

        private static Composite MockBanner()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Mocking Banner") &&
                    KeyboardPolling.IsKeyDown(Keys.G),
                    new Action(ret =>
                    {
                        SpellManager.Cast("Mocking Banner");
                        Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");
                        return;
                    }));
        }

        #region Coroutine Mocking Banner
        private static async Task<bool> CoMockingBanner()
        {
            if (!SpellManager.CanCast(MockingBanner))
                return false;

            if (!KeyboardPolling.IsKeyDown(Keys.G))
                return false;

            if (!SpellManager.Cast(MockingBanner))
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

        #region WarriorTalents
        enum WarriorTalents
        {
            None = 0,
            Juggernaut,
            DoubleTime,
            Warbringer,
            EnragedRegeneration,
            SecondWind,
            ImpendingVictory,
            StaggeringShout,
            PiercingHowl,
            DisruptingShout,
            Bladestorm,
            Shockwave,
            DragonRoar,
            MassSpellReflection,
            Safeguard,
            Vigilance,
            Avatar,
            Bloodbath,
            StormBolt
        }
        #endregion

        #region Warrior Spells
        private const int Avatar = 107574,
                          BattleShout = 6673,
                          Bladestorm = 46924,
                          BloodBath = 12292,
                          BerserkerRage = 18499,
                          Charge = 100,
                          Cleave = 845,
                          ColossusSmash = 86346,
                          CommandingShout = 469,
                          DemoralizingBanner = 114203,
                          DieByTheSword = 118038,
                          DragonRoar = 118000,
                          Enrage = 12880,
                          Execute = 5308,
                          HeroicLeap = 6544,
                          HeroicStrike = 78,
                          HeroicThrow = 57755,
                          ImpendingVictory = 103840,
                          MockingBanner = 114192,
                          MortalStrike = 12294,
                          Overpower = 7384,
                          Recklessness = 1719,
                          ShatteringThrow = 64382,
                          SkullBanner = 114207,
                          Slam = 1464,
                          StormBolt = 107570,
                          SweepingStrikes = 12328,
                          ThunderClap = 6343,
                          VictoryRush = 34428,
                          Whirlwind = 1680;
        #endregion
    }
}
