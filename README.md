# Forgotten Roads: Nemesis

Part of the **Forgotten Roads for Erenshor** mod collection.

A standalone persistent rival system for Erenshor. It works without Deep Sims or an LLM and never edits an Erenshor save.

**Standalone scope:** Nemesis selection, persistence, cadence, taunts/replies, zone-entry lines, and social rivalry work without any sibling mod. Actual rival fights are an optional PvP integration. Without Erenshor PvP installed there are no verified match results, so the social-only grudge contribution remains capped at 4 points and the rivalry stays in the `new` stage by design.

## Candidate selection

Automatic selection fails closed when native Friends or Guild state is unknown. It never chooses a Friend,
prefers eligible Sims who are neither Friends nor guildmates, and falls back to an eligible guildmate only
when no preferred candidate exists. An explicit `/enemesis select <Sim name>` may deliberately choose a Friend;
that player-directed choice is kept separate from automatic selection policy.

## Implemented in 0.3.0

- **Zero-command first run.** Once a player character and authoritative Sim/Friends/Guild state are ready, Nemesis selects from the existing automatic candidate policy and persists that rival. The picker uses a stable character-scoped hash over candidate identities rather than alphabetically choosing the first Sim.
- **Persistent stable identity.** New assignments store `SimPlayerTracking.simIndex` plus display name fallback metadata. Existing 0.2.x name-only assignments are upgraded in place when their Sim can be resolved. A missing rival is retained as temporarily unavailable until an authoritative persistent roster has failed to resolve it repeatedly for at least 30 seconds.
- **Manual override remains advanced control.** `/enemesis select <Sim>` persists a deliberate choice; `/enemesis reroll` (with `random` retained as an alias) requests another safe automatic candidate; `/enemesis disable` is a per-character opt-out and prevents immediate auto-reassignment.
- **Natural two-way rivalry chat.** An exact current-rival address such as `Ariadne, keep talking.` (also accepted inside `/group` or `/p`) is owned by Nemesis, while unrelated party/local chat falls through untouched. `/enemesis reply <text>` uses the same path.
- **Duplicate-response boundary.** Nemesis' `TypeText.CheckCommands` prefix is ordered before the current Deep Sims prefix. Once Nemesis accepts an exact-address line it clears/consumes the input, so Deep Sims cannot also schedule a generic party response for the same line.
- **Bounded HEARD rivalry thread.** At most six short player/Nemesis lines persist per character as conversational context. They remain explicitly HEARD and never enter the verified PvP record.
- **Optional Deep Sims voice, no second model.** Nemesis continues to call `ErenshorDeepSims.NemesisEventBridge.RequestNemesisLine(...)` by reflection. If Deep Sims is absent/refuses/fails/times out, deterministic Nemesis templates provide the bounded fallback.
- **Native chat presentation.** Rival messages contain visible text only (`Ariadne tells you: ...`). Nemesis learns the actual runtime native incoming/outgoing tell color argument from vanilla `UpdateSocialLog.LogAdd(text, color)` traffic and supplies that separately. Until a safe native tell style has been observed it uses the one-argument native `LogAdd(text)` path instead of guessing a color token. Literal `<color=...>` is never embedded.
- `/enemesis status` / `diagnose` now expose bounded assignment, identity availability, candidate count, Deep Sims availability, conversation state, and native chat-style status without private paths/prompts/account data.

### Conversation ownership

Nemesis consumes a normal player line only when the **current** rival's exact name starts the message and is immediately followed by directed punctuation (comma, colon, hyphen, or em dash). Examples:

- `Ariadne, keep talking.` -> Nemesis reply route.
- `/group Ariadne: we'll see.` -> Nemesis reply route, not generic Deep Sims party response.
- `Dancer, what do you think?` -> not Nemesis-owned when Ariadne is the rival.
- `/group anyone ready?` -> untouched.
- `Ariadne is nearby.` -> untouched; a mere name mention is not strong enough.

### Chat-color compatibility

Erenshor's current chat sink still exposes `UpdateSocialLog.LogAdd(string text, string color)`, but the supplied current project demonstrates that guessed legacy color strings can leak rich-text markup on some builds. 0.3.0 therefore does not hardcode a Nemesis color name. It reuses a color value actually observed from vanilla tell traffic; color stays metadata and the message string stays markup-free. If no native tell color has been observed yet, it uses `LogAdd(text)` and favors correct/readable native presentation over inventing an unverified purple encoding.

## Behavior retained from 0.2.0

- One Nemesis per player character, persisted in a mod-owned sidecar file (`plugins/config/ErenshorNemesis/nemesis-state.dat`) and keyed from the verified save-slot index plus the character name, so two slots sharing a name keep separate rivalries. Data written by 0.1.0 under the name-only key is migrated once.
- Same-level candidate discovery (default +/-3), excluding the current party, the player, the player's own characters in other save slots, GM/special Sims, tutorial Sims, remote co-op humans, and invalid or blank profiles. A same-zone Sim can be a social rival; PvP independently refuses to build an off-map party from anyone still present in the zone.
- Expanded NPC-style template pools for designation, per-stage taunts, per-stage replies, player victory, Nemesis victory, player escape, Nemesis retreat, and ambush arrival. Lines stay good-natured and never invent shared history, loot, or combat details.
- Bounded grudge stages (`new`, `rival`, `heated`) derived mainly from verified match results.
- Persistent seeded dialogue variation: a fresh rivalry receives its own seed, each interaction advances the sequence, and neither the previous index nor the previous line repeats immediately even across a stage or pool change. Gameplay rolls and automatic/reroll candidate selection never use the dialogue seed.
- Restart-safe cadence. Taunt and ambush opportunity deadlines are stored as UTC timestamps, so restarting cannot reroll a pending timer or bring an encounter forward. A deadline that elapsed while logged out simply arrives on the next tick. No long blocking timers are used.
- Live level compatibility checks. A Nemesis outside the configured range or currently in the player's party becomes dormant with a stated reason and wakes up on its own. It is never silently replaced.
- Optional reflection-only PvP bridge (contract v2). The PvP mod independently requires PvP enabled, an allowed non-protected zone, an off-map eligible Nemesis, valid party scaling, cooldown availability, and a clear spawn area.
- Optional Deep Sims bridge for grounded party reactions and guarded one-line Nemesis voice generation.

## Result handling

PvP classifies every terminal outcome and Nemesis applies only meaningful ones:

| Classification | Effect |
| --- | --- |
| `player_win` / `nemesis_win` | +3 grudge points, rivalry dialogue |
| `player_fled` / `enemy_retreated` | +2 grudge points, rivalry dialogue |
| `cancelled` | counted for diagnostics only; no points, no dialogue |
| `invalid` (interference, spawn failure, internal error) | counted for diagnostics only; no points, no dialogue |

Results are read from PvP's bounded non-destructive result queue rather than a single latest value, so two results between polls, or a late-starting poll, cannot lose one. Applied match ids are remembered persistently, so a result is never counted twice across restarts.

## Escalation

- `new` (0-4 points): early rivalry.
- `rival` (5-11): shorter taunt cadence.
- `heated` (12+): stronger dialogue, 15 percentage points more ambush opportunity chance, and shorter cadence.

Verified wins and losses add 3 points, verified disengagements add 2, and replies/taunts contribute at most 4 points combined - one short of `rival`. Social command spam therefore cannot escalate the rivalry on its own.

Natural ambush opportunities are gated on the configured minimum player level (default 5), a minimum rivalry age (default 20 minutes), and current level compatibility - not on stage, since stage is driven by fights and fights need ambushes to happen. Stage instead scales how often an opportunity converts: 60% of the configured chance at `new`, full chance at `rival`, and +15 points at `heated`.

`/enemesis ambush` remains available as a test request below the natural gates, but still cannot bypass PvP's own level, zone, cooldown, or spawn rules. A successful forced request consumes the current natural opportunity window, so testing cannot stack extra encounters. The fight it produces counts normally, because it is a real verified fight.

## Zone-entry lines

Changing zone during play arms a one-off opportunity 9-21 seconds after arrival. It produces a line
only if all of these hold, so it stays an occasional remark rather than a travel notification:

- `Cadence/ZoneEntryTaunts` is on (`/enemesis zone off` to disable).
- At least `ZoneEntryMinimumMinutes` (default 45) since the last zone line. The timestamp is UTC, so
  the cooldown survives a restart.
- The zone is not among the last 6 already remarked on, persisted per character.
- A `ZoneEntryChancePercent` roll (default 25) succeeds.
- The player is alive, not zoning, and the Nemesis is level-compatible.

Loading into the world is not an arrival and never triggers one. A zone line also consumes the
ordinary taunt window, so total chatter does not double, and it awards no grudge points - arriving
somewhere is not a rivalry event.

**What the line may say:** the Nemesis is off-map and these are tells, so a line may note *where*
the player has turned up and nothing more. It never claims the Nemesis is present, never describes
what is happening or happened there, and never invents shared history in that place. The only fact
used is the verified current scene name, rendered from the game's own identifier
(`PortAzure` → `Port Azure`).

## Protecting an established rivalry

A rivalry with at least one verified fight behind it is *established*, and its record cannot be
discarded by a single command:

- `select <other Sim>` prints exactly what would be lost and waits for `/enemesis confirm` (60
  seconds, or `/enemesis cancel`). Below that bar, selection switches immediately as before.
- `select <your current Nemesis>` is a no-op that reports the record, so re-running the same
  command can never wipe it.
- `disable` also asks for confirmation, keeps the record, and remembers the name. Selecting that
  name again *resumes* the rivalry with wins, losses, seed, and cadence intact rather than
  starting over.
- A pending confirmation is dropped on character switch, mod shutdown, and after 60 seconds, so it
  can never be redeemed against a different character's Nemesis.

## Dialogue safety

- Player reply text is HEARD context only. It is stripped of markup and instruction-like characters, screened for prompt-injection phrasing, bounded to 100 characters, and never enters the verified record or persistent event history.
- The optional generated line is subject to Deep Sims sanitization plus its grounding, instruction-leak, completeness, length, and voice guards. Anything rejected falls back to the template.
- A queued generated line that is replaced, dropped, or lost to shutdown still produces its template exactly once, via a bounded timeout (default 12s, `Nemesis/VoiceTimeoutSeconds`). A newer interaction speaks the older line's template immediately, so interactions stay in order and never double up.
- A callback that arrives after a character switch, Nemesis change, zone transition, or mod shutdown is dropped rather than spoken.

## Commands

`/enemesis` and `/dsnemesis` are aliases.

- `status`
- `candidates`
- `select <Sim name>`
- `random`
- `confirm` / `cancel` - answer a pending replacement or stop request
- `disable` - stop the rivalry; the record is kept and re-selecting the same name resumes it
- `history`
- `llm on|off` - toggle optional guarded LLM Nemesis voice
- `natural on|off` - toggle natural Nemesis ambush opportunities
- `zone on|off` - toggle zone-entry lines
- `taunt` - immediate dialogue test
- `reply <message>`
- `ambush` - immediate PvP request test; does not bypass PvP rules
- `diagnose`
- `selftest`

## Test path

1. Log into a character and run `/enemesis candidates`.
2. Run `/enemesis select <exact name>` and `/enemesis status`.
3. Run `/enemesis taunt`, then `/enemesis reply see you out there`.
4. Enable PvP, enter a configured wild ambush zone, leave combat, move to a clear area away from ordinary NPCs, and run `/enemesis ambush`.
5. If blocked, run `/enemesis diagnose` and `/epvp diagnose`. The block is intentional until both systems report eligibility.
6. Finish the fight and run `/enemesis history`; the verified record should update within several seconds.
7. Zone out mid-fight once. The match should be recorded as cancelled and leave the record unchanged.

Natural ambushes default to a 20% base roll at opportunities 35-75 minutes apart. Change these under `[Ambush]` in the Lunaris config UI.

## Build / install

This version requires **native Lunaris** — BepInEx is no longer required. `BUILD_AND_INSTALL.ps1` locates the current Erenshor install and the Lunaris developer reference, compiles, and installs only `ErenshorNemesis.dll` to `<Erenshor>\plugins\`. Lunaris manages enable/disable and the general settings (`[Nemesis]`, `[Ambush]`, `[Selection]`, `[Cadence]`). Per-character rivalry record/timestamps/dialogue-variety state live in their own mod-owned sidecar file, not in the Lunaris config UI — that data was never meant to be hand-edited as a "setting." A legacy BepInEx release remains available in this repository's Git history.

**Status:** the deterministic test suite (`tests/RUN_TESTS.ps1`) passes, including sidecar persistence, candidate selection, and control-policy coverage. A fresh native build and plugin-identity audit remain pending because the current Lunaris resolver is unavailable in this session. The mod's existing `/enemesis selftest` in-game self-check has not yet been run live under Lunaris. Do not assume hot-reload safety until that pass is done.

## Credits and Inspiration

### Compatibility / related projects

- **[Erenshor COOP](https://github.com/MizukiBelhi/ErenshorCoop) by MizukiBelhi** — important technical reference and compatibility target for remote-human/networked-Sim detection. I have also tested against a locally updated copy for recent Erenshor and Deep Sims compatibility.

This is an unofficial, community-made mod for Erenshor and is not affiliated with or endorsed by the game's developer.


## Optional Forgotten Roads Hub integration

Forgotten Roads Hub is **optional**. Nemesis exposes a versioned `NemesisControlApi`/Aura surface without referencing Hub types or assuming Hub load order. Hub can render concise rival status and the safe user-facing settings now exposed through `settings.basic`/`settings.advanced`: Nemesis enabled, natural ambush requests, zone-entry rivalry lines, Deep Sims notification, and optional LLM rivalry voice.

Nemesis intentionally has no dedicated module panel or floating launcher; `/enemesis` remains the complete standalone command surface. Selection/clear/confirm/cancel actions continue through the existing deferred Nemesis state machine, including established-rival confirmation semantics and persistent record protection.

The current Forgotten Roads Hub renderer can transport two-argument actions but does not yet render arbitrary argument-entry/action controls on a module page. The provider therefore advertises `select(name)`, `clear`, `confirm`, and `cancel` over Aura without fabricating candidate state or modifying Hub in this workstream.
