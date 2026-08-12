param(
    [Parameter(Mandatory = $false)]
    [string] $PackageRoot = ""
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($PackageRoot)) {
    $PackageRoot = Join-Path $projectRoot "Assets/NowUI"
} elseif (![System.IO.Path]::IsPathRooted($PackageRoot)) {
    $PackageRoot = Join-Path $projectRoot $PackageRoot
}

$package = Resolve-Path -LiteralPath $PackageRoot
$manifestPath = Join-Path $package.Path "package.json"
if (!(Test-Path -LiteralPath $manifestPath)) {
    throw "NowUI package manifest was not found at '$manifestPath'."
}

$releaseConfigPath = Join-Path $projectRoot ".releaserc"
if (!(Test-Path -LiteralPath $releaseConfigPath)) {
    throw "Semantic-release configuration was not found at '$releaseConfigPath'."
}

$releaseConfig = Get-Content -LiteralPath $releaseConfigPath -Raw | ConvertFrom-Json
$unityPackageConfigs = @(
    foreach ($plugin in @($releaseConfig.plugins)) {
        if ($plugin -is [System.Array] -and $plugin.Count -ge 2 -and
            [string] $plugin[0] -eq "@iam1337/create-unitypackage") {
            $plugin[1]
        }
    }
)
if ($unityPackageConfigs.Count -ne 1) {
    throw "Expected exactly one create-unitypackage release configuration; found $($unityPackageConfigs.Count)."
}

$unityPackageRoot = [string] $unityPackageConfigs[0].packageRoot
if ([string]::IsNullOrWhiteSpace($unityPackageRoot)) {
    throw "The create-unitypackage release configuration has no packageRoot."
}

$resolvedUnityPackageRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $unityPackageRoot))
if (![string]::Equals($resolvedUnityPackageRoot, $package.Path, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The unitypackage root '$resolvedUnityPackageRoot' differs from the validated package root '$($package.Path)'."
}

$forbiddenPaths = @(
    "Tests",
    "Tests.meta",
    "NowUITests",
    "NowUITests.meta",
    "Editor/Harness",
    "Editor/Harness.meta",
    "NowUIHarness",
    "NowUIHarness.meta"
)

$physicalFailures = @(
    $forbiddenPaths | Where-Object {
        Test-Path -LiteralPath (Join-Path $package.Path $_)
    }
)
if ($physicalFailures.Count -gt 0) {
    throw "Repository-only validation content exists under the package root: $($physicalFailures -join ', ')."
}

$requiredRepositoryFiles = @(
    "Assets/NowUITests/Tests.asmdef",
    "Assets/NowUITests/PlayMode/NowUI.PlayModeTests.asmdef",
    "Assets/NowUIHarness/NowUI.Editor.Harness.asmdef"
)
$missingRepositoryFiles = @(
    $requiredRepositoryFiles | Where-Object {
        !(Test-Path -LiteralPath (Join-Path $projectRoot $_))
    }
)
if ($missingRepositoryFiles.Count -gt 0) {
    throw "Repository validation assemblies are missing: $($missingRepositoryFiles -join ', ')."
}

$npm = Get-Command npm -ErrorAction Stop
$packOutput = & $npm.Source pack $package.Path --dry-run --json --ignore-scripts
if ($LASTEXITCODE -ne 0) {
    throw "npm pack failed with exit code $LASTEXITCODE."
}

$packResults = @(($packOutput -join [Environment]::NewLine) | ConvertFrom-Json)
if ($packResults.Count -ne 1) {
    throw "npm pack returned $($packResults.Count) package results; expected exactly one."
}

$entries = @($packResults[0].files)
$packedFailures = @(
    $entries | Where-Object {
        ([string] $_.path).Replace('\', '/') -match '^(Tests(?:/|\.meta$)|NowUITests(?:/|\.meta$)|Editor/Harness(?:/|\.meta$)|NowUIHarness(?:/|\.meta$))'
    }
)
if ($packedFailures.Count -gt 0) {
    $paths = @($packedFailures | ForEach-Object { $_.path }) -join ", "
    throw "Repository-only validation content is present in the npm package: $paths."
}

Write-Host "Validated NowUI npm and unitypackage boundaries across $($entries.Count) npm entries; NowUI test suites and the visual harness are repository-only."
