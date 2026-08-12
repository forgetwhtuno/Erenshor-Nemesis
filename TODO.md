# Nemesis follow-up

- [ ] Run `IN_GAME_VALIDATION.md` end to end. It covers selection persistence and the 0.1.0
      migration, every result classification including mid-fight zone-out, restart-safe cadence,
      and the LLM timeout/supersede/stale paths. Sections D2, H5, H7, and F2 are the P0 regressions.
- [ ] Add a Party Tools-style compact UI after command behavior is stable.
- [x] Add explicit replacement confirmation before overwriting an established Nemesis, plus
      non-destructive `disable`/resume and a no-op re-select (verified by mutation).
- [x] Add an asynchronous, tightly grounded LLM voice contract with a bounded template-fallback timeout, supersede handling, and stale-context rejection.
- [x] Add more personality/class-neutral template pools without abusive or fabricated-history language.
- [x] Consume PvP contract v2: bounded result queue, authoritative outcome classification, and persistent cross-restart deduplication.
- [ ] In-game verify LLM success, Templates/Off behavior, Ollama-unavailable fallback, and generated-line rejection diagnostics.
- [ ] Revisit escalation thresholds, the minimum rivalry age, and minimum level after several real characters have progressed naturally.
- [x] Add a verified zone-entry taunt trigger without treating dialogue as fact (bounded, cooldowned,
      per-zone deduplicated; consumes the ordinary taunt window and awards no grudge points).
- [ ] Add the achievement half of that trigger (level-up is the cheapest verified milestone; it needs
      the same gating and must not become a second chatter stream).
- [x] Add source-level PvP contract tests shared by both standalone mods and Deep Sims
      (`shared/PvpContractConformance.cs`; verified by mutation to fail on a drifted mirror).
