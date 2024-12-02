# SPDX-FileCopyrightText: 2024 Friedrich von Never <friedrich@fornever.me>
#
# SPDX-License-Identifier: MIT

param(
    [string] $RefName,
    [string] $RepositoryRoot = "$PSScriptRoot/..",

    $ProjectFile = "$RepositoryRoot/Emulsion/Emulsion.fsproj"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Write-Host "Determining version from ref `"$RefName`"…"
if ($RefName -match '^refs/tags/v') {
    $version = $RefName -replace '^refs/tags/v', ''
    Write-Host "Pushed ref is a version tag, version: $version"
} else {
    [xml] $props = Get-Content $ProjectFile
    $version = $props.Project.PropertyGroup.Version
    Write-Host "Pushed ref is a not version tag, get version from $($ProjectFile): $version"
}

Write-Output $version
