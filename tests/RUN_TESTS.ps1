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
    (Join-Path $ScriptRoot "StandaloneStateStoreTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Nemesis state store test compilation failed." }

try {
    & $out
    if ($LASTEXITCODE -ne 0) { throw "Nemesis state store tests failed with exit code $LASTEXITCODE." }
}
finally {
    Remove-Item $out -Force -ErrorAction SilentlyContinue
}
