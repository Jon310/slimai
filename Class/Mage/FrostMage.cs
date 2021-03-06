﻿using CommonBehaviors.Actions;
using SlimAI;
using SlimAI.Helpers;
using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System.Linq;

namespace SlimAI.Class.Mage
{
    class FrostMage
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }

        [Behavior(BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageFrost)]
        public static Composite FrostCombat()
            {
                return new PrioritySelector(


                    Spell.Cast("Ice Barrier", ret => !Me.HasAura("Ice Barrier")),

                    //Moving
                    new Decorator(ret => Me.IsMoving && !Me.HasAura("Ice Floes"),
                                  new PrioritySelector(
                                      Spell.Cast("Ice Floes"),
                                      Spell.Cast("Frostfire Bolt", ret => Me.HasAura("Brain Freeze")),
                                      CreateBomb(),
                                      Spell.Cast("Fire Blast", ret => Me.IsMoving),
                                      Spell.Cast("Ice Lance", ret => Me.IsMoving)
                                      )),


                    // Interrupt please.
                    Spell.Cast("Counterspell", ret => Me.CurrentTarget.IsCasting && Me.CurrentTarget.CanInterruptCurrentSpellCast),
                    //Spell.WaitForCastOrChannel(),
                    //cancel_buff,name=alter_time,moving=1
                    Spell.Cast("Alter Time", ret => Me.HasAura("Alter Time") && Me.IsMoving),

                    //Spell.CastOnGround("Rune of Power", ret => Me.Location, ret => !Me.HasAura("Rune of Power")),
                    new Throttle(3,
                        new PrioritySelector(
                    Spell.Cast("Evocation", ret => !Me.HasAura("Invoker's Energy") || Me.HasAuraExpired("Invoker's Energy", 2)))),
                    Spell.Cast("Evocation", ret => Spell.GetSpellCooldown("Icy Veins").TotalSeconds <= 2 && Me.HasAuraExpired("Invoker's Energy", 20)),
                    //Spell.CastOnGround("Rune of Power", ret => Me.Location, ret => GetSpellCooldown("Icy Veins").TotalSeconds ==0 && Me.HasAuraExpired("Invoker's Energy", 20)),

                    Spell.Cast("Mirror Image", ret => SlimAI.Burst),
                    Spell.Cast("Lifeblood", ret => Me.HasAura("Icy Veins")),
                    Spell.Cast("Frozen Orb", ret => !Me.HasAura("Fingers of Frost") && SlimAI.Burst),
                    Spell.Cast("Icy Veins", ret => SlimAI.Burst && (Me.HasAura("Brain Freeze") || Me.HasAura("Fingers of Frost")) && !Me.IsMoving),
                    Spell.Cast("Presence of Mind", ret => SlimAI.Burst && (Me.HasAura("Icy Veins") || Spell.GetSpellCooldown("Icy Veins").TotalSeconds > 15)),
                    Spell.Cast("Alter Time", ret => SlimAI.Burst && Me.HasAura("Icy Veins") && !Me.HasAura("Alter Time")),
                    CreateAoe(),
                    Spell.Cast("Frostfire Bolt", ret => Me.HasAura("Alter Time") && Me.HasAura("Brain Freeze")),
                    Spell.Cast("Ice Lance", ret => Me.HasAura("Alter Time") && Me.HasAura("Fingers of Frost")),
                    CreateBomb(),
                    Spell.Cast("Frostfire Bolt", ret => Me.HasAura("Brain Freeze")),
                    Spell.Cast("Ice Lance", ret => Me.HasAura("Fingers of Frost")),
                    Spell.Cast("Frostbolt")

                    );
            }
        

        private static Composite CreateBomb()
        {
            return new PrioritySelector(

                Spell.Cast("Nether Tempest", ret => !Me.CurrentTarget.HasMyAura("Nether Tempest")),
                Spell.Cast("Frost Bomb", ret => !Me.IsMoving),    
                new Decorator(ret => bombCount() < 3,
                    Spell.Cast("Living Bomb", on => bombTarget)));
        }
        private static Composite CreateAoe()
        {
            return new PrioritySelector(ret => Unit.UnfriendlyUnits(10).Count() > 1 && SlimAI.AOE,

                                        Spell.CastOnGround("Flamestrike", loc => Me.CurrentTarget.Location, ret => Unit.UnfriendlyUnitsNearTarget(10).Count() >= 2),
                                        Spell.Cast("Frozen Orb"),
                                        Spell.Cast("Arcane Explosion",ret => Unit.UnfriendlyUnits(10).Count() >= 4));
        }

  

                    //9	0.00	counterspell,if=target.debuff.casting.react
                    //A	0.00	cancel_buff,name=alter_time,moving=1
                    //B	0.00	conjure_mana_gem,if=mana_gem_charges<3&target.debuff.invulnerable.react
                    //C	0.50	time_warp,if=target.health.pct<25|time>5
                    //D	5.86	rune_of_power,if=buff.rune_of_power.remains<cast_time&buff.alter_time.down
                    //E	0.94	rune_of_power,if=cooldown.icy_veins.remains=0&buff.rune_of_power.remains<20
                    //F	2.04	mirror_image
                    //G	7.91	frozen_orb,if=!buff.fingers_of_frost.react
                    //H	2.94	icy_veins,if=(debuff.frostbolt.stack>=3&(buff.brain_freeze.react|buff.fingers_of_frost.react))|target.time_to_die<22,moving=0
                    //I	2.94	berserking,if=buff.icy_veins.up|target.time_to_die<18
                    //J	1.00	jade_serpent_potion,if=buff.icy_veins.up|target.time_to_die<45
                    //K	5.39	presence_of_mind,if=buff.icy_veins.up|cooldown.icy_veins.remains>15|target.time_to_die<15
                    //L	2.94	alter_time,if=buff.alter_time.down&buff.icy_veins.up
                    //M	0.00	flamestrike,if=active_enemies>=5
                    //N	3.48	frostfire_bolt,if=buff.alter_time.up&buff.brain_freeze.up
                    //O	8.24	ice_lance,if=buff.alter_time.up&buff.fingers_of_frost.up
                    //P	38.98	living_bomb,cycle_targets=1,if=(!ticking|remains<tick_time)&target.time_to_die>tick_time*3
                    //Q	5.15	frostbolt,if=debuff.frostbolt.stack<3
                    //R	61.17	frostfire_bolt,if=buff.brain_freeze.react&cooldown.icy_veins.remains>2
                    //S	70.82	ice_lance,if=buff.fingers_of_frost.react&cooldown.icy_veins.remains>2
                    //T	205.29	frostbolt
                    //U	0.00	fire_blast,moving=1
                    //V	0.00	ice_lance,moving=1                    




        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Mage, WoWSpec.MageFrost)]
        public static Composite FrostPreCombatBuffs()
        {
            return new PrioritySelector(
                new Decorator(ret => Me.Mounted,
                    new ActionAlwaysSucceed()),
                //PartyBuff.BuffGroup("Arcane Brilliance"),
                new Throttle(3,
                Spell.Cast("Frost Armor", ret => !Me.HasAura("Frost Armor")))
                );
        }

        #region Bombs Away!
        private static int bombCount()
        {
            var bombed = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
                            where unit.IsAlive
                            where unit.Combat
                            where unit.IsTargetingUs()
                            where unit.HasMyAura("Living Bomb")
                            where unit.Distance <= 40
                            select unit).Count();
            return bombed;
        }

        public static WoWUnit bombTarget
        {
            get
            {
                var Touched = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
                               where unit.IsAlive
                               where unit.Combat
                               where unit.IsTargetingUs()
                               where !unit.HasAura("Living Bomb")
                               where unit.Distance <= 40
                               select unit).FirstOrDefault();
                return Touched;
            }
        }
        #endregion

        #region MageTalents
        public enum MageTalents
        {
            PresenceofMind = 1,//Tier 1
            BazingSpeed,
            IceFloes,
            TemporalShield,//Tier 2
            Flameglow,
            IceBarrier,
            RingofFrost,//Tier 3
            IceWard,
            Frostjaw,
            GreaterInvisibility,//Tier 4
            Cauterize,
            ColdSnap,
            NetherTempest,//Tier 5
            LivingBomb,
            FrostBomb,
            Invocation,//Tier 6
            RuneofPower,
            IncantersWard
        }
        #endregion
    }
}

