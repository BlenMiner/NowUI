#requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet(
        "all",
        "WebGL",
        "StandaloneWindows64",
        "StandaloneLinux64",
        "StandaloneOSX",
        "Android",
        "iOS")]
    [string] $Target = "all"
)

$ErrorActionPreference = "Stop"

$targets = @(
    [ordered] @{
        target = "StandaloneWindows64"
        runs_on = '["self-hosted","Windows"]'
        unity_editor = 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe'
        playback_engines = 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Data\PlaybackEngines'
        module = "WindowsStandaloneSupport"
    },
    [ordered] @{
        target = "StandaloneLinux64"
        runs_on = '["self-hosted","Linux"]'
        unity_editor = "/opt/unity/Editor/Unity"
        playback_engines = "/opt/unity/Editor/Data/PlaybackEngines"
        module = "LinuxStandaloneSupport"
    },
    [ordered] @{
        target = "StandaloneOSX"
        runs_on = '["self-hosted","macOS"]'
        unity_editor = "/Applications/Unity/Hub/Editor/6000.4.0f1/Unity.app/Contents/MacOS/Unity"
        playback_engines = "/Applications/Unity/Hub/Editor/6000.4.0f1/Unity.app/Contents/PlaybackEngines"
        module = "MacStandaloneSupport"
    },
    [ordered] @{
        target = "Android"
        runs_on = '["self-hosted","Windows"]'
        unity_editor = 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe'
        playback_engines = 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Data\PlaybackEngines'
        module = "AndroidPlayer"
    },
    [ordered] @{
        target = "iOS"
        runs_on = '["self-hosted","macOS"]'
        unity_editor = "/Applications/Unity/Hub/Editor/6000.4.0f1/Unity.app/Contents/MacOS/Unity"
        playback_engines = "/Applications/Unity/Hub/Editor/6000.4.0f1/Unity.app/Contents/PlaybackEngines"
        module = "iOSSupport"
    },
    [ordered] @{
        target = "WebGL"
        runs_on = '["self-hosted","Windows"]'
        unity_editor = 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe'
        playback_engines = 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Data\PlaybackEngines'
        module = "WebGLSupport"
    }
)

$selected = @(
    if ($Target -eq "all") {
        $targets
    } else {
        $targets | Where-Object { $_.target -eq $Target }
    }
)

if ($selected.Count -eq 0) {
    throw "No build-verification target matched '$Target'."
}

[ordered] @{ include = $selected } | ConvertTo-Json -Depth 5 -Compress
