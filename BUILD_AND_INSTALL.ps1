param(
    [string]$GameDir = "",
    [string]$LunarisLibDir = "",
    # Defaults to true so a plain "-GameDir <dir> -LunarisLibDir <dir>" invocation matches every
    # sibling mod's BUILD_AND_INSTALL.ps1 convention (build, then place the DLL under GameDir\
    # plugins) - including the suite orchestrator (Erenshor-Mod-Suite/BUILD_ALL.ps1), which invokes
    # every mod's script this same way against its own temporary staging GameDir and only copies
    # into the REAL install after every mod has staged successfully. Pass -Install:$false to only
    # stage a candidate without installing it (e.g. reviewing a build against the real GameDir
    # without touching the live plugin). The running-process refusal, pre-overwrite backup, and
    # staged/installed hash-equality check below are unchanged either way.
    [switch]$Install = $true
)
$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Csc {
    foreach ($p in @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )) { if (Test-Path $p) { return $p } }
    throw "csc.exe not found. Install the .NET Framework Developer Pack or Visual Studio Build Tools."
}

if (-not $GameDir -or -not (Test-Path (Join-Path $GameDir "Erenshor.exe"))) { throw "Pass -GameDir pointing to the current Erenshor installation." }
if (-not $LunarisLibDir) { $LunarisLibDir = Join-Path $ScriptRoot "LunarisLibs" }
if (-not (Test-Path (Join-Path $LunarisLibDir "Lunaris.dll")) -or -not (Test-Path (Join-Path $LunarisLibDir "0Harmony.dll"))) {
    throw "Pass -LunarisLibDir pointing to the current Lunaris.dll and 0Harmony.dll."
}

$managed = Join-Path $GameDir "Erenshor_Data\Managed"
$pluginRoot = Join-Path $GameDir "plugins"
$refs = @(
    (Join-Path $LunarisLibDir "Lunaris.dll"),
    (Join-Path $LunarisLibDir "0Harmony.dll"),
    (Join-Path $managed "Assembly-CSharp.dll"),
    (Join-Path $managed "netstandard.dll"),
    (Join-Path $managed "UnityEngine.dll"),
    (Join-Path $managed "UnityEngine.CoreModule.dll"),
    (Join-Path $managed "UnityEngine.UIModule.dll"),
    (Join-Path $managed "UnityEngine.TextRenderingModule.dll"),
    (Join-Path $managed "UnityEngine.UI.dll")
)
foreach ($ref in $refs) { if (-not (Test-Path $ref)) { throw "Missing current reference: $ref" } }

$stageDir = Join-Path $ScriptRoot "staging"
$stageDll = Join-Path $stageDir "ErenshorNemesis.dll"
$TempDir = Join-Path $env:TEMP ("ErenshorNemesis-build-" + [Guid]::NewGuid().ToString("N"))
$TempDll = Join-Path $TempDir "ErenshorNemesis.dll"
$rsp = Join-Path $TempDir "ErenshorNemesis.rsp"
New-Item -ItemType Directory -Force -Path $TempDir | Out-Null
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null

try {
    $lines = @('/nologo','/target:library','/optimize+',('/out:"{0}"' -f $TempDll))
    $refs | ForEach-Object { $lines += ('/reference:"{0}"' -f $_) }
    Get-ChildItem (Join-Path $ScriptRoot "src") -Filter "*.cs" | ForEach-Object { $lines += ('"' + $_.FullName + '"') }

    $fallbackUi = Join-Path (Split-Path -Parent (Split-Path -Parent $ScriptRoot)) "Erenshor-Mod-Suite\shared\ErenshorSuite.UI\StandaloneFallbackUi.cs"
    if (-not (Test-Path -LiteralPath $fallbackUi)) { throw "Missing shared standalone UI source: $fallbackUi" }
    $lines += ('"' + $fallbackUi + '"')

    $shared = Join-Path (Split-Path -Parent $ScriptRoot) "shared"
    if (Test-Path $shared) {
        $lines += '/define:SHARED_CONTRACTS'
        Get-ChildItem $shared -Filter "*.cs" | ForEach-Object { $lines += ('"' + $_.FullName + '"') }
    }

    $lines | Set-Content $rsp -Encoding ASCII
    & (Find-Csc) "@$rsp"
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed." }
    if (-not (Test-Path $TempDll)) { throw "Compiler reported success but produced no candidate DLL." }

    Copy-Item -LiteralPath $TempDll -Destination $stageDll -Force
    $stageHash = (Get-FileHash $stageDll -Algorithm SHA256).Hash
    Write-Host "Staged Erenshor Nemesis candidate: $stageDll" -ForegroundColor Green
    Write-Host "Staged SHA256: $stageHash"

    if ($Install) {
        if (Get-Process -Name "Erenshor" -ErrorAction SilentlyContinue) {
            throw "Erenshor is running. Refusing to replace the live plugin. Close the game and rerun with -Install."
        }
        New-Item -ItemType Directory -Force -Path $pluginRoot | Out-Null
        $out = Join-Path $pluginRoot "ErenshorNemesis.dll"
        if (Test-Path $out) {
            # Backup must remain outside Git/source. TEMP is intentionally used rather than the repo.
            $backupDir = Join-Path $env:TEMP ("ErenshorNemesis-install-backup-" + (Get-Date -Format "yyyyMMdd-HHmmss-fff"))
            New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
            Copy-Item -LiteralPath $out -Destination (Join-Path $backupDir "ErenshorNemesis.dll") -Force
            (Get-FileHash $out -Algorithm SHA256).Hash | Set-Content (Join-Path $backupDir "installed-before.sha256") -Encoding ASCII
            Write-Host "Backed up prior installed DLL outside source: $backupDir"
        }
        Copy-Item -LiteralPath $stageDll -Destination $out -Force
        $installedHash = (Get-FileHash $out -Algorithm SHA256).Hash
        if ($installedHash -ne $stageHash) { throw "Installed SHA256 does not match staged SHA256." }
        $instances = @(Get-ChildItem -Path $pluginRoot -Filter "ErenshorNemesis.dll" -File -Recurse -ErrorAction SilentlyContinue)
        if ($instances.Count -ne 1) { throw "Expected exactly one ErenshorNemesis.dll under the plugin root; found $($instances.Count)." }
        Write-Host "Installed Erenshor Nemesis: $out" -ForegroundColor Green
        Write-Host "Installed SHA256: $installedHash"
        Write-Host "Plugin instances: 1"
    }
    else {
        Write-Host "Install skipped. Pass -Install only after reviewing the staged candidate and closing Erenshor."
    }
}
finally {
    if (Test-Path $TempDir) { Remove-Item -LiteralPath $TempDir -Recurse -Force -ErrorAction SilentlyContinue }
}
