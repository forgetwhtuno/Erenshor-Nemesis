# Changelog

## 0.3.0 - Automatic Rival / Two-Way Conversation / Native Chat

- Automatically assigns one valid rival after authoritative character/social state is ready; no normal-play `candidates`/`select` setup is required.
- Persists stable `SimPlayerTracking.simIndex` identity with display-name fallback and upgrades legacy name-only assignments without rerolling them.
- Distinguishes temporary roster unavailability from sustained authoritative invalidity before replacing a saved rival.
- Added `/enemesis reroll` (`random` remains an alias) while preserving manual selection, established-rival confirmation, and per-character stop/resume behavior.
- Added exact-current-name natural reply routing plus `/enemesis reply`; unrelated party/local chat is not consumed.
- Orders Nemesis chat ownership before Deep Sims to prevent one addressed line from producing both a Nemesis response and an ordinary party-Sim response.
- Added a six-line character-scoped HEARD rivalry thread; conversation never becomes verified fight history.
- Reuses the existing optional Deep Sims `NemesisEventBridge.RequestNemesisLine(...)` path; Nemesis contains no Ollama client/model selection/residency.
- Removed hardcoded `magenta` chat output. Visible lines contain no rich-text markup; Nemesis reuses a runtime-observed native tell color argument or safely falls back to one-argument `UpdateSocialLog.LogAdd(text)`.
- Expanded bounded status/diagnostics and deterministic policy tests.

## Unreleased - Suite Hub control-surface refinement

- Kept one-rival selection, grudge/cadence, verified-result, confirmation, and persistence behavior unchanged; no new standalone panel or launcher was added.
- Added bounded Hub-facing rival status and exposed established safe settings through `NemesisControlApi`/Aura: enabled, natural ambush requests, zone-entry rivalry lines, Deep Sims notification, and optional LLM rivalry voice.
- Preserved two-argument `select(name)` plus clear/confirm/cancel actions. The current Hub renderer does not render arbitrary argument-entry actions, so selection remains transport/API-ready rather than fabricating candidate state or changing Hub.
- Added deterministic tests for concise no-rival/rival/pending-confirmation presentation while retaining state-store persistence tests.


## Unreleased - Suite UI/API coherence handoff

- Added optional, versioned `NemesisControlApi` discovery/control surface for Suite Hub without a hard Hub dependency.
- Kept standalone commands and core gameplay authority intact.
- Documented the retained panel/launcher policy and Lunaris live-test requirement.
- Added a primitive-only Hub surface for rival status/candidates/select/clear while preserving one-rival state and existing confirmation semantics.
