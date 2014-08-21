using SlimAI.Managers;
using CommonBehaviors.Actions;
using SlimAI.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using SlimAI.Helpers;
using System.Linq;

namespace SlimAI.Class.Deathknight
{
    class UnholyDeathknight
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private const int SuddenDoom = 81340;
        internal static int BloodRuneSlotsActive { get { return Me.GetRuneCount(0) + Me.GetRuneCount(1); } }
        internal static int FrostRuneSlotsActive { get { return Me.GetRuneCount(2) + Me.GetRuneCount(3); } }
        internal static int UnholyRuneSlotsActive { get { return Me.GetRuneCount(4) + Me.GetRuneCount(5); } }
        private static DeathKnightSettings Settings { get { return GeneralSettings.Instance.DeathKnight(); } }
        internal const uint Ghoul = 26125;        
        internal static bool GhoulMinionIsActive
        {
            get { return Me.Minions.Any(u => u.Entry == Ghoul); }
        }

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy)]
        public static Composite UnholyDKCombat()
        {
            return new PrioritySelector(

                new Decorator(ret => SlimAI.PvPRotation,
                    CreatePvP()),
                new Decorator(ret => !Me.Combat || Me.Mounted || !Me.GotTarget,
                    new ActionAlwaysSucceed()),

                Spell.WaitForCastOrChannel(),
                Common.CreateInterruptBehavior(),
                Item.UsePotionAndHealthstone(40),
                Spell.Cast(Conversion, ret => Me.HealthPercent < 50 && Me.RunicPowerPercent >= 20 && !Me.HasAura("Conversion")),
                Spell.Cast(Conversion, ret => Me.HealthPercent > 65 && Me.HasAura("Conversion")),
                Spell.Cast(DeathPact, ret => Me.HealthPercent < 45),
                Spell.Cast(DeathSiphon, ret => Me.HealthPercent < 50),
                Spell.Cast(IceboundFortitude, ret => Me.HealthPercent < 40),
                Spell.Cast(DeathStrike, ret => Me.GotTarget && Me.HealthPercent < 15),
                Spell.Cast(Lichborne, ret => (Me.HealthPercent < 25 && Me.CurrentRunicPower >= 60)),
                Spell.Cast(DeathCoil, on => Me, ret => Me.HealthPercent < 50 && Me.HasAura("Lichborne")),
                new Throttle(2,
                    new PrioritySelector(
                        Spell.Cast(BloodTap, ret => Me.HasAura("Blood Charge", 10) && NoRunes))),
                Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 36),
                new Decorator(ret => !Me.CurrentTarget.HasMyAura("Frost Fever") || !Me.CurrentTarget.HasMyAura("Blood Plague"),
                    new PrioritySelector(
                        Spell.Cast(Outbreak),
                        Spell.Cast(PlagueStrike))),
                new Throttle(1, 2,
                    new PrioritySelector(
                        Spell.Cast(UnholyBlight, ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= 2 && Me.CurrentTarget.DistanceSqr <= 10 * 10 && !StyxWoW.Me.HasAura("Unholy Blight")),
                        Spell.Cast(BloodBoil, ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= 2 && TalentManager.IsSelected((int)DeathKnightTalents.RoillingBlood) && !Me.HasAura("Unholy Blight") && ShouldSpreadDiseases))),
                            
                new Decorator(ret => SlimAI.Burst,
                    new PrioritySelector(
                        Spell.Cast("Unholy Frenzy", ret => Me.CurrentTarget.IsWithinMeleeRange && !PartyBuff.WeHaveBloodlust),
                        new Action(ret => { Item.UseTrinkets(); return RunStatus.Failure; }),
                        new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                        Spell.Cast(SummonGargoyle))),
                new Throttle(1, 2,
                    new PrioritySelector(
                        Spell.Cast(Pestilence, ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= 2 && !Me.HasAura("Unholy Blight") && ShouldSpreadDiseases))),
                //Kill Time
                Spell.Cast(DarkTransformation, ret => Me.GotAlivePet && !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation") && Me.HasAura("Shadow Infusion", 5)),
                Spell.CastOnGround("Death and Decay", ret => Me.CurrentTarget.Location, ret => true, false),
                Spell.Cast(ScourgeStrike, ret => Me.UnholyRuneCount == 2),
                Spell.Cast("Festering Strike", ret => Me.BloodRuneCount == 2 && Me.FrostRuneCount == 2),
                Spell.Cast(DeathCoil, ret => (Me.HasAura(SuddenDoom) || Me.CurrentRunicPower >= 90)),
                Spell.Cast(ScourgeStrike),
                Spell.Cast(PlagueLeech, ret => SpellManager.Spells["Outbreak"].CooldownTimeLeft.Seconds <= 1),
                Spell.Cast("Festering Strike"),
                //Blood Tap
                Spell.Cast(BloodTap, ret => Me.HasAura("Blood Charge", 5) && NoRunes),
                Spell.Cast(DeathCoil, ret => SpellManager.Spells["Lichborne"].CooldownTimeLeft.Seconds >= 4 && Me.CurrentRunicPower < 60 || !Me.HasAura("Conversion")),
                Spell.Cast(HornofWinter),
                Spell.Cast(EmpowerRuneWeapon, ret => SlimAI.Burst && NoRunes));
        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.DeathKnight, WoWSpec.DeathKnightUnholy)]
        public static Composite UnholyPreCombatBuffs()
        {
            return new PrioritySelector(
                new Decorator(ret => Me.Mounted,
                    new ActionAlwaysSucceed()),
                    Spell.Cast("Raise Dead", ret => !Me.GotAlivePet),
                    Spell.Cast(HornofWinter, ret => !Me.HasPartyBuff(PartyBuffType.AttackPower)));
        }

        #region PvP
        private static Composite CreatePvP()
        {
            return new PrioritySelector(

                new Decorator(ret => !Me.Combat || Me.Mounted,
                    new ActionAlwaysSucceed()),
                Spell.Cast(Conversion, ret => Me.HealthPercent < 50 && Me.RunicPowerPercent >= 20 && !Me.HasAura("Conversion")),
                Spell.Cast(Conversion, ret => Me.HealthPercent > 65 && Me.HasAura("Conversion")),
                Spell.Cast(DeathPact, ret => Me.HealthPercent < 45),
                Spell.Cast(DeathSiphon, ret => Me.HealthPercent < 50),
                //Spell.Cast(IceboundFortitude, ret => Me.HealthPercent < 40),
                Spell.Cast(DeathStrike, ret => Me.GotTarget && Me.HealthPercent < 15),
                Spell.Cast(Lichborne, ret => (Me.HealthPercent < 25 && Me.CurrentRunicPower >= 60)),
                Spell.Cast(DeathCoil, on => Me, ret => Me.HealthPercent < 50 && Me.HasAura("Lichborne")),
                new Throttle(2,
                    new PrioritySelector(
                        Spell.Cast(BloodTap, ret => Me.HasAura("Blood Charge", 10) && NoRunes))),
                Spell.Cast("Soul Reaper", ret => Me.CurrentTarget.HealthPercent < 36),
                new Decorator(ret => !Me.CurrentTarget.HasMyAura("Frost Fever") || !Me.CurrentTarget.HasMyAura("Blood Plague"),
                    new PrioritySelector(
                        Spell.Cast(Outbreak),
                        Spell.Cast(PlagueStrike))),
                new Throttle(1, 2,
                    new PrioritySelector(
                        Spell.Cast(UnholyBlight, ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= 2 && Me.CurrentTarget.DistanceSqr <= 10 * 10 && !StyxWoW.Me.HasAura("Unholy Blight") && SlimAI.AOE),
                        Spell.Cast(BloodBoil, ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= 2 && TalentManager.IsSelected((int)DeathKnightTalents.RoillingBlood) && !Me.HasAura("Unholy Blight") && ShouldSpreadDiseases && SlimAI.AOE))),
                            
                new Decorator(ret => SlimAI.Burst,
                    new PrioritySelector(
                        Spell.Cast("Unholy Frenzy", ret => Me.CurrentTarget.IsWithinMeleeRange && !PartyBuff.WeHaveBloodlust),
                        new Action(ret => { Item.UseTrinkets(); return RunStatus.Failure; }),
                        new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                        Spell.Cast(SummonGargoyle))),
                new Throttle(1, 2,
                    new PrioritySelector(
                        Spell.Cast(Pestilence, ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= 2 && !Me.HasAura("Unholy Blight") && ShouldSpreadDiseases && SlimAI.AOE))),
                //Kill Time
                Spell.Cast(DarkTransformation, ret => Me.GotAlivePet && !Me.Pet.ActiveAuras.ContainsKey("Dark Transformation") && Me.HasAura("Shadow Infusion", 5)),
                Spell.Cast(NecroticStrike, ret => SlimAI.Weave),
                Spell.CastOnGround("Death and Decay", ret => Me.CurrentTarget.Location, ret => true, false),
                Spell.Cast(ScourgeStrike, ret => Me.UnholyRuneCount == 2),
                Spell.Cast("Festering Strike", ret => Me.BloodRuneCount == 2 && Me.FrostRuneCount == 2),
                Spell.Cast(DeathCoil, ret => (Me.HasAura(SuddenDoom) || Me.CurrentRunicPower >= 90)),
                Spell.Cast(ScourgeStrike),
                Spell.Cast(PlagueLeech, ret => SpellManager.Spells["Outbreak"].CooldownTimeLeft.Seconds <= 1),
                Spell.Cast("Festering Strike"),
                //Blood Tap
                Spell.Cast(BloodTap, ret => Me.HasAura("Blood Charge", 5) && NoRunes),
                Spell.Cast(DeathCoil, ret => SpellManager.Spells["Lichborne"].CooldownTimeLeft.Seconds >= 4 && Me.CurrentRunicPower < 60 || !Me.HasAura("Conversion")),
                Spell.Cast(HornofWinter),
                Spell.Cast(EmpowerRuneWeapon, ret => SlimAI.Burst && NoRunes),
                new ActionAlwaysSucceed()) ;
        }
        #endregion

        private static bool ShouldSpreadDiseases
        {
            get
            {
                int radius = TalentManager.HasGlyph("Pestilence") ? 15 : 10;
                return !Me.CurrentTarget.HasAuraExpired("Blood Plague")
                    && !Me.CurrentTarget.HasAuraExpired("Frost Fever")
                    && Unit.NearbyUnfriendlyUnits.Any(u => Me.SpellDistance(u) < radius && u.HasAuraExpired("Blood Plague") && u.HasAuraExpired("Frost Fever"));
            }
        }

        private static bool NoRunes
        {
            get { return BloodRuneSlotsActive == 0 || FrostRuneSlotsActive == 0 || UnholyRuneSlotsActive == 0; }
        }

        #region DeathKnightTalents

        private enum DeathKnightTalents
        {
            None = 0,
            RoillingBlood,//Tier 1
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
                          NecroticStrike = 73975,
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
