using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics.Actions;
using UnityEngine;

namespace EmeraldWitch
{
    static class Content
    {
        const string ArchetypeGuid = "a7c3e1d94b2f4e6a8c0d1e2f3a4b5c6d";
        const string CurseGuid = "b7c3e1d94b2f4e6a8c0d1e2f3a4b5c6d";
        const string CurseBuffGuid = "e7c3e1d94b2f4e6a8c0d1e2f3a4b5c6d";
        const string CurseToggleGuid = "f7c3e1d94b2f4e6a8c0d1e2f3a4b5c6d";
        const string PatronGuid = "c7c3e1d94b2f4e6a8c0d1e2f3a4b5c6d";

        static readonly string[] PatronSpellGuids =
        {
            "d7c3e1d94b2f4e6a8c0d1e2f3a4b5c01",
            "d7c3e1d94b2f4e6a8c0d1e2f3a4b5c02",
            "d7c3e1d94b2f4e6a8c0d1e2f3a4b5c03",
            "d7c3e1d94b2f4e6a8c0d1e2f3a4b5c04",
            "d7c3e1d94b2f4e6a8c0d1e2f3a4b5c05",
            "d7c3e1d94b2f4e6a8c0d1e2f3a4b5c06",
            "d7c3e1d94b2f4e6a8c0d1e2f3a4b5c07"
        };

        public static BlueprintFeature CurseFeature { get; private set; }
        public static BlueprintBuff CurseBuff { get; private set; }
        public static BlueprintActivatableAbility CurseToggle { get; private set; }
        public static BlueprintCharacterClass WitchClass { get; private set; }
        public static BlueprintFeature ImprovedReanimation { get; private set; }
        public static BlueprintFeature GreaterReanimation { get; private set; }
        public static BlueprintBuff ImprovedReanimationBuff { get; private set; }
        public static BlueprintBuff GreaterReanimationBuff { get; private set; }
        public static BlueprintAbility ChannelDamage { get; private set; }
        public static BlueprintAbility HoldPerson { get; private set; }
        public static ContextActionSpawnMonster SpawnTemplate { get; private set; }
        public static BlueprintBuff[] RepurposeBuffs { get; private set; }

        static bool _created;

        public static void Create()
        {
            if (_created)
            {
                return;
            }

            try
            {
                Build();
                _created = true;
                Main.Log("Emerald Witch archetype is ready.");
            }
            catch (Exception ex)
            {
                Main.Log("Emerald Witch setup failed: " + ex);
            }
        }

        static void Build()
        {
            var witch = BlueprintUtil.Load<BlueprintCharacterClass>("1b9873f1e7bfe5449bc84d03e9c8e3cc");
            var accursed = BlueprintUtil.Load<BlueprintArchetype>("c5f6e53e71059fb4d802ce81a277a12d");
            var hexSelection = BlueprintUtil.Load<BlueprintFeature>("9846043cf51251a4897728ed6e24e76f");
            var channel = BlueprintUtil.Load<BlueprintFeature>("7c8d5e2ab326fdb4cabafc1c84a5c8e2");
            var simple = BlueprintUtil.Load<BlueprintFeature>("d45a818a74b842f3912adc22419b2760");
            var improved = BlueprintUtil.Load<BlueprintFeature>("855ad33f36d3421da7293d8cfd4a02ef");
            var greater = BlueprintUtil.Load<BlueprintFeature>("d538d0cfbd3a4f16bd2b1682c57e06f8");
            var patronSelection = BlueprintUtil.Load<BlueprintFeatureSelection>("381cf4c890815d049a4420c6f31d063f");
            var patronTemplate = BlueprintUtil.Load<BlueprintProgression>("b9c4e782706099f42a2ebc901acf492d");
            var animateDead = BlueprintUtil.Load<BlueprintAbility>("4b76d32feb089ad4499c3a1ce8e1ac27");
            var paralyzed = BlueprintUtil.Load<BlueprintBuff>("af1e2d232ebbb334aaf25e2a46a92591");
            HoldPerson = BlueprintUtil.Load<BlueprintAbility>("defbbeaef79eda64abc645036228a31b");
            ChannelDamage = BlueprintUtil.Load<BlueprintAbility>("fb2df4978dd4fd745a7aaecfd1068512");
            RepurposeBuffs = new[]
            {
                BlueprintUtil.Load<BlueprintBuff>("11ff9c86d6929ac4ebec3152c5df1ce0"),
                BlueprintUtil.Load<BlueprintBuff>("d5dde709de33cf647ae72699f4b56e57"),
                BlueprintUtil.Load<BlueprintBuff>("5e18ce2e21330e34690c372fbd9d6d60"),
                BlueprintUtil.Load<BlueprintBuff>("57785c3bf386461ea3d623d627314afa")
            };

            WitchClass = witch;
            ImprovedReanimation = improved;
            GreaterReanimation = greater;
            ImprovedReanimationBuff = BlueprintUtil.Load<BlueprintBuff>("40737d4c65c44ef98029074c33f88249");
            GreaterReanimationBuff = BlueprintUtil.Load<BlueprintBuff>("4ae00abbd3ad4043aad8429056391210");

            if (witch == null || accursed == null || animateDead == null || patronTemplate == null || patronSelection == null)
            {
                throw new InvalidOperationException("Required witch blueprints were not loaded.");
            }

            SpawnTemplate = FindSpawn(animateDead);
            if (SpawnTemplate == null)
            {
                throw new InvalidOperationException("Animate Dead has no summon action.");
            }

            try
            {
                CurseFeature = CreateCurse(paralyzed, animateDead);
            }
            catch (Exception ex)
            {
                Main.Log("Emerald Curse was not created: " + ex);
            }

            BlueprintProgression patron = null;
            try
            {
                patron = CreateDeathPatron(witch, patronTemplate, animateDead);
            }
            catch (Exception ex)
            {
                Main.Log("Death patron was not created: " + ex);
            }

            var archetype = CreateArchetype(accursed, witch, patronSelection, patron, hexSelection, channel, simple, improved, greater);
            AddArchetype(witch, archetype);
        }

        static ContextActionSpawnMonster FindSpawn(BlueprintAbility animateDead)
        {
            var effect = BlueprintUtil.Component<Kingmaker.UnitLogic.Abilities.Components.AbilityEffectRunAction>(animateDead);
            if (effect?.Actions?.Actions == null)
            {
                return null;
            }

            foreach (var action in effect.Actions.Actions)
            {
                if (action is ContextActionSpawnMonster spawn)
                {
                    return spawn;
                }
            }

            return null;
        }

        static BlueprintFeature CreateCurse(BlueprintBuff paralyzed, BlueprintAbility animateDead)
        {
            var feature = BlueprintUtil.Create<BlueprintFeature>();
            feature.name = "EmeraldCurseFeature";
            feature.AssetGuid = BlueprintGuid.Parse(CurseGuid);
            feature.IsClassFeature = true;
            BlueprintUtil.SetName(
                feature,
                "EmeraldWitch.Curse",
                "Emerald Curse",
                "Whenever you slay a humanoid with a necromancy spell, or with Channel Negative Energy (Damage Living), you raise one undead as if you had cast animate dead, except that only a single creature appears. The undead is your summon and lasts one minute per witch level. Emerald Curse can be turned off. A body claimed by the Lich's Repurpose is not raised by this curse.");
            BlueprintUtil.CopyIcon(paralyzed ?? (BlueprintUnitFact)animateDead, feature);
            AttachToggle(feature, paralyzed ?? (BlueprintUnitFact)animateDead);
            BlueprintUtil.Register(feature);
            return feature;
        }

        static void AttachToggle(BlueprintFeature feature, BlueprintUnitFact iconSource)
        {
            var buff = BlueprintUtil.Create<BlueprintBuff>();
            buff.name = "EmeraldCurseBuff";
            buff.AssetGuid = BlueprintGuid.Parse(CurseBuffGuid);
            BlueprintUtil.SetName(
                buff,
                "EmeraldWitch.CurseBuff",
                "Emerald Curse",
                "While this is on, killing a humanoid with a necromancy spell or with Channel Negative Energy (Damage Living) raises one undead. The undead is summoned by you.");
            BlueprintUtil.CopyIcon(iconSource, buff);
            BlueprintUtil.Set(buff, "Components", new BlueprintComponent[0]);
            BlueprintUtil.Register(buff);
            CurseBuff = buff;

            var toggle = BlueprintUtil.Create<BlueprintActivatableAbility>();
            toggle.name = "EmeraldCurseToggle";
            toggle.AssetGuid = BlueprintGuid.Parse(CurseToggleGuid);
            BlueprintUtil.SetName(
                toggle,
                "EmeraldWitch.CurseToggle",
                "Emerald Curse",
                "Turn the Emerald Curse on or off. While it is on, a humanoid slain by your necromancy, or by Channel Negative Energy (Damage Living), rises as one undead summon. It lasts one minute per witch level.");
            BlueprintUtil.CopyIcon(iconSource, toggle);
            BlueprintUtil.Set(toggle, "m_Buff", buff.ToReference<BlueprintBuffReference>());
            toggle.IsOnByDefault = true;
            toggle.DoNotTurnOffOnRest = true;
            toggle.ActivationType = AbilityActivationType.Immediately;
            toggle.DeactivateIfCombatEnded = false;
            toggle.OnlyInCombat = false;
            toggle.DeactivateIfOwnerDisabled = false;
            toggle.DeactivateIfOwnerUnconscious = false;
            BlueprintUtil.Set(toggle, "Components", new BlueprintComponent[0]);
            BlueprintUtil.Register(toggle);
            CurseToggle = toggle;

            var add = new AddFacts();
            add.name = "AddEmeraldCurseToggle";
            BlueprintUtil.Set(add, "m_Facts", new BlueprintUnitFactReference[] { toggle.ToReference<BlueprintUnitFactReference>() });
            BlueprintUtil.Set(add, "<OwnerBlueprint>k__BackingField", feature);
            BlueprintUtil.Set(feature, "Components", new BlueprintComponent[] { add });
        }

        static BlueprintProgression CreateDeathPatron(BlueprintCharacterClass witch, BlueprintProgression template, BlueprintAbility animateDead)
        {
            var lists = new[]
            {
                BlueprintUtil.Load<BlueprintSpellList>("ba0401fdeb4062f40a7aa95b6f07fe89"),
                BlueprintUtil.Load<BlueprintSpellList>("8443ce803d2d31347897a3d85cc32f53"),
                BlueprintUtil.Load<BlueprintSpellList>("7d5987082120bb943ac96cde7b2257ad"),
                BlueprintUtil.Load<BlueprintSpellList>("e17df9977b879b64e8a8cbb4b3569f19")
            }.Where(list => list != null).ToArray();

            var wanted = new[]
            {
                new SpellRequest("Animate Dead, Lesser", "AnimateDeadLesser", null),
                new SpellRequest("Animate Dead", "AnimateDead", "4b76d32feb089ad4499c3a1ce8e1ac27"),
                new SpellRequest("Death Ward", null, null),
                new SpellRequest("Create Undead", null, null),
                new SpellRequest("Control Undead", "CommandUndead", "0b101dd5618591e478f825f0eef155b4"),
                new SpellRequest("Create Greater Undead", "CreateUndeadGreaterBase", "8ba9b6e4df4c46a597154e2b8e7e6e4a"),
                new SpellRequest("Energy Drain", null, null)
            };

            var patron = BlueprintUtil.Create<BlueprintProgression>();
            patron.name = "WitchDeathPatronProgression";
            patron.AssetGuid = BlueprintGuid.Parse(PatronGuid);
            CopyProgressionShell(template, patron);
            patron.IsClassFeature = true;
            BlueprintUtil.SetName(
                patron,
                "EmeraldWitch.DeathPatron",
                "Death",
                "The patron of Death teaches the witch to cross the threshold of the grave. She adds animate dead, lesser animate dead, death ward, create undead, control undead, create greater undead, and energy drain to her spells known.");
            BlueprintUtil.CopyIcon(animateDead, patron);

            var entries = new List<LevelEntry>();
            for (var i = 0; i < wanted.Length; i++)
            {
                var request = wanted[i];
                var spell = FindSpell(lists, request);
                if (spell == null)
                {
                    Main.Log("Death patron could not find spell \"" + request.Display + "\".");
                    continue;
                }

                var spellLevel = SpellLevelOf(lists, spell);
                if (spellLevel <= 0)
                {
                    Main.Log("Death patron found " + spell.name + " but not its spell level.");
                    continue;
                }

                var classLevel = ClassLevelForSpell(template, spellLevel);
                var feature = CreatePatronSpell(witch, spell, spellLevel, PatronSpellGuids[i]);
                BlueprintUtil.AddAtLevel(entries, classLevel, feature);
                Main.Log("Death patron: " + spell.name + " (" + request.Display + ") at witch level " + classLevel + " as spell level " + spellLevel + ".");
            }

            patron.LevelEntries = entries.OrderBy(entry => entry.Level).ToArray();
            if (patron.UIGroups == null)
            {
                patron.UIGroups = new UIGroup[0];
            }

            BlueprintUtil.Register(patron);
            return patron;
        }

        static void CopyProgressionShell(BlueprintProgression from, BlueprintProgression to)
        {
            BlueprintUtil.Set(to, "m_Classes", AccessTools.Field(typeof(BlueprintProgression), "m_Classes").GetValue(from));
            BlueprintUtil.Set(to, "m_Archetypes", AccessTools.Field(typeof(BlueprintProgression), "m_Archetypes").GetValue(from));
            to.ForAllOtherClasses = from.ForAllOtherClasses;
            to.AlternateProgressionType = from.AlternateProgressionType;
            to.GiveFeaturesForPreviousLevels = from.GiveFeaturesForPreviousLevels;
        }

        static BlueprintFeature CreatePatronSpell(BlueprintCharacterClass witch, BlueprintAbility spell, int spellLevel, string guid)
        {
            var feature = BlueprintUtil.Create<BlueprintFeature>();
            feature.name = "WitchDeathPatronSpell" + spellLevel + spell.name;
            feature.AssetGuid = BlueprintGuid.Parse(guid);
            feature.IsClassFeature = true;
            feature.HideInCharacterSheetAndLevelUp = true;
            BlueprintUtil.SetName(feature, "EmeraldWitch.Patron." + spell.name, spell.Name, spell.Description);
            BlueprintUtil.CopyIcon(spell, feature);

            var add = new AddKnownSpell();
            add.SpellLevel = spellLevel;
            BlueprintUtil.Set(add, "m_CharacterClass", witch.ToReference<BlueprintCharacterClassReference>());
            BlueprintUtil.Set(add, "m_Spell", spell.ToReference<BlueprintAbilityReference>());
            BlueprintUtil.Set(feature, "Components", new BlueprintComponent[] { add });
            BlueprintUtil.Register(feature);
            return feature;
        }

        static int ClassLevelForSpell(BlueprintProgression template, int spellLevel)
        {
            foreach (var entry in template.LevelEntries)
            {
                foreach (var feature in entry.Features)
                {
                    var known = BlueprintUtil.Component<AddKnownSpell>(feature as BlueprintScriptableObject);
                    if (known != null && known.SpellLevel == spellLevel)
                    {
                        return entry.Level;
                    }
                }
            }

            return Math.Max(1, spellLevel * 2 - 1);
        }

        static int SpellLevelOf(BlueprintSpellList[] lists, BlueprintAbility spell)
        {
            foreach (var list in lists)
            {
                var level = list.GetLevel(spell);
                if (level > 0)
                {
                    return level;
                }

                if (spell.Parent != null)
                {
                    level = list.GetLevel(spell.Parent);
                    if (level > 0)
                    {
                        return level;
                    }
                }
            }

            var best = -1;
            foreach (var component in spell.ComponentsArray ?? new BlueprintComponent[0])
            {
                if (component is SpellListComponent spellList && spellList.SpellLevel > 0 && (best < 0 || spellList.SpellLevel < best))
                {
                    best = spellList.SpellLevel;
                }
            }

            return best;
        }

        static BlueprintAbility FindSpell(BlueprintSpellList[] lists, SpellRequest request)
        {
            if (!string.IsNullOrEmpty(request.Guid))
            {
                var direct = BlueprintUtil.Load<BlueprintAbility>(request.Guid);
                if (direct != null)
                {
                    return direct;
                }
            }

            BlueprintAbility byAsset = null;
            BlueprintAbility byName = null;
            BlueprintAbility commandFallback = null;
            foreach (var list in lists)
            {
                for (var level = 0; level <= 10; level++)
                {
                    var spells = list.GetSpells(level);
                    if (spells == null)
                    {
                        continue;
                    }

                    foreach (var spell in spells)
                    {
                        if (spell == null)
                        {
                            continue;
                        }

                        if (byAsset == null && request.Asset != null && spell.name == request.Asset)
                        {
                            byAsset = spell;
                        }

                        if (byName == null && string.Equals(spell.Name, request.Display, StringComparison.OrdinalIgnoreCase))
                        {
                            byName = spell;
                        }

                        if (request.Display == "Control Undead" && commandFallback == null && string.Equals(spell.Name, "Command Undead", StringComparison.OrdinalIgnoreCase))
                        {
                            commandFallback = spell;
                        }
                    }
                }
            }

            if (byName != null)
            {
                return byName;
            }

            if (byAsset != null)
            {
                return byAsset;
            }

            if (commandFallback != null)
            {
                Main.Log("Control Undead was not found. Death patron will teach Command Undead instead.");
                return commandFallback;
            }

            return null;
        }

        static BlueprintArchetype CreateArchetype(
            BlueprintArchetype accursed,
            BlueprintCharacterClass witch,
            BlueprintFeatureSelection patronSelection,
            BlueprintProgression deathPatron,
            BlueprintFeature hexSelection,
            BlueprintFeature channel,
            BlueprintFeature simple,
            BlueprintFeature improved,
            BlueprintFeature greater)
        {
            var archetype = BlueprintUtil.Create<BlueprintArchetype>();
            BlueprintUtil.CopyMergedFields(accursed, archetype);
            BlueprintUtil.Set(archetype, "m_ParentClass", witch);
            BlueprintUtil.Set(archetype, "m_HiddenInUI", false);
            archetype.name = "EmeraldWitchArchetype";
            archetype.AssetGuid = BlueprintGuid.Parse(ArchetypeGuid);
            archetype.LocalizedName = BlueprintUtil.MakeString("EmeraldWitch.Archetype.Name", "Emerald Witch");
            archetype.LocalizedDescription = BlueprintUtil.MakeString(
                "EmeraldWitch.Archetype.Desc",
                "A witch of a bloodline, one of a secret circle that remains hidden because its inborn magic is given to the most profound of sorceries: necromancy. Such witches live in secret and are rare, for the gift requires a bloodline and is not always touched by the patron of Death. Those who are touched command the domain of death as no other witch can.");
            archetype.LocalizedDescriptionShort = BlueprintUtil.MakeString(
                "EmeraldWitch.Archetype.Short",
                "A spontaneous witch bound to the patron of Death.");

            var added = BlueprintUtil.CloneEntries(accursed.AddFeatures);
            var removed = BlueprintUtil.CloneEntries(accursed.RemoveFeatures);
            DropFeature(added, "b0a5118b4fb793241bc7042464b23fab", "OracleCurse");
            if (patronSelection != null)
            {
                RemoveFeatureAtItsLevel(witch, patronSelection, removed);
            }

            if (deathPatron != null)
            {
                BlueprintUtil.AddAtLevel(added, 1, deathPatron);
            }

            if (CurseFeature != null)
            {
                BlueprintUtil.AddAtLevel(added, 2, CurseFeature);
            }
            if (channel != null)
            {
                BlueprintUtil.AddAtLevel(added, 3, channel);
            }

            if (simple != null)
            {
                BlueprintUtil.AddAtLevel(added, 4, simple);
            }

            if (improved != null)
            {
                BlueprintUtil.AddAtLevel(added, 8, improved);
            }

            if (greater != null)
            {
                BlueprintUtil.AddAtLevel(added, 12, greater);
            }

            RemoveHexes(witch, hexSelection, removed);
            archetype.AddFeatures = added.OrderBy(entry => entry.Level).ToArray();
            archetype.RemoveFeatures = removed.OrderBy(entry => entry.Level).ToArray();
            BlueprintUtil.Register(archetype);
            return archetype;
        }

        static void DropFeature(List<LevelEntry> entries, string guid, string namePart)
        {
            foreach (var entry in entries.ToList())
            {
                var kept = new List<BlueprintFeatureBase>();
                if (entry.Features != null)
                {
                    foreach (var feature in entry.Features)
                    {
                        if (feature == null)
                        {
                            continue;
                        }

                        var named = feature.name != null && namePart != null && feature.name.IndexOf(namePart, StringComparison.Ordinal) >= 0;
                        if (named || string.Equals(feature.AssetGuid.ToString(), guid, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        kept.Add(feature);
                    }
                }

                if (kept.Count == 0)
                {
                    entries.Remove(entry);
                }
                else
                {
                    entry.SetFeatures(kept);
                }
            }
        }

        static void RemoveFeatureAtItsLevel(BlueprintCharacterClass witch, BlueprintFeature feature, List<LevelEntry> removed)
        {
            var progression = witch.Progression;
            if (progression?.LevelEntries == null)
            {
                BlueprintUtil.AddAtLevel(removed, 1, feature);
                return;
            }

            var found = false;
            foreach (var entry in progression.LevelEntries)
            {
                if (entry.Features == null)
                {
                    continue;
                }

                foreach (var existing in entry.Features)
                {
                    if (existing != null && existing.AssetGuid == feature.AssetGuid)
                    {
                        BlueprintUtil.AddAtLevel(removed, entry.Level, feature);
                        found = true;
                    }
                }
            }

            if (!found)
            {
                BlueprintUtil.AddAtLevel(removed, 1, feature);
            }
        }

        static void RemoveHexes(BlueprintCharacterClass witch, BlueprintFeature hexSelection, List<LevelEntry> removed)
        {
            foreach (var level in new[] { 2, 6, 10, 14 })
            {
                if (hexSelection != null)
                {
                    BlueprintUtil.AddAtLevel(removed, level, hexSelection);
                }

                var entry = witch.Progression?.GetLevelEntry(level);
                if (entry == null)
                {
                    continue;
                }

                foreach (var feature in entry.Features)
                {
                    if (feature != null && feature.name != null && feature.name.IndexOf("HexSelection", StringComparison.Ordinal) >= 0)
                    {
                        BlueprintUtil.AddAtLevel(removed, level, feature);
                    }
                }
            }
        }

        static void AddArchetype(BlueprintCharacterClass witch, BlueprintArchetype archetype)
        {
            var field = AccessTools.Field(typeof(BlueprintCharacterClass), "m_Archetypes");
            var current = (BlueprintArchetypeReference[])field.GetValue(witch) ?? new BlueprintArchetypeReference[0];
            if (current.Any(reference => reference != null && reference.Guid == archetype.AssetGuid))
            {
                return;
            }

            var list = current.ToList();
            list.Add(archetype.ToReference<BlueprintArchetypeReference>());
            field.SetValue(witch, list.ToArray());
            Main.Log("Emerald Witch is on the Witch archetype list (" + list.Count + ").");
        }

        struct SpellRequest
        {
            public string Display;
            public string Asset;
            public string Guid;

            public SpellRequest(string display, string asset, string guid)
            {
                Display = display;
                Asset = asset;
                Guid = guid;
            }
        }
    }
}
