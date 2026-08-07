#requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("urp", "hdrp")]
    [string] $Pipeline,

    [Parameter(Mandatory = $false)]
    [ValidatePattern('^17\.4\.\d+$')]
    [string] $PackageVersion = "17.4.0",

    [Parameter(Mandatory = $false)]
    [string] $ProjectPath = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

$project = Resolve-Path -LiteralPath $ProjectPath
$manifestPath = Join-Path $project.Path "Packages/manifest.json"

if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Unity project manifest was not found at '$manifestPath'."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable

if ($null -eq $manifest.dependencies -or $manifest.dependencies -isnot [System.Collections.IDictionary]) {
    throw "Unity project manifest '$manifestPath' has no dependency object."
}

$packages = @{
    urp = "com.unity.render-pipelines.universal"
    hdrp = "com.unity.render-pipelines.high-definition"
}

foreach ($packageName in $packages.Values) {
    [void] $manifest.dependencies.Remove($packageName)
}

$selectedPackage = $packages[$Pipeline]
$manifest.dependencies[$selectedPackage] = $PackageVersion

$json = $manifest | ConvertTo-Json -Depth 100
Set-Content -LiteralPath $manifestPath -Value $json -Encoding utf8NoBOM

Write-Host "Configured $selectedPackage@$PackageVersion for the $Pipeline validation workspace."
