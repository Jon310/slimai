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


namespace SlimAI.Class.Shaman
{
    class ElementalShaman
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static ShamanSettings Settings { get { return GeneralSettings.Instance.Shaman(); } }

        private static async Task<bool> CombatCoroutine()
        {

                //Common.CreateInterruptBehavior(),
            await CoBURST(SlimAI.Burst);

                
                //Spell.WaitForCast(),
            await Spell.CoCast(Thunderstorm, Me.ManaPercent < 60 && TalentManager.HasGlyph("Thunderstorm"));

            await Spell.CoCast(SpiritWalkersGrace, Me.IsMoving && !SpellManager.Spells["Lava Burst"].Cooldown && SlimAI.Burst);

            await Spell.CoCast(EarthShock, Me.HasAura("Lightning Shield", 12));
            await Spell.CoCastOnGround(Earthquake, Me.CurrentTarget.Location, Unit.UnfriendlyUnitsNearTarget(8).Count() > 2 && SlimAI.AOE);
            await Spell.CoCast(UnleashFlame, TalentManager.IsSelected((int)ShamanTalents.UnleashedFury));
            await Spell.CoCast(LavaBurst);
            await Spell.CoCast(UnleashFlame, Me.IsMoving);
            await Spell.CoCast(FlameShock, Me.CurrentTarget.HasAuraExpired("Flame Shock", 9));
            await Spell.CoCast(ElementalBlast);
            await Spell.CoCast(SearingTotem, !Totems.ExistInRange(Me.CurrentTarget.Location, WoWTotem.Searing) && !Totems.ExistInRange(Me.CurrentTarget.Location, WoWTotem.FireElemental));
            await Spell.CoCast(ChainLightning, Unit.UnfriendlyUnitsNearTarget(10).Count() > 1 && SlimAI.AOE);
            await Spell.CoCast(SpiritWalkersGrace, Me.IsMoving && !SpellManager.Spells["Lava Burst"].Cooldown && SlimAI.Burst);
            await Spell.CoCast(LightningBolt, !SpellManager.CanCast(LavaBurst)/*!Me.HasAura(EchooftheElements) || !Me.HasAura(LavaSurge)*/);


            return false;
        }

       [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanElemental)]
        public static Composite CoElementalCombat()
        {
            return new ActionRunCoroutine(ctx => CombatCoroutine());
        }

        #region Precombat Buff Coroutine
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Shaman, WoWSpec.ShamanElemental)]
        public static Composite ElementalPreCombatBuffs()
        {
            return new ActionRunCoroutine(ctx => PreCombatBuffCoroutine());
        }

        private static async Task<bool> PreCombatBuffCoroutine()
        {
            if (Me.Mounted)
                return false;


            await Spell.CoCast(LightningShield, !Me.HasAura("Lightning Shield"));

            return false;
        }
        #endregion

        #region Burst
        private static async Task<bool> CoBURST(bool reqs)
        {
            if (!reqs)
                return false;

            await Spell.CoCast(ElementalMastery);
            await Spell.CoCast(FlameShock, Me.CurrentTarget.HasAuraExpired("Flame Shock", 9) && Me.HasAura(Ascendance));
            await Spell.CoCast(FireElementalTotem);
            //await Spell.CoCast(EarthElementalTotem, Spell.GetSpellCooldown("Fire Elemental Totem").TotalSeconds > 61);
            await Spell.CoCast(Ascendance, Me.CurrentTarget.GetAuraTimeLeft("Flame Shock").TotalSeconds > 18);
            return false;
        }
        #endregion

        private static Composite AOE()
        {
            return new PrioritySelector(
                new Decorator(ret => Unit.UnfriendlyUnitsNearTarget(10).Count() > 5 && TalentManager.HasGlyph("Thunderstorm"),
                    Spell.Cast(Thunderstorm)),
                Spell.Cast(ChainLightning));
        }

        #region Flame Shock Target
        private static WoWUnit FlameShockTar
        {
            get
            {
                if (Unit.UnfriendlyUnitsNearTargetFacing(10).Count() < 2)
                    return Me.CurrentTarget;
                if (Unit.UnfriendlyUnitsNearTargetFacing(10).Count().Between(2, 3))
                {
                    var besttar = (from unit in ObjectManager.GetObjectsOfType<WoWUnit>(false)
                                   where unit.IsAlive
                                   where unit.IsTargetingMyPartyMember || unit.IsTargetingMyRaidMember
                                   where unit.HasAuraExpired("Flame Shock")
                                   where unit.InLineOfSight
                                   select unit).FirstOrDefault();
                    return besttar;
                }
                return null;
            }
        }
        #endregion

        //#region Imbue
        //private static Decorator CreateShamanImbueMainHandBehavior(params Imbue[] imbueList)
        //{
        //    return new Decorator(ret => CanImbue(Me.Inventory.Equipped.MainHand),
        //        new PrioritySelector(
        //            imb => imbueList.FirstOrDefault(i => SpellManager.HasSpell(i.ToString() + " Weapon")),

        //            new Decorator(
        //                ret => Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id != (int)ret
        //                    && SpellManager.HasSpell(((Imbue)ret).ToString() + " Weapon")
        //                    && SpellManager.CanCast(((Imbue)ret).ToString() + " Weapon", null, false, false),
        //                new Sequence(
        //                    new Action(ret => Lua.DoString("CancelItemTempEnchantment(1)")),
        //                    new WaitContinue(1,
        //                        ret => Me.Inventory.Equipped.MainHand != null && (Imbue)Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id == Imbue.None,
        //                        new ActionAlwaysSucceed()),
        //                    new DecoratorContinue(ret => ((Imbue)ret) != Imbue.None,
        //                        new Sequence(
        //                            new Action(ret => SpellManager.Cast(((Imbue)ret).ToString() + " Weapon", null)),
        //                            new Action(ret => SetNextAllowedImbueTime())
        //                            )
        //                        )
        //                    )
        //                )
        //            )
        //        );
        //}

        //private static DateTime nextImbueAllowed = DateTime.Now;

        //public static bool CanImbue(WoWItem item)
        //{
        //    if (item != null && item.ItemInfo.IsWeapon)
        //    {
        //        // during combat, only mess with imbues if they are missing
        //        if (Me.Combat && item.TemporaryEnchantment.Id != 0)
        //            return false;

        //        // check if enough time has passed since last imbue
        //        // .. guards against detecting is missing immediately after a cast but before buff appears
        //        // .. (which results in imbue cast spam)
        //        if (nextImbueAllowed > DateTime.Now)
        //            return false;

        //        switch (item.ItemInfo.WeaponClass)
        //        {
        //            case WoWItemWeaponClass.Axe:
        //                return true;
        //            case WoWItemWeaponClass.AxeTwoHand:
        //                return true;
        //            case WoWItemWeaponClass.Dagger:
        //                return true;
        //            case WoWItemWeaponClass.Fist:
        //                return true;
        //            case WoWItemWeaponClass.Mace:
        //                return true;
        //            case WoWItemWeaponClass.MaceTwoHand:
        //                return true;
        //            case WoWItemWeaponClass.Polearm:
        //                return true;
        //            case WoWItemWeaponClass.Staff:
        //                return true;
        //            case WoWItemWeaponClass.Sword:
        //                return true;
        //            case WoWItemWeaponClass.SwordTwoHand:
        //                return true;
        //        }
        //    }

        //    return false;
        //}

        //public static void SetNextAllowedImbueTime()
        //{
        //    // 2 seconds to allow for 0.5 seconds plus latency for buff to appear
        //    nextImbueAllowed = DateTime.Now + new TimeSpan(0, 0, 0, 0, 500); // 1500 + (int) StyxWoW.WoWClient.Latency << 1);
        //}

        //private static Imbue GetImbue(WoWItem item)
        //{
        //    if (item != null)
        //        return (Imbue)item.TemporaryEnchantment.Id;

        //    return Imbue.None;
        //}

        //public static bool IsImbuedForDPS(WoWItem item)
        //{
        //    Imbue imb = GetImbue(item);
        //    return imb == Imbue.Flametongue;
        //}

        //public static bool IsImbuedForHealing(WoWItem item)
        //{
        //    return GetImbue(item) == Imbue.Earthliving;
        //}

        //private enum Imbue
        //{
        //    None = 0,
        //    Flametongue = 5,
        //    Earthliving = 3345,

        //}
        //#endregion Imbue

        #region ShamanTalents
        private enum ShamanTalents
        {
            NaturesGuardian = 1,
            StoneBulwarkTotem,
            AstralShift,
            FrozenPower,
            EarthgrabTotem,
            WindwalkTotem,
            CallOfTheElements,
            TotemicRestoration,
            TotemicProjection,
            ElementalMastery,
            AncestralSwiftness,
            EchoOfTheElements,
            HealingTideTotem,
            AncestralGuidance,
            Conductivity,
            UnleashedFury,
            PrimalElementalist,
            ElementalBlast
        }
        #endregion

        #region Shaman Spells
        private const int AncestralGuidance = 108281,
                          AncestralSwiftness = 16188,
                          Ascendance = 114049,
                          AstralShift = 108271,
                          ChainHeal = 1064,
                          ChainLightning = 421,
                          Earthquake = 61882,
                          EarthElementalTotem = 2062,
                          EchooftheElements = 159101,
                          EarthShield = 974,
                          EarthShock = 8042,
                          ElementalBlast = 117014,
                          ElementalMastery = 16166,
                          FeralSpirit = 51533,
                          FireElementalTotem = 2894,
                          FireNova = 1535,
                          FlameShock = 8050,
                          GreaterHealingWave = 77472,
                          HealingRain = 73920,
                          HealingStreamTotem = 5394,
                          HealingSurge = 8004,
                          HealingTideTotem = 108280,
                          HealingWave = 331,
                          LavaBurst = 51505,
                          LavaLash = 60103,
                          LavaSurge = 77756,
                          LightningBolt = 403,
                          LightningShield = 324,
                          ManaTideTotem = 16190,
                          Purge = 370,
                          Riptide = 61295,
                          SearingTotem = 3599,
                          ShamanisticRage = 30823,
                          SpiritLinkTotem = 98008,
                          SpiritWalkersGrace = 79206,
                          StoneBulwarkTotem = 108270,
                          StormlashTotem = 120668,
                          StormStrike = 17364,
                          Thunderstorm = 51490,
                          TotemicRecall = 36936,
                          UnleashFlame = 165462,
                          WaterShield = 52127,
                          WindwalkTotem = 108273;
        #endregion
    }
}
