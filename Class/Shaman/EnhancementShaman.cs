using System;
using System.Linq;
using System.Windows.Forms;
using SlimAI.Helpers;
using SlimAI.Managers;
using CommonBehaviors.Actions;
using SlimAI.Settings;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;

namespace SlimAI.Class.Shaman
{
    class EnhancementShaman
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static ShamanSettings Settings { get { return GeneralSettings.Instance.Shaman(); } }

        #region Buffs
        [Behavior(BehaviorType.PreCombatBuffs, WoWClass.Shaman, WoWSpec.ShamanEnhancement)]
        public static Composite EnhancementPreCombatBuffs()        
            {
                return new PrioritySelector(
                    Spell.Cast("Lightning Shield", ret => !StyxWoW.Me.HasAura("Lightning Shield")),
                    CreateShamanImbueMainHandBehavior(Imbue.Windfury, Imbue.Flametongue),
                    CreateShamanImbueOffHandBehavior(Imbue.Flametongue));
            }
        #endregion

        #region Combat
        [Behavior(BehaviorType.Combat, WoWClass.Shaman, WoWSpec.ShamanEnhancement)]
        public static Composite EnhancementCombat()
        {
            HealerManager.NeedHealTargeting = true;
            return new PrioritySelector(
                new Throttle(1,
                    new Action(context => ResetVariables())),
                new Decorator(ret => SlimAI.PvPRotation,
                    CreatePvP()),
                new Decorator(ret => !Me.Combat || Me.Mounted || !Me.GotTarget || !Me.CurrentTarget.IsAlive,
                    new ActionAlwaysSucceed()),
                Spell.Cast(HealingStreamTotem, ret => HealerManager.GetCountWithHealth(80) >= 1 && !Totems.Exist(WoWTotemType.Water)),
                Spell.Cast(HealingTideTotem, ret => HealerManager.GetCountWithHealth(55) > 4),
                Common.CreateInterruptBehavior(),
                new Action(ret => { Item.UseTrinkets(); return RunStatus.Failure; }),
                new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                new Decorator(ret => SlimAI.Burst,
                    new PrioritySelector(
                        Spell.Cast(StormlashTotem, ret => !Me.HasAura("Stormlash Totem")),
                        Spell.Cast(ElementalMastery),
                        Spell.Cast(FireElementalTotem),
                        Spell.Cast(FeralSpirit),
                        Spell.Cast(Ascendance, ret => !Me.HasAura("Ascendance")))),
                //StormBlast
                new Decorator(ret => (Me.HasAura("Ascendance") && !WoWSpell.FromId(115356).Cooldown),
                    new Action(ret => Lua.DoString("RunMacroText('/cast Stormblast')"))),
                //new Decorator(ret => Unit.UnfriendlyUnits(10).Count() >= 3,
                //    CreateAoe()),
                Spell.Cast("Magma Totem", ret => SlimAI.AOE && Me.GotTarget && Me.CurrentTarget.SpellDistance() < Totems.GetTotemRange(WoWTotem.Magma) - 2f && !Totems.Exist(WoWTotemType.Fire) && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 6),
                new Throttle(2,
                    Spell.Cast(SearingTotem, ret => Me.GotTarget && Me.CurrentTarget.SpellDistance() <Totems.GetTotemRange(WoWTotem.Searing) - 2f && !Totems.Exist(WoWTotemType.Fire))),
                Spell.Cast(UnleashedElements, ret => SpellManager.HasSpell("Unleashed Fury")),
                Spell.Cast(ElementalBlast, ret => Me.HasAura("Maelstrom Weapon", 1)),
                new Throttle(2,
                    new Decorator(ret => Me.HasAura("Maelstrom Weapon", 5),
                        new PrioritySelector(
                            Spell.Cast(ChainLightning, ret => SlimAI.AOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3),
                            Spell.Cast(LightningBolt)))),
                Spell.Cast(StormStrike),
                Spell.Cast(FlameShock, ret => Me.HasAura("Unleash Flame") && !Me.CurrentTarget.HasMyAura("Flame Shock")),
                Spell.Cast(LavaLash),
                Spell.Cast("Fire Nova", ret => SlimAI.AOE && Me.CurrentTarget.HasAura("Flame Shock") && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 2),
                Spell.Cast(FlameShock, ret => (Me.HasAura("Unleash Flame") && Me.CurrentTarget.GetAuraTimeLeft("Flame Shock").TotalSeconds < 10) || !Me.CurrentTarget.HasMyAura("Flame Shock")),
                Spell.Cast(UnleashedElements),
                new Throttle(2,
                    new Decorator(ret => Me.HasAura("Maelstrom Weapon", 3) && !Me.HasAura("Ascendance"),
                        new PrioritySelector(
                            Spell.Cast(ChainLightning, ret => SlimAI.AOE && Unit.UnfriendlyUnitsNearTarget(10f).Count() >= 3),
                            Spell.Cast(LightningBolt)))),
                Spell.Cast(AncestralSwiftness, ret => !Me.HasAura("Maelstrom Weapon")),
                Spell.Cast(LightningBolt, ret => Me.HasAura("Ancestral Swiftness")),
                Spell.Cast(EarthShock),
                Spell.Cast(EarthElementalTotem, ret => SlimAI.Burst && SpellManager.Spells["Fire Elemental Totem"].CooldownTimeLeft.Seconds >= 50));
            }
        
        #endregion

        #region PvP
        private static Composite CreatePvP()
        {
            return new PrioritySelector(
                HexFocus(),
                TotemicProjection(),
                PurgeBubbles(),
                Spell.Cast("Cleanse Spirit", on => CleanseHex),
                new Decorator(retd => Me.CurrentTarget.HasAnyAura("Ice Block", "Hand of Protection", "Divine Shield") || !Me.Combat || Me.Mounted,
                    new ActionAlwaysSucceed()),
                Spell.Cast(HealingStreamTotem, ret => HealerManager.GetCountWithHealth(80) >= 1 && !Totems.Exist(WoWTotemType.Water)),
                Spell.Cast(HealingTideTotem, ret => HealerManager.GetCountWithHealth(35) >= 1),
                new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                new Decorator(ret => SlimAI.Burst,
                    new PrioritySelector(
                        Spell.Cast(StormlashTotem, ret => !Me.HasAura("Stormlash Totem")),
                        Spell.Cast(ElementalMastery),
                        Spell.Cast(FireElementalTotem),
                        Spell.Cast(FeralSpirit),
                        Spell.Cast(Ascendance, ret => !Me.HasAura("Ascendance")))),
                //StormBlast
                new Decorator(ret => (Me.HasAura("Ascendance") && !WoWSpell.FromId(115356).Cooldown),
                    new Action(ret => Lua.DoString("RunMacroText('/cast Stormblast')"))),
                Spell.Cast(SearingTotem, ret => Me.GotTarget && Me.CurrentTarget.SpellDistance() <Totems.GetTotemRange(WoWTotem.Searing) - 2f && !Totems.Exist(WoWTotemType.Fire)),
                Spell.Cast(UnleashedElements, ret => SpellManager.HasSpell("Unleashed Fury")),
                new Throttle(2,
                    Spell.Cast(HealingSurge,
                        on => HStarPvP,
                        ret => Me.HasAura("Maelstrom Weapon", 5))),
                Spell.Cast(ElementalBlast, ret => Me.HasAura("Maelstrom Weapon", 5)),
                //Spell.Cast(LightningBolt, ret => Me.HasAura("Maelstrom Weapon", 5)),
                Spell.Cast(StormStrike),
                Spell.Cast(FrostShock, ret => Me.CurrentTarget.Distance > 10 && SpellManager.HasSpell("Frozen Power")),
                Spell.Cast(FlameShock, ret => Me.HasAura("Unleash Flame") && !Me.CurrentTarget.HasMyAura("Flame Shock")),
                Spell.Cast(LavaLash),
                Spell.Cast(FlameShock, ret => (Me.HasAura("Unleash Flame") && Me.CurrentTarget.GetAuraTimeLeft("Flame Shock").TotalSeconds < 10) || !Me.CurrentTarget.HasMyAura("Flame Shock")),
                Spell.Cast(UnleashedElements),
                Spell.Cast(AncestralSwiftness, ret => !Me.HasAura("Maelstrom Weapon")),
                Spell.Cast(LightningBolt, ret => Me.HasAura("Ancestral Swiftness")),
                Spell.Cast(EarthShock),
                Spell.Cast(EarthElementalTotem, ret => SlimAI.Burst && SpellManager.Spells["Fire Elemental Totem"].CooldownTimeLeft.Seconds >= 50),
                new ActionAlwaysSucceed());
        }
        #endregion

        private static Composite CreateAoe()
        {
            return new PrioritySelector(
                //actions.aoe=fire_nova,if=active_flame_shock>=4
                //actions.aoe+=/magma_totem,if=active_enemies>5&!totem.fire.active
                //actions.aoe+=/searing_totem,if=active_enemies<=5&!totem.fire.active
                //actions.aoe+=/lava_lash,if=dot.flame_shock.ticking
                //actions.aoe+=/chain_lightning,if=active_enemies>=2&buff.maelstrom_weapon.react>=3
                //actions.aoe+=/unleash_elements
                //actions.aoe+=/flame_shock,cycle_targets=1,if=!ticking
                //actions.aoe+=/stormblast
                //actions.aoe+=/fire_nova,if=active_flame_shock>=3
                //actions.aoe+=/chain_lightning,if=active_enemies>=2&buff.maelstrom_weapon.react>=1
                //actions.aoe+=/stormstrike
                //actions.aoe+=/earth_shock,if=active_enemies<4
                //actions.aoe+=/feral_spirit
                //actions.aoe+=/earth_elemental_totem,if=!active&cooldown.fire_elemental_totem.remains>=50
                //actions.aoe+=/spiritwalkers_grace,moving=1
                //actions.aoe+=/fire_nova,if=active_flame_shock>=1
                );
        }

        #region Uility
        private static Composite HexFocus()
        {
            return
                new Decorator(ret => SpellManager.CanCast(Hex) &&
                    KeyboardPolling.IsKeyDown(Keys.C),
                    new PrioritySelector(
                        Spell.Cast(AncestralSwiftness, ret => !Me.HasAura("Maelstrom Weapon", 5)),
                        Spell.Cast(Hex, on => Me.FocusedUnit))

                    );
        }

        private static Composite Tranq()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Tranquility") &&
                    KeyboardPolling.IsKeyDown(Keys.Z),
                    new PrioritySelector(
                        Spell.Cast("Heart of the Wild", ret => SpellManager.HasSpell("Heart of the Wild") && SpellManager.CanCast("Heart of the Wild")),
                        Spell.Cast("Tranquility"))
                //new Action(ret => Spell.Cast(Paralysis, on => Me.FocusedUnit))
                    );
        }

        private static WoWUnit CleanseHex
        {
            get
            {
                var CleanseTar = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWPlayer>()
                              where unit.IsAlive
                              where unit.IsInMyPartyOrRaid
                              where unit.Distance < 40
                              where unit.InLineOfSight
                              where unit.HasAura("Hex")
                              select unit).OrderByDescending(u => u.HealthPercent).FirstOrDefault();
                return CleanseTar;
            }
        }

        private static WoWUnit HStarPvP
        {
            get
            {
                var eHheal = (from unit in ObjectManager.GetObjectsOfTypeFast<WoWPlayer>()
                              where unit.IsAlive
                              where unit.IsInMyPartyOrRaid
                              where unit.Distance < 40
                              where unit.InLineOfSight
                              where unit.HealthPercent <= 80
                              select unit).OrderByDescending(u => u.HealthPercent).LastOrDefault();
                return eHheal;
            }
        }

        #region Purge
        static Composite PurgeBubbles()
        {
            return new Decorator(
                    ret => Me.CurrentTarget.IsPlayer &&
                          Me.CurrentTarget.HasAnyAura("Hand of Protection", "Presence of Mind") && Me.CurrentTarget.InLineOfSight,
                //Me.CurrentTarget.ActiveAuras.ContainsKey("Ice Block") ||
                //Me.CurrentTarget.ActiveAuras.ContainsKey("Hand of Protection") ||
                //Me.CurrentTarget.ActiveAuras.ContainsKey("Divine Shield")),
                    new PrioritySelector(
                        Spell.Cast(Purge)));
        }
        #endregion

        //private static WoWUnit Ground(WoWUnit target)
        //{
            
        //    if (target == null || !target.IsPlayer)
        //        if (target.IsCasting && (target.CastingSpell.Name == "Cyclone" ||
        //                                 target.CastingSpell.Name == "Chaos Bolt" ||
        //                                 target.CastingSpell.Name == "Dominated Mind" ||
        //                                 target.CastingSpell.Name == "Fear" ||
        //                                 target.CastingSpell.Name == "Hex" ||
        //                                 target.CastingSpell.Name == "Polymorph" ||
        //                                 target.CastingSpell.Name == "Repentance")) ;

        //    return Ground();
        //}

        #endregion

        private static RunStatus ResetVariables()
        {
            KeyboardPolling.IsKeyDown(Keys.Z);
            KeyboardPolling.IsKeyDown(Keys.C);
            return RunStatus.Failure;
        }

        private static Composite TotemicProjection()
        {
            return
                new Decorator(ret => SpellManager.CanCast("Totemic Projection") &&
                    Lua.GetReturnVal<bool>("return IsLeftAltKeyDown() and not GetCurrentKeyBoardFocus()", 0),
                    new Action(ret =>
                    {
                        SpellManager.Cast("Totemic Projection");
                        Lua.DoString("if SpellIsTargeting() then CameraOrSelectOrMoveStart() CameraOrSelectOrMoveStop() end");
                    }));
        }

        #region Imbue

        private enum Imbue
        {
            None = 0,
            Flametongue = 5,
            Windfury = 283,
        }

        private static Decorator CreateShamanImbueMainHandBehavior(params Imbue[] imbueList)
        {
            return new Decorator(ret => CanImbue(Me.Inventory.Equipped.MainHand),
                new PrioritySelector(
                    imb => imbueList.FirstOrDefault(i => SpellManager.HasSpell(i.ToString() + " Weapon")),

                    new Decorator(
                        ret => Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id != (int)ret
                            && SpellManager.HasSpell(((Imbue)ret).ToString() + " Weapon")
                            && SpellManager.CanCast(((Imbue)ret).ToString() + " Weapon", null, false, false),
                        new Sequence(
                            new Action(ret => Lua.DoString("CancelItemTempEnchantment(1)")),
                            new WaitContinue(1,
                                ret => Me.Inventory.Equipped.MainHand != null && (Imbue)Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id == Imbue.None,
                                new ActionAlwaysSucceed()),
                            new DecoratorContinue(ret => ((Imbue)ret) != Imbue.None,
                                new Sequence(
                                    new Action(ret => SpellManager.Cast(((Imbue)ret).ToString() + " Weapon", null)),
                                    new Action(ret => SetNextAllowedImbueTime())
                                    )
                                )
                            )
                        )
                    )
                );
        }

        private static Decorator CreateShamanImbueOffHandBehavior(params Imbue[] imbueList)
        {
            return new Decorator(ret => CanImbue(Me.Inventory.Equipped.OffHand),
                new PrioritySelector(
                    imb => imbueList.FirstOrDefault(i => SpellManager.HasSpell(i.ToString() + " Weapon")),

                    new Decorator(
                        ret => Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id != (int)ret
                            && SpellManager.HasSpell(((Imbue)ret).ToString() + " Weapon")
                            && SpellManager.CanCast(((Imbue)ret).ToString() + " Weapon", null, false, false),
                        new Sequence(
                           new Action(ret => Lua.DoString("CancelItemTempEnchantment(2)")),
                            new WaitContinue(1,
                                ret => Me.Inventory.Equipped.OffHand != null && (Imbue)Me.Inventory.Equipped.OffHand.TemporaryEnchantment.Id == Imbue.None,
                                new ActionAlwaysSucceed()),
                            new DecoratorContinue(ret => ((Imbue)ret) != Imbue.None,
                                new Sequence(
                                    new Action(ret => SpellManager.Cast(((Imbue)ret).ToString() + " Weapon", null)),
                                    new Action(ret => SetNextAllowedImbueTime())
                                    )
                                )
                            )
                        )
                    )
                );
        }

        private static DateTime nextImbueAllowed = DateTime.Now;

        public static bool CanImbue(WoWItem item)
        {
            if (item != null && item.ItemInfo.IsWeapon)
            {
                // during combat, only mess with imbues if they are missing
                if (Me.Combat && item.TemporaryEnchantment.Id != 0)
                    return false;

                // check if enough time has passed since last imbue
                // .. guards against detecting is missing immediately after a cast but before buff appears
                // .. (which results in imbue cast spam)
                if (nextImbueAllowed > DateTime.Now)
                    return false;

                switch (item.ItemInfo.WeaponClass)
                {
                    case WoWItemWeaponClass.Axe:
                        return true;
                    case WoWItemWeaponClass.AxeTwoHand:
                        return true;
                    case WoWItemWeaponClass.Dagger:
                        return true;
                    case WoWItemWeaponClass.Fist:
                        return true;
                    case WoWItemWeaponClass.Mace:
                        return true;
                    case WoWItemWeaponClass.MaceTwoHand:
                        return true;
                    case WoWItemWeaponClass.Polearm:
                        return true;
                    case WoWItemWeaponClass.Staff:
                        return true;
                    case WoWItemWeaponClass.Sword:
                        return true;
                    case WoWItemWeaponClass.SwordTwoHand:
                        return true;
                }
            }

            return false;
        }

        public static void SetNextAllowedImbueTime()
        {
            // 2 seconds to allow for 0.5 seconds plus latency for buff to appear
            nextImbueAllowed = DateTime.Now + new TimeSpan(0, 0, 0, 0, 500); // 1500 + (int) StyxWoW.WoWClient.Latency << 1);
        }

        //string ToSpellName(this Imbue i)
        //{
        //    return i.ToString() + " Weapon";
        //}

        private static Imbue GetImbue(WoWItem item)
        {
            if (item != null)
                return (Imbue)item.TemporaryEnchantment.Id;

            return Imbue.None;
        }

        public static bool IsImbuedForDPS(WoWItem item)
        {
            Imbue imb = GetImbue(item);
            return imb == Imbue.Flametongue;
        }

        #endregion

        #region ShamanTalents
        public enum ShamanTalents
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
                          EarthElementalTotem = 2062,
                          EarthShield = 974,
                          EarthShock = 8042,
                          ElementalBlast = 117014,
                          ElementalMastery = 16166,
                          FeralSpirit = 51533,
                          FireElementalTotem = 2894,
                          FireNova = 1535,
                          FlameShock = 8050,
                          FrostShock = 8056, 
                          HealingRain = 73920,
                          HealingStreamTotem = 5394,
                          HealingSurge = 8004,
                          HealingTideTotem = 108280,
                          HealingWave = 331,
                          Hex = 51514,
                          LavaBurst = 51505,
                          LavaLash = 60103,
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
                          UnleashedElements = 73680,
                          WaterShield = 52127,
                          WindwalkTotem = 108273;
        #endregion
    }
}
