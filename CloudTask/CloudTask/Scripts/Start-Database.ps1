<#
.SYNOPSIS
Starts the local PostgreSQL Windows service and waits until it accepts connections. Nothing else.

.DESCRIPTION
The tables public.users and public.books already exist in the cloud_computing_task database;
BankService and BookstoreService only read them, so this script just has to make sure Postgres is up.
Idempotent. Called at the top of Deploy-FabricApplication.ps1 (F5 / Publish in Visual Studio),
or run it on its own:

    .\Start-Database.ps1
#>
[CmdletBinding()]
Param
(
    [string] $DbHost     = 'localhost',
    [int]    $Port       = 5432,
    [int]    $TimeoutSec = 60
)

$ErrorActionPreference = 'Stop'

function Find-PostgresBin
{
    $root = 'C:\Program Files\PostgreSQL'
    $newest = Get-ChildItem $root -Directory -ErrorAction SilentlyContinue | Sort-Object { [int]($_.Name -replace '\D', '0') } -Descending | Select-Object -First 1
    if (-not $newest) { throw "PostgreSQL is not installed under '$root'." }
    return Join-Path $newest.FullName 'bin'
}

function Start-PostgresService
{
    $service = Get-Service -Name 'postgresql*' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $service) { throw 'No PostgreSQL Windows service found (expected a service named postgresql-*).' }

    if ($service.Status -ne 'Running')
    {
        Write-Host "Starting service '$($service.Name)'..."
        try { Start-Service -Name $service.Name }
        catch { throw "Could not start '$($service.Name)'. Run this script from an elevated (Administrator) prompt. $_" }
    }
    else { Write-Host "Service '$($service.Name)' already running." }
}

function Wait-PostgresReady([string] $bin)
{
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline)
    {
        & "$bin\pg_isready.exe" -h $DbHost -p $Port | Out-Null
        if ($LASTEXITCODE -eq 0) { Write-Host 'PostgreSQL is accepting connections.'; return }
        Start-Sleep -Seconds 1
    }
    throw "PostgreSQL did not become ready within $TimeoutSec seconds."
}

Start-PostgresService
Wait-PostgresReady (Find-PostgresBin)
