# Changelog

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
