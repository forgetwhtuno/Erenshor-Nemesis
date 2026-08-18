using System;
using System.Collections.Generic;

namespace ErenshorNemesis
{
    internal static class NemesisAssignmentPolicy
    {
        internal const int MissingIdentityConfirmations = 3;
        internal const float MissingIdentityMinimumSeconds = 30f;
        internal const float AwaitingCandidateRetrySeconds = 15f;

        // Stable local choice from the already-filtered automatic pool. Persistence remains the
        // actual anti-reroll guarantee; this merely avoids an obviously alphabetical first choice.
        internal static int StableChoiceIndex(string characterScope, IList<string> candidateTokens)
        {
            if (candidateTokens == null || candidateTokens.Count == 0) return -1;
            unchecked
            {
                uint h = 2166136261u;
                string scope = characterScope ?? string.Empty;
                for (int i = 0; i < scope.Length; i++) { h ^= scope[i]; h *= 16777619u; }
                for (int i = 0; i < candidateTokens.Count; i++)
                {
                    string token = candidateTokens[i] ?? string.Empty;
                    h ^= (uint)(i + 1); h *= 16777619u;
                    for (int j = 0; j < token.Length; j++) { h ^= token[j]; h *= 16777619u; }
                }
                return (int)(h % (uint)candidateTokens.Count);
            }
        }

        internal static bool MissingIdentityIsPermanent(int consecutiveAuthoritativeMisses, float secondsSinceFirstMiss)
        {
            return consecutiveAuthoritativeMisses >= MissingIdentityConfirmations &&
                secondsSinceFirstMiss >= MissingIdentityMinimumSeconds;
        }
    }
}
