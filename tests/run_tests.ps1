# ============================================================
# Lightning Rename v1.0.0 - Automated Engine Test Runner
# Compiles the PRODUCTION Engine/Rules/Item source plus a test
# suite into a console harness, runs it and returns exit code.
# ============================================================
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$proj = Join-Path $root "LightningRename"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$harness = Join-Path $here "TestHarness"
$csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
$fx = "C:\Windows\Microsoft.NET\Framework\v4.0.30319"
$out = Join-Path $harness "TestHarness.exe"

& $csc /nologo /target:exe /platform:anycpu /optimize+ `
    /out:$out `
    /reference:"$fx\System.dll" `
    /reference:"$fx\System.Core.dll" `
    "$proj\Engine.cs" "$proj\Rules.cs" "$proj\Item.cs" "$harness\TestMain.cs"
if ($LASTEXITCODE -ne 0) { throw "Test compile failed, exit code $LASTEXITCODE" }

Write-Host "=== Running tests ==="
& $out
$code = $LASTEXITCODE
Write-Host "=== Test runner exit code: $code ==="
exit $code