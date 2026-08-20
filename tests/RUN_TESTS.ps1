$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModRoot = Split-Path -Parent $ScriptRoot

function Find-Csc {
    foreach ($path in @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )) { if (Test-Path $path) { return $path } }
    throw "csc.exe not found. Install the .NET Framework Developer Pack or Visual Studio Build Tools."
}

$csc = Find-Csc
$out = Join-Path $env:TEMP "ErenshorNemesis.StateStoreTests.exe"
& $csc /nologo /target:exe ("/out:{0}" -f $out) `
    (Join-Path $ModRoot "src\NemesisStateStore.cs") `
    (Join-Path $ModRoot "src\NemesisHubPresentation.cs") `
    (Join-Path $ModRoot "src\NemesisCandidateSelectionPolicy.cs") `
    (Join-Path $ModRoot "src\NemesisProgressionCohortPolicy.cs") `
    (Join-Path $ModRoot "src\NemesisAssignmentPolicy.cs") `
    (Join-Path $ModRoot "src\NemesisConversationPolicy.cs") `
    (Join-Path $ModRoot "src\NemesisResponsePolicy.cs") `
    (Join-Path $ScriptRoot "StandaloneStateStoreTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Nemesis deterministic test compilation failed." }
try {
    & $out
    if ($LASTEXITCODE -ne 0) { throw "Nemesis deterministic tests failed with exit code $LASTEXITCODE." }
}
finally { Remove-Item $out -Force -ErrorAction SilentlyContinue }

$directorSource = Get-Content (Join-Path $ModRoot "src\NemesisDirector.cs") -Raw
$pluginSource = Get-Content (Join-Path $ModRoot "src\ErenshorNemesisPlugin.cs") -Raw
$nativeSocialSource = Get-Content (Join-Path $ModRoot "src\NemesisNativeSocialRoster.cs") -Raw

# Automatic assignment/persistence boundaries.
foreach ($token in @('EnsureAutomaticAssignment','ChooseStableAutomaticCandidate','NemesisStableId','AssignmentOrigin','TrackingStableId','tracking.simIndex','MissingIdentityIsPermanent')) {
    if ($directorSource -notmatch [regex]::Escape($token)) { throw "Missing automatic-assignment source guard: $token" }
}
if ($directorSource -notmatch 'ApplySelection\(selected,\s*NemesisSelectionOrigin\.Automatic\)') { throw "Automatic assignment does not persist through ApplySelection." }
if ($directorSource -notmatch 'AssignmentOriginState\.Value\s*=\s*origin\s*==\s*NemesisSelectionOrigin\.Automatic\s*\?\s*"auto"\s*:\s*"manual"') { throw "Manual/auto assignment origin guard missing." }
if ($directorSource -notmatch 'AssignmentOriginState\.Value\s*=\s*"disabled"') { throw "Explicit stop does not suppress auto reassignment." }
if ($directorSource -notmatch 'PersistentRosterAuthoritative\(\)') { throw "Persisted-rival invalidation is not roster-authoritative." }

# Current candidate policy remains source-backed and Friends stay explicit-only.
if ($nativeSocialSource -notmatch 'sim\.FriendedBy\s*==\s*currentSlot' -or $nativeSocialSource -notmatch '!sim\.IsGMCharacter') { throw "Native Friends/GM guard missing." }
if ($directorSource -notmatch 'FRIENDS - explicit /enemesis select only' -or $directorSource -notmatch 'GUILD FALLBACK:') { throw "Candidate policy presentation guard missing." }
if ($directorSource -notmatch 'ExplicitCandidates\(\)\.FirstOrDefault') { throw "Manual selection path missing." }

# Exact-address ownership + compatibility command; Nemesis runs before Deep Sims and clears input.
foreach ($token in @('TryHandleNaturalAddress','NemesisConversationPolicy.TryExtractDirectAddress','reply <text>','HarmonyBefore("forgetwhtuno.erenshor.deepsims")','ClearInput(input)')) {
    if (($directorSource + $pluginSource) -notmatch [regex]::Escape($token)) { throw "Conversation ownership guard missing: $token" }
}

# Deep Sims stays optional; no direct Ollama/model implementation is allowed in Nemesis.
if ($directorSource -notmatch 'ErenshorDeepSims\.NemesisEventBridge' -or $directorSource -notmatch 'RequestNemesisLine') { throw "Deep Sims Nemesis bridge missing." }
foreach ($forbidden in @('Ollama','qwen3.5:2b','qwen3.5:4b','HttpClient','/api/generate','/api/chat')) {
    if (($directorSource + $pluginSource) -match [regex]::Escape($forbidden)) { throw "Nemesis contains forbidden independent model/client token: $forbidden" }
}

# 0.3.3 native whisper presentation: visible text stays markup-free, while channel/filter/color travel
# through the typed ChatLogLine path. The exact current game's typed API still requires local compile
# review because Assembly-CSharp.dll is intentionally absent from this review packet.
if ($directorSource -match 'ChatRivalTell\([^\r\n]*<color=') { throw "Visible Nemesis line embeds color markup." }
if ($directorSource -match 'ChatRivalTell\([^\r\n]*"magenta"') { throw "Nemesis line still hardcodes magenta." }
foreach ($token in @(
    'NativeWhisperFallbackColor = "#FF62D1"',
    'new ChatLogLine(value, ChatLogLine.LogType.Whisper, NativeWhisperColor())',
    'UpdateSocialLog.LogAdd(new ChatLogLine',
    'NoteNativeSocialStyle(ChatLogLine line)',
    'line.MyLogType & ChatLogLine.LogType.Whisper',
    'line.ColorString',
    'native-chatlog-whisper-captured',
    'native-chatlog-whisper-fallback',
    'HarmonyPatch(typeof(UpdateSocialLog), "LogAdd", new Type[] { typeof(ChatLogLine) })',
    'NoteLegacyNativeSocialStyle')) {
    if ($pluginSource -notmatch [regex]::Escape($token)) { throw "Native ChatLogLine whisper-style guard missing: $token" }
}
foreach ($token in @('[WHISPER TO] ', '[WHISPER FROM] ')) {
    if ($directorSource -notmatch [regex]::Escape($token)) { throw "Native whisper visible-format guard missing: $token" }
}
if ($pluginSource -match '(?s)private static void WriteNativeTell\(string value\).*?UpdateSocialLog\.LogAdd\(value\);.*?finally') {
    throw "Whisper presentation regressed to uncolored one-argument LogAdd(value)."
}

# HEARD conversation and VERIFIED record remain separate stores/labels.
if ($directorSource -notmatch 'RecentConversation' -or $directorSource -notmatch 'RememberConversation' -or $directorSource -notmatch 'VerifiedRecord\(\)') { throw "Conversation provenance guard missing." }
if ($directorSource -notmatch 'PLAYER MESSAGE \(HEARD\)') { throw "HEARD player-message label missing from bounded context." }

Write-Host "PASS: Nemesis automatic assignment, conversation ownership, optional Deep Sims, provenance, and native-chat source guards"

# 0.3.1 same-character progression-cohort gate.
$cohortSource = Get-Content (Join-Path $ModRoot "src\NemesisProgressionCohortPolicy.cs") -Raw
$candidateSource = Get-Content (Join-Path $ModRoot "src\NemesisCandidateSelectionPolicy.cs") -Raw

if ($cohortSource -notmatch 'GenericBucketSlot\s*=\s*12' -or $cohortSource -notmatch 'UnassignedSlot\s*=\s*99') {
    throw "Cohort guard failed: native sentinel TiedToSlot values (12/99) are not modeled."
}
if ($candidateSource -notmatch 'fact\.CohortKnown\s*&&\s*fact\.SameCohort' -or
    $candidateSource -notmatch 'if \(!fact\.CohortKnown \|\| !fact\.SameCohort\) return NemesisAutomaticPool\.None;') {
    throw "Cohort guard failed: automatic pool does not hard-gate on the same-character progression cohort."
}
if ($candidateSource -notmatch 'fact\.BaseEligible && fact\.CohortKnown && fact\.SameCohort') {
    throw "Cohort guard failed: explicit/manual eligibility does not require the same progression cohort."
}
# The cohort gate must be checked before the Friend/Guild preference gates within AutomaticPool.
if ($candidateSource -match '(?s)internal static NemesisAutomaticPool AutomaticPool.*?\{(.*?)\n        \}') {
    $body = $Matches[1]
    $cohortAt = $body.IndexOf('fact.CohortKnown')
    $friendAt = $body.IndexOf('fact.FriendKnown')
    if ($cohortAt -lt 0 -or $friendAt -lt 0 -or $cohortAt -gt $friendAt) {
        throw "Cohort guard failed: same-cohort gate does not run before Friend/Guild preference scoring."
    }
} else { throw "Cohort guard failed: could not locate AutomaticPool body for ordering check." }

if ($nativeSocialSource -notmatch 'TryCurrentProgressionCohort' -or $nativeSocialSource -notmatch 'TryIsSameProgressionCohort') {
    throw "Cohort guard failed: read-only native cohort accessors missing."
}
if ($directorSource -notmatch 'NemesisNativeSocialRoster\.TryCurrentProgressionCohort' -or
    $directorSource -notmatch 'buckets\.CohortAuthorityKnown') {
    throw "Cohort guard failed: candidate snapshot does not resolve/carry cohort authority."
}

# Persistence re-validation: a persisted rival is invalidated/reselected, not silently kept, once its
# TiedToSlot definitively belongs to a different character.
if ($directorSource -notmatch 'InvalidateOnCohortMismatch' -or
    $directorSource -notmatch 'nemesis_identity cohort_changed') {
    throw "Cohort guard failed: persisted-rival cohort re-validation on load is missing."
}
if ($directorSource -notmatch 'if \(InvalidateOnCohortMismatch\(legacy, -1\)\) return;' -or
    $directorSource -notmatch 'if \(InvalidateOnCohortMismatch\(resolvedTracking, savedId\)\) return;') {
    throw "Cohort guard failed: both legacy and stable-id reconciliation paths must re-validate the cohort."
}

# Manual selection: same-slot gate is required, and a cohort-specific rejection is reported rather
# than folded silently into the generic 'not eligible' message.
if ($directorSource -notmatch "tracks a different character's progression and cannot become this character's rival") {
    throw "Cohort guard failed: manual selection does not surface a clear same-slot rejection."
}

# Diagnostics: bounded, on-demand rivalSlot/currentSlot/sameCharacter view, not per-frame (Diagnose()
# is only reachable from the /enemesis diagnose command handler, never from Tick()).
if ($directorSource -notmatch 'CohortDiagnosticText' -or
    $directorSource -notmatch 'rivalSlot=' -or $directorSource -notmatch 'sameCharacter=') {
    throw "Cohort guard failed: diagnostics do not expose rivalSlot/currentSlot/sameCharacter."
}
if ($directorSource -match '(?s)internal static void Tick\(\).*?\n        \}' -and $Matches[0] -match 'CohortDiagnosticText|Diagnose\(\)') {
    throw "Cohort guard failed: cohort diagnostics must not run from the per-frame/periodic Tick() path."
}

# Nemesis must never write native progression/friend binding fields, and must never parse save
# files at runtime - it only reads the already-loaded GameData/SimPlayerTracking state.
$allNemesisSource = $directorSource + $pluginSource + $nativeSocialSource + $cohortSource + $candidateSource
foreach ($forbiddenWrite in @('\.TiedToSlot\s*=[^=]', '\.FriendedBy\s*=[^=]', '"/friend"', 'File\.ReadAllText', 'StreamReader', 'JsonUtility\.FromJson')) {
    if ($allNemesisSource -match $forbiddenWrite) { throw "Cohort guard failed: forbidden native-binding write or save-file parsing token matched: $forbiddenWrite" }
}

Write-Host "PASS: Nemesis same-character progression-cohort gate, persistence re-validation, manual-selection rejection, and read-only guards"

# 0.3.2 standalone rivalry-dialogue / personality pass.
$responsePolicySource = Get-Content (Join-Path $ModRoot "src\NemesisResponsePolicy.cs") -Raw
$conversationPolicySource = Get-Content (Join-Path $ModRoot "src\NemesisConversationPolicy.cs") -Raw

# 1/18: a whisper to the exact current Nemesis now reaches Reply() - the live gap where "hey" fell
# through to a generic native Sim line because nothing recognized a plain whisper.
if ($conversationPolicySource -notmatch 'internal static bool TryExtractWhisperAddress') {
    throw "Dialogue guard failed: whisper/tell direct-address parsing is missing."
}
if ($directorSource -notmatch 'internal static bool TryHandleWhisperAddress' -or
    $directorSource -notmatch 'NemesisConversationPolicy\.TryExtractWhisperAddress') {
    throw "Dialogue guard failed: NemesisDirector does not route a recognized whisper into Reply()."
}
if ($pluginSource -notmatch 'NemesisDirector\.TryHandleWhisperAddress') {
    throw "Dialogue guard failed: the chat patch does not check the whisper path at all."
}

# 2/3/4: normal small talk and light competitive flavor are always eligible (no fact required).
if ($responsePolicySource -notmatch 'valid\.Add\(NemesisResponseBucket\.NeutralSmallTalk\);' -or
    $responsePolicySource -notmatch 'valid\.Add\(NemesisResponseBucket\.CompetitiveGeneral\);') {
    throw "Dialogue guard failed: normal small talk / light competitive-general buckets are not unconditionally eligible."
}
if ($directorSource -notmatch 'ReplyNeutralGreeting' -or $directorSource -notmatch 'ReplyNeutralSmallTalk' -or
    $directorSource -notmatch 'ReplyCompetitiveGeneral') {
    throw "Dialogue guard failed: authored normal/competitive-general reply pools are missing."
}

# 7/8/9/10: fact-gated buckets can only ever become valid behind their real fact.
foreach ($token in @(
    'if \(facts\.LevelKnown && facts\.LevelDelta > 0\) valid\.Add\(NemesisResponseBucket\.CompetitiveAhead\);',
    'if \(facts\.LevelKnown && facts\.LevelDelta < 0\) valid\.Add\(NemesisResponseBucket\.CompetitiveBehind\);',
    'if \(facts\.HasRecentDuelFact && facts\.RecentDuelWasNemesisWin\) valid\.Add\(NemesisResponseBucket\.RecentWin\);',
    'if \(facts\.HasRecentDuelFact && !facts\.RecentDuelWasNemesisWin\) valid\.Add\(NemesisResponseBucket\.RecentLoss\);'
)) {
    if ($responsePolicySource -notmatch $token) { throw "Dialogue guard failed: a fact-gated bucket is not actually gated on its real fact: $token" }
}
if ($directorSource -notmatch 'facts\.LevelKnown = true;' -or $directorSource -notmatch 'facts\.LevelDelta = playerLevel - tracking\.Level;') {
    throw "Dialogue guard failed: level-relative facts are not derived from real PlayerLevel()/tracking.Level."
}

# 5/6: bounded anti-repeat at the tone-bucket level, without persisting a full transcript.
if ($responsePolicySource -notmatch 'internal const int RecentBucketHistoryBound = 3;') {
    throw "Dialogue guard failed: bucket repetition history is not small and bounded."
}
if ($directorSource -notmatch 'RecentResponseBuckets' -or $directorSource -notmatch 'NemesisResponsePolicy\.PushHistory') {
    throw "Dialogue guard failed: chosen response buckets are not persisted into a bounded history."
}

# 12/13/14: the same-character progression-cohort gate and its read-only guarantees are unchanged.
if ($cohortSource -notmatch 'GenericBucketSlot\s*=\s*12' -or $cohortSource -notmatch 'UnassignedSlot\s*=\s*99') {
    throw "Dialogue guard failed: progression-cohort sentinel handling regressed."
}

# 15/17: PvP and Deep Sims stay reflection-only optional integrations - unchanged by this pass.
if (($directorSource + $pluginSource) -match 'using ErenshorDeepSims' -or ($directorSource + $pluginSource) -match 'using ErenshorPvP') {
    throw "Dialogue guard failed: a hard compile-time reference to Deep Sims or PvP was introduced."
}

# 16: Practice Duel is a NEW optional integration - reflection-only, like the existing bridges.
if ($directorSource -match 'using ErenshorDuel') { throw "Dialogue guard failed: a hard compile-time reference to Practice Duel was introduced." }
foreach ($token in @(
    'internal static class DuelBridge',
    'GetType\("ErenshorDuel\.PracticeDuelEvents", false\)',
    'internal static void TrySubscribe\(\)',
    'internal static void Unsubscribe\(\)',
    'Delegate\.CreateDelegate\(ev\.EventHandlerType, handlerMethod\)',
    'internal static void HandleDuelCompleted'
)) {
    if ($directorSource -notmatch $token) { throw "Dialogue guard failed: optional Duel bridge is missing expected shape: $token" }
}
if ($directorSource -notmatch 'DuelBridge\.Unsubscribe\(\);') { throw "Dialogue guard failed: Duel event subscription is never released on shutdown." }
if ($directorSource -notmatch 'if \(!string\.Equals\(trimmedOpponent, Name\.Value, StringComparison\.OrdinalIgnoreCase\)\) return;') {
    throw "Dialogue guard failed: HandleDuelCompleted does not require the event's opponent to be the exact current Nemesis."
}
if ($directorSource -notmatch 'if \(!string\.Equals\(type, "duel_completed", StringComparison\.OrdinalIgnoreCase\)\) return;') {
    throw "Dialogue guard failed: only a duel_completed event may record a result."
}

# 18: the deterministic authored fallback is always computed before any optional LLM/Deep Sims call.
if ($directorSource -notmatch 'string fallback = line;' -or
    $directorSource -notmatch 'if \(UseLlmVoice != null && UseLlmVoice\.Value && DeepSimsBridge\.Available\)') {
    throw "Dialogue guard failed: the authored fallback is not unconditionally computed before any optional Deep Sims call."
}

# Diagnostics stay bounded and on-demand only, and never dump raw conversation text.
foreach ($token in @('standaloneTone=', 'lastResponseBucket=', 'recentRivalEvent=', 'templateHistoryCount=', 'deepSims=')) {
    if ($directorSource -notmatch [regex]::Escape($token)) { throw "Dialogue guard failed: /enemesis diagnose is missing expected field: $token" }
}
if ($directorSource -match 'LogInfo\([^\r\n]*RecentConversation\.Value') { throw "Dialogue guard failed: raw conversation text must never be logged." }

# Forbidden native-binding writes still hold with all newly added code included.
$allNemesisSourceV2 = $directorSource + $pluginSource + $nativeSocialSource + $cohortSource + $candidateSource + $responsePolicySource + $conversationPolicySource
foreach ($forbiddenWrite in @('\.TiedToSlot\s*=[^=]', '\.FriendedBy\s*=[^=]', '"/friend"')) {
    if ($allNemesisSourceV2 -match $forbiddenWrite) { throw "Dialogue guard failed: forbidden native-binding write token matched: $forbiddenWrite" }
}

Write-Host "PASS: Nemesis whisper direct-address, fact-gated response buckets, bounded bucket history, and optional Duel bridge guards"
