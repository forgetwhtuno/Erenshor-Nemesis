param([string]$GameDir = "", [string]$BepInExRoot = "")
$ErrorActionPreference = "Stop"; $ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
function Find-Csc { foreach ($p in @("$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe", "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe")) { if (Test-Path $p) { return $p } }; throw "csc.exe not found." }
if (-not $GameDir -or -not (Test-Path (Join-Path $GameDir "Erenshor.exe"))) { throw "Pass -GameDir pointing to Erenshor." }
if (-not $BepInExRoot -or -not (Test-Path (Join-Path $BepInExRoot "BepInEx\core\BepInEx.dll"))) { throw "Pass -BepInExRoot pointing to the active profile." }
$managed = Join-Path $GameDir "Erenshor_Data\Managed"; $core = Join-Path $BepInExRoot "BepInEx\core"; $pluginDir = Join-Path $BepInExRoot "BepInEx\plugins\ErenshorNemesis"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
$refs = @((Join-Path $core "BepInEx.dll"),(Join-Path $core "0Harmony.dll"),(Join-Path $managed "Assembly-CSharp.dll"),(Join-Path $managed "netstandard.dll"),(Join-Path $managed "UnityEngine.dll"),(Join-Path $managed "UnityEngine.CoreModule.dll"),(Join-Path $managed "UnityEngine.UI.dll"))
foreach ($ref in $refs) { if (-not (Test-Path $ref)) { throw "Missing reference: $ref" } }
$out = Join-Path $pluginDir "ErenshorNemesis.dll"; $rsp = Join-Path $env:TEMP "ErenshorNemesis.rsp"; $lines = @('/nologo','/target:library','/optimize+',('/out:"{0}"' -f $out))
$refs | ForEach-Object { $lines += ('/reference:"{0}"' -f $_) }; Get-ChildItem (Join-Path $ScriptRoot "src") -Filter "*.cs" | ForEach-Object { $lines += ('"' + $_.FullName + '"') }
# Cross-mod contract conformance tests, shared with Erenshor PvP and Deep Sims. Optional so a
# standalone copy of this mod still builds; the self-test simply covers less without it.
$shared = Join-Path (Split-Path -Parent $ScriptRoot) "shared"
if (Test-Path $shared) { $lines += '/define:SHARED_CONTRACTS'; Get-ChildItem $shared -Filter "*.cs" | ForEach-Object { $lines += ('"' + $_.FullName + '"') } }
$lines | Set-Content $rsp -Encoding ASCII
& (Find-Csc) "@$rsp"; if ($LASTEXITCODE -ne 0) { throw "Compilation failed." }
$item = Get-Item $out; Write-Host "Installed Erenshor Nemesis to $out" -ForegroundColor Green; Write-Host "SHA256: $((Get-FileHash $out -Algorithm SHA256).Hash)"; Write-Host "Size: $($item.Length)"
