#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Interactive Firebase (Blaze-plan) cost estimator for DesktopHub.

.DESCRIPTION
  Given a projected device fleet size + usage pattern, this script estimates
  monthly cost for the Cloud Functions + RTDB + Auth footprint that DesktopHub
  uses. It prints a line-by-line breakdown so you can see which dimension
  (invocations, egress, GB-seconds, storage) is driving cost, and how much
  headroom you have against each free-tier quota.

  All prices and quotas are taken from Firebase Blaze pricing as of 2026
  (us-central1 region). If Google updates the schedule, edit the constants
  at the top of the script.

.PARAMETER Devices
  How many machines have DesktopHub installed.

.PARAMETER LaunchesPerDevicePerDay
  How many times the average device launches the app in a day (cold-start
  token mint). Most users are once per workday.

.PARAMETER AdminPushesPerMonth
  How many times admins push forced updates in a month.

.EXAMPLE
  # Interactive (prompts for every value)
  ./scripts/firebase-cost-estimator.ps1

.EXAMPLE
  # Non-interactive with specific numbers
  ./scripts/firebase-cost-estimator.ps1 -Devices 50 -LaunchesPerDevicePerDay 2 -AdminPushesPerMonth 10

.EXAMPLE
  # Run the built-in scaling scenarios
  ./scripts/firebase-cost-estimator.ps1 -Scenarios
#>
param(
    [int]$Devices = -1,
    [double]$LaunchesPerDevicePerDay = -1,
    [int]$AdminPushesPerMonth = -1,
    [switch]$Scenarios
)

# ============================================================
#  PRICING CONSTANTS (Blaze plan, us-central1, 2026 values)
# ============================================================
$FreeInvocationsPerMonth  = 2000000
$CostPerMillionInvocations = 0.40

$FreeGBsecondsPerMonth    = 400000
$CostPerGBsecond          = 0.0000025   # GCF 2nd gen: $0.0000025/GB-s

$FreeCPUsecondsPerMonth   = 200000
$CostPerCPUsecond         = 0.00001

$FreeEgressGBPerMonth     = 5
$CostPerGBEgress          = 0.12

$FreeCloudBuildMinPerDay  = 120
$CostPerBuildMinute       = 0.003

$FreeArtifactStorageMB    = 500
$CostPerGBArtifactStorage = 0.10

# RTDB pricing (already in use before this change; unchanged)
$FreeRtdbStorageGB        = 1
$CostPerGBRtdbStorage     = 5.00
$FreeRtdbEgressGB         = 10
$CostPerGBRtdbEgress      = 1.00

# Firebase Auth is effectively free at our scale -- 50k MAU free.

# ============================================================
#  DESKTOPHUB USAGE ASSUMPTIONS (per invocation of the function)
# ============================================================
$IssueTokenMemoryGB       = 0.256     # 256 MB default for v2
$IssueTokenDurationSec    = 0.35      # cold start ~1s; warm ~0.1s; average
$IssueTokenCpuSecPerCall  = 0.18      # CPU time is less than wall time

$PushForceUpdateMemoryGB  = 0.256
$PushForceUpdateDurSec    = 0.25
$PushForceUpdateCpuSec    = 0.10

$BytesPerIssueTokenResp   = 1400      # custom JWT ~ 1.2KB + envelope
$BytesPerPushForceResp    = 200
$DeploysPerMonth          = 3         # typical iteration; avg 5 min each
$MinutesPerDeploy         = 5

function Format-Money([double]$d) { if ($d -lt 0.01) { '< $0.01' } else { ('$' + $d.ToString('F2')) } }

function Show-Breakdown {
    param(
        [int]$devices,
        [double]$launchesPerDay,
        [int]$adminPushes
    )

    # -------------- INVOCATIONS --------------
    $issueTokenInvocations = [long]($devices * $launchesPerDay * 30)
    $pushForceInvocations  = $adminPushes
    $totalInvocations      = $issueTokenInvocations + $pushForceInvocations

    $billableInvocations   = [Math]::Max(0, $totalInvocations - $FreeInvocationsPerMonth)
    $invocationCost        = ($billableInvocations / 1000000.0) * $CostPerMillionInvocations

    # -------------- GB-SECONDS --------------
    $gbSec =
        ($issueTokenInvocations * $IssueTokenMemoryGB * $IssueTokenDurationSec) +
        ($pushForceInvocations  * $PushForceUpdateMemoryGB * $PushForceUpdateDurSec)
    $billableGbSec   = [Math]::Max(0, $gbSec - $FreeGBsecondsPerMonth)
    $gbSecCost       = $billableGbSec * $CostPerGBsecond

    # -------------- CPU-SECONDS --------------
    $cpuSec =
        ($issueTokenInvocations * $IssueTokenCpuSecPerCall) +
        ($pushForceInvocations  * $PushForceUpdateCpuSec)
    $billableCpuSec = [Math]::Max(0, $cpuSec - $FreeCPUsecondsPerMonth)
    $cpuSecCost     = $billableCpuSec * $CostPerCPUsecond

    # -------------- EGRESS --------------
    $totalEgressBytes =
        ($issueTokenInvocations * $BytesPerIssueTokenResp) +
        ($pushForceInvocations  * $BytesPerPushForceResp)
    $egressGB = $totalEgressBytes / 1GB
    $billableEgressGB = [Math]::Max(0, $egressGB - $FreeEgressGBPerMonth)
    $egressCost = $billableEgressGB * $CostPerGBEgress

    # -------------- BUILD MINUTES --------------
    $buildMinutes = $DeploysPerMonth * $MinutesPerDeploy
    $dailyBudget  = $FreeCloudBuildMinPerDay
    $buildCost    = 0.0   # In practice always free at our deploy cadence
    if ($buildMinutes / 30.0 -gt $dailyBudget) {
        $billable = $buildMinutes - ($dailyBudget * 30)
        $buildCost = $billable * $CostPerBuildMinute
    }

    # -------------- TOTAL --------------
    $total = $invocationCost + $gbSecCost + $cpuSecCost + $egressCost + $buildCost

    # -------------- PRINT --------------
    Write-Host ""
    Write-Host ("  {0,-10} devices  x  {1,-5} launches/day  =  {2:N0} token mints/month" -f $devices, $launchesPerDay, $issueTokenInvocations) -ForegroundColor White
    Write-Host ("  {0,-10} admin pushes/month" -f $adminPushes) -ForegroundColor White
    Write-Host ""
    Write-Host "  +-------------------------+------------------+---------------------+-------------+" -ForegroundColor DarkGray
    Write-Host "  | Dimension               | Projected usage  | Free-tier quota     | Cost        |" -ForegroundColor DarkGray
    Write-Host "  +-------------------------+------------------+---------------------+-------------+" -ForegroundColor DarkGray

    $rows = @(
        @{ Name = "Invocations";          Used = $totalInvocations;   Unit = "calls";   Free = $FreeInvocationsPerMonth;  FreeUnit = "calls";     Cost = $invocationCost },
        @{ Name = "Memory (GB-seconds)";  Used = [long]$gbSec;        Unit = "GB-s";    Free = $FreeGBsecondsPerMonth;    FreeUnit = "GB-s";      Cost = $gbSecCost },
        @{ Name = "CPU (CPU-seconds)";    Used = [long]$cpuSec;       Unit = "CPU-s";   Free = $FreeCPUsecondsPerMonth;   FreeUnit = "CPU-s";     Cost = $cpuSecCost },
        @{ Name = "Outbound network";     Used = ([Math]::Round($egressGB, 3)); Unit = "GB"; Free = $FreeEgressGBPerMonth; FreeUnit = "GB";     Cost = $egressCost },
        @{ Name = "Cloud Build minutes";  Used = $buildMinutes;       Unit = "min";     Free = "$($FreeCloudBuildMinPerDay)/day"; FreeUnit = ""; Cost = $buildCost }
    )

    foreach ($r in $rows) {
        $usedStr  = if ($r.Used -is [double]) { "{0:N3} {1}" -f $r.Used, $r.Unit } else { "{0:N0} {1}" -f $r.Used, $r.Unit }
        $freeStr  = if ($r.Free -is [string]) { $r.Free } else { "{0:N0} {1}" -f $r.Free, $r.FreeUnit }
        $costStr  = Format-Money $r.Cost
        $color    = if ($r.Cost -gt 0) { 'Yellow' } else { 'Green' }

        # Compute percent of free tier used
        $pct = if ($r.Free -is [double] -or $r.Free -is [int] -or $r.Free -is [long]) {
            if ($r.Free -eq 0) { 0 } else { [Math]::Round(100.0 * [double]$r.Used / [double]$r.Free, 2) }
        } else { $null }
        $freeDisplay = if ($null -ne $pct) { "{0,-15}  ({1,5:N1}%)" -f $freeStr, $pct } else { $freeStr.PadRight(22) }

        Write-Host ("  | {0,-23} | {1,-16} | {2,-19} | " -f $r.Name, $usedStr, $freeDisplay) -NoNewline
        Write-Host ("{0,-11}" -f $costStr) -ForegroundColor $color -NoNewline
        Write-Host " |" -ForegroundColor DarkGray
    }

    Write-Host "  +-------------------------+------------------+---------------------+-------------+" -ForegroundColor DarkGray

    $totalColor = if ($total -gt 0) { 'Yellow' } else { 'Green' }
    Write-Host ""
    Write-Host ("  ESTIMATED MONTHLY COST:  " + (Format-Money $total)) -ForegroundColor $totalColor
    if ($total -eq 0) {
        Write-Host "  (entirely within free tier -- Blaze plan only means 'billing is enabled',"
        Write-Host "   not 'you will be charged')"
    }
    Write-Host ""
}

# ============================================================
#  SCENARIOS MODE
# ============================================================
if ($Scenarios) {
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host "  DesktopHub / Firebase Cost Estimator -- scaling scenarios" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan

    $presets = @(
        @{ Label = "Internal use today (~20 devices, 1 launch/day)"; Devices = 20;     Launches = 1.0; Pushes = 5 }
        @{ Label = "Internal growth (~200 devices)";                 Devices = 200;    Launches = 1.5; Pushes = 15 }
        @{ Label = "External SMB (1,000 devices)";                   Devices = 1000;   Launches = 2.0; Pushes = 20 }
        @{ Label = "Mid-market (10,000 devices)";                    Devices = 10000;  Launches = 2.0; Pushes = 30 }
        @{ Label = "Stress test (100,000 devices)";                  Devices = 100000; Launches = 2.0; Pushes = 50 }
    )

    foreach ($p in $presets) {
        Write-Host ""
        Write-Host "---- $($p.Label) ----" -ForegroundColor Cyan
        Show-Breakdown -devices $p.Devices -launchesPerDay $p.Launches -adminPushes $p.Pushes
    }

    exit 0
}

# ============================================================
#  INTERACTIVE / PARAMETERIZED MODE
# ============================================================
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  DesktopHub / Firebase Cost Estimator" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Answer three questions. Press Enter to accept the default in [brackets]."
Write-Host ""

if ($Devices -lt 0) {
    $reply = Read-Host "How many devices will have DesktopHub installed? [20]"
    $Devices = if ([string]::IsNullOrWhiteSpace($reply)) { 20 } else { [int]$reply }
}

if ($LaunchesPerDevicePerDay -lt 0) {
    $reply = Read-Host "Average app launches per device per day? [1]"
    $LaunchesPerDevicePerDay = if ([string]::IsNullOrWhiteSpace($reply)) { 1 } else { [double]$reply }
}

if ($AdminPushesPerMonth -lt 0) {
    $reply = Read-Host "How many forced-update pushes per month (admin action)? [10]"
    $AdminPushesPerMonth = if ([string]::IsNullOrWhiteSpace($reply)) { 10 } else { [int]$reply }
}

Show-Breakdown -devices $Devices -launchesPerDay $LaunchesPerDevicePerDay -adminPushes $AdminPushesPerMonth

Write-Host "Tip: run with -Scenarios to see built-in growth projections:" -ForegroundColor DarkGray
Write-Host "  ./scripts/firebase-cost-estimator.ps1 -Scenarios" -ForegroundColor DarkGray
Write-Host ""
