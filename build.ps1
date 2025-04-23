# Создаем папки для выходных файлов
$outputDir = "dist"
New-Item -ItemType Directory -Force -Path $outputDir

# Собираем автономную версию
Write-Host "Building standalone version..."
dotnet publish WinTimer/WinTimer.csproj -c Standalone -r win-x64
Copy-Item "WinTimer/bin/Standalone/WinTimer.exe" "$outputDir/WinTimer-Full.exe"

# Собираем зависимую версию
Write-Host "Building dependent version..."
dotnet publish WinTimer/WinTimer.csproj -c Dependent -r win-x64
Copy-Item "WinTimer/bin/Dependent/WinTimer.exe" "$outputDir/WinTimer-Light.exe"

# Выводим информацию о размерах файлов
Write-Host "`nFile sizes:"
Get-Item "$outputDir/WinTimer-Full.exe" | Select-Object Name, @{Name="Size(MB)";Expression={$_.Length/1MB}}
Get-Item "$outputDir/WinTimer-Light.exe" | Select-Object Name, @{Name="Size(MB)";Expression={$_.Length/1MB}} 