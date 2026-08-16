param([string]$GameDir = "", [string]$LunarisLibDir = "")
$ErrorActionPreference = "Stop"; $ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
function Find-Csc { foreach ($p in @("$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe", "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe")) { if (Test-Path $p) { return $p } }; throw "csc.exe not found." }
if (-not $GameDir -or -not (Test-Path (Join-Path $GameDir "Erenshor.exe"))) { throw "Pass -GameDir pointing to Erenshor." }
if (-not $LunarisLibDir) { $LunarisLibDir = Join-Path $ScriptRoot "LunarisLibs" }
if (-not (Test-Path (Join-Path $LunarisLibDir "Lunaris.dll")) -or -not (Test-Path (Join-Path $LunarisLibDir "0Harmony.dll"))) { throw "Pass -LunarisLibDir pointing to a folder with Lunaris.dll and 0Harmony.dll." }
$managed = Join-Path $GameDir "Erenshor_Data\Managed"; $pluginRoot = Join-Path $GameDir "plugins"
New-Item -ItemType Directory -Force -Path $pluginRoot | Out-Null
$refs = @((Join-Path $LunarisLibDir "Lunaris.dll"),(Join-Path $LunarisLibDir "0Harmony.dll"),(Join-Path $managed "Assembly-CSharp.dll"),(Join-Path $managed "netstandard.dll"),(Join-Path $managed "UnityEngine.dll"),(Join-Path $managed "UnityEngine.CoreModule.dll"),(Join-Path $managed "UnityEngine.UIModule.dll"),(Join-Path $managed "UnityEngine.TextRenderingModule.dll"),(Join-Path $managed "UnityEngine.UI.dll"))
foreach ($ref in $refs) { if (-not (Test-Path $ref)) { throw "Missing reference: $ref" } }
$TempDir = Join-Path $env:TEMP ("ErenshorNemesis-build-" + [Guid]::NewGuid().ToString("N")); New-Item -ItemType Directory -Force -Path $TempDir | Out-Null
$TempDll = Join-Path $TempDir "ErenshorNemesis.dll"; $rsp = Join-Path $TempDir "ErenshorNemesis.rsp"; $out = Join-Path $pluginRoot "ErenshorNemesis.dll"
try {
    $lines = @('/nologo','/target:library','/optimize+',('/out:"{0}"' -f $TempDll))
    $refs | ForEach-Object { $lines += ('/reference:"{0}"' -f $_) }; Get-ChildItem (Join-Path $ScriptRoot "src") -Filter "*.cs" | ForEach-Object { $lines += ('"' + $_.FullName + '"') }
    $fallbackUi = Join-Path (Split-Path -Parent (Split-Path -Parent $ScriptRoot)) "Erenshor-Mod-Suite\shared\ErenshorSuite.UI\StandaloneFallbackUi.cs"
    if (-not (Test-Path -LiteralPath $fallbackUi)) { throw "Missing shared standalone UI source: $fallbackUi" }
    $lines += ('"' + $fallbackUi + '"')
    # Cross-mod contract conformance tests, shared with Erenshor PvP and Deep Sims. Optional so a
    # standalone copy of this mod still builds; the self-test simply covers less without it.
    $shared = Join-Path (Split-Path -Parent $ScriptRoot) "shared"
    if (Test-Path $shared) { $lines += '/define:SHARED_CONTRACTS'; Get-ChildItem $shared -Filter "*.cs" | ForEach-Object { $lines += ('"' + $_.FullName + '"') } }
    $lines | Set-Content $rsp -Encoding ASCII
    & (Find-Csc) "@$rsp"; if ($LASTEXITCODE -ne 0) { throw "Compilation failed." }
    if (-not (Test-Path $TempDll)) { throw "Compiler reported success but did not produce $TempDll" }
    Copy-Item -LiteralPath $TempDll -Destination $out -Force
}
finally { if (Test-Path $TempDir) { Remove-Item -LiteralPath $TempDir -Recurse -Force -ErrorAction SilentlyContinue } }
$item = Get-Item $out; Write-Host "Installed Erenshor Nemesis to $out" -ForegroundColor Green; Write-Host "SHA256: $((Get-FileHash $out -Algorithm SHA256).Hash)"; Write-Host "Size: $($item.Length)"
