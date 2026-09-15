[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$SpriteForgeRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$spriteForgeCandidate = $SpriteForgeRoot
if ([string]::IsNullOrWhiteSpace($spriteForgeCandidate))
{
    $spriteForgeCandidate = $env:SPRITEFORGE_ROOT
}
if ([string]::IsNullOrWhiteSpace($spriteForgeCandidate))
{
    $spriteForgeCandidate = Join-Path (Split-Path -Parent $projectRoot) "SpriteForge"
}

$resolvedSpriteForgeRoot = (Resolve-Path -LiteralPath $spriteForgeCandidate -ErrorAction Stop).Path
$buildType = $Configuration.ToLowerInvariant()
$nativeDirectory = Join-Path $resolvedSpriteForgeRoot "build\windows-msvc-$buildType\release\bin"
$nativeLibrary = Join-Path $nativeDirectory "SpriteForge.dll"
if (-not (Test-Path -LiteralPath $nativeLibrary -PathType Leaf))
{
    throw "SpriteForge.dll was not found at '$nativeLibrary'. Build and install SpriteForge before building Spelljammer."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue))
{
    throw "'dotnet' was not found on PATH. Install the .NET 10 SDK."
}

$solution = Join-Path $projectRoot "Spelljammer.slnx"
$originalPlatform = [Environment]::GetEnvironmentVariable("Platform", "Process")

try
{
    # A Visual Studio developer shell publishes Platform=x64, which dotnet can
    # incorrectly interpret as the solution platform.
    [Environment]::SetEnvironmentVariable("Platform", $null, "Process")

    Write-Host "Building Spelljammer ($Configuration)..."
    & dotnet build `
        $solution `
        --configuration $Configuration `
        "-p:SpriteForgeRoot=$resolvedSpriteForgeRoot" `
        "-p:SpriteForgeNativeDir=$nativeDirectory"
    if ($LASTEXITCODE -ne 0)
    {
        throw "Spelljammer build failed with exit code $LASTEXITCODE."
    }

    Write-Host "Build complete. Native runtime: $nativeDirectory"
}
finally
{
    [Environment]::SetEnvironmentVariable("Platform", $originalPlatform, "Process")
}
