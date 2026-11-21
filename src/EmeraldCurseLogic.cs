using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;

namespace EmeraldWitch
{
    static class EmeraldCurseLogic
    {
        static readonly HashSet<UnitEntityData> Repurposed = new HashSet<UnitEntityData>();
        static readonly HashSet<UnitEntityData> AlreadyRaised = new HashSet<UnitEntityData>();
        static readonly Dictionary<UnitEntityData, PendingKill> Pending = new Dictionary<UnitEntityData, PendingKill>();
        static bool _raising;
        static TimeSpan _raiseDuration;

        struct PendingKill
        {
            public UnitEntityData Caster;
            public BlueprintAbility Ability;
        }

        public static void ForgetIfAlive(UnitEntityData unit)
        {
            if (unit == null || unit.State.IsDead)
            {
                return;
            }

            Repurposed.Remove(unit);
            AlreadyRaised.Remove(unit);
            Pending.Remove(unit);
        }

        public static void RememberKill(UnitEntityData caster, UnitEntityData victim, BlueprintAbility ability)
        {
            if (caster == null || victim == null || ability == null || victim.State.IsDead)
            {
                return;
            }

            Pending[victim] = new PendingKill { Caster = caster, Ability = ability };
        }

        public static void MarkRepurposed(UnitEntityData unit)
        {
            if (unit != null)
            {
                Repurposed.Add(unit);
            }
        }

        public static void TryRaise(UnitEntityData killer, UnitEntityData victim, BlueprintAbility ability)
        {
            if (victim == null || !victim.State.IsDead)
            {
                return;
            }

            if (Content.CurseFeature == null || Content.SpawnTemplate == null)
            {
                return;
            }

            if (Repurposed.Contains(victim) || HasRepurposeBuff(victim))
            {
                Pending.Remove(victim);
                return;
            }

            var killingSpell = ability != null && (IsChannelDamage(ability) || IsNecromancySpell(ability));
            if (!killingSpell && Pending.TryGetValue(victim, out var pending))
            {
                ability = pending.Ability;
                killer = pending.Caster ?? killer;
            }

            Pending.Remove(victim);

            if (killer?.Descriptor?.Facts == null || killer.Descriptor.Facts.Get(Content.CurseFeature) == null)
            {
                return;
            }

            if (!IsChannelDamage(ability) && !IsNecromancySpell(ability))
            {
                return;
            }

            if (!IsHoldPersonTarget(killer, victim))
            {
                return;
            }

            if (!AlreadyRaised.Add(victim))
            {
                return;
            }

            var fact = RaiseContext(killer);
            if (fact == null)
            {
                AlreadyRaised.Remove(victim);
                return;
            }

            var level = WitchLevel(killer);
            var spawn = CloneSpawn(Content.SpawnTemplate, level);
            var actions = new Kingmaker.ElementsSystem.ActionList
            {
                Actions = new Kingmaker.ElementsSystem.GameAction[] { spawn }
            };
            _raiseDuration = TimeSpan.FromMinutes(level);
            _raising = true;
            try
            {
                fact.RunActionInContext(actions, victim);
            }
            finally
            {
                _raising = false;
            }

            Main.Log("Emerald Curse raised one undead from " + victim.CharacterName + " for " + level + " minutes.");
        }

        static IFactContextOwner RaiseContext(UnitEntityData killer)
        {
            if (killer?.Descriptor?.Facts == null || killer.Descriptor.Facts.Get(Content.CurseFeature) == null)
            {
                return null;
            }

            if (Content.CurseToggle != null && killer.Descriptor.Facts.Get(Content.CurseToggle) != null)
            {
                var buff = killer.Buffs?.GetBuff(Content.CurseBuff);
                if (buff == null)
                {
                    return null;
                }

                return buff;
            }

            return killer.Descriptor.Facts.Get(Content.CurseFeature) as IFactContextOwner;
        }

        static int WitchLevel(UnitEntityData unit)
        {
            var level = 1;
            if (unit?.Descriptor?.Progression != null && Content.WitchClass != null)
            {
                level = unit.Descriptor.Progression.GetClassLevel(Content.WitchClass);
            }

            return Math.Max(1, level);
        }

        public static void BuffRaisedUndead(RuleSummonUnit rule)
        {
            if (!_raising || rule?.SummonedUnit == null)
            {
                return;
            }

            var summoned = rule.SummonedUnit;
            var caster = rule.Context?.MaybeCaster ?? rule.Initiator;
            if (summoned.Descriptor == null || caster == null)
            {
                Main.Log("Emerald Curse spawn did not create a unit.");
                return;
            }

            var duration = _raiseDuration;
            ApplyReanimation(caster, summoned, Content.ImprovedReanimation, Content.ImprovedReanimationBuff, duration);
            ApplyReanimation(caster, summoned, Content.GreaterReanimation, Content.GreaterReanimationBuff, duration);
            Main.Log("Emerald Curse summoned " + summoned.CharacterName + " for " + caster.CharacterName + ".");
        }

        static void ApplyReanimation(UnitEntityData caster, UnitEntityData summoned, BlueprintFeature feature, BlueprintBuff buff, TimeSpan duration)
        {
            if (feature == null || buff == null || caster.Descriptor.Facts.Get(feature) == null)
            {
                return;
            }

            if (summoned.Buffs.GetBuff(buff) != null)
            {
                return;
            }

            UnitHelper.AddBuff(summoned, buff, caster, duration, null);
        }

        static bool IsChannelDamage(BlueprintAbility ability)
        {
            if (ability == null || Content.ChannelDamage == null)
            {
                return false;
            }

            return ability == Content.ChannelDamage || ability.Parent == Content.ChannelDamage;
        }

        static bool IsNecromancySpell(BlueprintAbility ability)
        {
            if (ability == null)
            {
                return false;
            }

            if (ability.Type != AbilityType.Spell && !ability.IsSpell)
            {
                return false;
            }

            var school = ability.School;
            if (school == SpellSchool.None && ability.Parent != null)
            {
                school = ability.Parent.School;
            }

            return school == SpellSchool.Necromancy;
        }

        static bool IsHoldPersonTarget(UnitEntityData caster, UnitEntityData target)
        {
            var spell = Content.HoldPerson;
            if (spell?.TargetRestrictions == null)
            {
                return false;
            }

            var any = false;
            foreach (var restriction in spell.TargetRestrictions)
            {
                if (restriction is Kingmaker.UnitLogic.Abilities.Components.TargetCheckers.AbilityTargetHasFact fact)
                {
                    any = true;
                    if (!fact.IsTargetRestrictionPassed(caster, target))
                    {
                        return false;
                    }
                }
            }

            return any;
        }

        static bool HasRepurposeBuff(UnitEntityData unit)
        {
            if (Content.RepurposeBuffs == null)
            {
                return false;
            }

            foreach (var buff in Content.RepurposeBuffs)
            {
                if (buff != null && unit.Buffs.GetBuff(buff) != null)
                {
                    return true;
                }
            }

            return false;
        }

        static ContextActionSpawnMonster CloneSpawn(ContextActionSpawnMonster source, int level)
        {
            var copy = new ContextActionSpawnMonster();
            for (var type = source.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    field.SetValue(copy, field.GetValue(source));
                }
            }

            var count = Simple(1);
            copy.CountValue = new ContextDiceValue
            {
                DiceType = DiceType.Zero,
                DiceCountValue = Simple(0),
                BonusValue = count
            };
            copy.DurationValue = new ContextDurationValue
            {
                Rate = DurationRate.Minutes,
                DiceType = DiceType.Zero,
                DiceCountValue = Simple(0),
                BonusValue = Simple(level)
            };
            copy.LevelValue = Simple(level);
            copy.DoNotLinkToCaster = false;
            copy.IsDirectlyControllable = true;
            copy.UseLimitFromSummonPool = false;
            return copy;
        }

        static ContextValue Simple(int value)
        {
            return new ContextValue
            {
                ValueType = ContextValueType.Simple,
                Value = value
            };
        }

        public static BlueprintAbility AbilityFrom(RuleDealDamage rule)
        {
            if (rule == null)
            {
                return null;
            }

            if (rule.SourceAbility != null)
            {
                return rule.SourceAbility;
            }

            var reason = rule.Reason;
            if (reason == null)
            {
                return null;
            }

            if (reason.Ability != null)
            {
                return reason.Ability.Blueprint;
            }

            return reason.Context?.SourceAbility;
        }
    }

    [HarmonyPatch(typeof(RuleSummonUnit), nameof(RuleSummonUnit.OnTrigger))]
    static class EmeraldSummonBuffPatch
    {
        static void Postfix(RuleSummonUnit __instance)
        {
            try
            {
                EmeraldCurseLogic.BuffRaisedUndead(__instance);
            }
            catch (Exception ex)
            {
                Main.Log("Emerald Curse summon buff failed: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(RuleDealDamage), nameof(RuleDealDamage.OnTrigger))]
    static class RuleDealDamageCursePatch
    {
        static void Prefix(RuleDealDamage __instance)
        {
            EmeraldCurseLogic.ForgetIfAlive(__instance.Target);
        }

        static void Postfix(RuleDealDamage __instance)
        {
            try
            {
                EmeraldCurseLogic.TryRaise(__instance.Initiator, __instance.Target, EmeraldCurseLogic.AbilityFrom(__instance));
            }
            catch (Exception ex)
            {
                Main.Log("Emerald Curse damage hook failed: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(ContextActionKill), "RunAction")]
    static class ContextActionKillCursePatch
    {
        static void Prefix(ContextActionKill __instance)
        {
            var target = BlueprintUtil.Read(__instance, "Target") as Kingmaker.Utility.TargetWrapper;
            EmeraldCurseLogic.ForgetIfAlive(target?.Unit);
        }

        static void Postfix(ContextActionKill __instance)
        {
            try
            {
                var context = BlueprintUtil.Read(__instance, "Context") as Kingmaker.UnitLogic.Mechanics.MechanicsContext;
                var abilityContext = BlueprintUtil.Read(__instance, "AbilityContext") as Kingmaker.UnitLogic.Abilities.AbilityExecutionContext;
                var target = BlueprintUtil.Read(__instance, "Target") as Kingmaker.Utility.TargetWrapper;
                var ability = abilityContext?.Ability?.Blueprint ?? context?.SourceAbility;
                var caster = context?.MaybeCaster;
                var victim = target?.Unit;
                EmeraldCurseLogic.TryRaise(caster, victim, ability);
                if (victim != null && victim.State.MarkedForDeath)
                {
                    EmeraldCurseLogic.RememberKill(caster, victim, ability);
                }
            }
            catch (Exception ex)
            {
                Main.Log("Emerald Curse kill hook failed: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(Kingmaker.UnitLogic.Mechanics.Components.DeathActions), nameof(Kingmaker.UnitLogic.Mechanics.Components.DeathActions.HandleUnitDeath))]
    static class RepurposeDeathPatch
    {
        static void Prefix(Kingmaker.UnitLogic.Mechanics.Components.DeathActions __instance, UnitEntityData entityData)
        {
            RepurposeNotes.NoteRepurpose(__instance, entityData, 0);
        }
    }

    [HarmonyPatch(typeof(Kingmaker.UnitLogic.Mechanics.Components.DeathActions), nameof(Kingmaker.UnitLogic.Mechanics.Components.DeathActions.HandleUnitLifeStateChanged))]
    static class RepurposeLifeStatePatch
    {
        static void Prefix(Kingmaker.UnitLogic.Mechanics.Components.DeathActions __instance, UnitEntityData entityData)
        {
            RepurposeNotes.NoteRepurpose(__instance, entityData, 1);
        }
    }

    [HarmonyPatch(typeof(Kingmaker.Controllers.Units.UnitLifeController), "SetLifeState")]
    static class LifeStateCursePatch
    {
        static void Postfix(UnitEntityData unit)
        {
            if (unit == null || !unit.State.IsDead)
            {
                return;
            }

            try
            {
                var rule = unit.LastHandledDamage;
                EmeraldCurseLogic.TryRaise(rule?.Initiator, unit, EmeraldCurseLogic.AbilityFrom(rule));
            }
            catch (Exception ex)
            {
                Main.Log("Emerald Curse death hook failed: " + ex);
            }
        }
    }

    static class RepurposeNotes
    {
        public static void NoteRepurpose(Kingmaker.UnitLogic.Mechanics.Components.DeathActions instance, UnitEntityData entityData, int expectedTrigger)
        {
            try
            {
                var trigger = AccessTools.Field(typeof(Kingmaker.UnitLogic.Mechanics.Components.DeathActions), "m_DeathTrigger").GetValue(instance);
                if (trigger == null || (int)trigger != expectedTrigger)
                {
                    return;
                }

                var owner = BlueprintUtil.Read(instance, "Owner") as UnitEntityData;
                if (entityData == null || entityData != owner)
                {
                    return;
                }

                var fact = BlueprintUtil.Read(instance, "Fact") as Kingmaker.EntitySystem.EntityFact;
                var name = fact?.Blueprint?.name;
                if (!string.IsNullOrEmpty(name) && name.StartsWith("Repurpose", StringComparison.Ordinal))
                {
                    EmeraldCurseLogic.MarkRepurposed(entityData);
                }
            }
            catch (Exception ex)
            {
                Main.Log("Repurpose priority hook failed: " + ex);
            }
        }
    }
}
