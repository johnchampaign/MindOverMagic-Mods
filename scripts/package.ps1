param(
    [string]$BundleVersion = "1.2.4",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$distRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "dist"))

$modules = @(
    [PSCustomObject]@{
        Name = "CharacterLevelRelics"
        Project = "src\CharacterLevelRelics\CharacterLevelRelics.csproj"
        Assembly = "CharacterLevelRelics.dll"
        Output = "src\CharacterLevelRelics\bin\$Configuration\netstandard2.1\CharacterLevelRelics.dll"
    },
    [PSCustomObject]@{
        Name = "FactionBalance"
        Project = "src\FactionBalance\FactionBalance.csproj"
        Assembly = "FactionBalance.dll"
        Output = "src\FactionBalance\bin\$Configuration\netstandard2.1\FactionBalance.dll"
    },
    [PSCustomObject]@{
        Name = "SacrificialAltar"
        Project = "src\SacrificialAltar\SacrificialAltar.csproj"
        Assembly = "SacrificialAltar.dll"
        Output = "src\SacrificialAltar\bin\$Configuration\netstandard2.1\SacrificialAltar.dll"
        Content = "src\SacrificialAltar\Content"
    },
    [PSCustomObject]@{
        Name = "ArchmageProgression"
        Project = "src\ArchmageAscension\ArchmageAscension.csproj"
        Assembly = "ArchmageProgression.dll"
        Output = "src\ArchmageAscension\bin\$Configuration\netstandard2.1\ArchmageProgression.dll"
        Content = "src\ArchmageAscension\Content"
    }
)

foreach ($module in $modules) {
    dotnet build (Join-Path $repoRoot $module.Project) -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for $($module.Project)"
    }
}

New-Item -ItemType Directory -Force -Path $distRoot | Out-Null

function Assert-WithinDist([string]$Path) {
    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($distRoot + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to use a staging path outside the repository dist folder: $resolved"
    }
}

function New-PackageStage([string]$PackageName) {
    $stage = Join-Path $distRoot $PackageName
    $zip = Join-Path $distRoot "$PackageName.zip"
    Assert-WithinDist $stage
    Assert-WithinDist $zip
    if (Test-Path -LiteralPath $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
    if (Test-Path -LiteralPath $zip) {
        Remove-Item -LiteralPath $zip -Force
    }
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    return [PSCustomObject]@{ Stage = $stage; Zip = $zip }
}

function Add-Module([string]$Stage, $Module) {
    $moduleRoot = Join-Path $Stage "BepInEx\plugins\$($Module.Name)"
    New-Item -ItemType Directory -Force -Path $moduleRoot | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot $Module.Output) -Destination (Join-Path $moduleRoot $Module.Assembly)
    if ($Module.PSObject.Properties.Name -contains "Content") {
        Copy-Item -LiteralPath (Join-Path $repoRoot $Module.Content) -Destination $moduleRoot -Recurse
    }
}

function Add-Documentation([string]$Stage) {
    Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination $Stage
    Copy-Item -LiteralPath (Join-Path $repoRoot "CHANGELOG.md") -Destination $Stage
    Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $Stage
}

function Complete-Package($Package) {
    Compress-Archive -Path (Join-Path $Package.Stage "*") -DestinationPath $Package.Zip -CompressionLevel Optimal
    Get-FileHash -Algorithm SHA256 -LiteralPath $Package.Zip
}

# Convenience option: every independently installable module in one archive.
$allPackage = New-PackageStage "MindOverMagic-Mods-v$BundleVersion"
foreach ($module in $modules) {
    Add-Module $allPackage.Stage $module
}
Add-Documentation $allPackage.Stage
Complete-Package $allPackage

# Player-choice options: one directly extractable archive per module.
foreach ($module in $modules) {
    $singlePackage = New-PackageStage "MindOverMagic-$($module.Name)-v$BundleVersion"
    Add-Module $singlePackage.Stage $module
    Add-Documentation $singlePackage.Stage
    Complete-Package $singlePackage
}
