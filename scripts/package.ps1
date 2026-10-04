param(
    [string]$BundleVersion = "1.2.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$distRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "dist"))
$stageRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $distRoot "MindOverMagic-Mods-v$BundleVersion"))
$zipPath = Join-Path $distRoot "MindOverMagic-Mods-v$BundleVersion.zip"

if (-not $stageRoot.StartsWith($distRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a staging directory outside the repository dist folder."
}

$projects = @(
    "src\CharacterLevelRelics\CharacterLevelRelics.csproj",
    "src\FactionBalance\FactionBalance.csproj",
    "src\SacrificialAltar\SacrificialAltar.csproj",
    "src\ArchmageAscension\ArchmageAscension.csproj"
)

foreach ($project in $projects) {
    dotnet build (Join-Path $repoRoot $project) -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for $project"
    }
}

New-Item -ItemType Directory -Force -Path $distRoot | Out-Null
if (Test-Path -LiteralPath $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$pluginsRoot = Join-Path $stageRoot "BepInEx\plugins"
$characterRoot = Join-Path $pluginsRoot "CharacterLevelRelics"
$factionRoot = Join-Path $pluginsRoot "FactionBalance"
$altarRoot = Join-Path $pluginsRoot "SacrificialAltar"
$archmageRoot = Join-Path $pluginsRoot "ArchmageProgression"
New-Item -ItemType Directory -Force -Path $characterRoot,$factionRoot,$altarRoot,$archmageRoot | Out-Null

Copy-Item -LiteralPath (Join-Path $repoRoot "src\CharacterLevelRelics\bin\$Configuration\netstandard2.1\CharacterLevelRelics.dll") -Destination $characterRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "src\FactionBalance\bin\$Configuration\netstandard2.1\FactionBalance.dll") -Destination $factionRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "src\SacrificialAltar\bin\$Configuration\netstandard2.1\SacrificialAltar.dll") -Destination $altarRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "src\ArchmageAscension\bin\$Configuration\netstandard2.1\ArchmageProgression.dll") -Destination $archmageRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "src\SacrificialAltar\Content") -Destination $altarRoot -Recurse

Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination $stageRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "CHANGELOG.md") -Destination $stageRoot
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $stageRoot

Compress-Archive -Path (Join-Path $stageRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal
Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath
