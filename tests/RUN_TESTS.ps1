$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModRoot = Split-Path -Parent $ScriptRoot

function Find-Csc {
    foreach ($path in @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )) {
        if (Test-Path $path) { return $path }
    }
    throw "csc.exe not found. Install the .NET Framework Developer Pack or Visual Studio Build Tools."
}

$csc = Find-Csc
$out = Join-Path $env:TEMP "ErenshorNemesis.StateStoreTests.exe"

# NemesisStateStore.cs only uses System.* namespaces, so its tests compile and run standalone -
# no game, BepInEx, or Lunaris dependency.
& $csc /nologo /target:exe ("/out:{0}" -f $out) `
    (Join-Path $ModRoot "src\NemesisStateStore.cs") `
    (Join-Path $ModRoot "src\NemesisHubPresentation.cs") `
    (Join-Path $ModRoot "src\NemesisCandidateSelectionPolicy.cs") `
    (Join-Path $ScriptRoot "StandaloneStateStoreTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Nemesis state store test compilation failed." }

try {
    & $out
    if ($LASTEXITCODE -ne 0) { throw "Nemesis state store tests failed with exit code $LASTEXITCODE." }
}
finally {
    Remove-Item $out -Force -ErrorAction SilentlyContinue
}

# Social-selection release-candidate source guards. These guard the separation between automatic
# and deliberate selection without requiring game assemblies.
$directorSource = Get-Content (Join-Path $ModRoot "src\NemesisDirector.cs") -Raw
$nativeSocialSource = Get-Content (Join-Path $ModRoot "src\NemesisNativeSocialRoster.cs") -Raw
$fallbackSource = Get-Content (Join-Path $ModRoot "src\ErenshorNemesisPlugin.cs") -Raw

if ($directorSource -notmatch 'SelectResolved\(list\[UnityEngine\.Random\.Range\(0,\s*list\.Count\)\],\s*NemesisSelectionOrigin\.Automatic\)') {
    throw "Nemesis automatic-selection guard failed: random path is not marked automatic."
}
if ($directorSource -notmatch 'PendingSelectionOrigin\s*=\s*origin') {
    throw "Nemesis pending-selection guard failed: selection policy origin is not retained."
}
if ($directorSource -notmatch 'origin\s*==\s*NemesisSelectionOrigin\.Automatic\s*\?\s*AutomaticCandidates\(\)\s*:\s*ExplicitCandidates\(\)') {
    throw "Nemesis pending-selection guard failed: confirmation is not revalidated under its origin policy."
}
if ($directorSource -notmatch 'ExplicitCandidates\(\)\.FirstOrDefault') {
    throw "Nemesis explicit-selection guard failed: deliberate select path missing."
}
if ($nativeSocialSource -notmatch 'sim\.FriendedBy\s*==\s*currentSlot' -or
    $nativeSocialSource -notmatch '!sim\.IsGMCharacter') {
    throw "Nemesis native Friends guard failed: current character-slot predicate missing."
}
foreach ($token in @('GuildManager','GuildMngr','Guilds','GuildMembers','Members','MemberNames')) {
    if ($nativeSocialSource -notmatch [regex]::Escape($token)) {
        throw "Nemesis native Guild guard failed: proven roster token missing: $token"
    }
}
if ($fallbackSource -notmatch 'Select First Auto' -or
    $fallbackSource -notmatch 'AutomaticCandidateNames' -or
    $fallbackSource -notmatch 'TrySelectAutomatic') {
    throw "Nemesis fallback guard failed: fallback action can still select from the explicit/Friend list."
}
if ($directorSource -notmatch 'FRIENDS - explicit /enemesis select only' -or
    $directorSource -notmatch 'GUILD FALLBACK:' -or
    $directorSource -notmatch 'FormatCandidates\(buckets\.Primary,\s*5\)') {
    throw "Nemesis candidate presentation guard failed: bounded social sections missing."
}
Write-Host "PASS: Nemesis native social-selection source guards"
