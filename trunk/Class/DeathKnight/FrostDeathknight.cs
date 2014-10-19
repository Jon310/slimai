using CommonBehaviors.Actions;
using SlimAI.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using SlimAI.Helpers;
using SlimAI.Managers;
using System.Linq;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Class.Deathknight
{
    class FrostDeathknight
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static int BloodRuneSlotsActive { get { return Me.GetRuneCount(0) + Me.GetRuneCount(1); } }
        private static int FrostRuneSlotsActive { get { return Me.GetRuneCount(2) + Me.GetRuneCount(3); } }
        private static int UnholyRuneSlotsActive { get { return Me.GetRuneCount(4) + Me.GetRuneCount(5); } }
        private static DeathKnightSettings Settings { get { return GeneralSettings.Instance.DeathKnight(); } }
        
        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost)]
        public static Composite FrostDKCombat()
        {
            return new PrioritySelector(
                //Spell.WaitForCastOrChannel(),
                Common.CreateInterruptBehavior(),
                
                //Staying Alive
                Spell.Cast(Conversion, ret => Me.HealthPercent < 50 && Me.RunicPowerPercent >= 20 && !Me.HasAura("Conversion")),
                Spell.Cast(Conversion, ret => Me.HealthPercent > 65 && Me.HasAura("Conversion")),
                Spell.Cast(DeathPact, ret => Me.HealthPercent < 45),
                Spell.Cast(DeathSiphon, ret => Me.HealthPercent < 50),
                Spell.Cast(IceboundFortitude, ret => Me.HealthPercent < 40),
                Spell.Cast(DeathStrike, ret => Me.GotTarget && Me.HealthPercent < 15),
                Spell.Cast(Lichborne, ret => (Me.HealthPercent < 25 && Me.CurrentRunicPower >= 60)),
                Spell.Cast(DeathCoil, on => Me, ret => Me.HealthPercent < 50 && Me.HasAura("Lichborne")),
                Spell.Cast(BloodTap, ret => Me.HasAura("Blood Charge", 10) && NoRunes),

                new Decorator(ret => Unit.UnfriendlyUnits(10).Count() >= 5 && SlimAI.AOE,
                    CreateAoe()),
                   
                //Cooldowns
                new Decorator(ret => SlimAI.Burst,
                    new PrioritySelector(
                        new Action(ret => { Item.UseTrinkets(); return RunStatus.Failure; }),
                        Spell.Cast(PillarofFrost),
                        Spell.Cast(RaiseDead))),
                //new Action(ret => { Item.UseWaist(); return RunStatus.Failure; }),
                new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),

                new Decorator(IsDualWelding ? DualWield() : TwoHand()));
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.DeathKnight, WoWSpec.DeathKnightFrost)]
        public static Composite FrostDKPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.Cast(HornofWinter, ret => !Me.HasAura("Horn of Winter")));

        }

        private static Composite CreateAoe()
        {
            return new PrioritySelector(
                new Throttle(1, 2,
                    new PrioritySelector(
                        Spell.Cast(UnholyBlight, ret => TalentManager.IsSelected((int)DeathKnightTalents.UnholyBlight) && Me.CurrentTarget.DistanceSqr <= 10 * 10 && !StyxWoW.Me.HasAura("Unholy Blight")),
                        Spell.Cast(TalentManager.IsSelected((int)DeathKnightTalents.RoillingBlood) ? "Blood Boil" : "Pestilence", 
                            ret => !Me.HasAura("Unholy Blight") && ShouldSpreadDiseases))),
                Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent <= 35 || Me.HasAura(138347) && Me.CurrentTarget.HealthPercent <= 45),
                Spell.Cast(HowlingBlast),
                Spell.Cast(BloodTap, ret => Me.HasAura("Blood Charge", 10) && NoRunes),
                Spell.Cast(FrostStrike, ret => Me.RunicPowerPercent >= 76),
                Spell.CastOnGround("Death and Decay", on => Me.CurrentTarget.Location, ret => Me.UnholyRuneCount == 1),
                Spell.Cast(PlagueStrike, ret => Me.UnholyRuneCount == 2),
                Spell.Cast(BloodTap, ret => Me.HasAura("Blood Charge", 5) && NoRunes),
                Spell.Cast(FrostStrike),    
                Spell.Cast(HornofWinter),
                Spell.Cast(PlagueLeech, ret => Me.CurrentTarget.HasAura("Frost Fever") && Me.CurrentTarget.HasAura("Blood Plague") && Me.UnholyRuneCount > 0),
                Spell.Cast(PlagueStrike, ret => Me.UnholyRuneCount == 1),
                Spell.Cast(EmpowerRuneWeapon, ret => SlimAI.Burst && Me.UnholyRuneCount == 0 && Me.DeathRuneCount == 0 && Me.FrostRuneCount == 0));
        }

        private static Composite DualWield()
        {
            return new PrioritySelector(
                new Decorator(ret => Me.CurrentTarget.GetAuraTimeLeft("Blood Plague", true).TotalSeconds <= 3 || Me.CurrentTarget.GetAuraTimeLeft("Frost Fever", true).TotalSeconds <= 3,
                    new PrioritySelector(
                        Spell.Cast(PlagueLeech, ret => SpellManager.Spells["Outbreak"].CooldownTimeLeft.Seconds <= 1 && Me.CurrentTarget.HasMyAura("Frost Fever") && Me.CurrentTarget.HasMyAura("Blood Plague")),
                        Spell.Cast(Outbreak),
                        Spell.Cast(UnholyBlight))),
                Spell.Cast(FrostStrike, ret => Me.HasAura("Killing Machine") || Me.RunicPowerPercent >= 88),
                Spell.Cast(HowlingBlast, ret => Me.DeathRuneCount > 1 || Me.FrostRuneCount > 1 || !Me.CurrentTarget.HasMyAura("Frost Fever") || Me.HasAura("Freezing Fog")),
                Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent <= 35 || Me.HasAura(138347) && Me.CurrentTarget.HealthPercent <= 45),
                Spell.Cast(PlagueStrike, ret => !Me.CurrentTarget.HasMyAura("Blood Plague") && Me.UnholyRuneCount > 0),
                Spell.Cast(DeathSiphon, ret => Me.CurrentTarget.IsPlayer),
                Spell.Cast(FrostStrike, ret => Me.RunicPowerPercent >= 76),
                Spell.Cast(Obliterate, ret => Me.UnholyRuneCount > 0 && !Me.HasAura("Killing Machine")),
                Spell.Cast(HowlingBlast),
                Spell.CastOnGround("Death and Decay", ret => Me.CurrentTarget.Location, ret => true, false),
                Spell.Cast(BloodTap, ret => Me.HasAura("Blood Charge", 5) && NoRunes),
                Spell.Cast(FrostStrike, ret => Me.CurrentRunicPower >= 40),
                Spell.Cast(HornofWinter, ret => Me.RunicPowerPercent <= 76),
                Spell.Cast(HowlingBlast),
                Spell.Cast(EmpowerRuneWeapon, ret => SlimAI.Burst && Me.UnholyRuneCount == 0 && Me.DeathRuneCount == 0 && Me.FrostRuneCount == 0),
                new ActionAlwaysSucceed());
        }

        private static Composite TwoHand()
        {
            return new PrioritySelector(
                Spell.Cast(PlagueLeech, ret => Me.CurrentTarget.GetAuraTimeLeft("Blood Plague", true).TotalSeconds < 1 && Me.CurrentTarget.HasMyAura("Frost Fever")),
                new Decorator(ret => Me.CurrentTarget.GetAuraTimeLeft("Blood Plague").TotalSeconds <= 3 || Me.CurrentTarget.GetAuraTimeLeft("Frost Fever").TotalSeconds <= 3,
                    new PrioritySelector(
                        Spell.Cast(Outbreak),
                        Spell.Cast(UnholyBlight))),
                Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent <= 35 || Me.HasAura(138347) && Me.CurrentTarget.HealthPercent <= 45),
                Spell.Cast(BloodTap, ret => Me.HasAura("Blood Charge", 5) && NoRunes),
                Spell.Cast(HowlingBlast, ret => !Me.CurrentTarget.HasMyAura("Frost Fever")),
                Spell.Cast(PlagueStrike, ret => !Me.CurrentTarget.HasMyAura("Blood Plague")),
                Spell.Cast(HowlingBlast, ret => Me.HasAura("Freezing Fog")),
                new Decorator(ret => Me.CurrentTarget.HasMyAura("Frost Fever") && Me.CurrentTarget.HasMyAura("Blood Plague"),
                    new PrioritySelector(
                        Spell.Cast(Obliterate, ret => Me.HasAura("Killing Machine")),
                        Spell.Cast(FrostStrike, ret => Me.RunicPowerPercent >= 76),
                        Spell.Cast(Obliterate, ret => Me.UnholyRuneCount >= 1 && Me.DeathRuneCount >= 1 || Me.FrostRuneCount >= 1 && Me.DeathRuneCount >= 1 || Me.UnholyRuneCount >= 1 && Me.FrostRuneCount >= 1),
                        Spell.Cast(PlagueLeech, ret => Me.CurrentTarget.GetAuraTimeLeft("Blood Plague").TotalSeconds < 3),
                        Spell.Cast(FrostStrike, ret => !Me.HasAura("Killing Machine") && NoRunes),
                        Spell.Cast(Obliterate, ret => Me.RunicPowerPercent <= 76),
                        Spell.Cast(HornofWinter, ret => Me.RunicPowerPercent <= 76),
                        Spell.Cast(FrostStrike),
                        Spell.Cast(PlagueLeech),
                        Spell.Cast(EmpowerRuneWeapon, ret => SlimAI.Burst && Me.UnholyRuneCount == 0 && Me.DeathRuneCount == 0 && Me.FrostRuneCount == 0))));
        }

        private static bool ShouldSpreadDiseases
        {
            get
            {
                int radius = TalentManager.HasGlyph("Pestilence") ? 15 : 10;
                return !Me.CurrentTarget.HasAuraExpired("Blood Plague")
                    && !Me.CurrentTarget.HasAuraExpired("Frost Fever")
                    && Unit.NearbyUnfriendlyUnits.Any(u => Me.SpellDistance(u) < radius && u.HasAuraExpired("Blood Plague"));
            }
        }

        private static bool IsDualWelding
        {
            get { return Me.Inventory.Equipped.MainHand != null && Me.Inventory.Equipped.OffHand != null; }
        }

        private static bool NoRunes
        {
            get { return BloodRuneSlotsActive == 0 || FrostRuneSlotsActive == 0 || UnholyRuneSlotsActive == 0; }
        }

        #region DeathKnightTalents
        public enum DeathKnightTalents
        {
            RoillingBlood = 1,//Tier 1
            PlagueLeech,
            UnholyBlight,
            LichBorne,//Tier 2
            AntiMagicZone,
            Purgatory,
            DeathsAdvance,//Tier 3
            Chilblains,
            Asphyxiate,
            DeathPact,//Tier 4
            DeathSiphon,
            Conversion,
            BloodTap,//Tier 5
            RunicEmpowerment,
            RunicCorruption,
            GorefiendsGrasp,//Tier 6
            RemoreselessWinter,
            DesecratedGround
        }
        #endregion

        #region DeathKnight Spells
        private const int AntiMagicShell = 48707,
                          Asphyxiate = 108194,
                          BloodBoil = 48721,
                          BloodTap = 45529,
                          BoneShield = 49222,
                          Conversion = 119975,
                          DancingRuneWeapon = 49028,
                          DarkTransformation = 63560,
                          DeathandDecay = 43265,
                          DeathCoil = 47541,
                          DeathPact = 48743,
                          DeathSiphon = 108196,
                          DeathStrike = 49998,
                          DesecratedGround = 108201,
                          EmpowerRuneWeapon = 47568,
                          FrostStrike = 49143,
                          HeartStrike = 55050,
                          HornofWinter = 57330,
                          HowlingBlast = 49184,
                          IceboundFortitude = 48792,
                          IcyTouch = 45477,
                          Lichborne = 49039,
                          MightofUrsoc = 106922,
                          Obliterate = 49020,
                          Outbreak = 77575,
                          Pestilence = 50842,
                          PillarofFrost = 51271,
                          PlagueLeech = 123693,
                          PlagueStrike = 45462,
                          RaiseDead = 46584,
                          RemorselessWinter = 108200,
                          RuneStrike = 56815,
                          RuneTap = 48982,
                          ScourgeStrike = 55090,
                          SummonGargoyle = 49206,
                          UnholyBlight = 115989,
                          VampiricBlood = 55233;
        #endregion
    }
}
