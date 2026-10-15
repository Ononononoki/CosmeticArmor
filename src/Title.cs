using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CosmeticArmor
{
    // one "Cosmetic" title above the cosmetic group, in the strip Layout adds on top of ExtraSlots' panel
    internal static class Title
    {
        private const string TitleName = "CosmeticArmor_Title";
        internal const string Text = "Cosmetic";
        // the title's box: its bottom sits this far above the slots' top edge, and it is this tall, in px
        internal const float Lift = 1f;
        internal const float BoxHeight = 18f;

        private static readonly Vector3[] Corners = new Vector3[4];
        private static bool _failed;

        // where the title's bottom center goes, from the group's left, right and top edges
        internal static Vector2 Spot(float left, float right, float top) => new Vector2((left + right) / 2f, top + Lift);

        // runs every frame with the look tags
        internal static void Update(List<InventoryElement> elements)
        {
            if (_failed)
                return;
            try
            {
                var panel = Layout.Background;
                if (panel == null)
                    return;
                var title = panel.Find(TitleName);
                if (!Plugin.Enabled.Value || !Plugin.ShowTitle.Value || !Layout.GroupPlaced || !GroupEdges(panel, elements, out var left, out var right, out var top, out var font))
                {
                    if (title != null && title.gameObject.activeSelf)
                        title.gameObject.SetActive(false);
                    return;
                }
                if (title == null)
                    title = Create(panel, font);
                if (!title.gameObject.activeSelf)
                    title.gameObject.SetActive(true);
                // placed in the panel's own space, so it follows the panel when ExtraSlots moves it
                var rect = (RectTransform)title;
                rect.anchorMin = rect.anchorMax = panel.pivot;
                rect.sizeDelta = new Vector2(right - left, BoxHeight);
                rect.anchoredPosition = Spot(left, right, top);
                title.SetAsLastSibling();
            }
            catch (Exception ex)
            {
                // a broken title must never break the inventory, so it switches itself off
                _failed = true;
                Plugin.Log.LogError($"cosmetic title switched off: {ex}");
            }
        }

        // the cosmetic slots' outer edges in the panel's space, from their shown elements
        private static bool GroupEdges(RectTransform panel, List<InventoryElement> elements, out float left, out float right, out float top, out TMP_FontAsset font)
        {
            left = float.MaxValue;
            right = float.MinValue;
            top = float.MinValue;
            font = null;
            bool any = false;
            foreach (var part in CosmeticSlots.Parts)
            {
                var slot = ExtraSlots.API.FindSlot(CosmeticSlots.SlotId(part));
                if (slot == null || !slot.IsActive)
                    continue;
                var element = elements.Find(e => e != null && e.Position == slot.GridPosition);
                if (element == null || !element.gameObject.activeInHierarchy)
                    continue;
                ((RectTransform)element.transform).GetWorldCorners(Corners);
                foreach (var corner in Corners)
                {
                    var p = panel.InverseTransformPoint(corner);
                    left = Mathf.Min(left, p.x);
                    right = Mathf.Max(right, p.x);
                    top = Mathf.Max(top, p.y);
                }
                if (font == null && element.m_quality != null)
                    font = element.m_quality.font;
                any = true;
            }
            return any;
        }

        private static Transform Create(RectTransform panel, TMP_FontAsset font)
        {
            var go = new GameObject(TitleName, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(panel, false);
            rect.pivot = new Vector2(0.5f, 0f);
            // the game's font (taken from a slot's quality number), light gold like the Show tag
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
                tmp.font = font;
            tmp.text = Text;
            tmp.fontSize = 16f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.96f, 0.80f, 0.45f, 1f);
            tmp.alignment = TextAlignmentOptions.Bottom;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            // clicks go through to the panel (ExtraSlots drags the panel by its background)
            tmp.raycastTarget = false;
            return go.transform;
        }
    }
}
