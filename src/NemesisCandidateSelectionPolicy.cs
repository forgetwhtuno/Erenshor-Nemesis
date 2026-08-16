using System;
using System.Collections.Generic;

namespace ErenshorNemesis
{
    internal enum NemesisAutomaticPool
    {
        None = 0,
        Primary = 1,
        GuildFallback = 2
    }

    internal enum NemesisSelectionOrigin
    {
        Explicit = 0,
        Automatic = 1
    }

    internal sealed class NemesisCandidateFact
    {
        internal string Name;
        internal int LevelDistance;
        internal bool BaseEligible;
        internal bool FriendKnown;
        internal bool IsFriend;
        internal bool GuildKnown;
        internal bool IsGuildMember;
    }

    // Pure social-selection policy. Runtime actor safety is established before a fact reaches this
    // layer. Automatic selection is stricter than explicit player selection: a deliberate select
    // may target a native Friend, while random selection can never do so.
    internal static class NemesisCandidateSelectionPolicy
    {
        internal static bool ExplicitEligible(NemesisCandidateFact fact)
        {
            return fact != null && fact.BaseEligible;
        }

        internal static bool EligibleForSelection(NemesisCandidateFact fact, NemesisSelectionOrigin origin)
        {
            return origin == NemesisSelectionOrigin.Automatic
                ? AutomaticPool(fact) != NemesisAutomaticPool.None
                : ExplicitEligible(fact);
        }

        internal static NemesisAutomaticPool AutomaticPool(NemesisCandidateFact fact)
        {
            if (fact == null || !fact.BaseEligible) return NemesisAutomaticPool.None;
            // Unknown Friends state must not be interpreted as "not a Friend".
            if (!fact.FriendKnown || fact.IsFriend) return NemesisAutomaticPool.None;
            // Unknown Guild state must not be interpreted as the preferred non-Guild pool.
            if (!fact.GuildKnown) return NemesisAutomaticPool.None;
            return fact.IsGuildMember ? NemesisAutomaticPool.GuildFallback : NemesisAutomaticPool.Primary;
        }

        internal static List<NemesisCandidateFact> OrderedAutomaticCandidates(IList<NemesisCandidateFact> facts)
        {
            List<NemesisCandidateFact> primary = new List<NemesisCandidateFact>();
            List<NemesisCandidateFact> fallback = new List<NemesisCandidateFact>();
            if (facts != null)
            {
                for (int i = 0; i < facts.Count; i++)
                {
                    NemesisAutomaticPool pool = AutomaticPool(facts[i]);
                    if (pool == NemesisAutomaticPool.Primary) primary.Add(facts[i]);
                    else if (pool == NemesisAutomaticPool.GuildFallback) fallback.Add(facts[i]);
                }
            }
            Sort(primary);
            Sort(fallback);
            return primary.Count > 0 ? primary : fallback;
        }

        internal static List<NemesisCandidateFact> OrderedPool(IList<NemesisCandidateFact> facts, NemesisAutomaticPool requested)
        {
            List<NemesisCandidateFact> result = new List<NemesisCandidateFact>();
            if (facts != null)
            {
                for (int i = 0; i < facts.Count; i++)
                    if (AutomaticPool(facts[i]) == requested) result.Add(facts[i]);
            }
            Sort(result);
            return result;
        }

        internal static List<NemesisCandidateFact> OrderedExplicitFriends(IList<NemesisCandidateFact> facts)
        {
            List<NemesisCandidateFact> result = new List<NemesisCandidateFact>();
            if (facts != null)
            {
                for (int i = 0; i < facts.Count; i++)
                {
                    NemesisCandidateFact fact = facts[i];
                    if (ExplicitEligible(fact) && fact.FriendKnown && fact.IsFriend) result.Add(fact);
                }
            }
            Sort(result);
            return result;
        }

        internal static List<NemesisCandidateFact> OrderedExplicitCandidates(IList<NemesisCandidateFact> facts)
        {
            List<NemesisCandidateFact> result = new List<NemesisCandidateFact>();
            if (facts != null)
            {
                for (int i = 0; i < facts.Count; i++)
                    if (ExplicitEligible(facts[i])) result.Add(facts[i]);
            }
            Sort(result);
            return result;
        }

        private static void Sort(List<NemesisCandidateFact> values)
        {
            values.Sort(delegate(NemesisCandidateFact a, NemesisCandidateFact b)
            {
                int level = (a == null ? int.MaxValue : a.LevelDistance).CompareTo(b == null ? int.MaxValue : b.LevelDistance);
                if (level != 0) return level;
                return string.Compare(a == null ? string.Empty : a.Name,
                                      b == null ? string.Empty : b.Name,
                                      StringComparison.OrdinalIgnoreCase);
            });
        }

        internal static string RunSelfTests()
        {
            NemesisCandidateFact friend = Fact("Friend", 0, true, true, true, true, false);
            if (AutomaticPool(friend) != NemesisAutomaticPool.None) return "FAIL friend entered automatic pool";
            if (!EligibleForSelection(friend, NemesisSelectionOrigin.Explicit)) return "FAIL deliberate Friend selection blocked";
            if (EligibleForSelection(friend, NemesisSelectionOrigin.Automatic)) return "FAIL Friend allowed by automatic confirmation policy";

            NemesisCandidateFact perfectFriend = Fact("Perfect", 0, true, true, true, true, false);
            NemesisCandidateFact ordinary = Fact("Ordinary", 3, true, true, false, true, false);
            List<NemesisCandidateFact> automatic = OrderedAutomaticCandidates(new NemesisCandidateFact[] { perfectFriend, ordinary });
            if (automatic.Count != 1 || automatic[0].Name != "Ordinary") return "FAIL perfect-level Friend beat ordinary automatic candidate";

            NemesisCandidateFact guild = Fact("Guild", 0, true, true, false, true, true);
            automatic = OrderedAutomaticCandidates(new NemesisCandidateFact[] { guild, ordinary });
            if (automatic.Count != 1 || automatic[0].Name != "Ordinary") return "FAIL Guild fallback beat primary pool";

            automatic = OrderedAutomaticCandidates(new NemesisCandidateFact[] { guild });
            if (automatic.Count != 1 || automatic[0].Name != "Guild") return "FAIL Guild fallback unavailable";

            NemesisCandidateFact party = Fact("Party", 0, false, true, false, true, false);
            NemesisCandidateFact ownSlot = Fact("OwnSlot", 0, false, true, false, true, false);
            NemesisCandidateFact remote = Fact("Remote", 0, false, true, false, true, false);
            if (ExplicitEligible(party) || ExplicitEligible(ownSlot) || ExplicitEligible(remote)) return "FAIL base safety bypassed";

            NemesisCandidateFact unknownFriend = Fact("UnknownFriend", 0, true, false, false, true, false);
            NemesisCandidateFact unknownGuild = Fact("UnknownGuild", 0, true, true, false, false, false);
            if (AutomaticPool(unknownFriend) != NemesisAutomaticPool.None) return "FAIL unknown Friend state failed open";
            if (AutomaticPool(unknownGuild) != NemesisAutomaticPool.None) return "FAIL unknown Guild state failed open";

            List<NemesisCandidateFact> sorted = OrderedExplicitCandidates(new NemesisCandidateFact[]
            {
                Fact("Zulu", 2, true, true, false, true, false),
                Fact("Beta", 1, true, true, false, true, false),
                Fact("Alpha", 1, true, true, false, true, false)
            });
            if (sorted.Count != 3 || sorted[0].Name != "Alpha" || sorted[1].Name != "Beta" || sorted[2].Name != "Zulu")
                return "FAIL deterministic candidate ordering";

            return "PASS nemesis candidate selection policy";
        }

        private static NemesisCandidateFact Fact(string name, int distance, bool baseEligible,
            bool friendKnown, bool isFriend, bool guildKnown, bool isGuildMember)
        {
            NemesisCandidateFact fact = new NemesisCandidateFact();
            fact.Name = name;
            fact.LevelDistance = distance;
            fact.BaseEligible = baseEligible;
            fact.FriendKnown = friendKnown;
            fact.IsFriend = isFriend;
            fact.GuildKnown = guildKnown;
            fact.IsGuildMember = isGuildMember;
            return fact;
        }
    }
}
