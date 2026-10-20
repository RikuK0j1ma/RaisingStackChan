# Builds the StackChan firmware with our app (overlay) added.
# upstream/ (git submodule) is never modified: it is copied to .work/ first.
#
#   .\build.ps1                  copy upstream -> .work, add overlay, register the app, idf.py build
#   .\build.ps1 -Task prepare    do everything except idf.py build
#   .\build.ps1 -Task flash -Port COM5
#                                idf.py -p COM5 flash (writes to the device)
#   .\build.ps1 -Clean           delete .work first (re-fetches dependencies)
#
# Requires an ESP-IDF 5.5.x shell (idf.py on PATH), python and git.

param(
    [ValidateSet('build', 'prepare', 'flash')]
    [string]$Task = 'build',
    [string]$Port = '',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

$upstream = Join-Path $PSScriptRoot 'upstream\firmware'
$overlay  = Join-Path $PSScriptRoot 'overlay'
$work     = Join-Path $PSScriptRoot '.work'

if (-not (Test-Path (Join-Path $upstream 'CMakeLists.txt'))) {
    throw 'upstream is empty. Run: git submodule update --init'
}

if ($Clean -and (Test-Path $work)) {
    Remove-Item $work -Recurse -Force
}

function Copy-Tree([string]$from, [string[]]$excludeDirs = @()) {
    $args = @($from, $work, '/E', '/NFL', '/NDL', '/NJH', '/NJS', '/NP')
    if ($excludeDirs.Count -gt 0) { $args += '/XD'; $args += $excludeDirs }
    & robocopy @args | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
}

# Insert $line after the first line starting with $anchor, keeping indent and EOL style. Idempotent.
function Add-LineAfter([string]$path, [string]$anchor, [string]$line) {
    $text = [IO.File]::ReadAllText($path)
    if ($text.Contains($line)) { return }
    $m = [regex]::Match($text, '(?m)^(?<indent>[ \t]*)' + [regex]::Escape($anchor) + '[^\r\n]*')
    if (-not $m.Success) { throw "anchor not found in ${path}: $anchor" }
    $eol = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $text = $text.Insert($m.Index + $m.Length, $eol + $m.Groups['indent'].Value + $line)
    [IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false)))
}

New-Item -ItemType Directory -Force $work | Out-Null

# 1. upstream -> .work (keep fetched dependencies and build output between runs)
Copy-Tree $upstream @('build', 'components', 'xiaozhi-esp32')

# 2. our app
Copy-Tree $overlay

# 3. register the app (the only edits made to upstream sources)
Add-LineAfter (Join-Path $work 'main\apps\apps.h') '#include "app_dance/app_dance.h"' '#include "app_mr_link/app_mr_link.h"'
Add-LineAfter (Join-Path $work 'main\main.cpp') 'GetMooncake().installApp(std::make_unique<AppDance>());' 'GetMooncake().installApp(std::make_unique<AppMrLink>());'

# 4. dependencies (cloned into .work by upstream's own script)
# On Windows git converts clones to CRLF, which breaks upstream's patch for xiaozhi-esp32
# (fetch_repos.py then prints "skipped" and still exits 0). Clone with autocrlf off and keep patches LF.
Get-ChildItem (Join-Path $work 'patches') -Filter *.patch | ForEach-Object {
    $t = [IO.File]::ReadAllText($_.FullName)
    [IO.File]::WriteAllText($_.FullName, $t.Replace("`r`n", "`n"), (New-Object Text.UTF8Encoding($false)))
}

Push-Location $work
try {
    if (-not (Test-Path 'xiaozhi-esp32')) {
        $env:GIT_CONFIG_COUNT = '1'; $env:GIT_CONFIG_KEY_0 = 'core.autocrlf'; $env:GIT_CONFIG_VALUE_0 = 'false'
        try { python fetch_repos.py } finally { Remove-Item Env:GIT_CONFIG_COUNT, Env:GIT_CONFIG_KEY_0, Env:GIT_CONFIG_VALUE_0 }
        if ($LASTEXITCODE -ne 0) { throw 'fetch_repos.py failed' }
    }

    # fetch_repos.py does not fail when a patch is rejected, so check it was applied
    $ErrorActionPreference = 'Continue'
    git -C xiaozhi-esp32 apply --reverse --check (Join-Path $work 'patches\xiaozhi-esp32.patch') 2>$null
    $applied = ($LASTEXITCODE -eq 0)
    $ErrorActionPreference = 'Stop'
    if (-not $applied) { throw 'patches/xiaozhi-esp32.patch is not applied. Re-run with -Clean' }

    switch ($Task) {
        'prepare' { Write-Host "Prepared: $work" }
        'build'   { idf.py build;  if ($LASTEXITCODE -ne 0) { throw 'idf.py build failed' } }
        'flash'   {
            if (-not $Port) { throw 'flash needs -Port (e.g. -Port COM5)' }
            idf.py -p $Port flash; if ($LASTEXITCODE -ne 0) { throw 'idf.py flash failed' }
        }
    }
}
finally {
    Pop-Location
}
