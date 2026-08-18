# Erenshor Nemesis 0.3.0 - focused live validation

Use the **current installed Erenshor/Lunaris assemblies** and a staging-built candidate DLL. Source tests cannot prove runtime candidate availability, native chat color metadata, Harmony interaction, or Deep Sims callbacks.

## A. Automatic assignment

- [ ] Use a character scope with no current Nemesis assignment.
- [ ] Do **not** run `/enemesis select` or `/enemesis reroll`.
- [ ] After character + native Friends/Guild/Sim roster readiness, `/enemesis status` leaves `Awaiting Rival` and shows one rival.
- [ ] `/enemesis diagnose` reports `assignment=auto` and a resolved stable identity.
- [ ] No rapid repeated assignment/status spam occurs while no candidates exist.
- [ ] If candidates appear later, one can be assigned without a command.

## B. Persistence / identity

- [ ] Record the selected rival and `/enemesis diagnose` output.
- [ ] Zone once; the same rival remains.
- [ ] Restart if practical; the same rival remains.
- [ ] A temporarily unavailable rival is retained rather than immediately replaced.
- [ ] A manual `/enemesis select <Sim>` survives zone/restart and reports `assignment=manual`.
- [ ] Switching to another player-character slot does not inherit the first character's rival.

## C. Native chat presentation

- [ ] Trigger `/enemesis taunt`.
- [ ] Visible text is shaped like `Ariadne tells you: ...`.
- [ ] There is **no** literal `<color=` or any other rich-text markup in the visible line.
- [ ] After genuine vanilla incoming tell traffic has been observed, `/enemesis diagnose` reports `chatStyle=native-tell-captured`.
- [ ] Before a tell style is observed, `chatStyle=native-default-no-markup` remains readable and markup-free.

## D. Two-way rivalry with Deep Sims available

- [ ] Let the Nemesis speak once.
- [ ] Type `<NemesisName>, you're awfully confident.` as normal chat.
- [ ] Exactly one Nemesis response appears.
- [ ] No ordinary Deep Sims party response is also scheduled for that same input.
- [ ] Reply again (`<NemesisName>: we'll see about that.`); response continuity reflects recent HEARD chat without inventing a fight, loot, quest, location, or other history.
- [ ] Response contains no assistant/database/system/prompt wording.

## E. Ownership boundary

- [ ] Type another Sim's name: `Dancer, what do you think?` while Ariadne is the current rival. Nemesis does not consume it.
- [ ] Type ordinary `/group anyone ready?`; Nemesis does not consume it.
- [ ] Type `Ariadne is nearby.`; a mere name mention without directed punctuation is not consumed.
- [ ] `/enemesis reply testing this path` reaches the same current-rival reply route.
- [ ] With no current rival, `/enemesis reply test` reports the safe Awaiting Rival result.

## F. Deep Sims unavailable / failure

- [ ] Disable/unload Deep Sims, or make its local inference unavailable if practical.
- [ ] Nemesis still starts, assigns/persists its rival, and emits deterministic template taunts/replies.
- [ ] No exception text reaches player chat.
- [ ] No recursive retry/request spam occurs.

## G. Cadence / rivalry facts

- [ ] Ordinary taunts respect the persisted 18-45 minute configured window (stage scaling still applies).
- [ ] Zone-entry lines remain chance/cooldown gated and consume the ordinary taunt window.
- [ ] Only verified PvP outcomes change the fight record/grudge as before.
- [ ] HEARD player/Nemesis chat does not create verified fights/history.

## H. Install/hash gate

Run `BUILD_AND_INSTALL.ps1` without `-Install` first.

- [ ] Candidate is produced only under `staging/`.
- [ ] Record staged SHA-256.
- [ ] Close Erenshor before any install.
- [ ] Re-run with `-Install`; current installed DLL is backed up outside source/Git first.
- [ ] Installed SHA-256 equals staged SHA-256.
- [ ] Exactly one `ErenshorNemesis.dll` exists under the active plugin root.
