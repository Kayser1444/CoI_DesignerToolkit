# Parse BDT.Localization.cs
$locFile = Resolve-Path (Join-Path $PSScriptRoot "..\src\BDT.Localization.cs")
$translationsDir = Resolve-Path (Join-Path $PSScriptRoot "..\translations")

$content = [System.IO.File]::ReadAllText($locFile, [System.Text.Encoding]::UTF8)
# Find all matches of Loc.Str("key", "singular", "description")
$regex = [regex]::new('Loc\.Str\(\s*"([^"]+)"\s*,\s*"([^"]+)"', [System.Text.RegularExpressions.RegexOptions]::Singleline)
$matches = $regex.Matches($content)

$csharpKeys = @()
foreach ($m in $matches) {
    $key = $m.Groups[1].Value
    $val = $m.Groups[2].Value
    # Replace escaped quotes if any
    $val = $val.Replace('\"', '"')
    $csharpKeys += [PSCustomObject]@{
        Key = $key
        DefaultValue = $val
    }
}

Write-Host "Found $($csharpKeys.Count) translation keys in C#."

$svTranslations = @{
    "dtk.settings.height_filter.heading" = "H" + [char]0x00D6 + "JDFILTER"
    "dtk.settings.height_filter_max_visible.label" = "Max synlig niv" + [char]0x00E5
    "dtk.settings.height_filter_max_visible.description" = "Begr" + [char]0x00E4 + "nsar h" + [char]0x00F6 + "jdniv" + [char]0x00E5 + "n f" + [char]0x00F6 + "r transporter, strukturer och pelare som ritas i v" + [char]0x00E4 + "rlden."
    "dtk.settings.height_filter_show_hotkey.label" = "Snabbknapp f" + [char]0x00F6 + "r visa niv" + [char]0x00E5
    "dtk.settings.height_filter_hide_hotkey.label" = "Snabbknapp f" + [char]0x00F6 + "r d" + [char]0x00F6 + "lj niv" + [char]0x00E5
    "dtk.settings.global_hotkey.description" = "Konfigurerar tangentbordsgenv" + [char]0x00E4 + "gen som aktiverar eller utl" + [char]0x00F6 + "ser detta verktyg. Detta " + [char]0x00E4 + "r en global inst" + [char]0x00E4 + "llning som g" + [char]0x00E4 + "ller f" + [char]0x00F6 + "r alla sparade spel och sparas direkt i config.json."
}

# Now process each JSON file in the translations directory
$jsonFiles = Get-ChildItem -Path $translationsDir -Filter *.json
foreach ($file in $jsonFiles) {
    Write-Host "Processing $($file.Name)..."
    $jsonContent = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    # Parse existing JSON array of arrays
    $parsed = ConvertFrom-Json $jsonContent
    $existing = @{}
    foreach ($entry in $parsed) {
        $existing[$entry[0]] = $entry[1]
    }

    # Reconstruct the list of key-value pairs in C# order
    $newEntries = @()
    $isSwedish = $file.Name -eq "sv.json"
    
    foreach ($item in $csharpKeys) {
        $k = $item.Key
        $v = $item.DefaultValue
        
        $translatedVal = $null
        if ($existing.ContainsKey($k)) {
            $translatedVal = $existing[$k]
        } elseif ($isSwedish -and $svTranslations.ContainsKey($k)) {
            $translatedVal = $svTranslations[$k]
        } else {
            $translatedVal = $v # Fallback to English
        }
        
        $newEntries += [PSCustomObject]@{
            Key = $k
            Value = $translatedVal
        }
    }

    # Now write the JSON array manually to match the exact formatting
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("[")
    for ($i = 0; $i -lt $newEntries.Count; $i++) {
        $entry = $newEntries[$i]
        # Escape JSON string values
        $escapedKey = $entry.Key.Replace('\', '\\').Replace('"', '\"')
        $escapedVal = $entry.Value.Replace('\', '\\').Replace('"', '\"').Replace("`n", "\n").Replace("`r", "\r")
        
        [void]$sb.AppendLine("  [")
        [void]$sb.AppendLine("    `"$escapedKey`",")
        [void]$sb.AppendLine("    `"$escapedVal`"")
        if ($i -lt $newEntries.Count - 1) {
            [void]$sb.AppendLine("  ],")
        } else {
            [void]$sb.AppendLine("  ]")
        }
    }
    [void]$sb.Append("]")
    
    $newJson = $sb.ToString()
    # Write with UTF-8 encoding (no BOM, matching standard JSON format)
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($file.FullName, $newJson, $utf8NoBom)
}
Write-Host "Sync complete!"
