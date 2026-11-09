<#
.SYNOPSIS
Does the mechanical part of the kit's setup in a new game repo (PROMPT.md, "Before you start").

.DESCRIPTION
1. Checks the tools and the git identity. It changes nothing until every check passes.
2. Finds the Unity project. If Unity Hub made Unity/<Name>/, it moves the project up into Unity/.
3. Reads the Unity version and which URP template made the project.
4. Copies template/ into the repo and replaces __GAME__, __UNITY__, __DATE__ and __RENDER__, in file contents and
   in folder and file names. Then it checks that no placeholder remains.
5. Adds MCP for Unity (latest stable release, no version pin) to Unity/Packages/manifest.json.
6. Runs Tools/setup-machine.ps1 (Git LFS, Unity Smart Merge).

It prints what it found and what is left, one line each. Runs in Windows PowerShell 5.1 and later.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File C:\Kits\AgenticUnitySetup\setup.ps1 -Game StormHarbor
#>
[CmdletBinding()]
param(
    # The game's code name: one word in PascalCase, for example StormHarbor.
    [Parameter(Mandatory = $true)][string]$Game,
    # The game repo's root. Default: the current folder.
    [string]$Repo = (Get-Location).Path,
    # Delete Unity/.git, the second repo that Unity Hub can make. Use it only after the designer agrees.
    [switch]$RemoveNestedGit,
    # Copy the template again into a repo that already has it. This replaces every template file.
    [switch]$Force
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

$Kit = $PSScriptRoot
$Template = Join-Path $Kit 'template'
$Repo = (Resolve-Path -LiteralPath $Repo).Path
$Unity = Join-Path $Repo 'Unity'
$McpPackage = 'com.coplaydev.unity-mcp'
# The main branch is MCP for Unity's latest stable release; beta is its pre-release branch.
$McpUrl = 'https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main'
$Utf8 = New-Object System.Text.UTF8Encoding($false)

function Stop-Setup([string[]]$Lines) {
    Write-Output 'Setup stopped:'
    foreach ($line in $Lines) {
        Write-Output "- $line"
    }
    exit 1
}

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

function Read-Text([string]$Path) {
    return [System.IO.File]::ReadAllText($Path)
}

function Write-Text([string]$Path, [string]$Text) {
    [System.IO.File]::WriteAllText($Path, $Text, $Utf8)
}

# 1. Checks
$problems = New-Object System.Collections.Generic.List[string]
if ($Game -cnotmatch '^[A-Z][A-Za-z0-9]*$') {
    $problems.Add("'$Game' is not one PascalCase word (letters and digits, a capital first). Example: StormHarbor.")
}
if (-not (Test-Path -LiteralPath $Template)) {
    $problems.Add("The kit has no template/ folder beside setup.ps1 ($Kit).")
}
if ($null -eq (Invoke-Native 'git' @('-C', $Repo, 'rev-parse', '--show-toplevel'))) {
    $problems.Add("$Repo is not a git repo, or git is not on the PATH. Make an empty git repo first.")
} else {
    if (-not (Invoke-Native 'git' @('-C', $Repo, 'config', 'user.name'))) {
        $problems.Add('git config user.name is empty. Ask the designer which identity to use, and where (this repo or global).')
    }
    if (-not (Invoke-Native 'git' @('-C', $Repo, 'config', 'user.email'))) {
        $problems.Add('git config user.email is empty. Ask the designer which identity to use, and where (this repo or global).')
    }
}
if ($null -eq (Invoke-Native 'git' @('lfs', 'version'))) {
    $problems.Add('Git LFS is missing. Tell the designer to install it from https://git-lfs.com.')
}
if ($null -eq (Invoke-Native 'uvx' @('--version'))) {
    $problems.Add('uv is missing (the Unity MCP server runs through it). Tell the designer to install it from https://docs.astral.sh/uv/.')
}
$sdks = Invoke-Native 'dotnet' @('--list-sdks')
$newestSdk = 0
if ($sdks) {
    foreach ($line in ($sdks -split "`r?`n")) {
        if ($line -match '^(\d+)\.') {
            $newestSdk = [Math]::Max($newestSdk, [int]$Matches[1])
        }
    }
}
if ($newestSdk -lt 9) {
    $problems.Add('The .NET SDK 9 or later is missing. Tell the designer to install it from https://dotnet.microsoft.com/download.')
}
if ((Test-Path -LiteralPath (Join-Path $Repo 'AI')) -and -not $Force) {
    $problems.Add('The repo already has AI/: the template is already copied. Run again with -Force only to replace every template file.')
}
if ($problems.Count -gt 0) {
    Stop-Setup $problems
}

# 2. The Unity project
$versionFile = Join-Path $Unity 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile)) {
    $nested = @(Get-ChildItem -LiteralPath $Unity -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'ProjectSettings\ProjectVersion.txt') })
    if ($nested.Count -ne 1) {
        Stop-Setup @('No Unity project in Unity/ or in one Unity/<Name>/ folder. Tell the designer to make it in Unity Hub (README.md, step 3).')
    }
    $sub = $nested[0].FullName
    if (Test-Path -LiteralPath (Join-Path $sub 'Temp\UnityLockfile')) {
        Stop-Setup @("The Unity editor has $sub open (Temp/UnityLockfile exists). Tell the designer to close it. If it is closed, the file is left from a crash: delete it.")
    }
    foreach ($item in (Get-ChildItem -LiteralPath $sub -Force)) {
        $target = Join-Path $Unity $item.Name
        if (Test-Path -LiteralPath $target) {
            Stop-Setup @("Cannot move $($item.Name) up: Unity/$($item.Name) already exists.")
        }
        Move-Item -LiteralPath $item.FullName -Destination $target
    }
    Remove-Item -LiteralPath $sub
    Write-Output "Moved the Unity project from Unity/$($nested[0].Name)/ up into Unity/."
}
Get-ChildItem -LiteralPath $Unity -File | Where-Object { $_.Extension -in '.sln', '.slnx', '.csproj' } | Remove-Item
if (Test-Path -LiteralPath (Join-Path $Unity '.git')) {
    if (-not $RemoveNestedGit) {
        Stop-Setup @('Unity/.git exists: Unity Hub made a second repo inside the project. Ask the designer, then run again with -RemoveNestedGit.')
    }
    Remove-Item -LiteralPath (Join-Path $Unity '.git') -Recurse -Force
    Write-Output 'Deleted Unity/.git.'
}

# 3. Unity version and URP template
$match = Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion:\s*(\d+)\.(\d+)\.(\S+)' | Select-Object -First 1
if (-not $match) {
    Stop-Setup @('Unity/ProjectSettings/ProjectVersion.txt has no m_EditorVersion line.')
}
$major = [int]$match.Matches[0].Groups[1].Value
$minor = [int]$match.Matches[0].Groups[2].Value
$editorVersion = "$major.$minor.$($match.Matches[0].Groups[3].Value)"
$unityLine = if ($major -ge 6000) { "$($major / 1000).$minor" } else { "$major.$minor" }

$manifestPath = Join-Path $Unity 'Packages\manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    Stop-Setup @('Unity/Packages/manifest.json is missing.')
}
$manifest = Read-Text $manifestPath
$settings = Join-Path $Unity 'Assets\Settings'
$renderer2D = (Test-Path -LiteralPath $settings) -and @(Get-ChildItem -LiteralPath $settings -Recurse -Filter '*.asset' |
    Where-Object { $_.Name -like '*Renderer2D*' -or (Read-Text $_.FullName) -match 'Renderer2DData' }).Count -gt 0
$is2D = ($manifest -match '"com\.unity\.2d\.') -and $renderer2D
$render = if ($is2D) { '2D URP (Universal 2D template)' } else { '3D URP' }

# 4. The template
$replacements = [ordered]@{
    '__GAME__'   = $Game
    '__UNITY__'  = $unityLine
    '__DATE__'   = (Get-Date -Format 'yyyy-MM-dd')
    '__RENDER__' = $render
}
$written = New-Object System.Collections.Generic.List[string]
foreach ($file in (Get-ChildItem -LiteralPath $Template -Recurse -File -Force)) {
    $relative = $file.FullName.Substring($Template.Length).TrimStart('\', '/')
    $target = Join-Path $Repo ($relative.Replace('__GAME__', $Game))
    $folder = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $folder)) {
        New-Item -ItemType Directory -Path $folder | Out-Null
    }
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    if ([Array]::IndexOf($bytes, [byte]0) -ge 0) {
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        continue
    }
    $text = Read-Text $file.FullName
    foreach ($key in $replacements.Keys) {
        $text = $text.Replace($key, $replacements[$key])
    }
    if ($is2D -and $relative -eq 'AI\ARCHITECTURE.md') {
        $text = $text -replace '\bModels/', 'Sprites/'
    }
    Write-Text $target $text
    $written.Add($target)
}
$left = @($written | Where-Object { (Read-Text $_) -match '__(GAME|UNITY|DATE|RENDER)__' })
if ($left.Count -gt 0) {
    Stop-Setup @("Placeholders remain in: $($left -join ', ')")
}
Write-Output "Copied the template: $($written.Count) files."

# 5. MCP for Unity
if ($manifest -match ('"' + [regex]::Escape($McpPackage) + '"')) {
    Write-Output "MCP for Unity: already in manifest.json, left as it is."
} else {
    $dependencies = [regex]::Match($manifest, '"dependencies"\s*:\s*\{')
    if (-not $dependencies.Success) {
        Stop-Setup @('Unity/Packages/manifest.json has no "dependencies" object.')
    }
    $newline = if ($manifest.Contains("`r`n")) { "`r`n" } else { "`n" }
    $entry = $newline + '    "' + $McpPackage + '": "' + $McpUrl + '",'
    Write-Text $manifestPath $manifest.Insert($dependencies.Index + $dependencies.Length, $entry)
    Write-Output "MCP for Unity: added to manifest.json ($McpUrl)."
}

# 6. This machine
Write-Output ''
try {
    & (Join-Path $Repo 'Tools\setup-machine.ps1')
    $machineOk = $LASTEXITCODE -eq 0
} catch {
    Write-Output "- Tools/setup-machine.ps1 failed: $_"
    $machineOk = $false
}

Write-Output ''
Write-Output "Game: $Game"
Write-Output "Unity: $editorVersion (line $unityLine)"
if ($major -ne 6000 -or $minor -ne 7) {
    Write-Output "- The kit is made for Unity 6.7 (6000.7). Tell the designer before you continue, and expect differences (PROMPT.md)."
}
Write-Output "Render: $render"
if (-not $machineOk) {
    Write-Output '- Tools/setup-machine.ps1 left work open (listed above). Run it again when that is done.'
}
Write-Output 'Next: PROMPT.md, "Steps", step 1.'
