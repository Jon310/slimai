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

namespace SlimAI.Class.Deathknight
{
    [UsedImplicitly]
    class BloodDeathknight
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static int BloodRuneSlotsActive { get { return Me.GetRuneCount(0) + Me.GetRuneCount(1); } }
        private static int FrostRuneSlotsActive { get { return Me.GetRuneCount(2) + Me.GetRuneCount(3); } }
        private static int UnholyRuneSlotsActive { get { return Me.GetRuneCount(4) + Me.GetRuneCount(5); } }
        private static DeathKnightSettings Settings { get { return GeneralSettings.Instance.DeathKnight(); } }

        #region Coroutine Combat

        private static async Task<bool> CombatCoroutine()
        {

            if (!Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive) return true;
                        
            //Army(),
            //CreateInterruptSpellCast(),
            //Common.CreateInterruptBehavior(),
            //new Decorator(ret => Me.CurrentTarget.HasAuraExpired("Frost Fever") || Me.CurrentTarget.HasAuraExpired("Blood Plague"), 
            //    CreateApplyDiseases()),
            //BloodCombatBuffs(),
            //new Decorator(ret => SlimAI.AFK,
            //    CreateAFK()),
            //Item.UsePotionAndHealthstone(40),

            await Spell.CoCast(UnholyBlight, !Me.CurrentTarget.HasAura("Frost Fever") || !Me.CurrentTarget.HasAura("Blood Plague"));
            await Spell.CoCast(Outbreak, !Me.CurrentTarget.HasAura("Frost Fever") || !Me.CurrentTarget.HasAura("Blood Plague"));
            await Spell.CoCast(IcyTouch, !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && !Me.CurrentTarget.HasAura("Frost Fever") && Spell.GetSpellCooldown("Outbreak").TotalSeconds > 3);
            await Spell.CoCast(PlagueStrike, !Me.CurrentTarget.HasAura("Blood Plague") && Spell.GetSpellCooldown("Outbreak").TotalSeconds > 3);

            await Spell.CoCast(BoneShield, !Me.HasAura("Bone Shield"));
            await Spell.CoCast(Conversion, Me.HealthPercent < 60 && Me.RunicPowerPercent > 20 && !Me.HasAura("Conversion"));
            await Spell.CoCast(Conversion, Me.HealthPercent > 90 && Me.HasAura("Conversion"));

            await Spell.CoCast(DeathPact, Me.HealthPercent < 30);
            await Spell.CoCast(EmpowerRuneWeapon, !SpellManager.CanCast("Death Strike") && !SpellManager.CanCast(DeathPact));
            await Spell.CoCast(BloodTap, Me.HasAura("Blood Charge", 5) && Me.HealthPercent < 90 && !SpellManager.CanCast("Death Strike") && NoRunes);
            await Spell.CoCast(PlagueLeech, CanCastPlagueLeech);

            await Spell.CoCast(DeathStrike, ShouldDeathStrike);

            await Spell.CoCastOnGround(DeathandDecay, Me.Location, Me.HasAura(81141) && SlimAI.AOE);
            await Spell.CoCast(BloodBoil, Me.CurrentTarget.HasAuraExpired("Blood Plague", 3) && Spell.GetSpellCooldown("Outbreak").TotalSeconds > 3 || Me.HasAura(81141) && !SpellManager.CanCast("Death and Decay"));
            await Spell.CoCast(DeathCoil, Me.CurrentRunicPower >= 30);
            await Spell.CoCast(SoulReaper, BloodRuneUse && Me.CurrentTarget != null && Me.CurrentTarget.HealthPercent <= 35);
            await Spell.CoCast(BloodBoil, BloodRuneUse);
            await Spell.CoCast(BloodTap, Me.HasAura("Blood Charge", 10) && NoRunes);
            
            return false;
        }

        [Behavior(BehaviorType.Combat, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood)]
        public static Composite CoBloodCombat()
        {
            return new ActionRunCoroutine(ctx => CombatCoroutine());
        }

        #endregion


        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.DeathKnight, WoWSpec.DeathKnightBlood)]
        public static Composite BloodDKPreCombatBuffs()
        {
            return new PrioritySelector(
                new Decorator(ret => Me.Mounted,
                              new ActionAlwaysSucceed()),
                Spell.Cast(BoneShield, ret => !Me.HasAura("Bone Shield"))
                //Spell.Cast(HornofWinter, ret => !Me.HasPartyBuff(PartyBuffType.AttackPower))
                );
        }

        private static Composite BloodCombatBuffs()
        {
            return new PrioritySelector(
                Spell.Cast(DancingRuneWeapon, ret => IsCurrentTank() && !SpellManager.CanCast("Death Strike") && SlimAI.Burst),
                Spell.Cast(BloodTap, ret => Me.HasAura("Blood Charge", 5) && Me.HealthPercent < 90 && !SpellManager.CanCast("Death Strike") && NoRunes),
                Spell.Cast(BoneShield, ret => !Me.HasAura("Bone Shield")),
                Spell.Cast(Conversion, ret => Me.HealthPercent < 60 && Me.RunicPowerPercent > 20 && !Me.HasAura("Conversion")),
                Spell.Cast(Conversion, ret => Me.HealthPercent > 90 && Me.HasAura("Conversion")),
                new Decorator(ret => !Me.HasAnyAura("Bone Shield", "Vampiric Blood", "Dancing Rune Weapon", "Lichborne", "Icebound Fortitude"),
                    new PrioritySelector(
                        Spell.Cast(VampiricBlood, ret => Me.HealthPercent < 60),
                        Spell.Cast(IceboundFortitude, ret => Me.HealthPercent < 30))),
                new Decorator(ret => Me.HealthPercent < 45,
                    new PrioritySelector(
                        Spell.Cast(RaiseDead, ret => !GhoulMinionIsActive),
                        Spell.Cast(DeathPact),
                        Spell.Cast(EmpowerRuneWeapon, ret => !SpellManager.CanCast("Death Strike") && SpellManager.Spells["Death Pact"].Cooldown))),
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
                    Spell.Cast(UnholyBlight, ret => SlimAI.AOE && Unit.NearbyUnfriendlyUnits.Any(u => (u.IsPlayer || u.IsBoss) &&
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
                return SpellManager.CanCast("Death Strike") && (Me.HealthPercent < 40 || (Me.UnholyRuneCount + Me.FrostRuneCount + Me.DeathRuneCount >= 4) ||
                       (Me.HealthPercent < 90 && (Me.GetAuraTimeLeft("Blood Shield").TotalSeconds < 2)) ||
                       IsCurrentTank() && !Me.HasAura("Blood Shield") || Me.HasAura("Blood Charge", 10));
            }
        }

        private static bool BloodRuneUse
        {
            get { return BloodRuneSlotsActive >= 1; }
        }

        private static bool FewRunes
        {
            get { return BloodRuneSlotsActive <= 1 || FrostRuneSlotsActive <= 1 || UnholyRuneSlotsActive <= 1; }
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
        #region Army
        private static Composite Army()
        {
            return
                new Decorator(ret => SpellManager.CanCast(ArmyoftheDead) && !Me.IsMoving &&
                    KeyboardPolling.IsKeyDown(Keys.Z),
                    new Action(ret =>
                    {
                        SpellManager.Cast(ArmyoftheDead);
                        return;
                    }));
        }
        #endregion

        #region y'shaarj
        public static WoWUnit TouchedTar
        {
            get
            {
                var Touched = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWPlayer>()
                               where unit.IsAlive
                               where unit.HasAnyAura("Touch of Y'Shaarj", "Empowered Touch of Y'Shaarj")
                               where unit.IsCasting
                               select unit).FirstOrDefault();
                return Touched;
            }
        }

        public static Composite CreateInterruptSpellCast()
        {
            return new Decorator(
                // If the target is casting, and can actually be interrupted, AND we've waited out the double-interrupt timer, then find something to interrupt with.
                new PrioritySelector(
                Spell.Cast("Mind Freeze", on => TouchedTar),
                // AOE interrupt
                // Racials last.
                // Don't waste stomp on bosses. They can't be stunned 99% of the time!
                Spell.Cast("Strangulate", on => TouchedTar, ret => SpellManager.Spells["Mind Freeze"].Cooldown || TouchedTar.Distance >= 8)
                    ));
        }
        #endregion

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

        #region Reset
        private static RunStatus ResetVariables()
        {
            KeyboardPolling.IsKeyDown(Keys.Z);
            KeyboardPolling.IsKeyDown(Keys.C);
            return RunStatus.Failure;
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
            ArmyoftheDead = 42650,
            Asphyxiate = 108194,
            BloodBoil = 50842,
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
