using HarmonyLib;
using UnityEngine;

namespace CosmeticArmor
{
    // swaps the armor model the local player shows. the game sends the shown item hash through the player's
    // network data, so other players see the cosmetic look without the mod
    internal static class Looks
    {
        private static readonly AccessTools.FieldRef<Humanoid, VisEquipment> VisEquipmentRef =
            AccessTools.FieldRefAccess<Humanoid, VisEquipment>("m_visEquipment");

        private static bool IsLocal(VisEquipment vis)
        {
            var player = Player.m_localPlayer;
            return player != null && vis != null && VisEquipmentRef(player) == vis;
        }

        // the game's own hash for an item's model
        internal static int HashOf(ItemDrop.ItemData item) =>
            item?.m_dropPrefab == null ? 0 : item.m_dropPrefab.name.GetStableHashCode();

        // picks what a part shows. returns false when the worn piece stays as it is
        internal static bool Choose(Look shown, ItemDrop.ItemData cosmetic, ref int itemHash, ref int variant, ref int quality)
        {
            if (shown == Look.Hidden)
            {
                itemHash = 0;
                variant = 0;
                return true;
            }
            if (shown == Look.Cosmetic && cosmetic != null)
            {
                int hash = HashOf(cosmetic);
                if (hash == 0)
                    return false;
                itemHash = hash;
                variant = cosmetic.m_variant;
                quality = cosmetic.m_quality;
                return true;
            }
            return false;
        }

        private static void Apply(VisEquipment vis, Part part, ref int itemHash, ref int variant, ref int quality)
        {
            if (!Plugin.Enabled.Value || !IsLocal(vis))
                return;
            int before = itemHash;
            var shown = CosmeticSlots.Shown(part);
            if (Choose(shown, CosmeticSlots.ItemIn(part), ref itemHash, ref variant, ref quality)
                && Plugin.LogLooks.Value && itemHash != before)
                Plugin.Log.LogInfo($"{part} shows {(itemHash == 0 ? "nothing" : CosmeticSlots.ItemIn(part)?.m_dropPrefab?.name)} ({shown})");
        }

        // shows the current slots and looks at once, after a slot or a setting changed
        internal static void Refresh()
        {
            var player = Player.m_localPlayer;
            if (player == null)
                return;
            var vis = VisEquipmentRef(player);
            if (vis != null)
                Traverse.Create(player).Method("SetupVisEquipment", new[] { typeof(VisEquipment), typeof(bool) }).GetValue(vis, false);
        }

        [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetHelmetItem))]
        private static class SetHelmetItem
        {
            private static void Prefix(VisEquipment __instance, ref int itemHash)
            {
                int variant = 0, quality = 0;
                Apply(__instance, Part.Helmet, ref itemHash, ref variant, ref quality);
            }
        }

        [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetChestItem))]
        private static class SetChestItem
        {
            private static void Prefix(VisEquipment __instance, ref int itemHash)
            {
                int variant = 0, quality = 0;
                Apply(__instance, Part.Chest, ref itemHash, ref variant, ref quality);
            }
        }

        [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetLegItem))]
        private static class SetLegItem
        {
            private static void Prefix(VisEquipment __instance, ref int itemHash)
            {
                int variant = 0, quality = 0;
                Apply(__instance, Part.Legs, ref itemHash, ref variant, ref quality);
            }
        }

        // a cape's look can depend on its variant and quality, so the cosmetic cape brings its own
        [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetShoulderItem))]
        private static class SetShoulderItem
        {
            private static void Prefix(VisEquipment __instance, ref int itemHash, ref int variant, ref int quality)
            {
                Apply(__instance, Part.Cape, ref itemHash, ref variant, ref quality);
            }
        }

        [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetUtilityItem))]
        private static class SetUtilityItem
        {
            private static void Prefix(VisEquipment __instance, ref int itemHash)
            {
                int variant = 0, quality = 0;
                Apply(__instance, Part.Utility, ref itemHash, ref variant, ref quality);
            }
        }

        // the game passes the trinket by its prefab name, empty for none
        [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetTrinketItem))]
        private static class SetTrinketItem
        {
            private static void Prefix(VisEquipment __instance, ref string name)
            {
                if (!Plugin.Enabled.Value || !IsLocal(__instance))
                    return;
                string before = name;
                name = ChooseName(CosmeticSlots.Shown(Part.Trinket), CosmeticSlots.ItemIn(Part.Trinket), name);
                if (Plugin.LogLooks.Value && name != before)
                    Plugin.Log.LogInfo($"Trinket shows {(string.IsNullOrEmpty(name) ? "nothing" : name)} ({CosmeticSlots.Shown(Part.Trinket)})");
            }
        }

        // the trinket's name for what the part shows: nothing, the cosmetic item's prefab, or the worn one
        internal static string ChooseName(Look shown, ItemDrop.ItemData cosmetic, string worn)
        {
            if (shown == Look.Hidden)
                return "";
            if (shown == Look.Cosmetic && cosmetic?.m_dropPrefab != null)
                return cosmetic.m_dropPrefab.name;
            return worn;
        }

        // a change in the player's inventory (an item into or out of a cosmetic slot) updates the look
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class WatchInventory
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer)
                    return;
                var inventory = __instance.GetInventory();
                inventory.m_onChanged -= Refresh;
                inventory.m_onChanged += Refresh;
                Refresh();
            }
        }
    }
}
