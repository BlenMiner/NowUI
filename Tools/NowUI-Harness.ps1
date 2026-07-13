#requires -Version 7.0

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("EditMode", "PlayMode", "Visual", "Golden", "Perf", "All")]
    [string] $Mode = "All",

    [Parameter(Mandatory = $false)]
    [string] $Filter,

    [Parameter(Mandatory = $false)]
    [string] $UnityEditor = $env:UNITY_EDITOR,

    [Parameter(Mandatory = $false)]
    [string] $ProjectPath,

    [Parameter(Mandatory = $false)]
    [string] $ArtifactsPath,

    [Parameter(Mandatory = $false)]
    [switch] $UpdateBaselines,

    [Parameter(Mandatory = $false)]
    [switch] $CleanScriptAssemblies
)

$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = $repositoryRoot
}

$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path

if ([string]::IsNullOrWhiteSpace($ArtifactsPath)) {
    $ArtifactsPath = Join-Path $ProjectPath "artifacts/local"
} elseif (![System.IO.Path]::IsPathRooted($ArtifactsPath)) {
    $ArtifactsPath = Join-Path $ProjectPath $ArtifactsPath
}

$ArtifactsPath = [System.IO.Path]::GetFullPath($ArtifactsPath)

function Get-ProjectUnityVersion {
    param([string] $ProjectRoot)

    $versionPath = Join-Path $ProjectRoot "ProjectSettings/ProjectVersion.txt"
    if (!(Test-Path -LiteralPath $versionPath -PathType Leaf)) {
        throw "Unity project version file was not found at '$versionPath'."
    }

    foreach ($line in Get-Content -LiteralPath $versionPath) {
        if ($line -match '^m_EditorVersion:\s*(\S+)\s*$') {
            return $Matches[1]
        }
    }

    throw "Unity editor version was not found in '$versionPath'."
}

function Resolve-UnityEditor {
    param(
        [string] $RequestedPath,
        [string] $ProjectRoot
    )

    $expectedVersion = Get-ProjectUnityVersion $ProjectRoot

    if (![string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (Test-Path -LiteralPath $RequestedPath -PathType Leaf) {
            return (Resolve-Path -LiteralPath $RequestedPath).Path
        }

        throw "The requested Unity editor for project version $expectedVersion was not found at '$RequestedPath'."
    }

    $candidates = [System.Collections.Generic.List[string]]::new()

    if ($IsWindows) {
        $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
        if (![string]::IsNullOrWhiteSpace($programFiles)) {
            $candidates.Add((Join-Path $programFiles "Unity/Hub/Editor/$expectedVersion/Editor/Unity.exe"))
        }
    } elseif ($IsMacOS) {
        $candidates.Add("/Applications/Unity/Hub/Editor/$expectedVersion/Unity.app/Contents/MacOS/Unity")
    } else {
        if (![string]::IsNullOrWhiteSpace($HOME)) {
            $candidates.Add((Join-Path $HOME "Unity/Hub/Editor/$expectedVersion/Editor/Unity"))
        }

        $candidates.Add("/opt/unity/Hub/Editor/$expectedVersion/Editor/Unity")
        $candidates.Add("/opt/Unity/Hub/Editor/$expectedVersion/Editor/Unity")
    }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $searched = $candidates -join "', '"
    throw "Unity $expectedVersion was not found. Checked '$searched'. Pass -UnityEditor or set UNITY_EDITOR."
}

function Clear-ScriptAssemblies {
    $project = (Resolve-Path -LiteralPath $ProjectPath).Path
    $scriptAssembliesPath = Join-Path $project "Library/ScriptAssemblies"

    if (!(Test-Path -LiteralPath $scriptAssembliesPath)) {
        return
    }

    $resolvedScriptAssemblies = Resolve-Path -LiteralPath $scriptAssembliesPath
    $projectPrefix = $project.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar

    if (!$resolvedScriptAssemblies.Path.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove ScriptAssemblies outside the project path: '$($resolvedScriptAssemblies.Path)'."
    }

    Write-Host "Removing stale Unity script assemblies from '$($resolvedScriptAssemblies.Path)'."
    Remove-Item -LiteralPath $resolvedScriptAssemblies.Path -Recurse -Force
}

function Invoke-Unity {
    param(
        [string[]] $UnityArgs,
        [string] $LogPath
    )

    $editor = Resolve-UnityEditor -RequestedPath $UnityEditor -ProjectRoot $ProjectPath
    $project = $ProjectPath
    $logDirectory = Split-Path -Parent $LogPath
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

    $args = @(
        "-batchmode",
        "-projectPath", $project,
        "-logFile", $LogPath
    ) + $UnityArgs

    Write-Host "Running Unity from '$editor' for project '$project': $($UnityArgs -join ' ')"
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

function Read-UnityTestResults {
    param(
        [string] $ResultsPath,
        [string] $TestPlatform
    )

    if (!(Test-Path -LiteralPath $ResultsPath -PathType Leaf)) {
        throw "Unity did not write test results to '$ResultsPath'."
    }

    try {
        [xml] $document = Get-Content -LiteralPath $ResultsPath -Raw
    } catch {
        throw "Unity wrote invalid test result XML to '$ResultsPath': $($_.Exception.Message)"
    }

    $testRun = $document.'test-run'
    if ($null -eq $testRun) {
        throw "Unity test results at '$ResultsPath' do not contain an NUnit test-run element."
    }

    [int] $total = 0
    [int] $failed = 0
    [int] $passed = 0
    [int] $skipped = 0
    [int] $inconclusive = 0

    if (![int]::TryParse([string] $testRun.total, [ref] $total)) {
        throw "Unity test results at '$ResultsPath' do not contain a numeric total."
    }

    if (![int]::TryParse([string] $testRun.failed, [ref] $failed)) {
        throw "Unity test results at '$ResultsPath' do not contain a numeric failed count."
    }

    if (![int]::TryParse([string] $testRun.passed, [ref] $passed)) {
        throw "Unity test results at '$ResultsPath' do not contain a numeric passed count."
    }

    if (![int]::TryParse([string] $testRun.skipped, [ref] $skipped)) {
        throw "Unity test results at '$ResultsPath' do not contain a numeric skipped count."
    }

    if (![int]::TryParse([string] $testRun.inconclusive, [ref] $inconclusive)) {
        throw "Unity test results at '$ResultsPath' do not contain a numeric inconclusive count."
    }

    $result = [string] $testRun.result

    if ($total -le 0) {
        throw "Unity $TestPlatform test run discovered zero tests. Results: '$ResultsPath'."
    }

    if ($passed -lt 0 -or $failed -lt 0 -or $skipped -lt 0 -or $inconclusive -lt 0) {
        throw "Unity test results at '$ResultsPath' contain a negative result count."
    }

    if ($passed + $failed + $skipped + $inconclusive -ne $total) {
        throw "Unity test result counts at '$ResultsPath' are inconsistent: passed + failed + skipped + inconclusive does not equal total."
    }

    if ([string]::IsNullOrWhiteSpace($result)) {
        throw "Unity test results at '$ResultsPath' do not contain a result value."
    }

    if ($failed -gt 0 -or $result -like "Failed*") {
        throw "Unity $TestPlatform tests failed: $failed of $total failed (result '$result'). Results: '$ResultsPath'."
    }

    if ($result -notlike "Passed*" -and $result -notlike "Skipped*") {
        throw "Unity test results at '$ResultsPath' contain unsupported non-success result '$result'."
    }

    return [pscustomobject] @{
        Total = $total
        Passed = $passed
        Failed = $failed
        Skipped = $skipped
        Inconclusive = $inconclusive
        Result = $result
    }
}

function Invoke-TestRun {
    param([string] $TestPlatform)

    $platformArtifacts = Join-Path $ArtifactsPath $TestPlatform
    New-Item -ItemType Directory -Force -Path $platformArtifacts | Out-Null

    $resultPath = Join-Path $platformArtifacts "NowUI-$TestPlatform-results.xml"
    $logPath = Join-Path $platformArtifacts "NowUI-$TestPlatform.log"

    Remove-Item -LiteralPath $resultPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue

    $args = @(
        "-runTests",
        "-testPlatform", $TestPlatform,
        "-testResults", $resultPath
    )

    if (![string]::IsNullOrWhiteSpace($Filter)) {
        $args += @("-testFilter", $Filter)
    }

    Invoke-Unity -UnityArgs $args -LogPath $logPath

    try {
        $summary = Read-UnityTestResults -ResultsPath $resultPath -TestPlatform $TestPlatform
        Write-Host "Unity $TestPlatform results: $($summary.Result) ($($summary.Passed)/$($summary.Total) passed, $($summary.Failed) failed, $($summary.Skipped) skipped)."
    } catch {
        if (Test-Path -LiteralPath $logPath) {
            Write-Host "Unity test result validation failed. Last 120 log lines:"
            Get-Content -LiteralPath $logPath -Tail 120
        }

        throw
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

if ($CleanScriptAssemblies) {
    Clear-ScriptAssemblies
}

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
