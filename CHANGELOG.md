# Changelog

All notable changes to the Udon Stow System package.

## [1.2.0] - 2026-10-30

### Added
- Key groups on desktop: stow slots mapped to the same key form a group. Pressing the key shows the slot last stowed into or cycled to, and re-pressing within **Cycle Window Seconds** (1.2) steps to the next occupied slot. Stowing with the key while holding an item fills the first free slot in the group that fits it.
- **Display Linger Seconds** (0.8) on `PlayerStowPointManager`: a shown slot stays in front of the player for this long after its key is released, so you can tap the key and then click the item instead of holding the key. 0 keeps the old hold-only behaviour.
- `StowableManager` **Stow Icon** and **Stow Name**, with `GetStowIcon()`, `GetStowName()` and `SetStowDisplay(name, icon)` for owning systems such as pools (an icon set on the item itself wins), so HUDs can list stowed items.
- `PlayerStowPointManager.GetStowPointCount()`, `GetStowPoint(index)` and `GetActiveStow()`; `StowPoint.GetStowedPickup()` and `GetStowedManager()`. Read-only getters for HUDs.
- `StowLayoutSwitcher`: lets players pick between several stow layouts. Each layout is a complete `PlayerStowPointManager` hierarchy listed in **Layouts**; only the chosen one stays active. Stowed items move to free pockets of the new layout when they fit, otherwise they are let go, and the mirror choice carries over. Wire UI Buttons to `NextLayout` / `PreviousLayout`, or call `SelectLayout(index)`. Optional preview image, name and pocket-count labels. The choice is saved with VRChat Persistence (**Persist Choice**, PlayerData int key **Player Data Key**, `UdonStow_Layout`) and **Changed Event Targets** are sent `OnStowLayoutChanged`.
- `PlayerStowPointManager` **Layout Name** and **Preview Image** (with `GetLayoutName()` and `GetPreviewImage()`) for the layout switcher. A **Bake Preview** button under Preview Image renders one when the Udon Combat System is installed (greyed out otherwise).
- Setup tools, no scripting needed:
  - Right-click a VRC Pickup > **Make Stowable** adds a `StowableManager` and `StowablePickup` and wires them. It warns that the manager's object is shown and hidden as a whole, so it must belong to that pickup alone, and offers to use the pickup's parent (only if it holds nothing else) or create a new one.
  - **Add Stow Point** on the stow manager's inspector: pick Hips or Chest, Right / Left / Centre, a size and a shape (sphere or upright box), and it places a `PlayerStowSphere`, sizes it and adds it with the next free number key.
  - **Pocket Layout** (a foldout under Setup Tools): adds a ring or grid of pockets in one click. Choose the bone, size, **Shape** (a round sphere, or an **Upright Box**: a tall box that stays upright, sized from the size class or by hand, for magazines and grenades), **Pockets per row** (1-8), **Rows** (1-4), the **Centre angle** and **Arc width** of the ring (0 = straight ahead, 90 = right, -90 = left, 180 = behind), **Radius**, **Row spacing**, whether pockets **Face outward**, and the keys: one key for all (the pockets cycle on a single key), one per row, or one per pocket. Everything sits under one `Pocket Layout` object, so the whole set can be nudged together.
  - **Adjusting a layout after you make it:** Add Pocket Layout leaves the new `Pocket Layout` object selected, and its inspector has the same controls (size, shape, box size, pockets per row, rows, centre angle, arc width, radius, row spacing, face outward, keys). Changing any value rebuilds the pockets live, adding or removing pockets and their stow manager slots as needed. Keys you set by hand are kept until you change the key mode, the pocket count or the rows. **Mirror** flips the layout to the other side, **Detach** keeps the pockets as they are and removes the settings component so they can be moved by hand, and **Delete Layout** removes the pockets and their slots. The settings component (`StowPocketLayout`) does nothing at runtime and is stripped from builds.
  - **Remove Empty Slots** deletes slots whose stow point object is gone, for example after deleting a layout by hand.
  - **Mirror Left / Right** on the stow manager flips every stow point across the player's centre line (measured from the bone, so a moved Pocket Layout still mirrors correctly), turning a right-handed layout into a left-handed one and back. Undoable.
- **Runtime left / right handed layout for VR players.** `PlayerStowPointManager` has `SetMirrored(bool)`, `ToggleMirrored()`, `IsMirrored()` and the no-argument `MirrorOn()` / `MirrorOff()` for UI buttons. Flipping mirrors every stow point across the player's centre line in the frame of its bone, flipping again restores the layout, and stowed items follow. Desktop players are unaffected, since their stow points are shown in front of the camera rather than on the body.
  - The choice is saved with VRChat Persistence (**Persist Mirror**, default on) under the PlayerData bool key **Mirror Player Data Key** (`UdonStow_MirrorLayout`, true = mirrored) and restored when the player rejoins. Any other system can read it with `PlayerData.TryGetBool`.
  - **Mirror Event Targets** are sent **Mirror Changed Event Name** (`OnStowMirrorChanged`) whenever the state changes.
  - New `StowMirrorToggle` component: Interact toggles it, or wire a UI Toggle's On Value Changed to `OnToggleValueChanged`. It shows the current state with optional indicator objects and a UI Toggle, and hides itself for desktop players.

### Fixed
- Deleting a slot's array element with right-click in the stow manager's inspector left the stow point and key lists different lengths, which threw errors and made the inspector unusable. Each slot now has a **-** button that removes it from both lists, and a mismatch is padded instead of throwing.

### Changed
- `StowableManager.SetPickupEnabled(bool)` and `SetExternallyControlled()` are kept as deprecated aliases of `SetItemSpawned`, so older integrations still compile.

### Notes
- The Udon Combat System's `UCS_StowHud` uses these to draw a desktop hotbar of stowed items.
