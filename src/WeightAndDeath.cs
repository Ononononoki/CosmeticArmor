using System;
using System.Collections.Generic;
using HarmonyLib;

namespace CosmeticArmor
{
    internal static class WeightAndDeath
    {
        // the local player's inventory (the offline check swaps this)
        internal static Func<Inventory> PlayerInventory = () => Player.m_localPlayer?.GetInventory();

        private static readonly AccessTools.FieldRef<Inventory, List<ItemDrop.ItemData>> ItemsRef =
            AccessTools.FieldRefAccess<Inventory, List<ItemDrop.ItemData>>("m_inventory");

        private static readonly Action<Inventory> Changed = inventory =>
            Traverse.Create(inventory).Method("Changed", new[] { typeof(bool), typeof(bool) }).GetValue(false, false);

        // cosmetic items weigh nothing. runs after ExtraSlots' own weight factor
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetWeight))]
        [HarmonyAfter(Plugin.ExtraSlotsGuid)]
        [HarmonyPriority(Priority.Last)]
        private static class NoWeight
        {
            private static void Postfix(ItemDrop.ItemData __instance, ref float __result)
            {
                if (__result != 0f && CosmeticSlots.IsCosmeticItem(__instance))
                    __result = 0f;
            }
        }

        // on death the cosmetic items stay with the character, or go to the grave when the config says so.
        // an item that did not make it into the grave comes back to the character, so nothing is ever lost
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveInventoryToGrave))]
        [HarmonyPriority(Priority.First)]
        private static class KeepOnDeath
        {
            private static void Prefix(Inventory original, ref List<ItemDrop.ItemData> __state)
            {
                if (original == null || !ReferenceEquals(original, PlayerInventory()))
                    return;
                var cosmetic = CosmeticSlots.CosmeticItems();
                __state = cosmetic;
                if (cosmetic.Count == 0 || Plugin.ToGrave.Value)
                    return;
                // held out of the move, put back right after it
                ItemsRef(original).RemoveAll(i => cosmetic.Contains(i));
            }

            private static void Postfix(Inventory __instance, Inventory original, List<ItemDrop.ItemData> __state)
            {
                if (__state == null || __state.Count == 0)
                    return;
                var items = ItemsRef(original);
                int back = 0;
                foreach (var item in __state)
                {
                    if (items.Contains(item) || (Plugin.ToGrave.Value && __instance.ContainsItem(item)))
                        continue;
                    items.Add(item);
                    back++;
                }
                if (back > 0)
                    Changed(original);
                Plugin.Log.LogInfo(Plugin.ToGrave.Value
                    ? $"death: {__state.Count - back} cosmetic item(s) went to the grave, {back} stayed with you"
                    : $"death: {back} cosmetic item(s) stayed with you");
            }
        }
    }
}
