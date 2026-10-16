using System;
using HarmonyLib;

namespace CosmeticArmor
{
    // ExtraSlots draws the items of its extra utility slots itself, so the Utility look reached only the first utility slot.
    // after ExtraSlots has set them up, Hide and a shown cosmetic utility item clear them too, Armor leaves them as worn
    internal static class ExtraUtilityLooks
    {
        // ExtraSlots keeps four extra utility visuals on every VisEquipment
        private const int ExtraVisuals = 4;
        private static bool _loggedOnce;

        // clears one extra utility visual (replaced in the offline check)
        internal static Action<VisEquipment, int> Clear = (vis, index) => ExtraSlots.VisEquipmentExtension.SetCustomItemState(vis, index, "");

        // Hide, or Cosmetic with an item in the cosmetic utility slot: only that item (or nothing) shows
        internal static bool HideExtras() => CosmeticSlots.Shown(Part.Utility) != Look.Armor;

        [HarmonyPatch(typeof(Humanoid), "SetupVisEquipment")]
        [HarmonyAfter(Plugin.ExtraSlotsGuid)]
        private static class AfterSetup
        {
            private static void Postfix(Humanoid __instance, VisEquipment visEq)
            {
                if (visEq == null || __instance == null || __instance != Player.m_localPlayer || !Plugin.Enabled.Value || !HideExtras()) return;
                for (int i = 0; i < ExtraVisuals; i++)
                    Clear(visEq, i);
                if (_loggedOnce && !Plugin.LogLooks.Value) return;
                _loggedOnce = true;
                Plugin.Log.LogInfo($"extra utility items hidden ({CosmeticSlots.Shown(Part.Utility)})");
            }
        }
    }
}
