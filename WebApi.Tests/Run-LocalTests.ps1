param(
    [string]$PostgresBin = (Join-Path $env:TEMP 'codex-pg-17.6/pgsql/bin')
)

$ErrorActionPreference = 'Stop'
foreach ($binary in @('initdb.exe', 'postgres.exe', 'pg_ctl.exe', 'pg_isready.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PostgresBin $binary))) {
        throw "PostgreSQL binaries not found. Pass -PostgresBin with a local PostgreSQL bin directory."
    }
}
$probe = [System.Net.Sockets.TcpClient]::new()
try {
    $connection = $probe.ConnectAsync('127.0.0.1', 55439)
    if ($connection.Wait(250) -and $probe.Connected) { throw 'Port 55439 is already in use; no existing server will be modified.' }
} catch [System.AggregateException] {
    # Connection refused means the dedicated test port is free.
} finally { $probe.Dispose() }

$testRoot = Join-Path $env:TEMP ('codex-pm-api-test-' + [guid]::NewGuid().ToString('N'))
$data = Join-Path $testRoot 'data'
New-Item -ItemType Directory -Path $testRoot | Out-Null
$server = $null
$result = 1
try {
    & (Join-Path $PostgresBin 'initdb.exe') -D $data -U codex_test -A trust --encoding=UTF8 --no-locale --no-sync
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize the isolated test cluster.' }
    $server = Start-Process -FilePath (Join-Path $PostgresBin 'postgres.exe') `
        -ArgumentList @('-D', ('"' + $data + '"'), '-p', '55439', '-h', '127.0.0.1') `
        -WindowStyle Hidden -RedirectStandardOutput (Join-Path $testRoot 'stdout.log') `
        -RedirectStandardError (Join-Path $testRoot 'stderr.log') -PassThru
    $ready = $false
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        & (Join-Path $PostgresBin 'pg_isready.exe') -h 127.0.0.1 -p 55439 -U codex_test *> $null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Milliseconds 200
    }
    if (-not $ready) { throw "Test database did not start. Logs: $testRoot" }
    & dotnet run --project (Join-Path $PSScriptRoot 'WebApi.Tests.csproj')
    $result = $LASTEXITCODE
} finally {
    if ($null -ne $server -and -not $server.HasExited) {
        & (Join-Path $PostgresBin 'pg_ctl.exe') -D $data -m fast -w stop
    }
    Write-Host "Isolated cluster and logs: $testRoot"
}
exit $result
