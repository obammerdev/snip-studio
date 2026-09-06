param(
    [switch]$Publish,
    [switch]$Test,
    [switch]$HeadlessTests,
    [switch]$Installer,
    [string]$PublishDirectory
)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskLocalDotnet = Join-Path $taskRoot '.tools\dotnet\dotnet.exe'
$taskDotnet = if (Test-Path -LiteralPath $taskLocalDotnet) { $taskLocalDotnet } else { 'dotnet' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.tools\cli'
if (-not $PublishDirectory) { $PublishDirectory = if ($Installer) { 'artifacts\publish' } else { 'app' } }
if (-not [IO.Path]::IsPathRooted($PublishDirectory)) { $PublishDirectory = Join-Path $taskRoot $PublishDirectory }
Push-Location -LiteralPath $taskRoot
try {
    & $taskDotnet restore SnipStudio.csproj --configfile NuGet.Config
    if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }
    & $taskDotnet build SnipStudio.csproj -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if ($Test -or $HeadlessTests) {
        $taskTestData = Join-Path $taskRoot 'artifacts\tests'
        New-Item -ItemType Directory -Force -Path $taskTestData | Out-Null
        $taskArguments = @('--self-test', '--data-dir', ('"' + $taskTestData + '"'))
        if ($HeadlessTests) { $taskArguments += '--headless' }
        $taskProcess = Start-Process -FilePath '.\bin\Release\net10.0-windows\SnipStudio.exe' -ArgumentList $taskArguments -Wait -PassThru -WindowStyle Hidden
        if ($taskProcess.ExitCode -ne 0) { throw (Get-Content -LiteralPath (Join-Path $taskTestData 'self-test-failed.txt') -Raw) }
        Get-Content -LiteralPath (Join-Path $taskTestData 'self-test-results.json')
    }
    if ($Publish -or $Installer) {
        & $taskDotnet publish SnipStudio.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -p:RestoreConfigFile=NuGet.Config -o $PublishDirectory
        if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }
        Copy-Item -LiteralPath 'README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md' -Destination $PublishDirectory -Force
        $taskLicenses = Join-Path $PublishDirectory 'licenses'
        New-Item -ItemType Directory -Force -Path $taskLicenses | Out-Null
        $taskAssets = Get-Content 'obj\project.assets.json' -Raw | ConvertFrom-Json
        foreach ($taskPackage in @('Microsoft.NETCore.App.Runtime.win-x64', 'Microsoft.WindowsDesktop.App.Runtime.win-x64')) {
            $taskDependency = @($taskAssets.project.frameworks.PSObject.Properties.Value.downloadDependencies | Where-Object name -EQ $taskPackage)
            if ($taskDependency.Count -ne 1) { throw "Runtime license package not found: $taskPackage" }
            $taskRuntimeVersion = ($taskDependency[0].version.Trim('[', ']') -split ',')[0].Trim()
            $taskPackageRoot = $null
            foreach ($taskFolder in $taskAssets.packageFolders.PSObject.Properties.Name) {
                $taskCandidate = Join-Path $taskFolder ($taskPackage.ToLowerInvariant() + '/' + $taskRuntimeVersion)
                if (Test-Path -LiteralPath $taskCandidate) { $taskPackageRoot = $taskCandidate; break }
            }
            if (-not $taskPackageRoot) { throw "Runtime package directory not found: $taskPackage" }
            $taskLicenseFiles = @(Get-ChildItem -LiteralPath $taskPackageRoot -File | Where-Object Name -Match '^(LICENSE|THIRD-PARTY-NOTICES)(\..*)?$')
            if ($taskLicenseFiles.Count -eq 0) { throw "Missing runtime license: $taskPackage" }
            foreach ($taskLicense in $taskLicenseFiles) {
                Copy-Item -LiteralPath $taskLicense.FullName -Destination (Join-Path $taskLicenses ($taskPackage + '-' + $taskLicense.Name + '.txt')) -Force
            }
        }
        Write-Output "Published: $PublishDirectory\SnipStudio.exe"
    }
    if ($Installer) {
        $taskCompiler = & '.\scripts\get-inno.ps1'
        Copy-Item -LiteralPath (Join-Path (Split-Path $taskCompiler) 'License.txt') -Destination (Join-Path $taskLicenses 'inno-setup.txt') -Force
        $taskVersion = ([xml](Get-Content 'SnipStudio.csproj')).Project.PropertyGroup.Version
        $taskRelease = Join-Path $taskRoot 'release'
        New-Item -ItemType Directory -Force -Path $taskRelease | Out-Null
        & $taskCompiler '/Qp' "/DAppVersion=$taskVersion" "/DPublishDir=$PublishDirectory" "/DOutputDir=$taskRelease" 'installer\SnipStudio.iss'
        if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
        $taskPortable = Join-Path $taskRelease 'SnipStudio-Portable-win-x64.zip'
        # An explicit file list prevents stale development files entering the portable ZIP.
        $taskPortableFiles = @('SnipStudio.exe', 'README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'licenses') | ForEach-Object { Join-Path $PublishDirectory $_ }
        Compress-Archive -LiteralPath $taskPortableFiles -DestinationPath $taskPortable -Force
        $taskHashes = @('SnipStudio-Setup.exe', 'SnipStudio-Portable-win-x64.zip') | ForEach-Object {
            (Get-FileHash -LiteralPath (Join-Path $taskRelease $_) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_
        }
        [IO.File]::WriteAllLines((Join-Path $taskRelease 'SHA256SUMS.txt'), $taskHashes, [Text.UTF8Encoding]::new($false))
        Write-Output "Installer and portable release ready in $taskRelease"
    }
}
finally { Pop-Location }
