[CmdletBinding()]
param(
    [string]$OpenScad = "openscad"
)

$ErrorActionPreference = "Stop"
$resolvedOpenScad = (Get-Command $OpenScad -ErrorAction Stop).Source
if ($resolvedOpenScad.EndsWith(".exe", [StringComparison]::OrdinalIgnoreCase)) {
    $consoleExecutable = [IO.Path]::ChangeExtension($resolvedOpenScad, ".com")
    if (Test-Path -LiteralPath $consoleExecutable) {
        $resolvedOpenScad = $consoleExecutable
    }
}
$source = Join-Path $PSScriptRoot "ureteroscope_controller.scad"
$output = Join-Path $PSScriptRoot "stl"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$parts = [ordered]@{
    "handle_body" = 1
    "handle_lid" = 2
    "guide_base" = 3
    "guide_bushing" = 4
    "encoder_arm" = 5
    "encoder_wheel" = 6
}
foreach ($part in $parts.Keys) {
    $target = Join-Path $output "$part.stl"
    & $resolvedOpenScad "-D" "export_part=$($parts[$part])" "-o" $target $source
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $target)) {
        throw "OpenSCAD failed while exporting $part."
    }
}
Write-Host "Exported $($parts.Count) printable parts to $output"
