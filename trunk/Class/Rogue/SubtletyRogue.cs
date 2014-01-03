using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimAI.Helpers;
using SlimAI.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace SlimAI.Class.Rogue
{
    class SubtletyRogue
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static RogueSettings Settings { get { return GeneralSettings.Instance.Rogue(); } }
        private static double? _EnergyRegen;
        private static double? _timeToMax;
        private static double? _energy;

        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueSubtlety)]
        public static Composite SubtletyCombat()
        {
            return new PrioritySelector(
//B	5.70	use_item,slot=hands,if=buff.shadow_dance.up

//F	4.33	shadow_blades
            new Decorator(ret => SlimAI.Burst,
                new PrioritySelector(
                    Spell.Cast("Shadow Blades", ret => Me.CurrentTarget.TimeToDeath() > 5),
                    Spell.Cast("Premeditation", ret => Me.ComboPoints < 3))),
//G	13.26	premeditation,if=combo_points<3|(talent.anticipation.enabled&anticipation_charges<3)
//H	49.44	pool_resource,for_next=1
//I	62.44	ambush,if=combo_points<5|(talent.anticipation.enabled&anticipation_charges<3)|(buff.sleight_of_hand.up&buff.sleight_of_hand.remains<=gcd)
            Spell.Cast("Ambush", ret => Me.ComboPoints < 5 || Me.HasAura("Stealth")),
//J	55.26	pool_resource,for_next=1,extra_amount=75
//K	10.52	shadow_dance,if=energy>=75&buff.stealth.down&buff.vanish.down&debuff.find_weakness.down
            Spell.Cast("Shadow Dance", ret  => Me.EnergyPercent >= 75 && !Me.HasAura("Stealth") && !Me.HasAura("Vanish") && !Me.CurrentTarget.HasAura("Find Weakness") && SlimAI.Burst),
//L	0.43	pool_resource,for_next=1,extra_amount=45
//M	6.81	vanish,if=energy>=45&energy<=75&combo_points<=3&buff.shadow_dance.down&buff.master_of_subtlety.down&debuff.find_weakness.down
            //Spell.Cast("Vanish", ret => Me.EnergyPercent >= 45 && Me.EnergyPercent <= 75 && Me.ComboPoints <= 3 && !Me.HasAura("Shadow Dance") && !Me.HasAura("Master of Subtlety") && !Me.CurrentTarget.HasAura("Find Weakness")),
//N	0.00	marked_for_death,if=talent.marked_for_death.enabled&combo_points=0
//O	0.00	run_action_list,name=generator,if=talent.anticipation.enabled&anticipation_charges<4&buff.slice_and_dice.up&dot.rupture.remains>2&(buff.slice_and_dice.remains<6|dot.rupture.remains<4)
//P	0.00	run_action_list,name=finisher,if=combo_points=5
//Q	0.00	run_action_list,name=generator,if=combo_points<4|energy>80|talent.anticipation.enabled
//R	0.00	run_action_list,name=pool
            Spell.Cast("Slice and Dice", ret => Me.GetAuraTimeLeft("Slice and Dice").TotalSeconds < 4),
            CreateFinisher(),
            CreateFiller()
//actions.generator
//#	count	action,conditions
//X	0.00	run_action_list,name=pool,if=buff.master_of_subtlety.down&buff.shadow_dance.down&debuff.find_weakness.down&(energy+cooldown.shadow_dance.remains*energy.regen<80|energy+cooldown.vanish.remains*energy.regen<60)



//actions.pool
//#	count	action,conditions
//d	1.88	preparation,if=!buff.vanish.up&cooldown.vanish.remains>6
                );
        }

        private static Composite CreateFinisher()
        {
            return new PrioritySelector(
                new Decorator(ret => Me.ComboPoints == 5 && (!SpellManager.HasSpell("Anticipation") 
                    /* || cooldown.shadow_blades.remains<=11|anticipation_charges>=4|(buff.shadow_blades.up&anticipation_charges>=3)*/),
                    new PrioritySelector(
                        Spell.Cast("Rupture", ret => DebuffTimeLeft(1943, Me.CurrentTarget) < 4 && Me.CurrentTarget.TimeToDeath() >= 26 && Unit.UnfriendlyUnits(10).Count() < 3),
                        Spell.Cast("Crimson Tempest", ret => Unit.UnfriendlyUnits(8).Count() > 1 && SlimAI.AOE),
                        Spell.Cast("Eviscerate")
                        )));
            //S	12.62	slice_and_dice,if=buff.slice_and_dice.remains<4
            //T	18.97	rupture,if=ticks_remain<2&active_enemies<3
            //U	0.00	crimson_tempest,if=(active_enemies>1&dot.crimson_tempest_dot.ticks_remain<=2&combo_points=5)|active_enemies>=5
            //V	71.75	eviscerate,if=active_enemies<4|(active_enemies>3&dot.crimson_tempest_dot.ticks_remain>=2)

        }

        private static Composite CreateFiller()
        {
            return new PrioritySelector(
                new Decorator(ret =>  Me.ComboPoints < 5 && !Me.HasAura("Shadow Dance") && Me.EnergyPercent > 70,
                    //&(energy+cooldown.shadow_dance.remains*energy.regen<80|energy+cooldown.vanish.remains*energy.regen<60)
                    new PrioritySelector(
                        Spell.Cast("Fan Of Knives", ret => Unit.UnfriendlyUnits(10).Count() >= 4 && SlimAI.AOE),
                        Spell.Cast("Hemorrhage", ret => DebuffTimeLeft(89775, Me.CurrentTarget) < 3 || !Me.CurrentTarget.MeIsBehind),
                        Spell.Cast("Backstab", ret => Me.CurrentTarget.MeIsBehind)
//Y	0.00	fan_of_knives,if=active_enemies>=4
//Z	18.28	hemorrhage,if=remains<3|position_front
//a	0.00	shuriken_toss,if=talent.shuriken_toss.enabled&(energy<65&energy.regen<16)
//b	131.21	backstab
            )));
        }

        #region Engery
        protected static double EnergyRegen
        {
            get
            {
                if (!_EnergyRegen.HasValue)
                {
                    _EnergyRegen = Lua.GetReturnVal<float>("return GetPowerRegen()", 1);
                    return _EnergyRegen.Value;
                }
                return _EnergyRegen.Value;
            }
        }

        protected static double Energy
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

        protected static double TimeToMax
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

        private static double DebuffTimeLeft(int debuff, WoWUnit onTarget)
        {
            if (onTarget != null)
            {
                var results = onTarget.GetAuraById(debuff);
                if (results != null)
                {
                    if (results.TimeLeft.TotalSeconds > 0)
                        return results.TimeLeft.TotalSeconds;
                }
            }
            return 0;
        }
    }
}
