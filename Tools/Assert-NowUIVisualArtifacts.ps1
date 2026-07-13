param(
    [Parameter(Mandatory = $true)]
    [string] $ArtifactsPath,

    [Parameter(Mandatory = $false)]
    [int] $MinCaptures = 1,

    [Parameter(Mandatory = $false)]
    [int] $MinPngBytes = 1024,

    [Parameter(Mandatory = $false)]
    [string[]] $RequiredCaptures = @()
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path -LiteralPath $ArtifactsPath
$manifestPath = Join-Path $root.Path "manifest.json"

if (!(Test-Path -LiteralPath $manifestPath)) {
    throw "NowUI visual manifest was not found at '$manifestPath'."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$captures = @($manifest.captures)

if ($captures.Count -lt $MinCaptures) {
    throw "NowUI visual manifest contains $($captures.Count) captures, expected at least $MinCaptures."
}

function Read-PngDimension {
    param(
        [byte[]] $Bytes,
        [int] $Offset
    )

    $b0 = [int] $Bytes[$Offset]
    $b1 = [int] $Bytes[$Offset + 1]
    $b2 = [int] $Bytes[$Offset + 2]
    $b3 = [int] $Bytes[$Offset + 3]

    return (($b0 -shl 24) -bor ($b1 -shl 16) -bor ($b2 -shl 8) -bor $b3)
}

$failures = New-Object System.Collections.Generic.List[string]
$captureNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

foreach ($capture in $captures) {
    if ([string]::IsNullOrWhiteSpace($capture.name)) {
        $failures.Add("capture has no name")
        continue
    }

    $captureName = [string] $capture.name
    if (!$captureNames.Add($captureName)) {
        $failures.Add("$captureName`: capture name is duplicated")
    }

    if ([string]::IsNullOrWhiteSpace($capture.path)) {
        $failures.Add("$captureName`: capture has no path")
        continue
    }

    $capturePath = $capture.path
    if (![System.IO.Path]::IsPathRooted($capturePath)) {
        $capturePath = Join-Path $root.Path $capturePath
    }

    if (!(Test-Path -LiteralPath $capturePath)) {
        $failures.Add("$captureName`: PNG was not found at '$capturePath'")
        continue
    }

    $file = Get-Item -LiteralPath $capturePath
    if ($file.Length -lt $MinPngBytes) {
        $failures.Add("$captureName`: PNG is unexpectedly small ($($file.Length) bytes)")
        continue
    }

    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    if ($bytes.Length -lt 24 -or
        $bytes[0] -ne 0x89 -or
        $bytes[1] -ne 0x50 -or
        $bytes[2] -ne 0x4E -or
        $bytes[3] -ne 0x47 -or
        $bytes[4] -ne 0x0D -or
        $bytes[5] -ne 0x0A -or
        $bytes[6] -ne 0x1A -or
        $bytes[7] -ne 0x0A) {
        $failures.Add("$captureName`: file is not a PNG")
        continue
    }

    $actualWidth = Read-PngDimension -Bytes $bytes -Offset 16
    $actualHeight = Read-PngDimension -Bytes $bytes -Offset 20

    if ($actualWidth -ne [int] $capture.width -or $actualHeight -ne [int] $capture.height) {
        $failures.Add("$captureName`: PNG size is ${actualWidth}x${actualHeight}, manifest says $($capture.width)x$($capture.height)")
    }

    if ([int] $capture.batchCount -le 0) {
        $failures.Add("$captureName`: batchCount is $($capture.batchCount)")
    }

    if ([int] $capture.vertexCount -le 0) {
        $failures.Add("$captureName`: vertexCount is $($capture.vertexCount)")
    }
}

$requiredNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($requiredArgument in $RequiredCaptures) {
    foreach ($requiredName in ([string] $requiredArgument).Split(',')) {
        $requiredName = $requiredName.Trim()
        if (![string]::IsNullOrWhiteSpace($requiredName)) {
            [void] $requiredNames.Add($requiredName)
        }
    }
}

foreach ($requiredName in $requiredNames) {
    if (!$captureNames.Contains($requiredName)) {
        $failures.Add("required capture '$requiredName' is missing")
    }
}

if ($failures.Count -gt 0) {
    throw "NowUI visual artifact validation failed:`n$($failures -join "`n")"
}

Write-Host "Validated $($captures.Count) NowUI visual captures in '$($root.Path)'."
