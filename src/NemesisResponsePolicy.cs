using System;
using System.Collections.Generic;

namespace ErenshorNemesis
{
    // Deterministic authored-tone categories for a direct reply. A recurring rival should not be
    // hostile every line: most of these are ordinary MMO chatter, and the flavor-specific buckets
    // only ever become eligible when NemesisDirector supplies a genuinely verified fact for them.
    internal enum NemesisResponseBucket
    {
        NeutralGreeting,
        NeutralSmallTalk,
        CompetitiveGeneral,
        CompetitiveAhead,
        CompetitiveBehind,
        Respectful,
        RecentWin,
        RecentLoss,
        LongTimeNoSee
    }

    // Every field here is either a genuine verified fact NemesisDirector already tracks, or a plain
    // false/unknown default - this struct never invents anything. ValidBuckets below is the single
    // place a bucket's fact requirement is enforced, so a template pool can never be reached without
    // its gating fact actually being true.
    internal struct NemesisResponseFacts
    {
        internal bool IsGreeting;
        internal bool LevelKnown;
        internal int LevelDelta; // player level minus rival level. Only meaningful when LevelKnown.
        internal bool RivalStageEstablished; // Stage() is past "new" - genuine rivalry history exists.
        internal bool HasRecentDuelFact; // a real, recent PvP or Practice Duel result vs THIS rival exists.
        internal bool RecentDuelWasNemesisWin; // only meaningful when HasRecentDuelFact.
        internal bool LongGapSinceLastConversation;
    }

    // Pure, Unity-free bucket selection. NemesisDirector supplies measured facts and persisted
    // history; this file only decides which authored category may speak this turn and picks one with
    // a light bias toward ordinary small talk, while avoiding repeating a recently-used bucket when a
    // different valid one exists. Never touches game state, never fabricates a fact.
    internal static class NemesisResponsePolicy
    {
        // Small, bounded variety memory - not a transcript. Avoids "the same flavor of line three
        // times in a row" without persisting anything resembling real conversation history.
        internal const int RecentBucketHistoryBound = 3;

        internal static bool LooksLikeGreeting(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;
            string clean = message.Trim().TrimEnd('!', '?', '.', ',').ToLowerInvariant();
            switch (clean)
            {
                case "hey": case "hi": case "hello": case "yo": case "sup":
                case "hiya": case "heya": case "hey there": case "hi there":
                    return true;
                default:
                    return false;
            }
        }

        internal static List<NemesisResponseBucket> ValidBuckets(NemesisResponseFacts facts)
        {
            List<NemesisResponseBucket> valid = new List<NemesisResponseBucket>();
            if (facts.IsGreeting) valid.Add(NemesisResponseBucket.NeutralGreeting);
            // Ordinary small talk and light competitive flavor need no special fact - every assigned
            // rival is, by definition, a rival, and mundane chatter is always in-character.
            valid.Add(NemesisResponseBucket.NeutralSmallTalk);
            valid.Add(NemesisResponseBucket.CompetitiveGeneral);
            if (facts.LevelKnown && facts.LevelDelta > 0) valid.Add(NemesisResponseBucket.CompetitiveAhead);
            if (facts.LevelKnown && facts.LevelDelta < 0) valid.Add(NemesisResponseBucket.CompetitiveBehind);
            if (facts.RivalStageEstablished) valid.Add(NemesisResponseBucket.Respectful);
            if (facts.HasRecentDuelFact && facts.RecentDuelWasNemesisWin) valid.Add(NemesisResponseBucket.RecentWin);
            if (facts.HasRecentDuelFact && !facts.RecentDuelWasNemesisWin) valid.Add(NemesisResponseBucket.RecentLoss);
            if (facts.LongGapSinceLastConversation) valid.Add(NemesisResponseBucket.LongTimeNoSee);
            return valid;
        }

        // Approximate weight only, not a mandated percentage: normal small talk dominates, light
        // competition is the next-largest slice, and the fact-gated flavor buckets are already
        // naturally rare because their gating facts are not always true at once.
        private static int Weight(NemesisResponseBucket bucket)
        {
            switch (bucket)
            {
                case NemesisResponseBucket.NeutralGreeting: return 5;
                case NemesisResponseBucket.NeutralSmallTalk: return 4;
                case NemesisResponseBucket.CompetitiveGeneral: return 3;
                default: return 2;
            }
        }

        // Deterministic weighted pick using the same seeded-index technique NemesisDirector already
        // uses for line selection, so this stays reproducible and testable. A bucket used within
        // RecentBucketHistoryBound turns is skipped in favor of any other still-valid bucket; if every
        // valid bucket has been used recently, the guard is dropped rather than deadlocking (a bucket
        // repeat is always better than silence).
        internal static NemesisResponseBucket ChooseBucket(List<NemesisResponseBucket> validBuckets,
            IList<NemesisResponseBucket> recentHistory, int seed)
        {
            if (validBuckets == null || validBuckets.Count == 0) return NemesisResponseBucket.NeutralSmallTalk;

            bool allRecentlyUsed = true;
            for (int i = 0; i < validBuckets.Count; i++)
                if (!Contains(recentHistory, validBuckets[i])) { allRecentlyUsed = false; break; }

            List<NemesisResponseBucket> weighted = new List<NemesisResponseBucket>();
            for (int i = 0; i < validBuckets.Count; i++)
            {
                NemesisResponseBucket bucket = validBuckets[i];
                if (!allRecentlyUsed && Contains(recentHistory, bucket)) continue;
                int w = Math.Max(1, Weight(bucket));
                for (int j = 0; j < w; j++) weighted.Add(bucket);
            }
            if (weighted.Count == 0)
                for (int i = 0; i < validBuckets.Count; i++) weighted.Add(validBuckets[i]);

            return weighted[SeedIndex(seed, weighted.Count)];
        }

        internal static List<NemesisResponseBucket> PushHistory(IList<NemesisResponseBucket> history, NemesisResponseBucket chosen)
        {
            List<NemesisResponseBucket> result = history == null ? new List<NemesisResponseBucket>() : new List<NemesisResponseBucket>(history);
            result.Add(chosen);
            while (result.Count > RecentBucketHistoryBound) result.RemoveAt(0);
            return result;
        }

        private static bool Contains(IList<NemesisResponseBucket> list, NemesisResponseBucket value)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++) if (list[i] == value) return true;
            return false;
        }

        private static int SeedIndex(int seed, int count)
        {
            if (count <= 1) return 0;
            unchecked { uint x = (uint)seed; x ^= x << 13; x ^= x >> 17; x ^= x << 5; return (int)(x % (uint)count); }
        }

        internal static string RunSelfTests()
        {
            if (!LooksLikeGreeting("hey") || !LooksLikeGreeting("Hi!") || !LooksLikeGreeting("hello.") || !LooksLikeGreeting("Sup?"))
                return "FAIL greeting detection missed a plain greeting";
            if (LooksLikeGreeting("what are you doing") || LooksLikeGreeting("want to fight?") || LooksLikeGreeting(""))
                return "FAIL greeting detection false-positived on non-greeting text";

            NemesisResponseFacts bare = new NemesisResponseFacts();
            List<NemesisResponseBucket> bareValid = ValidBuckets(bare);
            if (bareValid.Contains(NemesisResponseBucket.NeutralGreeting)) return "FAIL greeting bucket valid without a greeting fact";
            if (bareValid.Contains(NemesisResponseBucket.CompetitiveAhead) || bareValid.Contains(NemesisResponseBucket.CompetitiveBehind))
                return "FAIL level-relative bucket valid without a level fact";
            if (bareValid.Contains(NemesisResponseBucket.Respectful)) return "FAIL respectful bucket valid without established rivalry stage";
            if (bareValid.Contains(NemesisResponseBucket.RecentWin) || bareValid.Contains(NemesisResponseBucket.RecentLoss))
                return "FAIL duel-outcome bucket valid without a duel fact";
            if (!bareValid.Contains(NemesisResponseBucket.NeutralSmallTalk) || !bareValid.Contains(NemesisResponseBucket.CompetitiveGeneral))
                return "FAIL normal/competitive-general buckets must always be available";

            NemesisResponseFacts greeting = new NemesisResponseFacts { IsGreeting = true };
            if (!ValidBuckets(greeting).Contains(NemesisResponseBucket.NeutralGreeting)) return "FAIL greeting fact did not unlock greeting bucket";

            NemesisResponseFacts ahead = new NemesisResponseFacts { LevelKnown = true, LevelDelta = 3 };
            List<NemesisResponseBucket> aheadValid = ValidBuckets(ahead);
            if (!aheadValid.Contains(NemesisResponseBucket.CompetitiveAhead) || aheadValid.Contains(NemesisResponseBucket.CompetitiveBehind))
                return "FAIL positive level delta must unlock only the ahead bucket";

            NemesisResponseFacts behind = new NemesisResponseFacts { LevelKnown = true, LevelDelta = -2 };
            List<NemesisResponseBucket> behindValid = ValidBuckets(behind);
            if (!behindValid.Contains(NemesisResponseBucket.CompetitiveBehind) || behindValid.Contains(NemesisResponseBucket.CompetitiveAhead))
                return "FAIL negative level delta must unlock only the behind bucket";

            NemesisResponseFacts win = new NemesisResponseFacts { HasRecentDuelFact = true, RecentDuelWasNemesisWin = true };
            List<NemesisResponseBucket> winValid = ValidBuckets(win);
            if (!winValid.Contains(NemesisResponseBucket.RecentWin) || winValid.Contains(NemesisResponseBucket.RecentLoss))
                return "FAIL nemesis-win duel fact must unlock only RecentWin, never RecentLoss";

            NemesisResponseFacts loss = new NemesisResponseFacts { HasRecentDuelFact = true, RecentDuelWasNemesisWin = false };
            List<NemesisResponseBucket> lossValid = ValidBuckets(loss);
            if (!lossValid.Contains(NemesisResponseBucket.RecentLoss) || lossValid.Contains(NemesisResponseBucket.RecentWin))
                return "FAIL player-win duel fact must unlock only RecentLoss, never RecentWin";

            List<NemesisResponseBucket> onlyTwo = new List<NemesisResponseBucket> { NemesisResponseBucket.NeutralSmallTalk, NemesisResponseBucket.CompetitiveGeneral };
            NemesisResponseBucket a = ChooseBucket(onlyTwo, new List<NemesisResponseBucket>(), 12345);
            NemesisResponseBucket b = ChooseBucket(onlyTwo, new List<NemesisResponseBucket> { a }, 12345);
            if (a == b) return "FAIL recently-used bucket was not avoided when an alternative existed";
            List<NemesisResponseBucket> onlyOne = new List<NemesisResponseBucket> { NemesisResponseBucket.NeutralSmallTalk };
            NemesisResponseBucket forced = ChooseBucket(onlyOne, new List<NemesisResponseBucket> { NemesisResponseBucket.NeutralSmallTalk, NemesisResponseBucket.NeutralSmallTalk, NemesisResponseBucket.NeutralSmallTalk }, 999);
            if (forced != NemesisResponseBucket.NeutralSmallTalk) return "FAIL a single valid bucket must still be chosen rather than producing nothing";

            List<NemesisResponseBucket> history = PushHistory(null, NemesisResponseBucket.NeutralGreeting);
            history = PushHistory(history, NemesisResponseBucket.RecentWin);
            history = PushHistory(history, NemesisResponseBucket.Respectful);
            history = PushHistory(history, NemesisResponseBucket.CompetitiveGeneral);
            if (history.Count != RecentBucketHistoryBound) return "FAIL bucket history is not bounded";
            if (history[0] != NemesisResponseBucket.RecentWin) return "FAIL bucket history did not drop the oldest entry";

            return "PASS nemesis response policy";
        }
    }
}
