using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmeticArmor
{
    // a small button in the corner of each cosmetic slot that cycles what the part shows
    internal static class LookButtons
    {
        private const string ButtonName = "CosmeticArmor_Look";

        private static readonly AccessTools.FieldRef<InventoryGrid, List<InventoryElement>> ElementsRef =
            AccessTools.FieldRefAccess<InventoryGrid, List<InventoryElement>>("m_elements");

        private static bool _failed;

        internal static string Label(Look shown)
        {
            switch (shown)
            {
                case Look.Cosmetic: return "Show";
                case Look.Armor: return "Armor";
                default: return "Hide";
            }
        }

        internal static string Tooltip(Part part, Look shown)
        {
            string now;
            switch (shown)
            {
                case Look.Cosmetic: now = "shows the cosmetic item"; break;
                case Look.Armor: now = "shows your worn armor"; break;
                default: now = "shows nothing"; break;
            }
            return $"{CosmeticSlots.NameOf(part)}: {now}.\nClick to switch: Show (the cosmetic item), Armor (your worn armor), Hide (nothing).";
        }

        // the tag's background: gold while the cosmetic item shows, blue for the worn armor, red when hidden
        internal static Color ColorOf(Look shown)
        {
            switch (shown)
            {
                case Look.Cosmetic: return new Color(0.85f, 0.62f, 0.10f, 0.95f);
                case Look.Armor: return new Color(0.20f, 0.42f, 0.70f, 0.95f);
                default: return new Color(0.62f, 0.14f, 0.12f, 0.95f);
            }
        }

        // the item's icon is faded while its look is not on the character
        private const float FadedIcon = 0.35f;

        // runs after ExtraSlots has laid out its slots for this frame
        [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
        [HarmonyAfter(Plugin.ExtraSlotsGuid)]
        private static class AddButtons
        {
            private static void Postfix(InventoryGrid __instance)
            {
                if (InventoryGui.instance == null || __instance != InventoryGui.instance.m_playerGrid)
                    return;
                // the title hides itself when the mod is off, so it runs before that check
                Title.Update(ElementsRef(__instance));
                if (_failed || !Plugin.Enabled.Value)
                    return;
                try
                {
                    foreach (var part in CosmeticSlots.Parts)
                        UpdateButton(__instance, part);
                }
                catch (Exception ex)
                {
                    // a broken button must never break the inventory, so it switches itself off
                    _failed = true;
                    Plugin.Log.LogError($"look buttons switched off: {ex}");
                }
            }
        }

        // each slot's tag and its parts, found once, so the every-frame update looks nothing up and builds no text
        private class Tag
        {
            internal InventoryElement Element;
            internal Transform Button;
            internal TMP_Text Text;
            internal Image Background;
            internal UITooltip Tooltip;
            internal Look Shown;
        }

        private static readonly Tag[] Tags = new Tag[CosmeticSlots.Parts.Length];

        // the grid element at a slot's position, without a lambda (it runs every frame)
        internal static InventoryElement ElementAt(List<InventoryElement> elements, Vector2i position)
        {
            foreach (var element in elements)
                if (element != null && element.Position == position)
                    return element;
            return null;
        }

        private static void UpdateButton(InventoryGrid grid, Part part)
        {
            var slot = ExtraSlots.API.FindSlot(CosmeticSlots.SlotId(part));
            if (slot == null || !slot.IsActive)
                return;
            var element = ElementAt(ElementsRef(grid), slot.GridPosition);
            if (element == null)
                return;

            var tag = Tags[(int)part] ?? (Tags[(int)part] = new Tag());
            if (tag.Element != element || tag.Button == null)
            {
                var button = element.transform.Find(ButtonName);
                if (button == null)
                    button = Create(element, part);
                tag.Element = element;
                tag.Button = button;
                tag.Text = button.GetComponentInChildren<TMP_Text>(true);
                tag.Background = button.GetComponent<Image>();
                tag.Tooltip = button.GetComponent<UITooltip>();
                tag.Shown = (Look)(-1);
            }

            // label, color and hover text change only with the look
            var shown = CosmeticSlots.Shown(part);
            if (shown != tag.Shown)
            {
                tag.Shown = shown;
                if (tag.Text != null)
                    tag.Text.text = Label(shown);
                if (tag.Background != null)
                    tag.Background.color = ColorOf(shown);
                if (tag.Tooltip != null)
                    tag.Tooltip.m_text = Tooltip(part, shown);
            }
            // the grid redraws the icon every frame before this runs, so the fade never sticks to the item
            if (element.m_icon != null && element.m_icon.enabled && shown != Look.Cosmetic)
            {
                var c = element.m_icon.color;
                c.a = FadedIcon;
                element.m_icon.color = c;
            }
            tag.Button.SetAsLastSibling();
        }

        private static Transform Create(InventoryElement element, Part part)
        {
            // a tag across the slot's bottom edge (the top holds ExtraSlots' slot name and the quality number)
            var go = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(element.transform, false);
            rect.anchorMin = new Vector2(0.06f, 0f);
            rect.anchorMax = new Vector2(0.94f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 2f);
            rect.sizeDelta = new Vector2(0f, 17f);
            go.GetComponent<Button>().onClick.AddListener(() => CosmeticSlots.Cycle(part));

            // its own text in the game's font (taken from the slot's quality number), white and bold
            var label = new GameObject("Label", typeof(RectTransform));
            // made inactive so the text wakes up with the game's font already set, not looking for TextMesh Pro's default one
            label.SetActive(false);
            var lr = (RectTransform)label.transform;
            lr.SetParent(rect, false);
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            if (element.m_quality != null)
                tmp.font = element.m_quality.font;
            tmp.fontSize = 13f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            label.SetActive(true);

            // the hover text copies the slot's own tooltip setup
            if (element.m_tooltip != null)
            {
                var tooltip = go.AddComponent<UITooltip>();
                tooltip.m_tooltipPrefab = element.m_tooltip.m_tooltipPrefab;
                tooltip.m_topic = "Cosmetic Armor";
            }

            go.transform.SetAsLastSibling();
            return go.transform;
        }
    }
}
