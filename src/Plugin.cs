using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace CosmeticArmor
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInDependency(ExtraSlotsGuid, BepInDependency.DependencyFlags.HardDependency)]
    // other look changers swap the same armor models, only one of them can win
    [BepInIncompatibility("Transmog")]
    [BepInIncompatibility("vanity")]
    [BepInIncompatibility("vktr.vanityoutfit")]
    [BepInIncompatibility("advize.Armoire")]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "onononoki.CosmeticArmor";
        public const string ModName = "CosmeticArmor";
        public const string ModVersion = "1.0.1";
        internal const string ExtraSlotsGuid = "shudnal.ExtraSlots";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> ToGrave;
        internal static ConfigEntry<bool> LogLooks;
        internal static ConfigEntry<bool> UseExtraSlotsLayout;
        internal static ConfigEntry<bool> ShowTitle;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Cosmetic slots change how your armor looks. Off shows your worn armor again and hides the slots (their items stay safe in your inventory).");
            ToGrave = Config.Bind("General", "CosmeticItemsToGrave", false,
                "Off: cosmetic items stay with you when you die. On: they go to your grave like everything else (anything that does not fit stays with you).");
            UseExtraSlotsLayout = Config.Bind("Layout", "UseExtraSlotsLayout", false,
                "Fallback. Off: the cosmetic slots sit in their own group to the right of the food slots. On: ExtraSlots places them itself, in the armor grid with its other slots (use this if the group looks wrong after an ExtraSlots update).");
            UseExtraSlotsLayout.SettingChanged += (_, __) => ExtraSlots.API.UpdateSlots();
            ShowTitle = Config.Bind("Layout", "ShowTitle", true,
                "Shows a \"Cosmetic\" title above the cosmetic group (the panel gets a little taller at the top). Off: no title, the panel keeps ExtraSlots' height.");
            ShowTitle.SettingChanged += (_, __) => ExtraSlots.API.UpdateSlots();
            LogLooks = Config.Bind("Debug", "LogLooks", false,
                "Writes a log line whenever a cosmetic slot changes what your character shows.");
            Enabled.SettingChanged += (_, __) =>
            {
                ExtraSlots.API.UpdateSlots();
                Looks.Refresh();
            };

            CosmeticSlots.Register();
            var harmony = new Harmony(ModGuid);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Layout.Patch(harmony);
            Log.LogInfo($"{ModName} {ModVersion} loaded");
        }
    }
}
