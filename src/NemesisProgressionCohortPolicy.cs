namespace ErenshorNemesis
{
    // Pure, Unity-free progression-cohort gate. Native Erenshor ties a SimPlayer's level catch-up to
    // SimPlayerTracking.TiedToSlot -> GameData.SaveSlots[TiedToSlot].CharLevel (verified against the
    // installed Assembly-CSharp: SimPlayerMngr.LoadActualSims/SimPlayerCatchupCode both compare a
    // Sim's level against GameData.SaveSlots[TiedToSlot].CharLevel whenever TiedToSlot is a real
    // 0..10 slot index). A candidate only progresses with the SAME real player character when its
    // TiedToSlot equals that character's own save-slot index, and that index is itself a real,
    // resolvable character slot - not an out-of-range index or one of native's non-character
    // sentinel bindings (12 = generic creation bucket, 99 = never tied to any slot).
    //
    // FriendedBy is a deliberately separate native field: /friend always rewrites TiedToSlot to the
    // acting character's slot (even when unfriending, which only clears FriendedBy back to -1), and
    // a non-Friended Sim can already share TiedToSlot with the active character by chance. Nemesis
    // eligibility is about the progression cohort, not the Friends list, so this policy never reads
    // or requires FriendedBy.
    internal static class NemesisProgressionCohortPolicy
    {
        // Native sentinel TiedToSlot values that never denote a real save slot.
        internal const int GenericBucketSlot = 12;
        internal const int UnassignedSlot = 99;

        internal static bool IsRealSlotIndex(int slotIndex, int saveSlotCount)
        {
            return slotIndex >= 0 && slotIndex < saveSlotCount &&
                   slotIndex != GenericBucketSlot && slotIndex != UnassignedSlot;
        }

        // Fails closed (returns false) unless the active character's own slot is unambiguously a
        // real, occupied save slot. An unresolvable/empty current slot must never be treated as a
        // valid cohort to match against - that would make every stray TiedToSlot value "safe".
        internal static bool TryResolveCurrentCohort(int currentSlotIndex, int saveSlotCount, bool currentSlotHasCharacter, out int resolvedSlot)
        {
            resolvedSlot = -1;
            if (!IsRealSlotIndex(currentSlotIndex, saveSlotCount) || !currentSlotHasCharacter) return false;
            resolvedSlot = currentSlotIndex;
            return true;
        }

        internal static bool IsSameProgressionCohort(int candidateTiedToSlot, int resolvedCurrentSlot)
        {
            return resolvedCurrentSlot >= 0 && candidateTiedToSlot == resolvedCurrentSlot;
        }

        internal static string RunSelfTests()
        {
            if (!IsRealSlotIndex(0, 12) || !IsRealSlotIndex(10, 12)) return "FAIL real slot index range rejected";
            if (IsRealSlotIndex(-1, 12)) return "FAIL negative slot treated as real";
            if (IsRealSlotIndex(12, 13)) return "FAIL generic bucket sentinel treated as real even in bounds";
            if (IsRealSlotIndex(99, 100)) return "FAIL unassigned sentinel treated as real even in bounds";
            if (IsRealSlotIndex(11, 5)) return "FAIL out-of-range slot treated as real";

            int resolved;
            if (TryResolveCurrentCohort(-1, 12, true, out resolved)) return "FAIL unresolvable current slot resolved";
            if (TryResolveCurrentCohort(0, 12, false, out resolved)) return "FAIL empty current slot resolved as real character";
            if (TryResolveCurrentCohort(12, 13, true, out resolved)) return "FAIL sentinel current slot resolved";
            if (!TryResolveCurrentCohort(0, 12, true, out resolved) || resolved != 0) return "FAIL valid current slot did not resolve";

            if (!IsSameProgressionCohort(0, 0)) return "FAIL matching cohort rejected";
            if (IsSameProgressionCohort(1, 0)) return "FAIL different cohort accepted";
            if (IsSameProgressionCohort(GenericBucketSlot, 0)) return "FAIL generic-bucket candidate matched a real cohort";
            if (IsSameProgressionCohort(UnassignedSlot, 0)) return "FAIL unassigned candidate matched a real cohort";
            if (IsSameProgressionCohort(0, -1)) return "FAIL unresolved current cohort accepted a candidate";

            return "PASS nemesis progression cohort policy";
        }
    }
}
