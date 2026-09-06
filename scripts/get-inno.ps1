# A project-local compiler, downloaded only when missing. No PATH or machine changes.
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskCompiler = Join-Path $taskRoot '.tools\inno\ISCC.exe'
if (Test-Path -LiteralPath $taskCompiler) { return $taskCompiler }
$taskDownload = Join-Path $taskRoot '.tools\downloads\innosetup-6.7.3.exe'
New-Item -ItemType Directory -Force -Path (Split-Path $taskDownload) | Out-Null
Invoke-WebRequest -UseBasicParsing 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $taskDownload
if ((Get-FileHash -LiteralPath $taskDownload -Algorithm SHA256).Hash -ne '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732') { throw 'Inno Setup checksum mismatch.' }
$taskSignature = Get-AuthenticodeSignature -LiteralPath $taskDownload
if ($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Subject -notmatch 'Pyrsys B.V.') { throw 'Inno Setup publisher signature could not be verified.' }
$taskProcess = Start-Process -FilePath $taskDownload -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', '/PORTABLE=1', ('/DIR="' + (Split-Path $taskCompiler) + '"')) -PassThru -Wait -WindowStyle Hidden
if ($taskProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $taskCompiler)) { throw 'Inno Setup compiler extraction failed.' }
return $taskCompiler
