using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CosmeticArmor
{
    // moves the cosmetic slots out of ExtraSlots' armor grid into their own group on the far side of the
    // food slots, and widens ExtraSlots' panel to fit. runs only when ExtraSlots lays out its slots
    internal static class Layout
    {
        // a small gap between the food column and the cosmetic group, in tiles
        internal const float Gap = 0.2f;

        // the strip added on top of ExtraSlots' panel for the "Cosmetic" title, in px
        internal const float TitleHeight = 20f;

        // true while the cosmetic slots sit in their own group (the slots then use short names and get the title)
        internal static bool GroupPlaced;

        internal struct SlotPos
        {
            public string Id;
            public Vector2 Pos;
            public bool Equipment;
        }

        // ExtraSlots stores an add-on slot as "Custom" + its id (CustomSlot.GetSlotID), so both forms count
        internal static bool IsOurs(string id) => id != null && (id.StartsWith("CosmeticArmor_") || id.StartsWith("CustomCosmeticArmor_"));

        // the distance from one slot to the next, measured on ExtraSlots' own armor grid (the smallest step between
        // two slots in a row or a column), so the plan follows whatever spacing it uses. fallback: the given value
        internal static float Pitch(List<SlotPos> equipment, float fallback)
        {
            float best = float.MaxValue;
            for (int i = 0; i < equipment.Count; i++)
                for (int j = i + 1; j < equipment.Count; j++)
                {
                    var d = equipment[i].Pos - equipment[j].Pos;
                    float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y);
                    // slots side by side or one above the other
                    if (ax < 0.5f && ay > 0.5f) best = Mathf.Min(best, ay);
                    if (ay < 0.5f && ax > 0.5f) best = Mathf.Min(best, ax);
                }
            return best < float.MaxValue ? best : fallback;
        }

        // the new positions (by slot id) and how much wider the panel gets. pure, so the offline check can run it
        internal static Dictionary<string, Vector2> Plan(List<SlotPos> slots, float tile, out float widthDelta)
        {
            var moves = new Dictionary<string, Vector2>();
            widthDelta = 0f;
            var equipment = slots.Where(s => s.Equipment).ToList();
            var ours = equipment.Where(s => IsOurs(s.Id)).ToList();
            if (ours.Count == 0 || tile <= 0f)
                return moves;
            var regular = equipment.Where(s => !IsOurs(s.Id)).ToList();
            tile = Pitch(equipment, tile);
            float x0 = equipment.Min(s => s.Pos.x);
            int Col(Vector2 p) => Mathf.RoundToInt((p.x - x0) / tile);
            // fallback: an armor grid that is not in straight columns (ExtraSlots' staggered "middle" alignment)
            // keeps ExtraSlots' own layout
            if (equipment.Any(s => Mathf.Abs((s.Pos.x - x0) / tile - Col(s.Pos)) > 0.1f))
                return moves;

            int currentLast = equipment.Max(s => Col(s.Pos));
            // the columns still in use without our slots: the armor, and anything else that stays put under or
            // beside it (ExtraSlots puts quick slots in a row under the armor grid)
            var staying = regular.Concat(slots.Where(s => !s.Equipment && Col(s.Pos) <= currentLast)).ToList();
            int newLast = staying.Count > 0 ? staying.Max(s => Col(s.Pos)) : -1;
            int shift = Math.Max(0, currentLast - newLast);

            // the columns right of the armor grid (food, ammo, misc) close the gap our slots leave
            float right = x0 + newLast * tile;
            foreach (var s in slots.Where(s => !s.Equipment && Col(s.Pos) > currentLast))
            {
                var p = new Vector2(s.Pos.x - shift * tile, s.Pos.y);
                moves[s.Id] = p;
                right = Mathf.Max(right, p.x);
            }

            // rows: the first armor slot is the top of its column, the next slot in that column gives the step
            float top = equipment[0].Pos.y;
            var firstColumn = equipment.Where(s => Col(s.Pos) == Col(equipment[0].Pos)).ToList();
            float rowStep = firstColumn.Count > 1 ? firstColumn[1].Pos.y - firstColumn[0].Pos.y : -tile;

            // the group fills columns top to bottom with as many rows as ExtraSlots' armor grid has (at least 2)
            int rows = Math.Max(2, equipment.Select(s => Mathf.RoundToInt((s.Pos.y - top) / rowStep)).Distinct().Count());
            int columns = (ours.Count + rows - 1) / rows;
            float start = right + tile * (1f + Gap);
            for (int i = 0; i < ours.Count; i++)
                moves[ours[i].Id] = new Vector2(start + (i / rows) * tile, top + (i % rows) * rowStep);

            // fallback: if any two slots would end up on the same spot, nothing moves (ExtraSlots' own layout stays)
            var final = slots.Select(s => moves.TryGetValue(s.Id, out var p) ? p : s.Pos)
                .Select(p => (Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y))).ToList();
            if (final.Distinct().Count() != final.Count)
            {
                moves.Clear();
                return moves;
            }

            widthDelta = tile * (columns + Gap - shift);
            return moves;
        }

        // the panel's new size and center when it grows: the left and bottom edges stay, it grows right by width
        // and up by height. pure, so the offline check can run it
        internal static void Grow(Vector2 size, Vector2 pos, Vector2 pivot, float width, float height, out Vector2 newSize, out Vector2 newPos)
        {
            newSize = size + new Vector2(width, height);
            newPos = pos + new Vector2(width * pivot.x, height * pivot.y);
        }

        // ExtraSlots' panel, for the title (null before the inventory first opens)
        internal static RectTransform Background => _background?.GetValue(null) as RectTransform;

        private static float _widthDelta, _heightDelta;
        private static string _lastLog;
        // the slot's stored position, written directly: Slot.SetPosition would mark the panel dirty and
        // ExtraSlots would lay it out again, undoing the move every frame
        private static AccessTools.FieldRef<ExtraSlots.Slots.Slot, Vector2> _position;
        // ExtraSlots' slot pitch (its GetSlotPosition uses 70), only used when the grid has a single slot
        private const float DefaultPitch = 70f;
        private static FieldInfo _background;
        // the gamepad's selection frame around the panel (ExtraSlots sizes it like the panel)
        private static FieldInfo _selectedFrame;

        // patched by hand, so a future ExtraSlots without these members only loses the separate group
        internal static void Patch(Harmony harmony)
        {
            var panel = AccessTools.TypeByName("ExtraSlots.EquipmentPanel");
            var setSlots = panel == null ? null : AccessTools.Method(panel, "SetSlotsPositions");
            var updateBackground = panel == null ? null : AccessTools.Method(panel, "UpdateBackground");
            _background = panel == null ? null : AccessTools.Field(panel, "equipmentBackground");
            _selectedFrame = panel == null ? null : AccessTools.Field(panel, "selectedFrame");
            if (AccessTools.Field(typeof(ExtraSlots.Slots.Slot), "_position")?.FieldType == typeof(Vector2))
                _position = AccessTools.FieldRefAccess<ExtraSlots.Slots.Slot, Vector2>("_position");
            if (setSlots == null || updateBackground == null || _position == null || _background == null)
            {
                Plugin.Log.LogWarning("this ExtraSlots version has another panel layout, the cosmetic slots stay in the armor grid");
                return;
            }
            harmony.Patch(setSlots, postfix: new HarmonyMethod(typeof(Layout), nameof(AfterSlotsPositions)));
            harmony.Patch(updateBackground, postfix: new HarmonyMethod(typeof(Layout), nameof(AfterBackground)));
        }

        private static void AfterSlotsPositions()
        {
            _widthDelta = 0f;
            _heightDelta = 0f;
            GroupPlaced = false;
            if (!Plugin.Enabled.Value || Plugin.UseExtraSlotsLayout.Value)
            {
                ApplyWidth();
                return;
            }
            try
            {
                // the armor grid first (ExtraSlots' own order), then every other group that sits in the panel
                var equipment = ExtraSlots.API.GetEquipmentSlots().Where(s => s != null && s.IsActive).ToList();
                var others = ExtraSlots.API.GetFoodSlots().Concat(ExtraSlots.API.GetAmmoSlots())
                    .Concat(ExtraSlots.API.GetMiscSlots()).Concat(ExtraSlots.API.GetQuickSlots())
                    .Where(s => s != null && s.IsActive && !equipment.Contains(s)).Distinct().ToList();
                var slots = equipment.Concat(others).ToList();
                var plan = Plan(slots.Select(s => new SlotPos { Id = s.ID, Pos = s.Position, Equipment = equipment.Contains(s) }).ToList(),
                    DefaultPitch, out _widthDelta);
                foreach (var s in slots)
                    if (plan.TryGetValue(s.ID, out var p))
                        _position(s) = p;
                GroupPlaced = plan.Keys.Any(IsOurs);
                _heightDelta = GroupPlaced && Plugin.ShowTitle.Value ? TitleHeight : 0f;
                string line = $"layout: {equipment.Count} armor slots, {others.Count} others, moved {plan.Count}, panel {_widthDelta:+0;-0;0} px wider, {_heightDelta:0} px taller";
                if (Plugin.LogLooks.Value && line != _lastLog)
                    Plugin.Log.LogInfo(line);
                _lastLog = line;
                ApplyWidth();
            }
            catch (Exception ex)
            {
                _widthDelta = 0f;
                _heightDelta = 0f;
                GroupPlaced = false;
                ApplyWidth();
                Plugin.Log.LogWarning($"could not place the cosmetic group: {ex.Message}");
            }
        }

        // ExtraSlots sizes the panel just before it lays out the slots, so its size is kept here and the extra
        // width is applied both after the sizing and after the layout (whichever comes last wins, same result)
        private static Vector2 _baseSize, _basePos;
        private static bool _hasBase;

        private static void AfterBackground()
        {
            if (!(_background.GetValue(null) is RectTransform rt) || rt == null)
                return;
            _baseSize = rt.sizeDelta;
            _basePos = rt.anchoredPosition;
            _hasBase = true;
            ApplyWidth();
        }

        private static void ApplyWidth()
        {
            if (!_hasBase || !(_background.GetValue(null) is RectTransform rt) || rt == null)
                return;
            // the left and bottom edges stay where ExtraSlots put them, the title strip goes on top
            Grow(_baseSize, _basePos, rt.pivot, _widthDelta, _heightDelta, out var size, out var pos);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            // the gamepad's selection frame follows the panel, the way ExtraSlots sets it
            if (_selectedFrame?.GetValue(null) is RectTransform frame && frame != null)
            {
                frame.sizeDelta = size + Vector2.one * 26f;
                frame.anchoredPosition = pos;
            }
        }
    }
}
