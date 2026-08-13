# Erenshor Nemesis

A standalone persistent rival system for Erenshor. It works without Deep Sims or an LLM and never edits an Erenshor save.

## Implemented in 0.2.0

- One explicitly selected Nemesis per player character, persisted in a mod-owned sidecar file (`plugins/config/ErenshorNemesis/nemesis-state.dat`) and keyed from the verified save-slot index plus the character name, so two slots sharing a name keep separate rivalries. Data written by 0.1.0 under the name-only key is migrated once.
- Same-level candidate discovery (default +/-3), excluding the current party, the player, the player's own characters in other save slots, GM/special Sims, tutorial Sims, remote co-op humans, and invalid or blank profiles. A same-zone Sim can be a social rival; PvP independently refuses to build an off-map party from anyone still present in the zone.
- Expanded NPC-style template pools for designation, per-stage taunts, per-stage replies, player victory, Nemesis victory, player escape, Nemesis retreat, and ambush arrival. Lines stay good-natured and never invent shared history, loot, or combat details.
- Bounded grudge stages (`new`, `rival`, `heated`) derived mainly from verified match results.
- Persistent seeded dialogue variation: a fresh rivalry receives its own seed, each interaction advances the sequence, and neither the previous index nor the previous line repeats immediately even across a stage or pool change. Gameplay rolls (ambush opportunity, `random` selection) never use the dialogue seed.
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

**Status:** this native build compiles cleanly against the installed Lunaris/Assembly-CSharp. A new deterministic test (`tests/RUN_TESTS.ps1`) covers the new sidecar persistence store (round-trip, section isolation, escaping, legacy-key migration semantics) and passes. The mod's existing `/enemesis selftest` in-game self-check has not yet been run live in-game under Lunaris. Do not assume hot-reload safety until that pass is done.

## Credits and Inspiration

### Compatibility / related projects

- **[Erenshor COOP](https://github.com/MizukiBelhi/ErenshorCoop) by MizukiBelhi** — important technical reference and compatibility target for remote-human/networked-Sim detection. I have also tested against a locally updated copy for recent Erenshor and Deep Sims compatibility.

## Development note

This project has been developed heavily with AI-assisted coding tools. The goal has been to build features I wanted to use in Erenshor, with development guided through design, testing, playtesting, audits, and iteration against the game. Bug reports, code review, corrections, and contributions from experienced Erenshor modders are welcome.

This is an unofficial, community-made mod for Erenshor and is not affiliated with or endorsed by the game's developer.
