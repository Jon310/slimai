using System.Linq;
using SlimAI;
using SlimAI.Helpers;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;


namespace SlimAI.Class.Mage
{
    class ArcaneMage
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }

        [Behavior(BehaviorType.Combat, WoWClass.Mage, WoWSpec.MageArcane)]
        public static Composite ArcaneCombat()
            {
                return new PrioritySelector(

                    new Throttle(2,
                    RoP()),
                    Spell.Cast("Alter Time", ret => Me.HasAura("Alter Time") && Me.IsMoving),
                    Spell.Cast("Ice Barrier", ret => !Me.HasAura("Ice Barrier")),

                    new Decorator(ret => Me.IsMoving,
                        new PrioritySelector(
                            Spell.Cast("Arcane Barrage"),
                            Spell.Cast("Arcane Explosion", ret => Unit.UnfriendlyUnits(10).Count() >= 2 && SlimAI.AOE),
                            Spell.Cast("Fire Blast"),
                            Spell.Cast("Ice Lance")                            
                            )),

                    new Decorator(ret => SlimAI.Burst && !Me.IsMoving,
                        new PrioritySelector(
                            Spell.Cast("Mirror Image"),
                            Spell.Cast("Arcane Power", ret => !PartyBuff.WeHaveBloodlust && Me.HasAura("Arcane Missiles!", 2) && Me.HasAura("Arcane Charge", 4) && Me.HasAura("Rune of Power")),
                            Spell.Cast("Alter Time", ret => !Me.HasAura("Alter Time") && Me.HasAura("Arcane Power"))
                            )),

//F	1.23	mana_gem,if=mana.pct<80&buff.alter_time.down
                    CreateUseManaGemBehavior(ret => Me.ManaPercent < 80 && !Me.HasAura("Alter Time")),

                    Spell.Cast("Arcane Blast", ret => Me.HasAura("Alter Time") && Me.GetAuraTimeLeft("Alter Time").TotalSeconds < 2),
                    Spell.Cast("Arcane Missles", ret => Me.HasAura("Alter Time")),
                    Spell.Cast("Arcane Blast", ret => Me.HasAura("Alter Time")),
//V	52.46	arcane_blast,if=buff.profound_magic.up&buff.arcane_charge.stack>3&mana.pct>93

                    Spell.Cast("Arcane Missiles", ret => (Me.HasAura("Arcane Missiles!", 2) && Spell.GetSpellCooldown("Arcane Power").TotalSeconds > 0) ||
                                                          (Me.HasAura("Arcane Charge", 4) && Spell.GetSpellCooldown("Arcane Power").TotalSeconds > 8)),
                    CreateBomb(),
                    Spell.Cast("Arcane Barrage", ret => Me.HasAura("Arcane Charge", 4) && Me.ManaPercent < 95),
                    Spell.Cast("Presence of Mind"),
                    Spell.Cast("Arcane Blast")


                    
                    );
            }
        

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Mage, WoWSpec.MageArcane)]
        public static Composite ArcanePreCombatBuffs()
            {
                return new PrioritySelector(
                    PartyBuff.BuffGroup("Arcane Brilliance"),
                    new Throttle(3,
                    Spell.Cast("Mage Armor", ret => !Me.HasAura("Mage Armor"))),
                    Spell.BuffSelf("Conjure Mana Gem", ret => !HaveManaGem)
                    );
            }

        #region Mage Bomb
        private static Composite CreateBomb()
        {
            return new PrioritySelector(

                Spell.Cast("Nether Tempest", ret => !Me.CurrentTarget.HasMyAura("Nether Tempest")),
                Spell.Cast("Frost Bomb", ret => !Me.IsMoving),
                Spell.Cast("Living Bomb", ret => (Me.CurrentTarget.GetAuraTimeLeft("Living Bomb", true).TotalSeconds < 2 || !Me.CurrentTarget.HasAura("Living Bomb")) && Me.CurrentTarget.TimeToDeath() > 6)
                );
        }
        #endregion

        #region RoP
        private static Composite RoP()
        {
            return new Decorator(ret => !Me.HasAura("Rune of Power"),
                new Action(ret =>
                {
                    var tpos = Me.CurrentTarget.Location;
                    var mpos = Me.Location;

                    SpellManager.Cast("Rune of Power");
                    SpellManager.ClickRemoteLocation(mpos);
                }));
        }
        #endregion
        
        #region Mana Gem
        private static bool HaveManaGem
        {
            get
            {
                return StyxWoW.Me.BagItems.Any(i => i.Entry == 36799 || i.Entry == 81901);
            }
        }

        public static Composite CreateUseManaGemBehavior(SimpleBooleanDelegate requirements)
        {
            return new Throttle(2,
                new PrioritySelector(
                    ctx => StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == 36799 || i.Entry == 81901),
                    new Decorator(
                        ret => ret != null && StyxWoW.Me.ManaPercent < 80 && ((WoWItem)ret).Cooldown == 0 && requirements(ret),
                        new Sequence(
                            new Action(ret => ((WoWItem)ret).Use())
                            )
                        )
                    )
                );
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
