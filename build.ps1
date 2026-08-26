# ============================================================
# Lightning Rename v1.0.0 - Release build script
# Uses the built-in .NET Framework 4.0 csc.exe to produce a
# native EXE that runs without any new runtime - compatible
# with 10-year-old PCs (Win7 / 32-bit).
# ============================================================
$ErrorActionPreference = "Stop"

$root   = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj   = Join-Path $root "LightningRename"
$outDir = Join-Path $root "ReleaseOut"
$csc    = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
$outExe = Join-Path $outDir "LightningRename_v1.0.0.exe"
$fx     = "C:\Windows\Microsoft.NET\Framework\v4.0.30319"

if (-not (Test-Path $csc)) { throw "csc not found: $csc" }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Push-Location $proj
try {
    & $csc /nologo /target:winexe /platform:x86 /optimize+ `
        /out:$outExe `
        /win32icon:app.ico `
        /win32manifest:app.manifest `
        /reference:"$fx\System.dll" `
        /reference:"$fx\System.Core.dll" `
        /reference:"$fx\System.Drawing.dll" `
        /reference:"$fx\System.Windows.Forms.dll" `
        /reference:"$fx\Microsoft.CSharp.dll" `
        Engine.cs Item.cs MainForm.cs Program.cs Rules.cs UndoLog.cs Properties\AssemblyInfo.cs
    if ($LASTEXITCODE -ne 0) { throw "Build failed, exit code $LASTEXITCODE" }
} finally {
    Pop-Location
}

Write-Host "Build OK: $outExe"
Get-Item $outExe | Select-Object FullName, Length, LastWriteTime