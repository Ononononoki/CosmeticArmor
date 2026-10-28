# Changelog

## 1.0.3

- Fixed the font warnings of the Cosmetic title and the Show, Armor and Hide tags in the log. They look the same as before.

## 1.0.2

- Fixed ExtraSlots' extra utility slots. With the cosmetic slots installed, a second utility item (like a belt together with a Wisplight) could no longer be worn.
- The Utility tag now covers every utility item, also the ones in ExtraSlots' extra utility slots: Hide hides them all, a cosmetic utility item shows alone, Armor shows what you wear.
- Fixed the cosmetic group layout when ExtraSlots' extra utility slots are unlocked (it showed an empty middle row and a mixed up order).

## 1.0.1

- Lighter on the game. The cosmetic slots no longer read their settings again every frame, so less memory churn while playing and with the inventory open.

## 1.0.0

- First release.
- 6 cosmetic slots (helmet, chest, legs, cape, utility, trinket) as an ExtraSlots add-on, in their own group under a "Cosmetic" title next to the food slots.
- A colored tag per slot (Show, Armor, Hide) to show the cosmetic item, your worn armor or nothing.
- Cosmetic items weigh nothing and stay with you on death (config option for the grave).
- Other players see your look without the mod.
- Fallback settings: `Layout.ShowTitle` turns the title off, `Layout.UseExtraSlotsLayout` lets ExtraSlots place the slots itself.
