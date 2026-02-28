$version = "v0.1"
$zipName = "Frostbite-$version.zip"
$tempDir = "Frostbite_Package_Temp"

Write-Host "--- Starting Build Process ---" -ForegroundColor Cyan

if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
New-Item -ItemType Directory -Path $tempDir | Out-Null

# 1. GATHER SCRIPTS & ASSETS
$assets = @(
    "install_frostbite.bat", 
    "UNWISE.bat", 
    "zz_install_frostbite.ps1", 
    "zz_uninstall_frostbite.ps1", 
    "LICENSE", 
    "wm.ico"
)
foreach ($file in $assets) {
    if (Test-Path $file) {
        Copy-Item $file -Destination $tempDir
        Write-Host "Copied $file" -ForegroundColor DarkGray
    } else {
        Write-Warning "Could not find $file!"
    }
}

# 2. GATHER XMLs
Get-ChildItem -Filter "*.xml" | Copy-Item -Destination $tempDir

# 3. DYNAMICALLY FIND PUBLISH DIR
$possiblePaths = @(
    "bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\win-x64\",
    "bin\Release\net8.0\publish",
    "bin\Release\net8.0\win-x64\publish",
    "bin\publish"
)

$foundBinaries = $false
foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        Copy-Item -Path "$path\*" -Destination $tempDir -Recurse -Force
        Write-Host "Found and copied binaries from: $path" -ForegroundColor Green
        $foundBinaries = $true
        break
    }
}

if (-not $foundBinaries) {
    Write-Error "COULD NOT FIND PUBLISH FOLDER. Please click 'Publish' in Visual Studio first!"
    Remove-Item $tempDir -Recurse -Force
    return
}

# 4. ZIP IT UP
Write-Host "Creating $zipName..." -ForegroundColor Cyan
Compress-Archive -Path "$tempDir\*" -DestinationPath $zipName -Force
Remove-Item $tempDir -Recurse -Force

Write-Host "Zip created successfully: $zipName" -ForegroundColor Green

# 5. AUTOMATED GITHUB RELEASE
$tag = "v0.1"
$title = "Frostbite v0.1"
$notesFile = "notes.md" # This must exist in your Frostbite folder

if (Test-Path $notesFile) {
    Write-Host "Pushing to GitHub..." -ForegroundColor Cyan
    # -c specifies the commit (optional), -F uses your notes file
    gh release create $tag $zipName --title $title -F $notesFile
    Write-Host "Successfully published $tag to GitHub!" -ForegroundColor Green
} else {
    Write-Warning "notes.md not found. Release created on GitHub without description."
    gh release create $tag $zipName --title $title
}