using SlimAI.Managers;
using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using SlimAI.Helpers;
using SlimAI.Settings;
using System.Linq;

namespace SlimAI.Class.Deathknight
{
    class BloodDeathknight
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static int BloodRuneSlotsActive { get { return Me.GetRuneCount(0) + Me.GetRuneCount(1); } }
        private static int FrostRuneSlotsActive { get { return Me.GetRuneCount(2) + Me.GetRuneCount(3); } }
        private static int UnholyRuneSlotsActive { get { return Me.GetRuneCount(4) + Me.GetRuneCount(5); } }
        private static DeathKnightSettings Settings { get { return GeneralSettings.Instance.DeathKnight(); } }

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood)]
        public static Composite BloodDKCombat()
        {
            return new PrioritySelector(
                Common.CreateInterruptBehavior(),
                new Decorator(ret =>!Me.Combat || Me.Mounted || Me.IsCasting || Me.IsChanneling,
                              new ActionAlwaysSucceed()),
            new Decorator(ret => Me.CurrentTarget.HasAuraExpired("Frost Fever") || Me.CurrentTarget.HasAuraExpired("Blood Plague"), 
                CreateApplyDiseases()),
            BloodCombatBuffs(),
            new Decorator(ret => SlimAI.AFK,
                CreateAFK()),
            new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),

            Spell.Cast(DeathStrike, ret => ShouldDeathStrike),

            new Throttle(1, 2,
                new Decorator(ret => Unit.UnfriendlyUnits(12).Count() >= 2 && !Me.HasAura("Unholy Blight") && SlimAI.AOE && ShouldSpreadDiseases,
                    new PrioritySelector(
                        Spell.Cast(BloodBoil, ret => SpellManager.HasSpell("Roiling Blood")),
                        Spell.Cast(Pestilence, ret => !SpellManager.HasSpell("Roiling Blood"))))),

            new Decorator(ret => !ShouldDeathStrike,
                new PrioritySelector(
                    DnD(),
                    Spell.Cast(BloodBoil, ret => SlimAI.AOE && ((Me.CurrentTarget.HasAuraExpired("Blood Plague", 3) && Me.CurrentTarget.HasAura("Blood Plague")) && Spell.GetSpellCooldown("Outbreak").TotalSeconds > 3 ||
                                                 Me.HasAura(81141) && !SpellManager.CanCast("Death and Decay"))),
                    Spell.Cast(RuneTap, ret => Me.HealthPercent <= 80 && Me.BloodRuneCount >= 1),
                    new Decorator(ret => Me.CurrentRunicPower >= 30 && !Me.HasAura("Lichborne"),
                        Spell.Cast(RuneStrike, ret => (Me.CurrentRunicPower >= 60 || Me.HealthPercent > 90) && NoRunes)),
                    Spell.Cast(SoulReaper, ret => Me.BloodRuneCount > 0 && Me.CurrentTarget != null && Me.CurrentTarget.HealthPercent <= 35),
                    Spell.Cast(BloodBoil, ret => SlimAI.AOE && !SpellManager.CanCast("Death and Decay") && Unit.UnfriendlyUnits(10).Count() >= 3 && Me.BloodRuneCount > 0),
                    Spell.Cast(HeartStrike, ret => Me.BloodRuneCount > 0),
                    Spell.Cast(HornofWinter, ret => Me.CurrentRunicPower < 90))));

        }

        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood)]
        public static Composite BloodDKPreCombatBuffs()
        {
            return new PrioritySelector(
                Spell.Cast(BoneShield, ret => !Me.HasAura("Bone Shield")),
                Spell.Cast(HornofWinter, ret => !Me.HasPartyBuff(PartyBuffType.AttackPower)));
        }

        private static Composite BloodCombatBuffs()
        {
            return new PrioritySelector(
                Spell.Cast(DancingRuneWeapon, ret => IsCurrentTank()),
                Spell.Cast(BloodTap, ret => Me.HasAura("Blood Charge", 5) && Me.HealthPercent < 90 && !SpellManager.CanCast("Death Strike") && NoRunes),
                Spell.Cast(BoneShield, ret => !Me.HasAura("Bone Shield")),
                Spell.Cast(Conversion, ret => Me.HealthPercent < 60 && Me.RunicPowerPercent > 20 && !Me.HasAura("Conversion")),
                Spell.Cast(Conversion, ret => Me.HealthPercent > 90 && Me.HasAura("Conversion")),
                new Decorator(ret => !Me.HasAnyAura("Bone Shield", "Vampiric Blood", "Dancing Rune Weapon", "Lichborne", "Icebound Fortitude"),
                    new PrioritySelector(
                        Spell.Cast(VampiricBlood, ret => Me.HealthPercent < 60),
                        Spell.Cast(IceboundFortitude, ret => Me.HealthPercent < 30))),
                Spell.Cast(MightofUrsoc, ret => Me.HealthPercent < 60),
                new Decorator(ret => Me.HealthPercent < 45,
                    new PrioritySelector(
                        Spell.Cast(RaiseDead, ret => !GhoulMinionIsActive),
                        Spell.Cast(DeathPact),
                        Spell.Cast(EmpowerRuneWeapon, ret => !SpellManager.CanCast("Death Strike")))),
                Spell.Cast(BloodTap, ret => Me.HasAura("Blood Charge", 10) && NoRunes),
                Spell.Cast(PlagueLeech, ret => CanCastPlagueLeech));
        }

        private static Composite CreateAFK()
        {
            return new PrioritySelector(
                Spell.Cast(AntiMagicShell, ret => Me.CurrentTarget.IsCasting),
                Spell.Cast(Asphyxiate, ret => Unit.UnfriendlyUnits(8).Count() < 3),
                Spell.Cast(RemorselessWinter, ret => Unit.UnfriendlyUnits(8).Count() >= 3),
                Spell.Cast(DesecratedGround, ret => Me.IsCrowdControlled()));
    }

        private static Composite CreateApplyDiseases()
        {
            return new Throttle(
                new PrioritySelector(
                    Spell.Cast(UnholyBlight, ret => SlimAI.AOE && Unit.NearbyUnfriendlyUnits.Any(u => (u.IsPlayer || u.IsBoss()) &&
                                                       u.Distance < 10 && u.HasAuraExpired("Blood Plague"))),
                    Spell.Cast(Outbreak),
                    new Decorator(ret => Spell.GetSpellCooldown("Outbreak").TotalSeconds > 3,
                        new PrioritySelector(
                    Spell.Cast(IcyTouch, ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.CurrentTarget.HasAuraExpired("Frost Fever")),
                    Spell.Cast(PlagueStrike, ret => Me.CurrentTarget.HasAuraExpired("Blood Plague"))))
                    ));
        }

        private static bool ShouldSpreadDiseases
        {
            get
            {
                var radius = TalentManager.HasGlyph("Pestilence") ? 15 : 10;
                return Me.CurrentTarget.HasAura("Blood Plague") && Me.CurrentTarget.HasAura("Frost Fever")
                    && Unit.UnfriendlyUnits(radius).Any(u => !u.HasAura("Blood Plague") && !u.HasAura("Frost Fever"));
            }
        }

        private static bool ShouldDeathStrike
        {
            get
            {
                return Me.HealthPercent < 40 || (Me.UnholyRuneCount + Me.FrostRuneCount + Me.DeathRuneCount >= 4) ||
                       (Me.HealthPercent < 90 && (Me.GetAuraTimeLeft("Blood Shield").TotalSeconds < 2)) ||
                       IsCurrentTank() && !Me.HasAura("Blood Shield") || Me.HasAura("Blood Charge", 10);
            }
        }

        private static bool NoRunes
        {
            get { return BloodRuneSlotsActive == 0 || FrostRuneSlotsActive == 0 || UnholyRuneSlotsActive == 0; }
        }

        private static bool CanCastPlagueLeech
        {
            get
            {
                if (!Me.GotTarget)
                    return false;
                var frostFever = (int)Me.CurrentTarget.GetAuraTimeLeft("Frost Fever").TotalMilliseconds;
                var bloodPlague = (int)Me.CurrentTarget.GetAuraTimeLeft("Blood Plague").TotalMilliseconds;
                return (frostFever.Between(350, 3000) || bloodPlague.Between(350, 3000)) && NoRunes;
            }
        }

        private const uint Ghoul = 26125;

        private static bool GhoulMinionIsActive
        {
            get { return Me.Minions.Any(u => u.Entry == Ghoul); }
        }

        #region Is Tank
        static bool IsCurrentTank()
        {
            return StyxWoW.Me.CurrentTarget.CurrentTargetGuid == StyxWoW.Me.Guid;
        }
        #endregion

        #region DnD
        private static Composite DnD()
        {
            return new Decorator(ret => SpellManager.CanCast("Death and Decay") && SlimAI.AOE && (Unit.UnfriendlyUnits(12).Count() >= 3 || Me.HasAura(81141)),
                new Action(ret =>
                {
                    var tpos = StyxWoW.Me.CurrentTarget.Location;
                    SpellManager.Cast("Death and Decay");
                    SpellManager.ClickRemoteLocation(tpos);
                }));
        }
        #endregion

        #region DeathKnightTalents

        internal enum DeathKnightTalents
        {
            RoilingBlood = 1,//Tier 1
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
        private const int 
            AntiMagicShell = 48707,
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
            SoulReaper = 114866,
            SummonGargoyle = 49206,
            UnholyBlight = 115989,
            VampiricBlood = 55233;
        #endregion
    }
}
