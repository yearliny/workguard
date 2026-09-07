param([string]$Executable = "$PSScriptRoot/../src/WorkGuard.Windows/bin/Release/net10.0-windows/WorkGuard.exe")
$ErrorActionPreference = 'Stop'
$report = Join-Path ([IO.Path]::GetTempPath()) ("workguard-smoke-" + [guid]::NewGuid() + '.txt')
try {
    $process = Start-Process -FilePath $Executable -ArgumentList @('--smoke-test', "`"$report`"") -PassThru
    if (-not $process.WaitForExit(30000)) {
        $process.Kill()
        throw 'Windows smoke test timed out.'
    }
    if (-not (Test-Path $report)) { throw "Smoke test produced no report. Exit: $($process.ExitCode)" }
    $result = Get-Content $report -Raw
    Write-Host $result
    if ($process.ExitCode -ne 0 -or -not $result.StartsWith('PASS:')) { throw 'Windows smoke test failed.' }
} finally { if (Test-Path $report) { Remove-Item $report } }
