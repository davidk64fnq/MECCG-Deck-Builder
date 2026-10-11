$extensions = @("*.cs", "*.md", "*.html", "*.css")
$outputFile = "meccg-context.xml"

# Find all matching files, excluding typical visual studio / git build folders
$files = Get-ChildItem -Recurse -File -Include $extensions | 
    Where-Object { $_.FullName -notmatch '\\(bin|obj|\.vs|\.git)\\' }

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("<project name='MECCG-Deck-Builder'>")

foreach ($file in $files) {
    # Make the path relative to the solution root (cleaner for the AI to read)
    $relativePath = Resolve-Path -Relative -Path $file.FullName
    $content = Get-Content $file.FullName -Raw

    [void]$sb.AppendLine("  <file path='$relativePath'>")
    [void]$sb.AppendLine("<![CDATA[")
    [void]$sb.AppendLine($content)
    [void]$sb.AppendLine("]]>")
    [void]$sb.AppendLine("  </file>")
}

[void]$sb.AppendLine("</project>")

# Write using UTF-8
[System.IO.File]::WriteAllText((Join-Path (Get-Location) $outputFile), $sb.ToString(), [System.Text.Encoding]::UTF8)

Write-Host "Successfully bundled $($files.Count) files into $outputFile" -ForegroundColor Green