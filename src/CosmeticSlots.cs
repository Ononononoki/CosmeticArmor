using System;
using System.Collections.Generic;
using System.Linq;

namespace CosmeticArmor
{
    // the armor parts a cosmetic slot can dress
    internal enum Part { Helmet, Chest, Legs, Cape, Utility, Trinket }

    // what a part shows: the cosmetic slot's item, the worn armor, or nothing
    internal enum Look { Cosmetic, Armor, Hidden }

    internal static class CosmeticSlots
    {
        internal static readonly Part[] Parts = { Part.Helmet, Part.Chest, Part.Legs, Part.Cape, Part.Utility, Part.Trinket };

        internal const string LooksKey = "CosmeticArmor_Looks";

        // where the slot items and the saved looks come from (the offline check swaps these)
        internal static Func<Part, ItemDrop.ItemData> SlotItem = part => ExtraSlots.API.FindSlot(SlotId(part))?.Item;
        internal static Func<Dictionary<string, string>> SavedData = () => Player.m_localPlayer?.m_customData;
        internal static Func<ItemDrop.ItemData, bool> IsWorn = item => Player.m_localPlayer?.IsItemEquiped(item) == true;

        internal static string SlotId(Part part) => "CosmeticArmor_" + part;

        internal static ItemDrop.ItemData.ItemType TypeOf(Part part)
        {
            switch (part)
            {
                case Part.Helmet: return ItemDrop.ItemData.ItemType.Helmet;
                case Part.Chest: return ItemDrop.ItemData.ItemType.Chest;
                case Part.Legs: return ItemDrop.ItemData.ItemType.Legs;
                case Part.Utility: return ItemDrop.ItemData.ItemType.Utility;
                case Part.Trinket: return ItemDrop.ItemData.ItemType.Trinket;
                default: return ItemDrop.ItemData.ItemType.Shoulder;
            }
        }

        // the name ExtraSlots writes on the slot: short in the cosmetic group (the title says "Cosmetic"),
        // the full name when the slots sit in ExtraSlots' armor grid next to the real armor slots
        internal static string SlotName(Part part) => Layout.GroupPlaced ? part.ToString() : NameOf(part);

        internal static string NameOf(Part part)
        {
            switch (part)
            {
                case Part.Helmet: return "Cosmetic helmet";
                case Part.Chest: return "Cosmetic chest";
                case Part.Legs: return "Cosmetic legs";
                case Part.Utility: return "Cosmetic utility";
                case Part.Trinket: return "Cosmetic trinket";
                default: return "Cosmetic cape";
            }
        }

        // a piece fits its own cosmetic slot only while it is not worn, so worn armor keeps its normal slot
        internal static bool Fits(Part part, ItemDrop.ItemData item) =>
            item?.m_shared != null && item.m_shared.m_itemType == TypeOf(part) && !IsWorn(item);

        internal static void Register()
        {
            foreach (var part in Parts)
            {
                var p = part;
                if (!ExtraSlots.API.AddSlot(SlotId(p), () => SlotName(p), item => Fits(p, item), () => Plugin.Enabled.Value))
                    Plugin.Log.LogWarning($"ExtraSlots refused the slot {SlotId(p)}, check its free custom slots");
            }
        }

        internal static ItemDrop.ItemData ItemIn(Part part)
        {
            try { return SlotItem(part); }
            catch { return null; }
        }

        internal static bool IsCosmeticItem(ItemDrop.ItemData item) =>
            item != null && Parts.Any(p => ReferenceEquals(ItemIn(p), item));

        internal static List<ItemDrop.ItemData> CosmeticItems() =>
            Parts.Select(ItemIn).Where(i => i != null).ToList();

        // the chosen look per part, saved with the character as "0,0,0,0,0,0"
        internal static Look GetLook(Part part)
        {
            var data = SavedData();
            if (data == null || !data.TryGetValue(LooksKey, out var saved))
                return Look.Cosmetic;
            var parts = saved.Split(',');
            int i = (int)part;
            return i < parts.Length && int.TryParse(parts[i], out var v) && Enum.IsDefined(typeof(Look), v) ? (Look)v : Look.Cosmetic;
        }

        internal static void SetLook(Part part, Look look)
        {
            var data = SavedData();
            if (data == null)
                return;
            var looks = Parts.Select(GetLook).ToArray();
            looks[(int)part] = look;
            data[LooksKey] = string.Join(",", looks.Select(l => ((int)l).ToString()));
        }

        // what the part really shows: an empty slot can not show a cosmetic item, so it shows the armor
        internal static Look Shown(Part part) =>
            GetLook(part) == Look.Cosmetic && ItemIn(part) == null ? Look.Armor : GetLook(part);

        // the cycle button's order: cosmetic, armor, hidden. an empty slot only switches armor and hidden
        internal static Look Next(Look shown, bool hasItem)
        {
            switch (shown)
            {
                case Look.Cosmetic: return Look.Armor;
                case Look.Armor: return Look.Hidden;
                default: return hasItem ? Look.Cosmetic : Look.Armor;
            }
        }

        internal static void Cycle(Part part)
        {
            var next = Next(Shown(part), ItemIn(part) != null);
            SetLook(part, next);
            if (Plugin.LogLooks.Value)
                Plugin.Log.LogInfo($"{part} look set to {next}");
            Looks.Refresh();
        }
    }
}
