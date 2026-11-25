$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw 'C# compiler not found. Windows .NET Framework 4.x is required.'
}

$out = Join-Path $root 'WpsReportTimer_1.0.1\WpsReportTimer.exe'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 `
    /out:$out `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:Microsoft.CSharp.dll `
    (Join-Path $root 'src\WpsReportTimer.cs') (Join-Path $root 'src\TimerLogic.cs')
if ($LASTEXITCODE -ne 0) { throw 'Timer build failed.' }

$testOut = Join-Path $root 'src\TimerLogicTests.exe'
& $csc /nologo /target:exe /platform:anycpu /optimize+ /codepage:65001 `
    /out:$testOut /reference:System.dll `
    (Join-Path $root 'src\TimerLogic.cs') (Join-Path $root 'src\TimerLogicTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
& $testOut
if ($LASTEXITCODE -ne 0) { throw 'Timer logic tests failed.' }
Write-Output 'Build and tests completed successfully.'
