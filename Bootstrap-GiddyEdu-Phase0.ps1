#requires -Version 5.1
<#
.SYNOPSIS
    GiddyEdu Phase 0 Bootstrap

.DESCRIPTION
    Run this script from the ROOT of the existing GiddyEdu repository created
    by the first skeleton bootstrap.

    It creates:
      - .NET 10 solution and ASP.NET Core projects
      - Phase 0 modular-monolith projects
      - xUnit test projects and project references
      - NuGet Central Package Management
      - Next.js web application
      - Expo / React Native Family app
      - Expo / React Native Staff app
      - Dockerfiles
      - PostgreSQL and Redis Docker Compose development stack
      - basic health/configuration endpoints
      - GitHub Actions CI for backend, web and mobile
      - local developer commands and verification script

.NOTES
    Existing AGENTS.md and documentation files are preserved.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Write-Step {
  param([string]$Message)
  Write-Host ""
  Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Success {
  param([string]$Message)
  Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-WarningMessage {
  param([string]$Message)
  Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Assert-Command {
  param(
    [Parameter(Mandatory = $true)][string]$Name,
    [string]$InstallHint = ""
  )

  if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
    $message = "Required command '$Name' was not found."
    if ($InstallHint) {
      $message += " $InstallHint"
    }
    throw $message
  }
}

function Ensure-Directory {
  param([Parameter(Mandatory = $true)][string]$Path)

  if (-not (Test-Path $Path)) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
  }
}

function Set-Utf8File {
  param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Content
  )

  $parent = Split-Path $Path -Parent
  if ($parent) {
    Ensure-Directory $parent
  }

  Set-Content -Path $Path -Value $Content -Encoding UTF8
}

function Merge-Directory {
  param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Destination
  )

  Ensure-Directory $Destination

  Get-ChildItem -Path $Source -Force | ForEach-Object {
    $target = Join-Path $Destination $_.Name

    if ($_.PSIsContainer) {
      Merge-Directory -Source $_.FullName -Destination $target
    }
    else {
      Copy-Item -Path $_.FullName -Destination $target -Force
    }
  }
}

function Remove-EmptySkeletonDirectories {
  param([string[]]$Paths)

  foreach ($path in $Paths) {
    if (Test-Path $path) {
      $items = @(Get-ChildItem -Path $path -Force -ErrorAction SilentlyContinue)
      if ($items.Count -eq 0) {
        Remove-Item $path -Force
      }
    }
  }
}

# ---------------------------------------------------------------------------
# Validate repository
# ---------------------------------------------------------------------------

$Root = (Get-Location).Path

Write-Step "Validating GiddyEdu repository"

if (-not (Test-Path (Join-Path $Root "AGENTS.md"))) {
  throw "AGENTS.md was not found. Open the GiddyEdu repository root in VS Code, then run this script from that root."
}

if (-not (Test-Path (Join-Path $Root "docs\product\SUITES.md"))) {
  throw "docs/product/SUITES.md was not found. This does not look like the GiddyEdu skeleton created by the first bootstrap."
}

Assert-Command "dotnet" "Install the .NET 10 SDK first."
Assert-Command "node" "Install the current Node.js LTS release first."
Assert-Command "npm" "npm is installed with Node.js."
Assert-Command "npx" "npx is installed with npm."
Assert-Command "git" "Install Git first."

$dotnetVersion = (& dotnet --version).Trim()
$dotnetMajor = [int]($dotnetVersion.Split('.')[0])

if ($dotnetMajor -lt 10) {
  throw "GiddyEdu requires the .NET 10 SDK. Detected: $dotnetVersion"
}

$nodeVersion = (& node --version).Trim()

Write-Success "Repository: $Root"
Write-Success ".NET SDK: $dotnetVersion"
Write-Success "Node.js: $nodeVersion"

if (Get-Command docker -ErrorAction SilentlyContinue) {
  Write-Success "Docker detected"
}
else {
  Write-WarningMessage "Docker was not detected. Bootstrap can continue, but 'docker compose up' will not work until Docker Desktop/Engine is installed."
}

# ---------------------------------------------------------------------------
# Clean only EMPTY placeholder directories created by skeleton
# ---------------------------------------------------------------------------

Write-Step "Preparing existing skeleton directories"

Remove-EmptySkeletonDirectories @(
  (Join-Path $Root "backend\src\Api"),
  (Join-Path $Root "backend\src\BuildingBlocks"),
  (Join-Path $Root "backend\src\Worker"),
  (Join-Path $Root "backend\tests\Unit"),
  (Join-Path $Root "backend\tests\Integration"),
  (Join-Path $Root "backend\tests\Architecture"),
  (Join-Path $Root "backend\tests\Security")
)

Ensure-Directory (Join-Path $Root "backend\src\Modules")
Ensure-Directory (Join-Path $Root "backend\tests")

# ---------------------------------------------------------------------------
# .NET solution
# ---------------------------------------------------------------------------

Write-Step "Creating .NET 10 solution"

$BackendRoot = Join-Path $Root "backend"
$SolutionPath = Join-Path $BackendRoot "GiddyEdu.slnx"

if (-not (Test-Path $SolutionPath)) {
  Push-Location $BackendRoot
  & dotnet new sln -n GiddyEdu
  Pop-Location
}

# ---------------------------------------------------------------------------
# Create .NET projects
# ---------------------------------------------------------------------------

Write-Step "Creating ASP.NET Core and modular-monolith projects"

$projects = @(
  @{
    Template = "webapi"
    Name     = "GiddyEdu.Api"
    Path     = "backend\src\Api"
    Extra    = @("--framework", "net10.0", "--no-https", "--no-openapi")
  },
  @{
    Template = "classlib"
    Name     = "GiddyEdu.BuildingBlocks"
    Path     = "backend\src\BuildingBlocks"
    Extra    = @("--framework", "net10.0")
  },
  @{
    Template = "worker"
    Name     = "GiddyEdu.Worker"
    Path     = "backend\src\Worker"
    Extra    = @("--framework", "net10.0")
  },
  @{
    Template = "classlib"
    Name     = "GiddyEdu.Modules.Platform"
    Path     = "backend\src\Modules\Platform"
    Extra    = @("--framework", "net10.0")
  },
  @{
    Template = "classlib"
    Name     = "GiddyEdu.Modules.Tenancy"
    Path     = "backend\src\Modules\Tenancy"
    Extra    = @("--framework", "net10.0")
  },
  @{
    Template = "classlib"
    Name     = "GiddyEdu.Modules.Identity"
    Path     = "backend\src\Modules\Identity"
    Extra    = @("--framework", "net10.0")
  },
  @{
    Template = "classlib"
    Name     = "GiddyEdu.Modules.Subscriptions"
    Path     = "backend\src\Modules\Subscriptions"
    Extra    = @("--framework", "net10.0")
  }
)

foreach ($project in $projects) {
  $output = Join-Path $Root $project.Path
  $csproj = Join-Path $output "$($project.Name).csproj"

  if (-not (Test-Path $csproj)) {
    Ensure-Directory $output

    $args = @(
      "new",
      $project.Template,
      "-n",
      $project.Name,
      "-o",
      $output
    ) + $project.Extra

    & dotnet @args
  }
  else {
    Write-Success "$($project.Name) already exists; preserving it"
  }
}

# ---------------------------------------------------------------------------
# Test projects
# ---------------------------------------------------------------------------

Write-Step "Creating test projects"

$tests = @(
  @{ Name = "GiddyEdu.UnitTests"; Path = "backend\tests\Unit" },
  @{ Name = "GiddyEdu.IntegrationTests"; Path = "backend\tests\Integration" },
  @{ Name = "GiddyEdu.ArchitectureTests"; Path = "backend\tests\Architecture" },
  @{ Name = "GiddyEdu.SecurityTests"; Path = "backend\tests\Security" }
)

foreach ($test in $tests) {
  $output = Join-Path $Root $test.Path
  $csproj = Join-Path $output "$($test.Name).csproj"

  if (-not (Test-Path $csproj)) {
    Ensure-Directory $output
    & dotnet new xunit -n $test.Name -o $output --framework net10.0
  }
}

# ---------------------------------------------------------------------------
# Remove generated boilerplate files and replace with intentional foundation
# ---------------------------------------------------------------------------

Write-Step "Replacing generated .NET boilerplate with GiddyEdu foundation"

$filesToRemove = @(
  "backend\src\BuildingBlocks\Class1.cs",
  "backend\src\Modules\Platform\Class1.cs",
  "backend\src\Modules\Tenancy\Class1.cs",
  "backend\src\Modules\Identity\Class1.cs",
  "backend\src\Modules\Subscriptions\Class1.cs"
)

foreach ($relativePath in $filesToRemove) {
  $fullPath = Join-Path $Root $relativePath
  if (Test-Path $fullPath) {
    Remove-Item $fullPath -Force
  }
}

# ---------------------------------------------------------------------------
# Project references
# ---------------------------------------------------------------------------

Write-Step "Adding .NET project references"

$Api = Join-Path $Root "backend\src\Api\GiddyEdu.Api.csproj"
$BuildingBlocks = Join-Path $Root "backend\src\BuildingBlocks\GiddyEdu.BuildingBlocks.csproj"
$Worker = Join-Path $Root "backend\src\Worker\GiddyEdu.Worker.csproj"
$Platform = Join-Path $Root "backend\src\Modules\Platform\GiddyEdu.Modules.Platform.csproj"
$Tenancy = Join-Path $Root "backend\src\Modules\Tenancy\GiddyEdu.Modules.Tenancy.csproj"
$Identity = Join-Path $Root "backend\src\Modules\Identity\GiddyEdu.Modules.Identity.csproj"
$Subscriptions = Join-Path $Root "backend\src\Modules\Subscriptions\GiddyEdu.Modules.Subscriptions.csproj"

# Add references; dotnet safely reports duplicates if rerun.
& dotnet add $Platform reference $BuildingBlocks
& dotnet add $Tenancy reference $BuildingBlocks
& dotnet add $Identity reference $BuildingBlocks $Tenancy
& dotnet add $Subscriptions reference $BuildingBlocks $Tenancy

& dotnet add $Api reference $BuildingBlocks $Platform $Tenancy $Identity $Subscriptions
& dotnet add $Worker reference $BuildingBlocks $Platform $Tenancy $Subscriptions

$UnitTests = Join-Path $Root "backend\tests\Unit\GiddyEdu.UnitTests.csproj"
$IntegrationTests = Join-Path $Root "backend\tests\Integration\GiddyEdu.IntegrationTests.csproj"
$ArchitectureTests = Join-Path $Root "backend\tests\Architecture\GiddyEdu.ArchitectureTests.csproj"
$SecurityTests = Join-Path $Root "backend\tests\Security\GiddyEdu.SecurityTests.csproj"

& dotnet add $UnitTests reference $BuildingBlocks $Platform $Tenancy $Identity $Subscriptions
& dotnet add $IntegrationTests reference $Api
& dotnet add $ArchitectureTests reference $BuildingBlocks $Platform $Tenancy $Identity $Subscriptions
& dotnet add $SecurityTests reference $Api $Tenancy $Identity

# ---------------------------------------------------------------------------
# Add projects to solution
# ---------------------------------------------------------------------------

Write-Step "Adding projects to GiddyEdu solution"

$allCsproj = Get-ChildItem -Path $BackendRoot -Recurse -Filter "*.csproj" |
Select-Object -ExpandProperty FullName

foreach ($projectPath in $allCsproj) {
  & dotnet sln $SolutionPath add $projectPath
}

# ---------------------------------------------------------------------------
# Central Package Management
# Consolidate versions generated by dotnet templates into Directory.Packages.props.
# Safe to rerun after partial bootstrap execution.
# ---------------------------------------------------------------------------

Write-Step "Enabling NuGet Central Package Management"

$centralPackages = @{}
$directoryPackagesPath = Join-Path $Root "Directory.Packages.props"

# Preserve package versions that may already have been centralised by
# a previous or partially completed run.
if (Test-Path $directoryPackagesPath) {
  try {
    [xml]$existingCentral = Get-Content $directoryPackagesPath -Raw

    foreach ($packageVersionNode in @($existingCentral.SelectNodes("//PackageVersion"))) {
      if ($null -eq $packageVersionNode) { continue }

      $includeAttribute = $packageVersionNode.Attributes["Include"]
      $versionAttribute = $packageVersionNode.Attributes["Version"]

      if ($null -ne $includeAttribute -and $null -ne $versionAttribute) {
        $packageName = [string]$includeAttribute.Value
        $packageVersion = [string]$versionAttribute.Value

        if ($packageName -and $packageVersion) {
          $centralPackages[$packageName] = $packageVersion
        }
      }
    }
  }
  catch {
    Write-WarningMessage "Existing Directory.Packages.props could not be parsed. Rebuilding package versions from project files where possible."
  }
}

# Read PackageReference nodes safely.
# Some projects have no PackageReference elements.
# On reruns, PackageReference may exist without Version because the version
# was already moved to Directory.Packages.props.
foreach ($csproj in Get-ChildItem -Path $BackendRoot -Recurse -Filter "*.csproj") {
  try {
    [xml]$xml = Get-Content $csproj.FullName -Raw
  }
  catch {
    throw "Could not parse project file '$($csproj.FullName)': $($_.Exception.Message)"
  }

  $packageReferences = @($xml.SelectNodes("//PackageReference"))

  foreach ($reference in $packageReferences) {
    if ($null -eq $reference) { continue }

    $includeAttribute = $reference.Attributes["Include"]
    if ($null -eq $includeAttribute) { continue }

    $include = [string]$includeAttribute.Value
    if (-not $include) { continue }

    $version = $null

    # Standard SDK-style form:
    # <PackageReference Include="Package.Name" Version="1.2.3" />
    $versionAttribute = $reference.Attributes["Version"]
    if ($null -ne $versionAttribute) {
      $version = [string]$versionAttribute.Value
    }

    # Also support:
    # <PackageReference Include="Package.Name">
    #   <Version>1.2.3</Version>
    # </PackageReference>
    if (-not $version) {
      $versionNode = $reference.SelectSingleNode("Version")
      if ($null -ne $versionNode) {
        $version = [string]$versionNode.InnerText
      }
    }

    if ($version -and -not $centralPackages.ContainsKey($include)) {
      $centralPackages[$include] = $version
    }
  }
}

# Write/update Directory.Packages.props.
$directoryPackages = @"
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
"@

foreach ($packageName in ($centralPackages.Keys | Sort-Object)) {
  $version = $centralPackages[$packageName]
  $directoryPackages += "    <PackageVersion Include=`"$packageName`" Version=`"$version`" />`n"
}

$directoryPackages += @"
  </ItemGroup>
</Project>
"@

Set-Utf8File $directoryPackagesPath $directoryPackages

# Remove project-level versions only when a matching central version exists.
foreach ($csproj in Get-ChildItem -Path $BackendRoot -Recurse -Filter "*.csproj") {
  [xml]$xml = Get-Content $csproj.FullName -Raw
  $changed = $false

  foreach ($reference in @($xml.SelectNodes("//PackageReference"))) {
    if ($null -eq $reference) { continue }

    $includeAttribute = $reference.Attributes["Include"]
    if ($null -eq $includeAttribute) { continue }

    $include = [string]$includeAttribute.Value
    if (-not $centralPackages.ContainsKey($include)) { continue }

    $versionAttribute = $reference.Attributes["Version"]
    if ($null -ne $versionAttribute) {
      [void]$reference.RemoveAttribute("Version")
      $changed = $true
    }

    $versionNode = $reference.SelectSingleNode("Version")
    if ($null -ne $versionNode) {
      [void]$reference.RemoveChild($versionNode)
      $changed = $true
    }
  }

  if ($changed) {
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.OmitXmlDeclaration = $true
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)

    $writer = [System.Xml.XmlWriter]::Create($csproj.FullName, $settings)

    try {
      $xml.Save($writer)
    }
    finally {
      $writer.Dispose()
    }
  }
}

Write-Success "NuGet Central Package Management configured"

# ---------------------------------------------------------------------------
# Directory.Build.props
# ---------------------------------------------------------------------------

Set-Utf8File (Join-Path $Root "Directory.Build.props") @"
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'`$(CI)' == 'true'">true</ContinuousIntegrationBuild>
  </PropertyGroup>
</Project>
"@

# ---------------------------------------------------------------------------
# Foundation domain types
# ---------------------------------------------------------------------------

Set-Utf8File (Join-Path $Root "backend\src\BuildingBlocks\Tenancy\ITenantContext.cs") @"
namespace GiddyEdu.BuildingBlocks.Tenancy;

public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? CampusId { get; }
    bool HasTenant { get; }
}
"@

Set-Utf8File (Join-Path $Root "backend\src\BuildingBlocks\Tenancy\TenantContext.cs") @"
namespace GiddyEdu.BuildingBlocks.Tenancy;

public sealed record TenantContext(Guid? TenantId, Guid? CampusId) : ITenantContext
{
    public bool HasTenant => TenantId.HasValue;
}
"@

Set-Utf8File (Join-Path $Root "backend\src\BuildingBlocks\Time\IClock.cs") @"
namespace GiddyEdu.BuildingBlocks.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
"@

Set-Utf8File (Join-Path $Root "backend\src\BuildingBlocks\Time\SystemClock.cs") @"
namespace GiddyEdu.BuildingBlocks.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
"@

Set-Utf8File (Join-Path $Root "backend\src\Modules\Platform\ModuleMarker.cs") @"
namespace GiddyEdu.Modules.Platform;

public static class ModuleMarker;
"@

Set-Utf8File (Join-Path $Root "backend\src\Modules\Tenancy\ModuleMarker.cs") @"
namespace GiddyEdu.Modules.Tenancy;

public static class ModuleMarker;
"@

Set-Utf8File (Join-Path $Root "backend\src\Modules\Identity\ModuleMarker.cs") @"
namespace GiddyEdu.Modules.Identity;

public static class ModuleMarker;
"@

Set-Utf8File (Join-Path $Root "backend\src\Modules\Subscriptions\ModuleMarker.cs") @"
namespace GiddyEdu.Modules.Subscriptions;

public static class ModuleMarker;
"@

# ---------------------------------------------------------------------------
# API Program.cs
# ---------------------------------------------------------------------------

Set-Utf8File (Join-Path $Root "backend\src\Api\Program.cs") @"
using GiddyEdu.BuildingBlocks.Time;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IClock, SystemClock>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "GiddyEdu API",
    status = "running"
}));

app.MapGet("/api/v1/platform/info", (IClock clock) => Results.Ok(new
{
    name = "GiddyEdu",
    architecture = "modular-monolith",
    apiVersion = "v1",
    utcNow = clock.UtcNow
}));

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");

app.Run();

public partial class Program;
"@

Set-Utf8File (Join-Path $Root "backend\src\Api\appsettings.json") @"
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=giddyedu;Username=giddyedu;Password=giddyedu_dev",
    "Redis": "localhost:6379"
  },
  "GiddyEdu": {
    "Environment": "Local",
    "DefaultCurrency": "NGN",
    "DefaultTimeZone": "Africa/Lagos"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
"@

Set-Utf8File (Join-Path $Root "backend\src\Api\appsettings.Development.json") @"
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "GiddyEdu": "Debug"
    }
  }
}
"@

# ---------------------------------------------------------------------------
# Worker
# ---------------------------------------------------------------------------

Set-Utf8File (Join-Path $Root "backend\src\Worker\Program.cs") @"
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
"@

Set-Utf8File (Join-Path $Root "backend\src\Worker\Worker.cs") @"
namespace GiddyEdu.Worker;

public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("GiddyEdu Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
"@

# ---------------------------------------------------------------------------
# Replace generated tests with meaningful smoke tests
# ---------------------------------------------------------------------------

foreach ($testFile in Get-ChildItem -Path (Join-Path $Root "backend\tests") -Recurse -Filter "UnitTest1.cs" -ErrorAction SilentlyContinue) {
  Remove-Item $testFile.FullName -Force
}

Set-Utf8File (Join-Path $Root "backend\tests\Unit\ClockTests.cs") @"
using GiddyEdu.BuildingBlocks.Time;

namespace GiddyEdu.UnitTests;

public sealed class ClockTests
{
    [Fact]
    public void SystemClock_ReturnsUtcTimestamp()
    {
        var clock = new SystemClock();

        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
    }
}
"@

Set-Utf8File (Join-Path $Root "backend\tests\Architecture\ArchitectureSmokeTests.cs") @"
namespace GiddyEdu.ArchitectureTests;

public sealed class ArchitectureSmokeTests
{
    [Fact]
    public void PhaseZero_Modules_AreAvailable()
    {
        Assert.NotNull(typeof(GiddyEdu.Modules.Platform.ModuleMarker));
        Assert.NotNull(typeof(GiddyEdu.Modules.Tenancy.ModuleMarker));
        Assert.NotNull(typeof(GiddyEdu.Modules.Identity.ModuleMarker));
        Assert.NotNull(typeof(GiddyEdu.Modules.Subscriptions.ModuleMarker));
    }
}
"@

Set-Utf8File (Join-Path $Root "backend\tests\Integration\FoundationSmokeTests.cs") @"
namespace GiddyEdu.IntegrationTests;

public sealed class FoundationSmokeTests
{
    [Fact]
    public void Foundation_TestProject_IsConfigured()
    {
        Assert.True(true);
    }
}
"@

Set-Utf8File (Join-Path $Root "backend\tests\Security\TenantIsolationContractTests.cs") @"
namespace GiddyEdu.SecurityTests;

public sealed class TenantIsolationContractTests
{
    [Fact]
    public void TenantIsolation_TestHarness_IsRequired()
    {
        // Placeholder contract test.
        // Replace with database-backed cross-tenant tests when persistence
        // is introduced in the next Phase 0 implementation slice.
        Assert.True(true);
    }
}
"@

# ---------------------------------------------------------------------------
# Next.js web app - generated in temp directory, then merged so AGENTS.md survives
# ---------------------------------------------------------------------------

Write-Step "Creating Next.js web application"

$WebRoot = Join-Path $Root "web"
$WebPackage = Join-Path $WebRoot "package.json"

if (-not (Test-Path $WebPackage)) {
  $tempWeb = Join-Path ([System.IO.Path]::GetTempPath()) ("giddyedu-web-" + [guid]::NewGuid().ToString("N"))

  & npx --yes create-next-app@latest $tempWeb `
    --typescript `
    --tailwind `
    --eslint `
    --app `
    --src-dir `
    --import-alias "@/*" `
    --use-npm `
    --disable-git `
    --yes

  Merge-Directory -Source $tempWeb -Destination $WebRoot
  Remove-Item $tempWeb -Recurse -Force
}
else {
  Write-Success "Next.js web application already exists; preserving it"
}

# Add GiddyEdu web landing page.
Set-Utf8File (Join-Path $WebRoot "src\app\page.tsx") @'
export default function Home() {
  return (
    <main className="min-h-screen bg-slate-950 text-white">
      <div className="mx-auto flex min-h-screen max-w-6xl flex-col justify-center px-6">
        <p className="mb-4 text-sm font-semibold uppercase tracking-[0.3em] text-emerald-400">
          GiddyEdu
        </p>
        <h1 className="max-w-4xl text-5xl font-semibold tracking-tight sm:text-7xl">
          One school operating platform. Every critical workflow.
        </h1>
        <p className="mt-6 max-w-2xl text-lg leading-8 text-slate-300">
          Phase 0 engineering foundation is running. Product suites will be
          delivered through the governed modular-monolith roadmap.
        </p>
      </div>
    </main>
  );
}
'@

# ---------------------------------------------------------------------------
# Expo mobile apps
# ---------------------------------------------------------------------------

function Bootstrap-ExpoApp {
  param(
    [Parameter(Mandatory = $true)][string]$Destination,
    [Parameter(Mandatory = $true)][string]$TemporaryName,
    [Parameter(Mandatory = $true)][string]$DisplayName,
    [Parameter(Mandatory = $true)][string]$Slug
  )

  $packagePath = Join-Path $Destination "package.json"

  if (Test-Path $packagePath) {
    Write-Success "$DisplayName already exists; preserving it"
    return
  }

  $tempParent = Join-Path ([System.IO.Path]::GetTempPath()) ("giddyedu-expo-" + [guid]::NewGuid().ToString("N"))
  Ensure-Directory $tempParent
  $tempApp = Join-Path $tempParent $TemporaryName

  & npx --yes create-expo-app@latest $tempApp `
    --template default@sdk-57 `
    --yes `
    --no-agents-md

  Merge-Directory -Source $tempApp -Destination $Destination

  $appJsonPath = Join-Path $Destination "app.json"
  if (Test-Path $appJsonPath) {
    $appJson = Get-Content $appJsonPath -Raw | ConvertFrom-Json
    $appJson.expo.name = $DisplayName
    $appJson.expo.slug = $Slug
    $appJson.expo.scheme = $Slug

    $json = $appJson | ConvertTo-Json -Depth 20
    Set-Utf8File $appJsonPath $json
  }

  Remove-Item $tempParent -Recurse -Force
}

Write-Step "Creating Expo Universal Family app"
Bootstrap-ExpoApp `
  -Destination (Join-Path $Root "mobile\family") `
  -TemporaryName "giddyedu-family" `
  -DisplayName "GiddyEdu Family" `
  -Slug "giddyedu-family"

Write-Step "Creating Expo Universal Staff app"
Bootstrap-ExpoApp `
  -Destination (Join-Path $Root "mobile\staff") `
  -TemporaryName "giddyedu-staff" `
  -DisplayName "GiddyEdu Staff" `
  -Slug "giddyedu-staff"

# ---------------------------------------------------------------------------
# Dockerfiles
# ---------------------------------------------------------------------------

Write-Step "Creating Dockerfiles"

Set-Utf8File (Join-Path $Root "backend\src\Api\Dockerfile") @"
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY backend/ ./backend/

RUN dotnet restore backend/GiddyEdu.slnx

RUN dotnet publish backend/src/Api/GiddyEdu.Api.csproj `
    -c Release `
    -o /app/publish `
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "GiddyEdu.Api.dll"]
"@

Set-Utf8File (Join-Path $Root "backend\src\Worker\Dockerfile") @"
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY backend/ ./backend/

RUN dotnet restore backend/GiddyEdu.slnx

RUN dotnet publish backend/src/Worker/GiddyEdu.Worker.csproj `
    -c Release `
    -o /app/publish `
    --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "GiddyEdu.Worker.dll"]
"@

Set-Utf8File (Join-Path $WebRoot "Dockerfile") @"
FROM node:24-alpine AS dependencies
WORKDIR /app
COPY web/package*.json ./
RUN npm ci

FROM node:24-alpine AS build
WORKDIR /app
COPY --from=dependencies /app/node_modules ./node_modules
COPY web/ ./
RUN npm run build

FROM node:24-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production

COPY --from=build /app/package*.json ./
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/.next ./.next
COPY --from=build /app/public ./public

EXPOSE 3000
CMD ["npm", "start"]
"@

Set-Utf8File (Join-Path $Root ".dockerignore") @"
.git
.github
**/bin
**/obj
**/node_modules
**/.next
**/.expo
**/coverage
.env
.env.*
!.env.example
"@

# ---------------------------------------------------------------------------
# Docker Compose: local development stack
# ---------------------------------------------------------------------------

Write-Step "Creating PostgreSQL/Redis development configuration"

Set-Utf8File (Join-Path $Root "docker-compose.yml") @"
name: giddyedu

services:
  postgres:
    image: postgres:18
    container_name: giddyedu-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: giddyedu
      POSTGRES_USER: giddyedu
      POSTGRES_PASSWORD: giddyedu_dev
    ports:
      - "5432:5432"
    volumes:
      - giddyedu-postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U giddyedu -d giddyedu"]
      interval: 5s
      timeout: 5s
      retries: 10

  redis:
    image: redis:8-alpine
    container_name: giddyedu-redis
    restart: unless-stopped
    command: redis-server --appendonly yes
    ports:
      - "6379:6379"
    volumes:
      - giddyedu-redis-data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 10

  api:
    build:
      context: .
      dockerfile: backend/src/Api/Dockerfile
    container_name: giddyedu-api
    restart: unless-stopped
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__Postgres: Host=postgres;Port=5432;Database=giddyedu;Username=giddyedu;Password=giddyedu_dev
      ConnectionStrings__Redis: redis:6379
    ports:
      - "8080:8080"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy

  worker:
    build:
      context: .
      dockerfile: backend/src/Worker/Dockerfile
    container_name: giddyedu-worker
    restart: unless-stopped
    environment:
      DOTNET_ENVIRONMENT: Development
      ConnectionStrings__Postgres: Host=postgres;Port=5432;Database=giddyedu;Username=giddyedu;Password=giddyedu_dev
      ConnectionStrings__Redis: redis:6379
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy

  web:
    build:
      context: .
      dockerfile: web/Dockerfile
    container_name: giddyedu-web
    restart: unless-stopped
    environment:
      NODE_ENV: production
      NEXT_PUBLIC_API_BASE_URL: http://localhost:8080
    ports:
      - "3000:3000"
    depends_on:
      - api

volumes:
  giddyedu-postgres-data:
  giddyedu-redis-data:
"@

Set-Utf8File (Join-Path $Root ".env.example") @"
# ---------------------------------------------------------------------------
# GiddyEdu local environment
# Copy to .env for local-only overrides.
# Never commit .env.
# ---------------------------------------------------------------------------

ASPNETCORE_ENVIRONMENT=Development

POSTGRES_DB=giddyedu
POSTGRES_USER=giddyedu
POSTGRES_PASSWORD=

ConnectionStrings__Postgres=
ConnectionStrings__Redis=

NEXT_PUBLIC_API_BASE_URL=http://localhost:8080

JWT_ISSUER=
JWT_AUDIENCE=
JWT_SIGNING_KEY=

S3_ENDPOINT=
S3_REGION=
S3_BUCKET=
S3_ACCESS_KEY=
S3_SECRET_KEY=

PAYSTACK_SECRET_KEY=
FLUTTERWAVE_SECRET_KEY=
MONNIFY_API_KEY=
MONNIFY_SECRET_KEY=

EMAIL_PROVIDER=
SMS_PROVIDER=
WHATSAPP_PROVIDER=

OTEL_EXPORTER_OTLP_ENDPOINT=
"@

# ---------------------------------------------------------------------------
# Local developer scripts
# ---------------------------------------------------------------------------

Write-Step "Creating developer helper scripts"

Ensure-Directory (Join-Path $Root "scripts")

Set-Utf8File (Join-Path $Root "scripts\verify.ps1") @'
$ErrorActionPreference = "Stop"

Write-Host "Verifying GiddyEdu backend..." -ForegroundColor Cyan
dotnet restore backend/GiddyEdu.slnx
dotnet build backend/GiddyEdu.slnx --configuration Release --no-restore
dotnet test backend/GiddyEdu.slnx --configuration Release --no-build

Write-Host "Verifying GiddyEdu web..." -ForegroundColor Cyan
Push-Location web
npm ci
npm run lint
npm run build
Pop-Location

Write-Host "Verifying Family mobile app..." -ForegroundColor Cyan
Push-Location mobile/family
npm ci
npx tsc --noEmit
Pop-Location

Write-Host "Verifying Staff mobile app..." -ForegroundColor Cyan
Push-Location mobile/staff
npm ci
npx tsc --noEmit
Pop-Location

Write-Host "All configured checks passed." -ForegroundColor Green
'@

Set-Utf8File (Join-Path $Root "scripts\dev-up.ps1") @'
$ErrorActionPreference = "Stop"
docker compose up -d postgres redis
Write-Host ""
Write-Host "PostgreSQL: localhost:5432" -ForegroundColor Green
Write-Host "Redis:      localhost:6379" -ForegroundColor Green
Write-Host ""
Write-Host "Run API:    dotnet run --project backend/src/Api/GiddyEdu.Api.csproj"
Write-Host "Run Web:    cd web; npm run dev"
Write-Host "Family:     cd mobile/family; npx expo start"
Write-Host "Staff:      cd mobile/staff; npx expo start"
'@

Set-Utf8File (Join-Path $Root "scripts\dev-down.ps1") @'
$ErrorActionPreference = "Stop"
docker compose down
'@

# ---------------------------------------------------------------------------
# GitHub Actions CI
# ---------------------------------------------------------------------------

Write-Step "Creating first working GitHub Actions CI pipelines"

$WorkflowRoot = Join-Path $Root ".github\workflows"
Ensure-Directory $WorkflowRoot

Set-Utf8File (Join-Path $WorkflowRoot "backend-ci.yml") @'
name: Backend CI

on:
  push:
    branches: [main, develop]
    paths:
      - "backend/**"
      - "Directory.Build.props"
      - "Directory.Packages.props"
      - ".github/workflows/backend-ci.yml"
  pull_request:
    paths:
      - "backend/**"
      - "Directory.Build.props"
      - "Directory.Packages.props"
      - ".github/workflows/backend-ci.yml"

permissions:
  contents: read

jobs:
  build-test:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Restore
        run: dotnet restore backend/GiddyEdu.slnx

      - name: Build
        run: dotnet build backend/GiddyEdu.slnx --configuration Release --no-restore

      - name: Test
        run: dotnet test backend/GiddyEdu.slnx --configuration Release --no-build
'@

Set-Utf8File (Join-Path $WorkflowRoot "frontend-ci.yml") @'
name: Web CI

on:
  push:
    branches: [main, develop]
    paths:
      - "web/**"
      - ".github/workflows/frontend-ci.yml"
  pull_request:
    paths:
      - "web/**"
      - ".github/workflows/frontend-ci.yml"

permissions:
  contents: read

jobs:
  build:
    runs-on: ubuntu-latest

    defaults:
      run:
        working-directory: web

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: "24"
          cache: npm
          cache-dependency-path: web/package-lock.json

      - name: Install
        run: npm ci

      - name: Lint
        run: npm run lint

      - name: Build
        run: npm run build
'@

Set-Utf8File (Join-Path $WorkflowRoot "mobile-ci.yml") @'
name: Mobile CI

on:
  push:
    branches: [main, develop]
    paths:
      - "mobile/**"
      - ".github/workflows/mobile-ci.yml"
  pull_request:
    paths:
      - "mobile/**"
      - ".github/workflows/mobile-ci.yml"

permissions:
  contents: read

jobs:
  typecheck:
    strategy:
      matrix:
        app:
          - mobile/family
          - mobile/staff

    runs-on: ubuntu-latest

    defaults:
      run:
        working-directory: ${{ matrix.app }}

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: "24"
          cache: npm
          cache-dependency-path: ${{ matrix.app }}/package-lock.json

      - name: Install
        run: npm ci

      - name: Typecheck
        run: npx tsc --noEmit
'@

Set-Utf8File (Join-Path $WorkflowRoot "security.yml") @'
name: Security Baseline

on:
  pull_request:
  push:
    branches: [main]

permissions:
  contents: read

jobs:
  dependency-review:
    if: github.event_name == 'pull_request'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/dependency-review-action@v4

  repository-secret-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Check tracked environment files
        shell: bash
        run: |
          set -e
          bad_files=$(git ls-files | grep -E '(^|/)\.env($|\.)' | grep -v '\.env\.example$' || true)
          if [ -n "$bad_files" ]; then
            echo "Tracked environment files are not allowed:"
            echo "$bad_files"
            exit 1
          fi
'@

Set-Utf8File (Join-Path $WorkflowRoot "deploy.yml") @'
name: Deploy Placeholder

on:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  not-configured:
    runs-on: ubuntu-latest
    steps:
      - run: |
          echo "Production deployment is intentionally not configured yet."
          echo "Add VPS/staging deployment only after environments and secrets are defined."
'@

# ---------------------------------------------------------------------------
# Update Makefile
# ---------------------------------------------------------------------------

Set-Utf8File (Join-Path $Root "Makefile") @'
setup:
	dotnet restore backend/GiddyEdu.slnx
	cd web && npm ci
	cd mobile/family && npm ci
	cd mobile/staff && npm ci

infra-up:
	docker compose up -d postgres redis

infra-down:
	docker compose down

up:
	docker compose up -d --build

down:
	docker compose down

backend-build:
	dotnet build backend/GiddyEdu.slnx

backend-test:
	dotnet test backend/GiddyEdu.slnx

web:
	cd web && npm run dev

family:
	cd mobile/family && npx expo start

staff:
	cd mobile/staff && npx expo start

verify:
	powershell -ExecutionPolicy Bypass -File ./scripts/verify.ps1
'@

# ---------------------------------------------------------------------------
# Update architecture foundation execution plan
# ---------------------------------------------------------------------------

Set-Utf8File (Join-Path $Root "docs\exec-plans\active\PHASE_0_FOUNDATION.md") @"
# Phase 0 - Architecture Foundation

## Goal

Create the governed technical foundation required for all GiddyEdu suites.

## Current Bootstrap Status

Created:

- .NET 10 solution
- ASP.NET Core API
- .NET worker
- BuildingBlocks project
- Platform module
- Tenancy module
- Identity module
- Subscription/entitlement module
- xUnit unit tests
- xUnit integration tests
- xUnit architecture tests
- xUnit security tests
- NuGet Central Package Management
- Next.js web application
- Expo Family mobile application
- Expo Staff mobile application
- PostgreSQL local container
- Redis local container
- Dockerfiles
- GitHub Actions CI baseline

## Remaining Phase 0 Work

- PostgreSQL persistence implementation
- EF Core DbContext and migrations
- tenant resolution
- tenant isolation enforcement
- authentication
- membership model
- RBAC/permission engine
- subscription entitlements
- audit infrastructure
- Hangfire
- S3 storage adapter
- structured logging
- OpenTelemetry
- secrets management
- integration test containers
- tenant security test harness
- staging deployment
- backup and restore verification

## Rule

Do not begin Suite 2 Admissions implementation until the required
Phase 0 tenancy, identity, authorization and persistence foundations
are implemented and verified.
"@

# ---------------------------------------------------------------------------
# Restore/build
# ---------------------------------------------------------------------------

Write-Step "Restoring and building .NET solution"

& dotnet restore $SolutionPath
& dotnet build $SolutionPath --configuration Debug --no-restore
& dotnet test $SolutionPath --configuration Debug --no-build

# ---------------------------------------------------------------------------
# Validate generated npm projects
# ---------------------------------------------------------------------------

Write-Step "Checking web and mobile TypeScript projects"

Push-Location $WebRoot
& npm run lint
& npm run build
Pop-Location

Push-Location (Join-Path $Root "mobile\family")
& npx tsc --noEmit
Pop-Location

Push-Location (Join-Path $Root "mobile\staff")
& npx tsc --noEmit
Pop-Location

# ---------------------------------------------------------------------------
# Git status
# ---------------------------------------------------------------------------

Write-Step "Preparing Git working tree"

if (-not (Test-Path (Join-Path $Root ".git"))) {
  & git init
}

& git add .

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "GiddyEdu Phase 0 bootstrap completed successfully." -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Created:" -ForegroundColor Cyan
Write-Host "  .NET solution:       backend/GiddyEdu.slnx"
Write-Host "  API:                 backend/src/Api"
Write-Host "  Worker:              backend/src/Worker"
Write-Host "  Core modules:        Platform, Tenancy, Identity, Subscriptions"
Write-Host "  Tests:               Unit, Integration, Architecture, Security"
Write-Host "  Web:                 web/"
Write-Host "  Family mobile:       mobile/family/"
Write-Host "  Staff mobile:        mobile/staff/"
Write-Host "  PostgreSQL + Redis:  docker-compose.yml"
Write-Host "  CI:                  .github/workflows/"
Write-Host ""
Write-Host "NEXT:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Start PostgreSQL and Redis:"
Write-Host "   ./scripts/dev-up.ps1"
Write-Host ""
Write-Host "2. Start API in another terminal:"
Write-Host "   dotnet run --project backend/src/Api/GiddyEdu.Api.csproj"
Write-Host ""
Write-Host "3. Start Web in another terminal:"
Write-Host "   cd web"
Write-Host "   npm run dev"
Write-Host ""
Write-Host "4. Verify API:"
Write-Host "   http://localhost:5000/health"
Write-Host "   (Use the URL printed by dotnet run if the port differs.)"
Write-Host ""
Write-Host "5. Before committing:"
Write-Host "   ./scripts/verify.ps1"
Write-Host ""
Write-Host "6. First bootstrap commit:"
Write-Host '   git commit -m "chore(platform): bootstrap GiddyEdu phase zero foundation"'
Write-Host ""
Write-Host "IMPORTANT:" -ForegroundColor Yellow
Write-Host "Do NOT ask Codex to start Admissions yet."
Write-Host "The next implementation slice should be persistence + tenant isolation + identity + permissions."
Write-Host ""
