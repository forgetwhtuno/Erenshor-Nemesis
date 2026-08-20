using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ErenshorNemesis
{
    internal sealed class NemesisGuildRosterSnapshot
    {
        internal bool Known;
        internal bool PlayerInGuild;
        internal readonly HashSet<string> Members = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    // Read-only native social authority. Friends mirrors the current Group Builder predicate proven
    // by Party Tools. Guild membership mirrors the current Guild Life reader's native Guilds/member
    // shape. No sibling DLL is required at runtime.
    internal static class NemesisNativeSocialRoster
    {
        private static Type _guildManagerType;
        private static bool _guildManagerTypeResolved;

        internal static bool TryCurrentCharacterSlot(out int slot)
        {
            slot = -1;
            try
            {
                if (GameData.CurrentCharacterSlot == null || GameData.CurrentCharacterSlot.index < 0) return false;
                slot = GameData.CurrentCharacterSlot.index;
                return true;
            }
            catch { return false; }
        }

        internal static bool TryIsFriend(SimPlayerTracking sim, int currentSlot, out bool isFriend)
        {
            isFriend = false;
            if (sim == null || currentSlot < 0) return false;
            try
            {
                isFriend = !sim.IsGMCharacter && sim.FriendedBy == currentSlot;
                return true;
            }
            catch { return false; }
        }

        // Read-only: resolves the active character's own progression cohort (its real save-slot
        // index), fully independent of the Friends list (SimPlayerTracking.FriendedBy). Never reads
        // or writes TiedToSlot/FriendedBy on the player's own slot data, and never touches disk -
        // GameData.CurrentCharacterSlot/GameData.SaveSlots are already the loaded, live native state.
        internal static bool TryCurrentProgressionCohort(out int cohortSlot)
        {
            cohortSlot = -1;
            try
            {
                if (GameData.CurrentCharacterSlot == null || GameData.SaveSlots == null) return false;
                int index = GameData.CurrentCharacterSlot.index;
                int count = GameData.SaveSlots.Count;
                bool hasCharacter = index >= 0 && index < count && GameData.SaveSlots[index] != null &&
                    !string.IsNullOrWhiteSpace(GameData.SaveSlots[index].CharName);
                return NemesisProgressionCohortPolicy.TryResolveCurrentCohort(index, count, hasCharacter, out cohortSlot);
            }
            catch { return false; }
        }

        // Read-only: compares a candidate's native TiedToSlot against the already-resolved cohort
        // slot. Never mutates sim.TiedToSlot/sim.FriendedBy.
        internal static bool TryIsSameProgressionCohort(SimPlayerTracking sim, int cohortSlot, out bool sameCohort)
        {
            sameCohort = false;
            if (sim == null || cohortSlot < 0) return false;
            try
            {
                sameCohort = NemesisProgressionCohortPolicy.IsSameProgressionCohort(sim.TiedToSlot, cohortSlot);
                return true;
            }
            catch { return false; }
        }

        internal static NemesisGuildRosterSnapshot ReadCurrentGuild(string verifiedPlayerName)
        {
            NemesisGuildRosterSnapshot result = new NemesisGuildRosterSnapshot();
            if (string.IsNullOrWhiteSpace(verifiedPlayerName)) return result;

            object manager = ReadStaticMember(typeof(GameData), new string[] { "GuildManager", "GuildMngr" });
            if (manager == null)
            {
                Type managerType = ResolveGuildManagerType();
                if (managerType != null)
                {
                    try { manager = UnityEngine.Object.FindObjectOfType(managerType); } catch { }
                }
            }

            IEnumerable guilds = ReadMember(manager, new string[] { "Guilds" }) as IEnumerable;
            if (guilds == null) return result;

            bool unreadableRoster = false;
            foreach (object guild in guilds)
            {
                if (guild == null) continue;
                bool resolved;
                List<string> members = ReadMemberNames(guild, out resolved);
                if (!resolved) { unreadableRoster = true; continue; }

                bool playerMember = Contains(members, verifiedPlayerName);
                if (!playerMember) continue;

                result.Known = true;
                result.PlayerInGuild = true;
                for (int i = 0; i < members.Count; i++)
                    if (!string.IsNullOrWhiteSpace(members[i])) result.Members.Add(members[i].Trim());
                return result;
            }

            // If every native roster was readable, absence from all of them is authoritative:
            // this character is not in a Guild, so every Sim is known non-Guild for this player.
            if (!unreadableRoster)
            {
                result.Known = true;
                result.PlayerInGuild = false;
            }
            return result;
        }

        private static Type ResolveGuildManagerType()
        {
            if (_guildManagerTypeResolved) return _guildManagerType;
            _guildManagerTypeResolved = true;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    Type type = assemblies[i].GetType("GuildManager", false);
                    if (type != null) { _guildManagerType = type; return type; }
                }
                catch { }
            }
            return null;
        }

        private static List<string> ReadMemberNames(object guild, out bool resolved)
        {
            List<string> result = new List<string>();
            object raw;
            bool shape = TryReadMember(guild, new string[] { "GuildMembers", "Members", "MemberNames" }, out raw);
            IEnumerable members = raw as IEnumerable;
            resolved = shape && members != null;
            if (!resolved) return result;

            foreach (object value in members)
            {
                string name = MemberName(value);
                if (name.Length > 0 && !Contains(result, name)) result.Add(name);
            }
            return result;
        }

        private static string MemberName(object value)
        {
            if (value == null) return string.Empty;
            string direct = value as string;
            if (!string.IsNullOrWhiteSpace(direct)) return direct.Trim();
            object reflected;
            if (!TryReadMember(value, new string[] { "SimName", "MemberName", "CharacterName", "Name" }, out reflected) || reflected == null)
                return string.Empty;
            return (reflected as string ?? Convert.ToString(reflected) ?? string.Empty).Trim();
        }

        private static bool Contains(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value)) return false;
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static object ReadStaticMember(Type type, string[] names)
        {
            if (type == null || names == null) return null;
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    FieldInfo field = type.GetField(names[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (field != null) return field.GetValue(null);
                    PropertyInfo property = type.GetProperty(names[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(null, null);
                }
                catch { }
            }
            return null;
        }

        private static object ReadMember(object target, string[] names)
        {
            object value;
            return TryReadMember(target, names, out value) ? value : null;
        }

        private static bool TryReadMember(object target, string[] names, out object value)
        {
            value = null;
            if (target == null || names == null) return false;
            Type type = target.GetType();
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    FieldInfo field = type.GetField(names[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null) { value = field.GetValue(target); return true; }
                    PropertyInfo property = type.GetProperty(names[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (property != null && property.GetIndexParameters().Length == 0) { value = property.GetValue(target, null); return true; }
                }
                catch { }
            }
            return false;
        }
    }
}
