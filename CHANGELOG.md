# Changelog

## 0.3.3 - native ChatLogLine whisper presentation

- Kept 0.3.2 whisper ownership, response buckets, rivalry facts, anti-repeat state, and optional Deep Sims/Duel bridges unchanged; this pass changes presentation only.
- Nemesis-owned outgoing/incoming tells now use native-style `[WHISPER TO]` / `[WHISPER FROM]` text and a typed `ChatLogLine` with `LogType.Whisper`, so channel/filter routing no longer falls through to white Say/default chat.
- Added a typed native-style observer that learns `ChatLogLine.ColorString` from real Whisper traffic. Because Nemesis intercepts its own target before vanilla can bootstrap a first style observation, the known native whisper hex `#FF62D1` is the bounded compatibility fallback instead of uncolored one-argument `LogAdd`. The old two-string observer remains secondary compatibility only.
- No rich-text `<color=...>` markup is embedded in visible messages. Verified against the installed `Assembly-CSharp.dll`: native `/whisper` (`SimPlayerMngr.SimReceiveMsg`) writes outgoing as `[WHISPER TO] {target}: {msg}` and flushes queued incoming replies (`SimPlayerMngr.Update`, `Responses` list) as `[WHISPER FROM] {name}: {msg}`, both via `new ChatLogLine(text, ChatLogLine.LogType.Whisper, "#FF62D1")` - every native call site hardcodes that literal hex, so there is no live theming to capture and no reusable presentation helper to call into (each site inlines its own `ChatLogLine` construction). Nemesis's own `[WHISPER TO]` / `[WHISPER FROM]` text and `#FF62D1` fallback match this exactly.

## 0.3.2 - whisper interception, fact-gated rival personality

- Whispers/tells (`/whisper`, `/tell`, `/w`, `/t <target> <message>`) addressed to the exact current
  Nemesis by name are now intercepted before native chat sees them. This closes the actual gap behind
  a live observation: whispering "hey" to a rival previously fell straight through Nemesis (which only
  ever recognized party/local direct-address with directed punctuation) to native Erenshor's own Sim
  chat AI, which is why the reply looked generic ("Hello, you XPing today?") instead of coming from
  Nemesis at all. New `NemesisConversationPolicy.TryExtractWhisperAddress` mirrors the same
  exact-name, case-insensitive matching already used for natural chat; unrelated whisper targets and
  ordinary party/local chat are still never consumed.
- Replaced the old three-tier `Stage()`-only reply pool with a fact-gated tone system
  (`NemesisResponsePolicy`/`NemesisResponseBucket`, new `NemesisResponseFacts` struct). A category is
  only ever eligible when a real fact backs it: level-relative flavor
  (`CompetitiveAhead`/`CompetitiveBehind`) requires a real, currently-known level comparison; win/loss
  flavor (`RecentWin`/`RecentLoss`) requires a verified duel result recorded against this exact
  Nemesis within the last 72 hours; `LongTimeNoSee` requires a real gap since the last conversation.
  Ordinary `NeutralGreeting`/`NeutralSmallTalk`/`CompetitiveGeneral`/`Respectful` chatter needs no
  fact and stays the bulk of what a rival says - a recurring rival is meant to read as competitive
  over many interactions, not hostile in every line.
- Added a small bounded per-character history of the last 3 chosen reply *categories*
  (`RecentResponseBuckets`) so the same tone doesn't repeat several turns running, on top of the
  pre-existing per-line anti-repeat within a category. This is a few bucket names, not a stored
  conversation - the existing bounded HEARD chat log is untouched and still the only transcript kept.
- Added an optional, reflection-only bridge to Erenshor-Duel's public `PracticeDuelEvents` contract
  (v2). When Duel is present, a `duel_completed` event naming this exact Nemesis as the opponent
  records `LastDuelVerdict`/`LastDuelUtcTicks`, which is the only source that can ever make
  `RecentWin`/`RecentLoss` eligible - no duel result is ever fabricated from a challenge, accept,
  decline, or timeout event, and events naming a different Sim are ignored. Nemesis has no
  compile-time reference to `ErenshorDuel`; it compiles, loads, and answers whispers identically with
  Duel entirely absent, exactly like the existing optional PvP and Deep Sims bridges.
- Extended `/enemesis diagnose` with bounded `standaloneTone=`, `lastResponseBucket=`,
  `recentRivalEvent=`, `templateHistoryCount=`, and `duel=` fields. No raw conversation text is ever
  logged, and none of this runs from the per-frame `Tick()` path.
- Friend status, the same-character progression-cohort gate (`TiedToSlot ==
  GameData.CurrentCharacterSlot.index`), and the read-only-native-state guarantees from 0.3.1 are all
  unchanged: dialogue never requires Friend status, and Nemesis still never writes `TiedToSlot` or
  `FriendedBy` and never invokes `/friend`.

## 0.3.1 - same-character progression-cohort gate

- Automatic rival assignment now requires `candidate.TiedToSlot == GameData.CurrentCharacterSlot.index`
  before any Friend/Guild preference scoring, verified against the currently installed
  `Assembly-CSharp.dll`: native `SimPlayerMngr.LoadActualSims`/`SimPlayerCatchupCode` both catch a
  Sim's level up toward `GameData.SaveSlots[SimPlayerTracking.TiedToSlot].CharLevel`, so a candidate
  whose `TiedToSlot` points at a different (or no) character does not actually progress with this
  one. Fails closed - an unresolvable current character slot, or one of native's non-character
  sentinel bindings (`TiedToSlot == 12` generic-creation-bucket, `== 99` never-tied), never counts
  as a match. Level-proximity, current zone, nearby status, Friend status, and name matching remain
  unused as substitutes for this gate.
- Friend status is deliberately not required or substituted: `/friend` always rewrites `TiedToSlot`
  to the acting character's slot (even when unfriending, which only clears `FriendedBy` back to
  `-1`), and a non-Friended Sim can already share `TiedToSlot` with the active character by chance.
  A same-slot, non-Friended Sim remains eligible; the existing prefer-non-Friend/prefer-non-Guild
  ordering is unchanged and still runs after the new gate.
- Manual `/enemesis select <Sim>` now applies the same same-character-slot requirement (previously
  unenforced) and, when that is specifically why a request was refused, reports it explicitly:
  `'<Sim>' tracks a different character's progression and cannot become this character's rival.`
  This is a deliberate behavior change, not a silent one.
- Persisted rivals are re-validated on load, per active character. `TiedToSlot` is a Sim-global
  native field, not per-character: another character (a different save slot) can `/friend` this
  same Sim later and silently repoint it away from this character's cohort. That case is now
  detected and the rival is invalidated/reselected immediately, the same "invalidate cleanly" path
  already used for a permanently-missing identity - not debounced like a merely-not-yet-loaded
  roster, since the mismatch is a definitively known fact rather than a transient absence.
  Per-character persistence itself (`Character.slot{N}_{name}` sections) already existed and is
  unchanged; switching characters already selects that character's own persisted rival.
- Added `/enemesis diagnose`'s bounded `rivalSlot=/currentSlot=/sameCharacter=/level=/friend=`
  fields, and an "AUTO: waiting for authoritative native ... state" candidates message that now also
  accounts for pending Character-slot authority alongside the existing Friends/Guild wait states.
- New `NemesisProgressionCohortPolicy` (pure, Unity-free) plus `NemesisNativeSocialRoster` read-only
  accessors (`TryCurrentProgressionCohort`, `TryIsSameProgressionCohort`). Nemesis never writes
  `TiedToSlot`/`FriendedBy`, never invokes `/friend` behavior, and never parses save files at
  runtime; it only reads the already-loaded `GameData.CurrentCharacterSlot`, `GameData.SaveSlots`,
  and `SimPlayerTracking.TiedToSlot`.

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
