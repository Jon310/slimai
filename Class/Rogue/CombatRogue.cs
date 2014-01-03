using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlimAI.Helpers;
using CommonBehaviors.Actions;
using SlimAI.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Class.Rogue
{
    class CombatRogue
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static RogueSettings Settings { get { return GeneralSettings.Instance.Rogue(); } }
        private static double? _EnergyRegen;
        private static double? _timeToMax;
        private static double? _energy;

        [Behavior(BehaviorType.Combat, WoWClass.Rogue, WoWSpec.RogueCombat)]
        public static Composite CombatCombat()
        {
            return new PrioritySelector(
//B	1.84	preparation,if=!buff.vanish.up&cooldown.vanish.remains>60
//C	6.18	use_item,slot=hands,if=time=0|buff.shadow_blades.up
            new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),

            Spell.Cast("Blade Flurry", ret => (Unit.UnfriendlyUnits(5).Count() >= 2 && SlimAI.AOE && !Me.HasAura("Blade Flurry")) || (Unit.UnfriendlyUnits(5).Count() < 2 && Me.HasAura("Blade Flurry"))),
//H	5.70	ambush
            Spell.Cast("Ambush"),
//I	4.70	vanish,if=time>10&(combo_points<3|(talent.anticipation.enabled&anticipation_charges<3)|(buff.shadow_blades.down&(combo_points<4|(talent.anticipation.enabled&anticipation_charges<4))))&((talent.shadow_focus.enabled&buff.adrenaline_rush.down&energy<20)|(talent.subterfuge.enabled&energy>=90)|(!talent.shadow_focus.enabled&!talent.subterfuge.enabled&energy>=60))
            new Decorator(ret => SlimAI.Burst,
                new PrioritySelector(
                    Spell.Cast("Shadow Blades", ret => Me.CurrentTarget.TimeToDeath() > 5),
                    Spell.Cast("Killing Spree", ret => Me.CurrentEnergy < 45 && !Me.HasAura("Adrenaline Rush")),
                    Spell.Cast("Adrenaline Rush", ret => Me.CurrentEnergy < 35 || Me.HasAura("Shadow Blades")))),

            Spell.Cast("Slice and Dice", ret => Me.GetAuraTimeLeft("Slice and Dice").TotalSeconds < 2 /* || (buff.slice_and_dice.remains<15&buff.bandits_guile.stack=11&combo_points>=4)*/),
//N	0.00	marked_for_death,if=talent.marked_for_death.enabled&(combo_points=0&dot.revealing_strike.ticking)


            CreateFinisher(),
            CreateFiller()
                );

        }

                private static Composite CreateFinisher()
        {
            return new PrioritySelector(
                new Decorator(ret => Me.ComboPoints == 5 && (!SpellManager.HasSpell("Anticipation") || Me.HasAura("Deep Insight") 
                                    /* || cooldown.shadow_blades.remains<=11|anticipation_charges>=4|(buff.shadow_blades.up&anticipation_charges>=3)*/),
                    new PrioritySelector(
                        Spell.Cast("Rupture", ret => DebuffTimeLeft(1943, Me.CurrentTarget) <= 2 && Me.CurrentTarget.TimeToDeath() >= 26 && (!Me.HasAura("Blade Flurry") || Unit.UnfriendlyUnits(10).Count() < 2 && SlimAI.AOE)),
                        Spell.Cast("Crimson Tempest", ret => Unit.UnfriendlyUnits(8).Count() >= 7 && SlimAI.AOE),
                        Spell.Cast("Eviscerate")
                        )));
//R	15.83	rupture,if=ticks_remain<2&target.time_to_die>=26&(active_enemies<2|!buff.blade_flurry.up)
//S	0.00	crimson_tempest,if=active_enemies>=7&dot.crimson_tempest_dot.ticks_remain<=2

        }

        private static Composite CreateFiller()
        {
            return new PrioritySelector(
                new Decorator(ret => Me.CurrentEnergy > 60 || !Me.HasAura("Deep Insight") || /*buff.deep_insight.remains>5-combo_points || (talent.anticipation.enabled&anticipation_charges<=4&!dot.revealing_strike.ticking) ||*/
                                     Me.ComboPoints < 5,
                    new PrioritySelector(
                        Spell.Cast("Fan Of Knives", ret => Unit.UnfriendlyUnits(10).Count() >= 4 && SlimAI.AOE),
                        Spell.Cast("Revealing Strike", ret => DebuffTimeLeft(84617, Me.CurrentTarget) <= 2),
                        Spell.Cast("Sinister Strike")
            )));
        }

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
