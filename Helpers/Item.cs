using System;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using CommonBehaviors.Actions;
using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System.Collections.Generic;
using Action = Styx.TreeSharp.Action;
using Styx.Common;
using Styx.CommonBot;

namespace SlimAI.Helpers
{
    internal static class Item
    {
        private static LocalPlayer Me { get { return StyxWoW.Me; } } 

        public static bool HasItem(uint itemId)
        {
            return StyxWoW.Me.CarriedItems.Any(i => i.Entry == itemId);
        }

        public static bool HasWeaponImbue(WoWInventorySlot slot, string imbueName, int imbueId)
        {
            Logging.Write("Checking Weapon Imbue on " + slot + " for " + imbueName);
            //Logger.Write("Checking Weapon Imbue on " + slot + " for " + imbueName);
            var item = StyxWoW.Me.Inventory.Equipped.GetEquippedItem(slot);
            if (item == null)
            {
                Logging.Write("We have no " + slot + " equipped!");
                //Logger.Write("We have no " + slot + " equipped!");
                return true;
            }

            var enchant = item.TemporaryEnchantment;

            return enchant != null && (enchant.Name == imbueName || imbueId == enchant.Id);
        }


        /// <summary>
        ///  Creates a behavior to use an equipped item.
        /// </summary>
        /// <param name="slot"> The slot number of the equipped item. </param>
        /// <returns></returns>
        public static Composite UseEquippedItem(uint slot)
        {
            return new PrioritySelector(
                ctx => StyxWoW.Me.Inventory.GetItemBySlot(slot),
                new Decorator(
                    ctx => ctx != null && CanUseEquippedItem((WoWItem)ctx),
                    new Action(ctx => UseItem((WoWItem)ctx))
                    )
                );

        }

        public static Composite UsePotionAndHealthstone(double healthPercent)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => StyxWoW.Me.HealthPercent < healthPercent,
                    new PrioritySelector(
                            ctx => FindFirstUsableItemBySpell("Healthstone", "Life Spirit"),
                            new Decorator(
                                ret => ret != null,
                                new Sequence(
                                        new Action(ret => ((WoWItem)ret).UseContainerItem()))))));
        }

        #region Coroutine Healthstone Useage
        public static async Task<bool> CoUseHS(double healthPercent)
        {
            if (Me.HealthPercent < healthPercent && FindFirstUsableItemBySpell("Healthstone", "Life Spirit") != null)
            {
                await Coroutine.ExternalTask(Task.Run(() =>
                    FindFirstUsableItemBySpell("Healthstone", "Life Spirit").UseContainerItem()));
                return true;
            }

            return false;
        }
        #endregion

        public static void UseTrinkets()
        {
            WoWItem firstTrinket = StyxWoW.Me.Inventory.Equipped.Trinket1;
            WoWItem secondTrinket = StyxWoW.Me.Inventory.Equipped.Trinket2;

            if (firstTrinket != null && CanUseEquippedItem(firstTrinket))
                firstTrinket.Use();

            if (secondTrinket != null && CanUseEquippedItem(secondTrinket))
                secondTrinket.Use();
        }

        public static void UseHands()
        {
            WoWItem Gloves = StyxWoW.Me.Inventory.Equipped.Hands;

            if (Gloves != null && Me.Combat && CanUseEquippedItem(Gloves))
                Gloves.Use();
        }

        #region Coroutines UseHands
        public static async Task<bool> CoUseHands(bool reqs = true)
        {
            if (!reqs)
                return false;

            var gloves = StyxWoW.Me.Inventory.Equipped.Hands;
            if (gloves == null || !Me.Combat || !CanUseEquippedItem(gloves))
            {
                
                return false;
            }

            await Coroutine.ExternalTask(Task.Run(() => gloves.Use()));
            return true;
        }
        #endregion

        public static void UseWaist()
        {
            WoWItem waist = StyxWoW.Me.Inventory.Equipped.Waist;
            if (waist != null && Me.CurrentTarget != null && Me.Combat && CanUseEquippedItem(waist))
                waist.Use();
            var tpos = StyxWoW.Me.CurrentTarget.Location;
            SpellManager.ClickRemoteLocation(tpos);
        }

        /// <summary>
        ///  Creates a behavior to use an item, in your bags or paperdoll.
        /// </summary>
        /// <param name="id"> The entry of the item to be used. </param>
        /// <returns></returns>
        public static Composite UseItem(uint id)
        {
            return new PrioritySelector(
                ctx => ObjectManager.GetObjectsOfType<WoWItem>().FirstOrDefault(item => item.Entry == id),
                new Decorator(
                    ctx => ctx != null && CanUseItem((WoWItem)ctx),
                    new Action(ctx => UseItem((WoWItem)ctx))));
        }

        private static bool CanUseItem(WoWItem item)
        {
            return item.Usable && item.Cooldown == 0;
        }

        private static bool CanUseEquippedItem(WoWItem item)
        {
            // Check for engineering tinkers!
            string itemSpell = Lua.GetReturnVal<string>("return GetItemSpell(" + item.Entry + ")",0);
            if (string.IsNullOrEmpty(itemSpell))
                return false;

            return item.Usable && item.Cooldown == 0;
        }

        private static void UseItem(WoWItem item)
        {
            Logging.Write("Using item: " + item.Name);
            //Logger.Write( Color.DodgerBlue, "Using item: " + item.Name);
            item.Use();
        }

        /// <summary>
        ///  Checks for items in the bag, and returns the first item that has an usable spell from the specified string array.
        /// </summary>
        /// <param name="spellNames"> Array of spell names to be check.</param>
        /// <returns></returns>
        public static WoWItem FindFirstUsableItemBySpell(params string[] spellNames)
        {
            List<WoWItem> carried = StyxWoW.Me.CarriedItems;
            // Yes, this is a bit of a hack. But the cost of creating an object each call, is negated by the speed of the Contains from a hash set.
            // So take your optimization bitching elsewhere.
            var spellNameHashes = new HashSet<string>(spellNames);

            return (from i in carried
                    let spells = i.ItemSpells
                    where i.ItemInfo != null && spells != null && spells.Count != 0 &&
                          i.Usable &&
                          i.Cooldown == 0 &&
                          i.ItemInfo.RequiredLevel <= StyxWoW.Me.Level &&
                          spells.Any(s => s.IsValid && s.ActualSpell != null && spellNameHashes.Contains(s.ActualSpell.Name))
                    orderby i.ItemInfo.Level descending
                    select i).FirstOrDefault();
        }

        /// <summary>
        ///  Returns true if you have a wand equipped, false otherwise.
        /// </summary>
        public static bool HasWand
        {
            get
            {
                return StyxWoW.Me.Inventory.Equipped.Ranged != null &&
                       StyxWoW.Me.Inventory.Equipped.Ranged.ItemInfo.WeaponClass == WoWItemWeaponClass.Wand;
            }
        }

        /// <summary>
        ///   Creates a composite to use potions and healthstone.
        /// </summary>
        /// <param name = "healthPercent">Healthpercent to use health potions and healthstone</param>
        /// <param name = "manaPercent">Manapercent to use mana potions</param>
        /// <returns></returns>
        public static Composite CreateUsePotionAndHealthstone(double healthPercent, double manaPercent)
        {
            return new PrioritySelector(
                new Decorator(
                    ret => StyxWoW.Me.HealthPercent < healthPercent,
                    new PrioritySelector(
                        ctx => FindFirstUsableItemBySpell("Healthstone", "Healing Potion", "Life Spirit"),
                        new Decorator(
                            ret => ret != null,
                            new Sequence(
                                new Action(ret => Logging.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
                                    //Logger.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
                                new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                Helpers.Common.CreateWaitForLagDuration()))
                        )),
                new Decorator(
                    ret => Me.PowerType == WoWPowerType.Mana && StyxWoW.Me.ManaPercent < manaPercent,
                    new PrioritySelector(
                        ctx => FindFirstUsableItemBySpell("Restore Mana", "Water Spirit"),
                        new Decorator(
                            ret => ret != null,
                            new Sequence(
                                new Action(ret => Logging.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
                                new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                Helpers.Common.CreateWaitForLagDuration()))))
                );
        }


        public static Composite CreateUseAlchemyBuffsBehavior()
        {
            return new PrioritySelector(
                new Decorator(
                    ret =>
                    StyxWoW.Me.GetSkill(SkillLine.Alchemy).CurrentValue >= 400 &&
                    !StyxWoW.Me.Auras.Any(aura => aura.Key.StartsWith("Enhanced ") || aura.Key.StartsWith("Flask of ")), // don't try to use the flask if we already have or if we're using a better one
                    new PrioritySelector(
                        ctx => StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == 58149) ?? StyxWoW.Me.CarriedItems.FirstOrDefault(i => i.Entry == 47499),
                // Flask of Enhancement / Flask of the North
                        new Decorator(
                            ret => ret != null,
                            new Sequence(
                                new Action(ret => Logging.Write(String.Format("Using {0}", ((WoWItem)ret).Name))),
                                new Action(ret => ((WoWItem)ret).UseContainerItem()),
                                Helpers.Common.CreateWaitForLagDuration(stopIf => StyxWoW.Me.Auras.Any(aura => aura.Key.StartsWith("Enhanced ") || aura.Key.StartsWith("Flask of ")))
                                )
                            )
                        )
                    )
                );
        }

        public static bool RangedIsType(WoWItemWeaponClass wepType)
        {
            var ranged = StyxWoW.Me.Inventory.Equipped.Ranged;
            if (ranged != null && ranged.IsValid)
            {
                return ranged.ItemInfo != null && ranged.ItemInfo.WeaponClass == wepType;
            }
            return false;
        }

        public static uint CalcTotalGearScore()
        {
            uint totalItemLevel = 0;
            for (uint slot = 0; slot < Me.Inventory.Equipped.Slots; slot++)
            {
                WoWItem item = Me.Inventory.Equipped.GetItemBySlot(slot);
                if (item != null && IsItemImportantToGearScore(item))
                {
                    uint itemLvl = GetGearScore(item);
                    totalItemLevel += itemLvl;
                }
            }

            // double main hand score if have a 2H equipped
            if (GetInventoryType(Me.Inventory.Equipped.MainHand) == InventoryType.TwoHandWeapon)
                totalItemLevel += GetGearScore(Me.Inventory.Equipped.MainHand);

            return totalItemLevel;
        }

        private static uint GetGearScore(WoWItem item)
        {
            uint iLvl = 0;
            try
            {
                if (item != null)
                    iLvl = (uint)item.ItemInfo.Level;
            }
            catch
            {
                Logging.WriteDiagnostic("GearScore: ItemInfo not available for [0] #{1}", item.Name, item.Entry );
            }

            return iLvl;
        }

        private static InventoryType GetInventoryType(WoWItem item)
        {
            InventoryType typ = Styx.InventoryType.None;
            try
            {
                if (item != null)
                    typ = item.ItemInfo.InventoryType;
            }
            catch
            {
                Logging.WriteDiagnostic("InventoryType: ItemInfo not available for [0] #{1}", item.Name, item.Entry);
            }

            return typ;
        }

        private static bool IsItemImportantToGearScore(WoWItem item)
        {
            if (item != null && item.ItemInfo != null)
            {
                switch (item.ItemInfo.InventoryType)
                {
                    case InventoryType.Head:
                    case InventoryType.Neck:
                    case InventoryType.Shoulder:
                    case InventoryType.Cloak:
                    case InventoryType.Body:
                    case InventoryType.Chest:
                    case InventoryType.Robe:
                    case InventoryType.Wrist:
                    case InventoryType.Hand:
                    case InventoryType.Waist:
                    case InventoryType.Legs:
                    case InventoryType.Feet:
                    case InventoryType.Finger:
                    case InventoryType.Trinket:
                    case InventoryType.Relic:
                    case InventoryType.Ranged:
                    case InventoryType.Thrown:

                    case InventoryType.Holdable:
                    case InventoryType.Shield:
                    case InventoryType.TwoHandWeapon:
                    case InventoryType.Weapon:
                    case InventoryType.WeaponMainHand:
                    case InventoryType.WeaponOffHand:
                        return true;
                }
            }

            return false;
        }

        // public static bool Tier14TwoPieceBonus { get { return NumItemSetPieces(1144) >= 2; } }
        // public static bool Tier14FourPieceBonus { get { return NumItemSetPieces(1144) >= 4; } }

        private static int NumItemSetPieces( int setId)
        {
            // return StyxWoW.Me.CarriedItems.Count(i => i.ItemInfo.ItemSetId == setId);
            return Me.Inventory.Equipped.Items.Count(i => i != null && i.ItemInfo.ItemSetId == setId);
        }


        class SecondaryStats
        {
            public float MeleeHit { get; set; }
            public float SpellHit { get; set; }
            public float Expertise { get; set; }
            public float MeleeHaste { get; set; }
            public float SpellHaste { get; set; }
            public float SpellPen { get; set; }
            public float Mastery { get; set; }
            public float Crit { get; set; }
            public float Resilience { get; set; }
            public float PvpPower { get; set; }

            public SecondaryStats()
            {
                Refresh();
            }

            public void Refresh()
            {
                MeleeHit = Lua.GetReturnVal<float>("return GetCombatRating(CR_HIT_MELEE)", 0);
                SpellHit = Lua.GetReturnVal<float>("return GetCombatRating(CR_HIT_SPELL)", 0);
                Expertise = Lua.GetReturnVal<float>("return GetCombatRating(CR_EXPERTISE)", 0);
                MeleeHaste = Lua.GetReturnVal<float>("return GetCombatRating(CR_HASTE_MELEE)", 0);
                SpellHaste = Lua.GetReturnVal<float>("return GetCombatRating(CR_HASTE_SPELL)", 0);
                SpellPen = Lua.GetReturnVal<float>("return GetSpellPenetration()", 0);
                Mastery = Lua.GetReturnVal<float>("return GetCombatRating(CR_MASTERY)", 0);
                Crit = Lua.GetReturnVal<float>("return GetCritChance()", 0);               
                Resilience = Lua.GetReturnVal<float>("return GetCombatRating(COMBAT_RATING_RESILIENCE_CRIT_TAKEN)", 0);
                PvpPower = Lua.GetReturnVal<float>("return GetCombatRating(CR_PVP_POWER)", 0);
            }

        }

        private static WoWItem bandage = null;

        public static Composite CreateUseBandageBehavior()
        {
            return new Decorator( 

                ret => Me.HealthPercent < 95 && SpellManager.HasSpell( "First Aid") && !Me.HasAura( "Recently Bandaged") && !Me.ActiveAuras.Any( a => a.Value.IsHarmful ),

                new PrioritySelector(

                    new Action( ret => {
                        bandage = FindBestBandage();
                        return RunStatus.Failure;
                    }),

                    new Decorator(
                        ret => bandage != null && !Me.IsMoving,

                        new Sequence(
                            new Action(ret => UseItem(bandage)),
                            new WaitContinue( new TimeSpan(0,0,0,0,750), ret => Me.IsCasting || Me.IsChanneling, new ActionAlwaysSucceed()),
                            new WaitContinue(8, ret => (!Me.IsCasting && !Me.IsChanneling) || Me.HealthPercent > 99, new ActionAlwaysSucceed()),
                            new DecoratorContinue(
                                ret => Me.IsCasting || Me.IsChanneling,
                                new Sequence(
                                    new Action( r => Logging.Write( "/cancel First Aid @ {0:F0}%", Me.HealthPercent )),
                                    new Action( r => SpellManager.StopCasting() )
                                    )
                                )
                            )
                        )
                    )
                );
        }

        public static bool HasBandage()
        {
            return null != FindBestBandage();
        }

        public static WoWItem FindBestBandage()
        {
            return Me.CarriedItems
                .Where(b => b.ItemInfo.ItemClass == WoWItemClass.Consumable 
                    && b.ItemInfo.ContainerClass == WoWItemContainerClass.Bandage
                    && b.ItemInfo.RecipeClass == WoWItemRecipeClass.FirstAid
                    && Me.GetSkill(SkillLine.FirstAid).CurrentValue >= b.ItemInfo.RequiredSkillLevel
                    && CanUseItem(b))
                .OrderByDescending(b => b.ItemInfo.RequiredSkillLevel)
                .FirstOrDefault();
        }

    }
}
