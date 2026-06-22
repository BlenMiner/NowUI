param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("EditMode", "PlayMode", "Visual", "Golden", "Perf", "All")]
    [string] $Mode = "All",

    [Parameter(Mandatory = $false)]
    [string] $Filter,

    [Parameter(Mandatory = $false)]
    [string] $UnityEditor = $env:UNITY_EDITOR,

    [Parameter(Mandatory = $false)]
    [string] $ProjectPath = (Resolve-Path ".").Path,

    [Parameter(Mandatory = $false)]
    [string] $ArtifactsPath = (Join-Path (Resolve-Path ".").Path "artifacts\local"),

    [Parameter(Mandatory = $false)]
    [switch] $UpdateBaselines
)

$ErrorActionPreference = "Stop"

function Resolve-UnityEditor {
    param([string] $RequestedPath)

    if (![string]::IsNullOrWhiteSpace($RequestedPath) -and (Test-Path -LiteralPath $RequestedPath)) {
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    $fallback = "C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe"
    if (Test-Path -LiteralPath $fallback) {
        return $fallback
    }

    throw "Unity editor was not found. Pass -UnityEditor or set UNITY_EDITOR."
}

function Invoke-Unity {
    param(
        [string[]] $UnityArgs,
        [string] $LogPath
    )

    $editor = Resolve-UnityEditor $UnityEditor
    $project = (Resolve-Path -LiteralPath $ProjectPath).Path
    $logDirectory = Split-Path -Parent $LogPath
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

    $args = @(
        "-batchmode",
        "-projectPath", $project,
        "-logFile", $LogPath
    ) + $UnityArgs

    Write-Host "Running Unity: $($UnityArgs -join ' ')"
    $processInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = $editor
    $processInfo.UseShellExecute = $false

    foreach ($arg in $args) {
        [void] $processInfo.ArgumentList.Add($arg)
    }

    $process = [System.Diagnostics.Process]::Start($processInfo)
    $process.WaitForExit()
    $exitCode = $process.ExitCode

    if ($exitCode -ne 0 -and (Test-Path -LiteralPath $LogPath)) {
        Write-Host "Unity command failed. Last 200 log lines:"
        Get-Content -LiteralPath $LogPath -Tail 200
    }

    if ($exitCode -ne 0) {
        throw "Unity exited with code $exitCode."
    }
}

function Invoke-TestRun {
    param([string] $TestPlatform)

    $platformArtifacts = Join-Path $ArtifactsPath $TestPlatform
    New-Item -ItemType Directory -Force -Path $platformArtifacts | Out-Null

    $resultPath = Join-Path $platformArtifacts "NowUI-$TestPlatform-results.xml"
    $logPath = Join-Path $platformArtifacts "NowUI-$TestPlatform.log"

    $args = @(
        "-runTests",
        "-testPlatform", $TestPlatform,
        "-testResults", $resultPath,
        "-quit"
    )

    if (![string]::IsNullOrWhiteSpace($Filter)) {
        $args += @("-testFilter", $Filter)
    }

    Invoke-Unity -UnityArgs $args -LogPath $logPath

    if (!(Test-Path -LiteralPath $resultPath)) {
        if (Test-Path -LiteralPath $logPath) {
            Write-Host "Unity did not write test results. Last 120 log lines:"
            Get-Content -LiteralPath $logPath -Tail 120
        }

        throw "Unity did not write test results to '$resultPath'."
    }
}

function Invoke-ExecuteMethod {
    param(
        [string] $Method,
        [string] $Name
    )

    $methodArtifacts = Join-Path $ArtifactsPath $Name
    New-Item -ItemType Directory -Force -Path $methodArtifacts | Out-Null

    $args = @(
        "-executeMethod", $Method,
        "-nowuiArtifactsPath", $methodArtifacts,
        "-quit"
    )

    if ($UpdateBaselines) {
        $args += "-nowuiUpdateBaselines"
    }

    Invoke-Unity -UnityArgs $args -LogPath (Join-Path $methodArtifacts "NowUI-$Name.log")
}

New-Item -ItemType Directory -Force -Path $ArtifactsPath | Out-Null

switch ($Mode) {
    "EditMode" { Invoke-TestRun "EditMode" }
    "PlayMode" { Invoke-TestRun "PlayMode" }
    "Visual" { Invoke-ExecuteMethod "NowUI.Editor.NowVisualHarnessRunner.Capture" "visual" }
    "Golden" { Invoke-ExecuteMethod "NowUI.Editor.NowVisualHarnessRunner.CompareGoldens" "golden" }
    "Perf" { Invoke-ExecuteMethod "NowUI.Editor.NowPerfSmokeRunner.Run" "perf" }
    "All" {
        Invoke-TestRun "EditMode"
        Invoke-TestRun "PlayMode"
        Invoke-ExecuteMethod "NowUI.Editor.NowVisualHarnessRunner.Capture" "visual"
        Invoke-ExecuteMethod "NowUI.Editor.NowVisualHarnessRunner.CompareGoldens" "golden"
        Invoke-ExecuteMethod "NowUI.Editor.NowPerfSmokeRunner.Run" "perf"
    }
}
