# Changelog

Changes in this fork (aradix85/Sleeptalker), on top of Sleeptalker 0.9.0 by
EarthboundPromoter. Work branch: `fix/responsiveness`. Every change was verified in game
through a local dev bridge (injected key presses, spoken output read back with frame
numbers). The guiding rule: stay as close as possible to the game's own behaviour, driving
its designed controls and FSM events. Several fixes are shared with the same changes in
the Citizen Speaker fork.

## Unreleased

### Assist menu with cheats (F5), version 0.10.0
- **Contains cheats.** Optional and inert until used: the minimum die roll defaults to
  off, and nothing changes unless the player presses F5 and performs a row.
- F5 opens an assist menu in the help sheet's table grammar: Up and Down walk the rows,
  Enter performs a row, Space re-reads it, F5 or Backspace closes. It needs a loaded game
  (not on the title, the pause menu, a cycle transition or travel).
- Row 1, **Minimum die roll** (off, 2 to 6; Left/Right or Enter change it, saved as
  `[Assist] MinimumDieRoll` in the config). Every die rolls through a "Roll" or
  "Safe Roll" state whose RandomEvent picks one of six transitions named "1" to "6". A
  Harmony postfix on `RandomEvent.GetRandomEvent` swaps a face below the minimum for a
  uniform pick among the faces at or above it, so the game still runs its own face
  state, stress check and render. Verified live: 12 rerolls at minimum 4, all 4 or
  higher, no die damage.
- The other rows reproduce the developers' hidden DEBUG MENU (`Letterbox Canvas/DEBUG
  MENU`, inactive in release builds; its FSM decoded live). Raise every die to 6, reroll
  every die, repair all dice (`Die1..5_Health = 3`), fill energy (`Player_Energy = 100`),
  lower stress by 1, add 100 cryo, add 1 fuel, add 1 supplies. Dice reload through the
  die FSM's own global "Check Variables" event (the Load From Save path, no stress check);
  the debug menu's own repair sent that event to itself, so it never reloaded the dice.
  Dice actions refuse while the die picker or a slotted die owns the dice, then read the
  settled dice back. Fuel and supplies are capped by the rig's stowage, so their answer
  comes a beat later from the value that actually landed ("No room for more. Still 5.").
- F5 is handled before the gamepad-mode switch, so the first F5 after a load is no longer
  talked over by a fresh focus read. Opening F5 over the F1 sheet retires the sheet.

### "New" on visited locations
- Station and rig rows said "New" for every location on camera, also long after a
  visit. The New Shine object stays active on every drawn marker; the game shows or hides
  it with the Animator bool "New?" (the Location Button FSM sets it from the Lua flag
  `<ID>_OLD` in "Is New?" and clears it in "Set Old" on the first visit). The mod now
  reads that bool, so "New" matches what is drawn. Falls back to the old read when a
  marker has no such bool. Adds a reference to UnityEngine.AnimationModule.

### Die picker with no dice left
- When the die picker opens with every die spent or broken (tray and any rendered crew
  panel), it says "Die picker. No dice left." instead of leaving the player to walk the
  spent cursors. The game still opens the picker as it always does. Parity with the same
  fix in the Citizen Speaker fork.

### Keys during the opening splash
- The engine splash (the logo crawl before the title) runs for about 8 s and cannot be
  skipped; nothing is selectable. A key there said "Nothing to focus." (or nothing at all
  in the first 8 s), as if the game had hung. Any key now says "Loading, please wait.",
  read from SplashScreen.isFinished, which the title FSM's WaitForSplashToFinish waits
  on. F1, Z and F3 keep working. The key that launched the game, landing in the first
  frames, stays silent. Same change as in the Citizen Speaker fork.
- The title's start button says "Press Enter to start" ("Title screen. Press Enter to
  start."). Its label is "CLICK TO START" for the mouse and "PRESS [A] TO START" for a
  gamepad, and the glyph was dropped, so it read "CLICK TO START" or "PRESS TO START".
- The first arrow at the title no longer speaks the button twice: the game re-selects it
  when the input mode switches to gamepad, in the same frame as the dead-end repeat.
- Class select: a Left/Right press during the slide is no longer lost. The game ignores
  input mid-slide, so a quick second press spoke the same class again. The mod now
  speaks the class the press asks for and steps there with the carousel's own event once
  it rests.

### Faster answers to keys
- A dialogue line the player asked for (Enter on Continue, a choice by Enter or number)
  interrupts the line still being read. Tolk_IsSpeaking is unsupported for NVDA and always
  returns false, so Queued lines went straight into NVDA's own queue and the new line had
  to wait behind the old one.
- Key answers are Immediate: die picker open, close, refusal and slotted line; push
  refusal reasons and the confirm box; inventory open and close; character sheet entry and
  confirm; drive log tab names.
- Class select: Left/Right speak the destination at the key press from the carousel FSM
  (was after the slide plus 0.35 s), with "class N of 3"; edges say "First class." /
  "Last class."; a press mid-slide says where it lands.
- Station: Enter right after an arrow, while the camera is still panning, is held and
  pressed once the node is drawn, instead of "Off screen."
- Tutorials: the speech gate opens when the panel starts to fade after CONTINUE, not when
  the fade ends. A key's own Immediate answer is never held by a tutorial.

### New announcements
- The notification feed (new drive, drive complete or failed, new location, new crisis,
  new contract, new crew, contract concluded or failed) is spoken when a card shows.
- The title slot-overwrite warning and the pause quit warnings speak their own text.
- After loading a save or arriving from a journey, the place is spoken ("Hexport.").
- The station/rig swap is named ("Rig." / "Station.").
- After the player closes a tutorial and nothing else speaks, the screen is named.
- The drive log announces opening (tab, number of drives) and closing.
- The options panel is announced when it opens, not at the next key.
- The wake-up summary ends with the rig's clocks ("Clocks: ON THE RUN, 1 of 16."), so
  cycle clocks such as THE HUNT are heard as they move.
- The credits screen speaks its keys: Enter or Backspace skips (the SKIP button was
  gamepad-only), Space reads the credits.
- The DANGEROUS-run end screen ("THE BELT FADES...") is read.
- Item changes outside an action (conversation rewards, contract pay) are spoken from the
  inventory strip's own counts ("RAW SCRAP plus 2, now 2."); outcomes, pushes and the
  cycle transition keep voicing their own.
- Out-of-reach map rows give the game's own requirement words ("Debris field, HAZNAV
  required.", "Patrolled territory, SPOOF TRANSPONDER required.", "Radiation,
  impassable hazard.", "Blockade in effect.").

### Fixes
- Title menus: Backspace goes back (class select, difficulty, slot lists, overwrite
  warning, language) through the MAIN MENU FSM's own Back event.
- The queued-duplicate guard dropped any repeat of the last queued line forever.
- First Down no longer skips the first row (options, character sheet, drive log, station,
  map); the map's first row names its section.
- Drive log Abandon now works (the confirm closed the row and hid the button).
- A choice that ended a conversation no longer keeps Up/Down remapped.
- Back at the title, vitals and the cycle summary start clean for the next game.
- Tutorials: a destroyed panel could keep the speech gate shut (Unity fake null); the
  refocus line after a tutorial was dropped; a chained panel could say "Tutorial still
  open."; prompt text had a space before punctuation, a literal "\n", and "LB" read as
  the B prompt.
- Map: keys during a travel commit no longer reach the game's selection (a second Enter
  could commit twice).
- Push: a quick second P no longer says "not available"; a re-arm after a cancel speaks
  its box.
- Raw object names are no longer spoken for station markers ("Location Button"), the die
  slot on arrival, and the commit button the slotted line already named; a player die's
  "DICE SLOTTED" is not read as a crew verdict.
- Inventory: an end-of-list press keeps the item description.
- Cycle Count is read with Find* (Get* creates a missing variable).
- Enter at the press-start gate after a quit no longer says "Nothing focused."

### Performance
- FSM lookups scanned all ~5000 loaded FSM components on every mod key and on several
  per-frame dials; unique names are now cached and misses retried after 0.5 s.
- The drive log window search ran every frame where no window exists; now once a second.
- The options panel and drive log button are held references.
- The belt map rebuilt several times per key while fuel costs were unpublished; it now
  retries after 0.1 s.
