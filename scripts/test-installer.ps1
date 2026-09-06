param([string]$PublishDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
if (-not $PublishDirectory) { $PublishDirectory = Join-Path $taskRoot 'artifacts\publish' }
$taskTestRoot = Join-Path $taskRoot 'artifacts\installer-tests'
$taskInstallRoot = [IO.Path]::GetFullPath((Join-Path $taskTestRoot 'Installed app with spaces'))
if (-not $taskInstallRoot.StartsWith([IO.Path]::GetFullPath($taskTestRoot) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test installation path.' }
$taskProduct = 'Snip Studio Installer Test'
$taskRunName = 'SnipStudio.InstallerTest'
$taskRunKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$taskUninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\SnipStudio.InstallerTest_is1'
$taskDesktop = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) ($taskProduct + '.lnk')
$taskStartMenu = Join-Path ([Environment]::GetFolderPath('Programs')) ($taskProduct + '.lnk')
$taskSetup = Join-Path $taskTestRoot 'SnipStudio-InstallerTest.exe'
$taskExe = Join-Path $taskInstallRoot 'SnipStudio.exe'
$taskUninstaller = Join-Path $taskInstallRoot 'unins000.exe'
$taskResults = [Collections.Generic.List[string]]::new()
$taskStep = 0
function Check([bool]$Condition, [string]$Description) {
    if (-not $Condition) { throw "FAIL: $Description" }
    $taskResults.Add($Description)
}
function Read-Startup {
    (Get-ItemProperty -LiteralPath $taskRunKey -Name $taskRunName -ErrorAction SilentlyContinue).$taskRunName
}
function Run-Setup([string[]]$ExtraArguments = @(), [switch]$ExpectBlocked) {
    $script:taskStep++
    $taskArguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/DIR="' + $taskInstallRoot + '"'), ('/LOG="' + (Join-Path $taskTestRoot "setup-$script:taskStep.log") + '"')) + $ExtraArguments
    $taskProcess = Start-Process -FilePath $taskSetup -ArgumentList $taskArguments -PassThru -Wait -WindowStyle Hidden
    if ($ExpectBlocked) { Check ($taskProcess.ExitCode -ne 0) 'Running-app mutex blocks installation'; return }
    if ($taskProcess.ExitCode -ne 0) { throw "Setup failed with exit code $($taskProcess.ExitCode). See setup-$script:taskStep.log." }
}
function Run-Uninstall([switch]$ExpectBlocked) {
    if (-not (Test-Path -LiteralPath $taskUninstallKey)) { throw 'Test product registration is missing; refusing uninstall.' }
    $taskRegistered = (Get-ItemProperty -LiteralPath $taskUninstallKey).InstallLocation.TrimEnd('\')
    if ($taskRegistered -ne $taskInstallRoot) { throw 'Unexpected test product location; refusing uninstall.' }
    $taskProcess = Start-Process -FilePath $taskUninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -PassThru -Wait -WindowStyle Hidden
    if ($ExpectBlocked) { Check ($taskProcess.ExitCode -ne 0) 'Running-app mutex blocks uninstall'; return }
    if ($taskProcess.ExitCode -ne 0) { throw "Uninstall failed with exit code $($taskProcess.ExitCode)." }
}

New-Item -ItemType Directory -Force -Path $taskTestRoot | Out-Null
if (Test-Path -LiteralPath $taskUninstallKey) { Run-Uninstall }
if ((Read-Startup) -or (Test-Path -LiteralPath $taskDesktop) -or (Test-Path -LiteralPath $taskStartMenu)) { throw 'Previous test resources remain; inspect them before rerunning.' }
$taskCompiler = & (Join-Path $PSScriptRoot 'get-inno.ps1')
$taskVersion = ([xml](Get-Content (Join-Path $taskRoot 'SnipStudio.csproj'))).Project.PropertyGroup.Version
& $taskCompiler '/Qp' '/DInstallerTest=1' "/DAppVersion=$taskVersion" "/DPublishDir=$PublishDirectory" "/DOutputDir=$taskTestRoot" (Join-Path $taskRoot 'installer\SnipStudio.iss')
if ($LASTEXITCODE -ne 0) { throw 'Test installer compilation failed.' }

try {
    $taskMutex = [Threading.Mutex]::new($false, 'Local\SnipStudio.InstallerTest')
    try { Run-Setup -ExpectBlocked } finally { $taskMutex.Dispose() }
    Run-Setup -ExtraArguments @('/TASKS=""')
    Check (Test-Path -LiteralPath $taskExe) 'Fresh install creates the standalone application'
    Check ((Get-FileHash -LiteralPath $taskExe).Hash -eq (Get-FileHash -LiteralPath (Join-Path $PublishDirectory 'SnipStudio.exe')).Hash) 'Installed application exactly matches the published payload'
    Check (Test-Path -LiteralPath $taskUninstallKey) 'Installation appears in Windows Installed apps'
    Check (Test-Path -LiteralPath $taskStartMenu) 'Start menu shortcut is created'
    Check (-not (Test-Path -LiteralPath $taskDesktop)) 'Desktop shortcut is optional'
    Check (-not (Read-Startup)) 'Startup is off when not selected'
    Check (Test-Path -LiteralPath (Join-Path $taskInstallRoot 'licenses\inno-setup.txt')) 'Third-party license notices are installed'

    $taskData = Join-Path $taskTestRoot 'Preserved user data'
    $taskProcess = Start-Process -FilePath $taskExe -ArgumentList @('--self-test', '--headless', '--data-dir', ('"' + $taskData + '"')) -Wait -PassThru -WindowStyle Hidden
    Check ($taskProcess.ExitCode -eq 0) 'Installed executable passes the headless app tests using its bundled runtime'
    $taskSettingsHash = (Get-FileHash -LiteralPath (Join-Path $taskData 'settings.json')).Hash
    $taskSampleHash = (Get-FileHash -LiteralPath (Join-Path $taskData 'sample.png')).Hash

    Run-Setup -ExtraArguments @('/TASKS="startup,desktopicon"')
    Check ((Read-Startup) -eq ('"' + $taskExe + '" --background')) 'Startup opt-in quotes paths with spaces and launches quietly'
    Check (Test-Path -LiteralPath $taskDesktop) 'Desktop opt-in creates its shortcut'
    Run-Setup
    Check ((Read-Startup) -eq ('"' + $taskExe + '" --background')) 'Upgrade preserves enabled startup'
    Check (Test-Path -LiteralPath $taskDesktop) 'Upgrade preserves the desktop shortcut'
    Remove-ItemProperty -LiteralPath $taskRunKey -Name $taskRunName
    Run-Setup
    Check (-not (Read-Startup)) 'Upgrade respects startup disabled later in the app'
    Run-Setup -ExtraArguments @('/TASKS=""')
    Check (-not (Test-Path -LiteralPath $taskDesktop)) 'Opting out removes an existing desktop shortcut'
    Run-Setup -ExtraArguments @('/TASKS="startup,desktopicon"')

    $taskMutex = [Threading.Mutex]::new($false, 'Local\SnipStudio.InstallerTest')
    try { Run-Uninstall -ExpectBlocked } finally { $taskMutex.Dispose() }
    Run-Uninstall
    Check (-not (Test-Path -LiteralPath $taskExe)) 'Uninstall removes application files'
    Check (-not (Test-Path -LiteralPath $taskUninstallKey)) 'Uninstall removes Installed apps registration'
    Check (-not (Read-Startup)) 'Uninstall removes startup for its own installation'
    Check (-not (Test-Path -LiteralPath $taskDesktop) -and -not (Test-Path -LiteralPath $taskStartMenu)) 'Uninstall removes both shortcuts'
    Check ((Get-FileHash -LiteralPath (Join-Path $taskData 'settings.json')).Hash -eq $taskSettingsHash) 'Uninstall preserves saved settings outside the installation directory'
    Check ((Get-FileHash -LiteralPath (Join-Path $taskData 'sample.png')).Hash -eq $taskSampleHash) 'Uninstall preserves saved images outside the installation directory'

    Run-Setup -ExtraArguments @('/TASKS="startup"')
    $taskOtherCopy = '"C:\Another folder\SnipStudio.exe" --background'
    Set-ItemProperty -LiteralPath $taskRunKey -Name $taskRunName -Value $taskOtherCopy
    Run-Uninstall
    Check ((Read-Startup) -eq $taskOtherCopy) 'Uninstall keeps a startup entry repointed to another copy'
}
finally {
    if (Test-Path -LiteralPath $taskUninstallKey) { Run-Uninstall }
    # This registry value is exclusively the test product's; never touch SnipStudio.
    Remove-ItemProperty -LiteralPath $taskRunKey -Name $taskRunName -ErrorAction SilentlyContinue
}
$taskReport = [ordered]@{ Passed = $taskResults.Count; Tests = $taskResults }
$taskReport | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskTestRoot 'results.json') -Encoding UTF8
$taskReport | ConvertTo-Json -Depth 4
