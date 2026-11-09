<#
.SYNOPSIS
Prepares this machine for the repo. Run it once per machine, and again after a Unity version change.

.DESCRIPTION
- Turns on Git LFS for this user.
- Registers Unity's Smart Merge (UnityYAMLMerge) in this repo's git config, so scenes and prefabs merge as Unity
  YAML. .gitattributes names the driver; without this step git merges them as plain text.
- Checks the tools that the agent needs: uv (the Unity MCP server) and the .NET SDK 9 or later (the headless compile).

It changes only this repo's .git/config and the user's Git LFS hooks. Runs in Windows PowerShell 5.1 and later.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\setup-machine.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$problems = New-Object System.Collections.Generic.List[string]

# Runs a native command. Returns its output, or $null when it is missing or fails. Windows PowerShell 5.1 turns
# native stderr into errors under 'Stop', so this lowers the preference while the command runs.
function Invoke-Native([string]$Exe, [string[]]$Arguments) {
    if (-not (Get-Command $Exe -ErrorAction SilentlyContinue)) {
        return $null
    }
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & $Exe @Arguments 2>&1 | ForEach-Object { "$_" }
        if ($LASTEXITCODE -ne 0) {
            return $null
        }
        return (($output | Out-String).Trim())
    } finally {
        $ErrorActionPreference = $previous
    }
}

function Find-UnityYamlMerge([string]$EditorVersion) {
    $roots = New-Object System.Collections.Generic.List[string]
    $roots.Add((Join-Path $env:ProgramFiles 'Unity\Hub\Editor'))
    $secondary = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
    if (Test-Path -LiteralPath $secondary) {
        # Unity Hub writes the folder as a JSON string, with escaped backslashes.
        try {
            $custom = Get-Content -LiteralPath $secondary -Raw | ConvertFrom-Json
        } catch {
            $custom = $null
        }
        if ($custom) {
            $roots.Add([string]$custom)
        }
    }
    foreach ($editors in $roots) {
        $candidate = Join-Path $editors "$EditorVersion\Editor\Data\Tools\UnityYAMLMerge.exe"
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }
    return $null
}

if ($null -eq (Invoke-Native 'git' @('--version'))) {
    Write-Error 'git is not on the PATH. Install Git for Windows, then run this again.'
}

# Git LFS
if ($null -eq (Invoke-Native 'git' @('lfs', 'version'))) {
    $problems.Add('Git LFS is missing. Install it from https://git-lfs.com, then run this again.')
} elseif ($null -eq (Invoke-Native 'git' @('lfs', 'install'))) {
    $problems.Add('git lfs install failed. Run it by hand to see why.')
} else {
    Write-Output 'Git LFS: on.'
}

# Unity Smart Merge
$versionFile = Join-Path $Root 'Unity\ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile)) {
    $problems.Add('Unity/ProjectSettings/ProjectVersion.txt is missing, so the Unity version is unknown. Smart Merge is not registered.')
} else {
    $match = Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion:\s*(\S+)' | Select-Object -First 1
    $editorVersion = if ($match) { $match.Matches[0].Groups[1].Value } else { $null }
    $yamlMerge = if ($editorVersion) { Find-UnityYamlMerge $editorVersion } else { $null }
    if ($null -eq $editorVersion) {
        $problems.Add('ProjectVersion.txt has no m_EditorVersion line. Smart Merge is not registered.')
    } elseif ($null -eq $yamlMerge) {
        $problems.Add("UnityYAMLMerge.exe for Unity $editorVersion is not in the Unity Hub editor folders. Install that editor through Unity Hub, then run this again.")
    } else {
        # Single quotes: git runs the driver through sh, and Windows PowerShell 5.1 mangles embedded double quotes.
        $driver = "'" + ($yamlMerge -replace '\\', '/') + "' merge -h -p --force %O %B %A %A"
        $settings = @(
            @('merge.unityyamlmerge.name', 'Unity Smart Merge'),
            @('merge.unityyamlmerge.driver', $driver),
            @('merge.unityyamlmerge.recursive', 'binary')
        )
        foreach ($setting in $settings) {
            if ($null -eq (Invoke-Native 'git' @('-C', $Root, 'config', '--local', $setting[0], $setting[1]))) {
                Write-Error "git config $($setting[0]) failed."
            }
        }
        Write-Output "Unity Smart Merge: registered ($yamlMerge)."
    }
}

# Agent tools
if ($null -eq (Invoke-Native 'uvx' @('--version'))) {
    $problems.Add('uv is missing (Claude Code starts the Unity MCP server with it). Install it from https://docs.astral.sh/uv/.')
} else {
    Write-Output 'uv: found.'
}
$sdks = Invoke-Native 'dotnet' @('--list-sdks')
$newest = 0
if ($sdks) {
    foreach ($line in ($sdks -split "`r?`n")) {
        if ($line -match '^(\d+)\.') {
            $newest = [Math]::Max($newest, [int]$Matches[1])
        }
    }
}
if ($newest -lt 9) {
    $problems.Add('The .NET SDK 9 or later is missing (the headless compile needs it). Install it from https://dotnet.microsoft.com/download.')
} else {
    Write-Output ".NET SDK: $newest found."
}

if ($problems.Count -gt 0) {
    Write-Output ''
    Write-Output 'Not done:'
    foreach ($problem in $problems) {
        Write-Output "- $problem"
    }
    exit 1
}
Write-Output 'This machine is ready.'
exit 0
