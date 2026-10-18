# Run this ONLY after installing .NET 10 and GitHub CLI, and signing in with: gh auth login
$ErrorActionPreference = "Stop"
Set-Location (Split-Path -Parent $PSScriptRoot)
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "Missing .NET 10 SDK" }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw "Missing GitHub CLI" }
dotnet restore PADO.CampusHub.sln
if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
dotnet build PADO.CampusHub.sln -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
dotnet test PADO.CampusHub.sln -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "Tests failed. Do not publish." }
if (-not (Test-Path .git)) { git init -b main }
git checkout main
git add .
git commit -m "feat: unify CampusHub API, reporting and HTTP CLI"
if ($LASTEXITCODE -ne 0) { throw "Commit failed" }
# Create public repo once. If it exists, add origin and push manually instead.
gh repo create PADOALEX/PADO-CampusHub --public --source . --remote origin --push
if ($LASTEXITCODE -ne 0) { throw "Publish failed. Inspect gh output." }
Write-Host "Published at https://github.com/PADOALEX/PADO-CampusHub"
