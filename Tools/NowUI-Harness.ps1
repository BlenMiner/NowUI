#requires -Version 7.0

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("EditMode", "PlayMode", "Visual", "Golden", "Perf", "Animation", "Encode", "All")]
    [string] $Mode = "All",

    [Parameter(Mandatory = $false)]
    [string] $Filter,

    [Parameter(Mandatory = $false)]
    [string] $ScenarioFilter,

    [Parameter(Mandatory = $false)]
    [string] $UnityEditor = $env:UNITY_EDITOR,

    [Parameter(Mandatory = $false)]
    [string] $ProjectPath,

    [Parameter(Mandatory = $false)]
    [string] $ArtifactsPath,

    [Parameter(Mandatory = $false)]
    [switch] $UpdateBaselines,

    [Parameter(Mandatory = $false)]
    [string] $Ffmpeg,

    [Parameter(Mandatory = $false)]
    [ValidateRange(0, 100)]
    [int] $WebpQuality = 80,

    [Parameter(Mandatory = $false)]
    [switch] $Gif,

    [Parameter(Mandatory = $false)]
    [switch] $Mp4,

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

function Resolve-Ffmpeg {
    param([string] $RequestedPath)

    $requested = $RequestedPath
    if ([string]::IsNullOrWhiteSpace($requested)) {
        if (![string]::IsNullOrWhiteSpace($env:FFMPEG)) {
            $requested = $env:FFMPEG
        } elseif (![string]::IsNullOrWhiteSpace($env:FFMPEG_PATH)) {
            $requested = $env:FFMPEG_PATH
        }
    }

    if (![string]::IsNullOrWhiteSpace($requested)) {
        if (Test-Path -LiteralPath $requested -PathType Leaf) {
            return (Resolve-Path -LiteralPath $requested).Path
        }

        $looksLikePath = [System.IO.Path]::IsPathRooted($requested) -or
            $requested.Contains([System.IO.Path]::DirectorySeparatorChar) -or
            $requested.Contains([System.IO.Path]::AltDirectorySeparatorChar)
        if ($looksLikePath) {
            throw "The requested ffmpeg executable was not found at '$requested'."
        }

        $requestedCommand = Get-Command -Name $requested -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($null -ne $requestedCommand) {
            return $requestedCommand.Source
        }

        throw "The requested ffmpeg command '$requested' was not found on PATH."
    }

    $pathCommand = Get-Command -Name "ffmpeg" -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -ne $pathCommand) {
        return $pathCommand.Source
    }

    throw "ffmpeg is required for Animation mode. Pass -Ffmpeg, set FFMPEG or FFMPEG_PATH, or add ffmpeg to PATH."
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

function Invoke-Ffmpeg {
    param(
        [string] $Executable,
        [string[]] $Arguments,
        [string] $Description
    )

    Write-Host "Running ffmpeg for $Description."
    $processInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = $Executable
    $processInfo.UseShellExecute = $false
    $processInfo.RedirectStandardOutput = $true
    $processInfo.RedirectStandardError = $true

    foreach ($arg in $Arguments) {
        [void] $processInfo.ArgumentList.Add($arg)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $processInfo

    try {
        if (!$process.Start()) {
            throw "ffmpeg did not start."
        }

        $standardOutput = $process.StandardOutput.ReadToEndAsync()
        $standardError = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $output = $standardOutput.GetAwaiter().GetResult()
        $errorOutput = $standardError.GetAwaiter().GetResult()

        if ($process.ExitCode -ne 0) {
            if (![string]::IsNullOrWhiteSpace($output)) {
                Write-Host $output.Trim()
            }

            if (![string]::IsNullOrWhiteSpace($errorOutput)) {
                Write-Host $errorOutput.Trim()
            }

            throw "ffmpeg exited with code $($process.ExitCode) while encoding $Description."
        }
    } finally {
        $process.Dispose()
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

    if ($Name -in @("visual", "animation") -and ![string]::IsNullOrWhiteSpace($ScenarioFilter)) {
        $args += @("-nowuiScenarioFilter", $ScenarioFilter)
    }

    Invoke-Unity -UnityArgs $args -LogPath (Join-Path $methodArtifacts "NowUI-$Name.log")
}

function Invoke-AnimationEncode {
    param(
        [string] $ManifestPath,
        [string] $AnimationRoot
    )

    try {
        $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    } catch {
        throw "Animation manifest '$ManifestPath' is invalid: $($_.Exception.Message)"
    }

    $captures = @($manifest.captures)
    if ($captures.Count -eq 0) {
        Write-Host "Animation manifest '$ManifestPath' lists no captures. Nothing was encoded."
        return
    }

    $ffmpegPath = Resolve-Ffmpeg -RequestedPath $Ffmpeg
    Write-Host "Using ffmpeg from '$ffmpegPath'."

    $animationRoot = [System.IO.Path]::GetFullPath($AnimationRoot)
    $rootPrefix = $animationRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $pathComparison = if ($IsWindows) {
        [System.StringComparison]::OrdinalIgnoreCase
    } else {
        [System.StringComparison]::Ordinal
    }

    foreach ($capture in $captures) {
        if ($null -eq $capture) {
            throw "Animation manifest '$ManifestPath' contains an empty capture."
        }

        $name = [string] $capture.name
        [int] $frameCount = $capture.frameCount
        [double] $frameRate = $capture.framesPerSecond
        $frameDirectory = [System.IO.Path]::GetFullPath([string] $capture.frameDirectory)
        $framePattern = [string] $capture.framePattern
        $outputStem = [System.IO.Path]::GetFullPath([string] $capture.outputStem)

        if ([string]::IsNullOrWhiteSpace($name) -or $frameCount -le 0 -or
            $frameRate -le 0 -or [double]::IsNaN($frameRate) -or [double]::IsInfinity($frameRate)) {
            throw "Animation manifest '$ManifestPath' contains invalid timing metadata for '$name'."
        }

        if ($framePattern -ne "frame-%04d.png") {
            throw "Animation '$name' uses unsupported frame pattern '$framePattern'."
        }

        if (!$frameDirectory.StartsWith($rootPrefix, $pathComparison) -or
            !$outputStem.StartsWith($rootPrefix, $pathComparison)) {
            throw "Animation '$name' resolves outside '$animationRoot'."
        }

        $firstFrame = Join-Path $frameDirectory "frame-0000.png"
        $lastFrame = Join-Path $frameDirectory ("frame-{0:D4}.png" -f ($frameCount - 1))
        if (!(Test-Path -LiteralPath $firstFrame -PathType Leaf) -or
            !(Test-Path -LiteralPath $lastFrame -PathType Leaf)) {
            throw "Animation '$name' did not produce its complete numbered PNG sequence in '$frameDirectory'."
        }

        $frameRateText = [string]::Format(
            [System.Globalization.CultureInfo]::InvariantCulture,
            "{0:0.###}",
            $frameRate)
        $frameInput = Join-Path $frameDirectory $framePattern
        $inputArguments = @(
            "-hide_banner",
            "-loglevel", "warning",
            "-y",
            "-framerate", $frameRateText,
            "-start_number", "0",
            "-i", $frameInput,
            "-frames:v", [string] $frameCount
        )

        # Animated WebP is the README format: full 24-bit colour, no palette
        # dithering, under half the GIF size overall, and it still autoplays
        # inside a plain <img> tag on GitHub. libwebp_anim (not libwebp) is what
        # exploits inter-frame redundancy; compression level 5 is within a few
        # percent of level 6 at a fraction of the encode time.
        $webpPath = "$outputStem.webp"
        Remove-Item -LiteralPath $webpPath -Force -ErrorAction SilentlyContinue
        Invoke-Ffmpeg -Executable $ffmpegPath -Description "animation '$name' (webp)" -Arguments ($inputArguments + @(
            "-c:v", "libwebp_anim",
            "-quality", [string] $WebpQuality,
            "-compression_level", "5",
            "-loop", "0",
            $webpPath
        ))
        Write-Host "Encoded '$webpPath'."

        if ($Gif) {
            # Ordered dithering stays stable between frames, preserving gradients
            # without the large temporal-noise penalty of error diffusion in GIFs.
            $filter = "[0:v]split[palette_source][frames];[palette_source]palettegen=max_colors=256:stats_mode=diff[palette];[frames][palette]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle"
            $gifPath = "$outputStem.gif"
            Remove-Item -LiteralPath $gifPath -Force -ErrorAction SilentlyContinue
            Invoke-Ffmpeg -Executable $ffmpegPath -Description "animation '$name' (gif)" -Arguments ($inputArguments + @(
                "-filter_complex", $filter,
                "-loop", "0",
                "-gifflags", "+transdiff",
                $gifPath
            ))
            Write-Host "Encoded '$gifPath'."
        }

        if ($Mp4) {
            # H.264 needs even dimensions; the scale filter only rounds down
            # odd sizes and is a no-op for the 960x540 README captures.
            $mp4Path = "$outputStem.mp4"
            Remove-Item -LiteralPath $mp4Path -Force -ErrorAction SilentlyContinue
            Invoke-Ffmpeg -Executable $ffmpegPath -Description "animation '$name' (mp4)" -Arguments ($inputArguments + @(
                "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2",
                "-c:v", "libx264",
                "-preset", "veryslow",
                "-crf", "18",
                "-pix_fmt", "yuv420p",
                "-movflags", "+faststart",
                $mp4Path
            ))
            Write-Host "Encoded '$mp4Path'."
        }
    }
}

function Invoke-AnimationCapture {
    $animationArtifacts = Join-Path $ArtifactsPath "animation"
    $manifestPath = Join-Path $animationArtifacts "manifest.json"
    Remove-Item -LiteralPath $manifestPath -Force -ErrorAction SilentlyContinue

    Invoke-ExecuteMethod "NowUI.Editor.NowVisualHarnessRunner.CaptureAnimations" "animation"

    if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Unity did not write an animation manifest to '$manifestPath'."
    }

    Invoke-AnimationEncode -ManifestPath $manifestPath -AnimationRoot $animationArtifacts
}

function Invoke-AnimationReencode {
    $animationArtifacts = Join-Path $ArtifactsPath "animation"
    $manifestPath = Join-Path $animationArtifacts "manifest.json"

    if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "No animation manifest was found at '$manifestPath'. Run '-Mode Animation' first."
    }

    Invoke-AnimationEncode -ManifestPath $manifestPath -AnimationRoot $animationArtifacts
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
    "Animation" { Invoke-AnimationCapture }
    "Encode" { Invoke-AnimationReencode }
    "All" {
        Invoke-TestRun "EditMode"
        Invoke-TestRun "PlayMode"
        Invoke-ExecuteMethod "NowUI.Editor.NowVisualHarnessRunner.Capture" "visual"
        Invoke-ExecuteMethod "NowUI.Editor.NowVisualHarnessRunner.CompareGoldens" "golden"
        Invoke-ExecuteMethod "NowUI.Editor.NowPerfSmokeRunner.Run" "perf"
    }
}
