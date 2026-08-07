$ErrorActionPreference = 'Stop'

$deployDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $deployDirectory 'configure-portproxy.ps1')

& wsl.exe -d Ubuntu-24.04 -u root -- bash -lc 'systemctl start vllm; exec sleep infinity'
exit $LASTEXITCODE
