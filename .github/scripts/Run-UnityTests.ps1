param(
    [Parameter(Mandatory = $false)]
    [string] $UnityEditor = $env:UNITY_EDITOR,

    [Parameter(Mandatory = $false)]
    [string] $ProjectPath = (Resolve-Path ".").Path,

    [Parameter(Mandatory = $true)]
    [ValidateSet("EditMode", "PlayMode")]
    [string] $TestPlatform,

    [Parameter(Mandatory = $false)]
    [string] $ArtifactsPath = (Join-Path (Resolve-Path ".").Path "artifacts"),

    [Parameter(Mandatory = $false)]
    [int] $ResultsTimeoutSeconds = 300,

    [Parameter(Mandatory = $false)]
    [switch] $CleanScriptAssemblies
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($UnityEditor)) {
    throw "Unity editor path is required. Set UNITY_EDITOR or pass -UnityEditor."
}

if (!(Test-Path -LiteralPath $UnityEditor)) {
    throw "Unity editor was not found at '$UnityEditor'."
}

$project = Resolve-Path -LiteralPath $ProjectPath
$artifacts = New-Item -ItemType Directory -Force -Path $ArtifactsPath
$resultPath = Join-Path $artifacts.FullName "NowUI-$TestPlatform-results.xml"
$logPath = Join-Path $artifacts.FullName "NowUI-$TestPlatform.log"

if ($CleanScriptAssemblies) {
    $scriptAssembliesPath = Join-Path $project.Path "Library\ScriptAssemblies"

    if (Test-Path -LiteralPath $scriptAssembliesPath) {
        $resolvedScriptAssemblies = Resolve-Path -LiteralPath $scriptAssembliesPath
        $projectPrefix = $project.Path.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar

        if (!$resolvedScriptAssemblies.Path.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove ScriptAssemblies outside the project path: '$($resolvedScriptAssemblies.Path)'."
        }

        Write-Host "Removing stale Unity script assemblies from '$($resolvedScriptAssemblies.Path)'."
        Remove-Item -LiteralPath $resolvedScriptAssemblies.Path -Recurse -Force
    }
}

Remove-Item -LiteralPath $resultPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue

$unityArgs = @(
    "-batchmode",
    "-projectPath", $project.Path,
    "-runTests",
    "-testPlatform", $TestPlatform,
    "-testResults", $resultPath,
    "-logFile", $logPath
)

Write-Host "Running Unity $TestPlatform tests with '$UnityEditor'."
& $UnityEditor @unityArgs
$exitCode = if ($null -eq $LASTEXITCODE) { 0 } else { [int] $LASTEXITCODE }

function Write-UnityLogTail {
    if (Test-Path -LiteralPath $logPath) {
        Write-Host "Last 200 Unity log lines:"
        Get-Content -LiteralPath $logPath -Tail 200
    }
}

$deadline = [DateTime]::UtcNow.AddSeconds($ResultsTimeoutSeconds)
$results = $null
$testRun = $null
$lastParseError = $null

do {
    if (Test-Path -LiteralPath $resultPath) {
        try {
            [xml] $results = Get-Content -LiteralPath $resultPath -Raw
            $testRun = $results.'test-run'

            if ($null -ne $testRun) {
                break
            }
        } catch {
            $lastParseError = $_
        }
    }

    Start-Sleep -Seconds 1
} while ([DateTime]::UtcNow -lt $deadline)

if ($null -eq $testRun) {
    Write-Host "Unity did not write parseable test results within $ResultsTimeoutSeconds seconds: $resultPath"
    if ($null -ne $lastParseError) {
        Write-Host $lastParseError
    }
    Write-UnityLogTail
    exit 1
}

$total = [int] $testRun.total
$passed = [int] $testRun.passed
$failed = [int] $testRun.failed
$skipped = [int] $testRun.skipped

Write-Host "Unity $TestPlatform results: $($testRun.result) ($passed/$total passed, $failed failed, $skipped skipped)."

if ($exitCode -ne 0) {
    Write-Host "Unity exited with code $exitCode."
    Write-UnityLogTail
    exit $exitCode
}

if ($failed -gt 0 -or $testRun.result -like "Failed*") {
    Write-Host "Unity $TestPlatform tests failed."
    Write-UnityLogTail
    exit 1
}

exit 0
