param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$configPath = Join-Path $Root 'config.json'
$settingsPath = Join-Path $Root 'src\BDT.Settings.cs'
$hotkeysPath = Join-Path $Root 'src\BDT.HotkeysRegistry.cs'
$localizationPath = Join-Path $Root 'src\BDT.Localization.cs'
$translationsPath = Join-Path $Root 'translations'

function Get-TranslationKeys([string]$Path) {
    Get-Content -Raw $Path |
        ConvertFrom-Json |
        ForEach-Object { [string]$_.Item(0) }
}

$config = Get-Content -Raw $configPath | ConvertFrom-Json
$configKeys = @($config.PSObject.Properties.Name)
$pollutionKeys = @(
    'pollution_overlay_enabled',
    'pollution_glow_enabled',
    'pollution_glow_color',
    'pollution_days_to_average',
    'pollution_show_air',
    'pollution_show_ground',
    'pollution_show_solid_waste',
    'pollution_show_vehicle',
    'pollution_show_ship'
)

$missingConfigKeys = @($pollutionKeys | Where-Object { $configKeys -notcontains $_ })
if ($missingConfigKeys.Count -gt 0) {
    throw "Missing BDT pollution config key(s): $($missingConfigKeys -join ', ')"
}

$hotkeyLines = Get-Content $hotkeysPath
$hotkeyKeys = foreach ($line in $hotkeyLines) {
    if ($line -match '^\s*\[Kb\([^,]+,\s*"(?<id>[^"]+)",\s*"(?<label>[^"]+)",\s*"(?<tooltip>[^"]*)"') {
        "Kb_$($Matches.id)__label"
        if (-not [string]::IsNullOrEmpty($Matches.tooltip)) {
            "Kb_$($Matches.id)__tooltip"
        }
    }
}

$localizationText = Get-Content -Raw $localizationPath
$missingSourceKeys = @($hotkeyKeys | Where-Object { $localizationText -notmatch [regex]::Escape('"' + $_ + '"') })
if ($missingSourceKeys.Count -gt 0) {
    throw "Missing BDT hotkey source localization key(s): $($missingSourceKeys -join ', ')"
}

$translationFiles = @(Get-ChildItem $translationsPath -Filter '*.json' -File | Where-Object { -not $_.Name.StartsWith('.') })
foreach ($translationFile in $translationFiles) {
    $translationKeys = @(Get-TranslationKeys $translationFile.FullName)
    $missingTranslationKeys = @($hotkeyKeys | Where-Object { $translationKeys -notcontains $_ })
    if ($missingTranslationKeys.Count -gt 0) {
        throw "Missing BDT hotkey translation key(s) in $($translationFile.Name): $($missingTranslationKeys -join ', ')"
    }
}

Write-Output "BDT config and hotkey localization checks passed ($($translationFiles.Count) translation files)."
