param(
    [Parameter(Mandatory = $false)]
    [string] $UnityEditor = $env:UNITY_EDITOR,

    [Parameter(Mandatory = $false)]
    [string] $ProjectPath = (Resolve-Path ".").Path,

    [Parameter(Mandatory = $true)]
    [ValidateSet("EditMode", "PlayMode")]
    [string] $TestPlatform,

    [Parameter(Mandatory = $false)]
    [string] $ArtifactsPath = (Join-Path (Resolve-Path ".").Path "artifacts")
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

$unityArgs = @(
    "-batchmode",
    "-projectPath", $project.Path,
    "-runTests",
    "-testPlatform", $TestPlatform,
    "-testResults", $resultPath,
    "-logFile", $logPath,
    "-quit"
)

Write-Host "Running Unity $TestPlatform tests with '$UnityEditor'."
& $UnityEditor @unityArgs
$exitCode = $LASTEXITCODE

if ($exitCode -ne 0 -and (Test-Path -LiteralPath $logPath)) {
    Write-Host "Unity test run failed. Last 200 log lines:"
    Get-Content -LiteralPath $logPath -Tail 200
}

exit $exitCode
