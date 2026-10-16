using System;
using HarmonyLib;

namespace CosmeticArmor
{
    // ExtraSlots counts an item that fits any custom slot as a custom slot item: it then skips its extra utility slots for
    // that item and keeps it out of the regular equipment slots. our cosmetic slots fit every armor and utility item, so
    // every belt and wisplight became a custom slot item and only one utility item could be worn. this leaves our own
    // slots out of that check, items still go into the cosmetic slots through the slots' own filter
    internal static class CustomSlotItems
    {
        // true when a custom slot of another mod takes the item (replaced in the offline check)
        internal static Func<ItemDrop.ItemData, bool> FitsOtherCustomSlot = item =>
        {
            foreach (var slot in ExtraSlots.Slots.GetCustomSlots(true))
                if (slot != null && !Layout.IsOurs(slot.ID) && slot.ItemFits(item)) return true;
            return false;
        };

        private static bool _changedOnce;

        internal static void Patch(Harmony harmony)
        {
            var target = AccessTools.Method("ExtraSlots.Slots:IsCustomSlotItem");
            if (target == null)
            {
                Plugin.Log.LogWarning("ExtraSlots.Slots.IsCustomSlotItem not found, extra utility slots may stay blocked by the cosmetic slots");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(CustomSlotItems), nameof(Postfix)));
            Plugin.Log.LogInfo("custom slot check: the cosmetic slots no longer count, extra utility slots work with them");
        }

        internal static void Postfix(ItemDrop.ItemData item, ref bool __result)
        {
            if (!__result || item?.m_shared == null || FitsOtherCustomSlot(item)) return;
            __result = false;
            if (_changedOnce) return;
            _changedOnce = true;
            Plugin.Log?.LogInfo($"custom slot check: {item.m_shared.m_name} fits only a cosmetic slot, ExtraSlots treats it as normal equipment (first time this session)");
        }
    }
}
