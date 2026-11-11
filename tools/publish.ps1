$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
 dotnet restore catTransform.sln
 if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
 dotnet test tests/CatTransform.Tests --no-restore --logger 'trx;LogFileName=release-tests.trx' --results-directory artifacts/test-results
 if ($LASTEXITCODE -ne 0) { throw 'Tests failed; release aborted' }
 dotnet publish src/CatTransform.App -c Release -r win-x64 --self-contained true --no-restore -o artifacts/release
 if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
 python tools/prepare_epub_runtime.py
 if ($LASTEXITCODE -ne 0) { throw 'EPUB runtime preparation failed' }
 Copy-Item README.md,THIRD_PARTY_NOTICES.md,TECHNICAL_VALIDATION.md,DEVELOPMENT_PROGRESS.md -Destination artifacts/release
 Copy-Item docs -Destination artifacts/release -Recurse -Force
 $compiler = Join-Path $projectRoot 'tools/InnoSetup/ISCC.exe'
 if (Test-Path -LiteralPath $compiler) {
  & $compiler installer/catTransform.iss
  if ($LASTEXITCODE -ne 0) { throw 'Installer compile failed' }
 } else { Write-Warning 'ISCC.exe missing. Publish folder built; installer not built.' }
} finally { Pop-Location }
