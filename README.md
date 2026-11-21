# Emerald Witch

Unity Mod Manager mod for Pathfinder: Wrath of the Righteous.

Adds the **Emerald Witch** archetype: a spontaneous witch, like Stigmatized Witch, with the Death patron fixed in place.

## Archetype

Emerald Witch uses the Stigmatized Witch spellbook (Charisma, spontaneous) and keeps the familiar. She does not choose a patron and she does not take an oracle curse. Death is granted at level 1. She does not pick a hex at levels 2, 6, 10, and 14.

- **Level 2 — Emerald Curse.** A toggle, on by default. Killing a humanoid with a necromancy spell, or with Channel Negative Energy (Damage Living), summons one undead as animate dead would. It is the witch's summon, so reanimation bonuses apply, and it lasts one minute per witch level. If the Lich's Repurpose claims the body, the curse does not.
- **Level 3 — Channel Negative Energy.** The same damage-living / heal-undead channel a Hex Channeler uses, scaling with witch level.
- **Level 4 — Simple Reanimation.** The flag the game uses to buff raised undead.
- **Level 8 — Improved Reanimation.**
- **Level 12 — Greater Reanimation.**

## Death patron

Death is not a choice. Emerald Witch receives it automatically. It adds these spells to her spells known, at the same witch levels other patrons teach a spell of that level:

Animate Dead, Lesser; Animate Dead; Death Ward; Create Undead; Control Undead (Command Undead, if that is the spell in this version of the game); Create Greater Undead; Energy Drain.

## Install

The build copies `EmeraldWitch.dll` and `Info.json` to the game's `Mods\EmeraldWitch` folder. Enable the mod in Unity Mod Manager and start a new character, or respec.
