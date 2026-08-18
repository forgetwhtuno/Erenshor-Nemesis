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
    (Join-Path $ModRoot "src\NemesisAssignmentPolicy.cs") `
    (Join-Path $ModRoot "src\NemesisConversationPolicy.cs") `
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

# Visible rivalry lines contain no color markup; color is carried separately from text and learned from native tells.
if ($directorSource -match 'ChatRivalTell\([^\r\n]*<color=') { throw "Visible Nemesis line embeds color markup." }
if ($directorSource -match 'ChatRivalTell\([^\r\n]*"magenta"') { throw "Nemesis line still hardcodes magenta." }
foreach ($token in @('NoteNativeSocialStyle','ChatRivalTell','UpdateSocialLog.LogAdd(value, nativeColor)','UpdateSocialLog.LogAdd(value)','native-tell-captured','native-default-no-markup')) {
    if ($pluginSource -notmatch [regex]::Escape($token)) { throw "Native chat-style guard missing: $token" }
}

# HEARD conversation and VERIFIED record remain separate stores/labels.
if ($directorSource -notmatch 'RecentConversation' -or $directorSource -notmatch 'RememberConversation' -or $directorSource -notmatch 'VerifiedRecord\(\)') { throw "Conversation provenance guard missing." }
if ($directorSource -notmatch 'PLAYER MESSAGE \(HEARD\)') { throw "HEARD player-message label missing from bounded context." }

Write-Host "PASS: Nemesis automatic assignment, conversation ownership, optional Deep Sims, provenance, and native-chat source guards"
