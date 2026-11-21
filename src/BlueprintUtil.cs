using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Localization;
using UnityEngine;

namespace EmeraldWitch
{
    static class BlueprintUtil
    {
        public static T Load<T>(string guid) where T : BlueprintScriptableObject
        {
            try
            {
                var blueprint = ResourcesLibrary.TryGetBlueprint<T>(guid);
                if (blueprint == null)
                {
                    Main.Log("Missing blueprint " + guid);
                }

                return blueprint;
            }
            catch (InvalidCastException)
            {
                var found = ResourcesLibrary.TryGetBlueprint(BlueprintGuid.Parse(guid));
                var actual = found == null ? "missing" : found.GetType().Name + " " + found.name;
                Main.Log("Blueprint " + guid + " is " + actual + ", not " + typeof(T).Name + ".");
                return null;
            }
        }

        public static T Create<T>() where T : BlueprintScriptableObject, new()
        {
            return new T();
        }

        public static void Register(SimpleBlueprint blueprint)
        {
            ResourcesLibrary.BlueprintsCache.AddCachedBlueprint(blueprint.AssetGuid, blueprint);
        }

        public static object Read(object target, string propertyName)
        {
            if (target == null)
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty(propertyName, flags);
                if (property != null)
                {
                    return property.GetValue(target, null);
                }
            }

            throw new MissingMemberException(target.GetType().Name, propertyName);
        }

        public static LocalizedString MakeString(string key, string value)
        {
            var pack = LocalizationManager.CurrentPack;
            if (pack == null)
            {
                Main.Log("Localization pack is not loaded yet. Text for " + key + " may be blank until you restart.");
            }
            else
            {
                try
                {
                    pack.PutString(key, value);
                }
                catch (Exception ex)
                {
                    Main.Log("Could not store text for " + key + ": " + ex.Message);
                }
            }

            var localized = new LocalizedString();
            AccessTools.Field(typeof(LocalizedString), "m_Key").SetValue(localized, key);
            return localized;
        }

        public static void SetName(BlueprintUnitFact fact, string key, string title, string description)
        {
            Set(fact, "m_DisplayName", MakeString(key + ".Name", title));
            Set(fact, "m_Description", MakeString(key + ".Desc", description));
        }

        public static void CopyIcon(BlueprintUnitFact from, BlueprintUnitFact to)
        {
            if (from == null || to == null)
            {
                return;
            }

            var icon = AccessTools.Field(typeof(BlueprintUnitFact), "m_Icon").GetValue(from);
            AccessTools.Field(typeof(BlueprintUnitFact), "m_Icon").SetValue(to, icon);
        }

        public static void Set(object target, string fieldName, object value)
        {
            var field = AccessTools.Field(target.GetType(), fieldName);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().Name, fieldName);
            }

            field.SetValue(target, value);
        }

        public static void CopyMergedFields(object from, object to)
        {
            for (var type = from.GetType(); type != null && type.Namespace != "UnityEngine"; type = type.BaseType)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.Name == "name" || field.Name == "AssetGuid" || field.Name == "m_PrototypeId" || field.Name == "m_Overrides")
                    {
                        continue;
                    }

                    field.SetValue(to, field.GetValue(from));
                }
            }
        }

        public static T Component<T>(BlueprintScriptableObject blueprint) where T : BlueprintComponent
        {
            var components = blueprint?.ComponentsArray;
            if (components == null)
            {
                return null;
            }

            foreach (var component in components)
            {
                if (component is T match)
                {
                    return match;
                }
            }

            return null;
        }

        public static List<Kingmaker.Blueprints.Classes.LevelEntry> CloneEntries(Kingmaker.Blueprints.Classes.LevelEntry[] source)
        {
            var entries = new List<Kingmaker.Blueprints.Classes.LevelEntry>();
            if (source == null)
            {
                return entries;
            }

            foreach (var entry in source)
            {
                var copy = new Kingmaker.Blueprints.Classes.LevelEntry { Level = entry.Level };
                var features = new List<Kingmaker.Blueprints.Classes.BlueprintFeatureBase>();
                foreach (var feature in entry.Features)
                {
                    if (feature != null)
                    {
                        features.Add(feature);
                    }
                }

                copy.SetFeatures(features);
                entries.Add(copy);
            }

            return entries;
        }

        public static void AddAtLevel(List<Kingmaker.Blueprints.Classes.LevelEntry> entries, int level, Kingmaker.Blueprints.Classes.BlueprintFeatureBase feature)
        {
            var entry = entries.FirstOrDefault(e => e.Level == level);
            if (entry == null)
            {
                entry = new Kingmaker.Blueprints.Classes.LevelEntry { Level = level };
                entries.Add(entry);
            }

            var features = new List<Kingmaker.Blueprints.Classes.BlueprintFeatureBase>();
            if (entry.Features != null)
            {
                foreach (var existing in entry.Features)
                {
                    if (existing != null && existing != feature)
                    {
                        features.Add(existing);
                    }
                }
            }

            features.Add(feature);
            entry.SetFeatures(features);
        }
    }
}
