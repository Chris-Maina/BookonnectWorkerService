# Bookonnect Worker Service

A .NET 8 background service that synchronizes Bookonnect's relational and vector databases and processes updated user preferences.

## Features

- Initializes relational and vector database services at startup.
- Checks database synchronization immediately, then repeats one hour after each cycle finishes.
- Synchronizes relational book data to the vector database when the databases are not fully synchronized.
- Processes updated user preferences after an initial one-hour delay, then repeats after each one-hour delay.
- Runs both jobs concurrently with scoped dependencies, structured logging, and cancellation-aware delays.
- Logs periodic job failures and retries on the next cycle; initialization failures stop the worker.

## Prerequisites

- .NET 8 SDK.
- The BookonnectCore project, available as a sibling directory.
- Access to the database services and configuration required by BookonnectCore. The worker references the PostgreSQL EF Core provider and ChromaDB client packages.

The project reference expects this layout:

```text
workspace/
|-- BookonnectCore/
|   `-- BookonnectCore.csproj
`-- BookonnectWorkerService/
    |-- BookonnectWorkerService.sln
    `-- BookonnectWorkerService/
        `-- BookonnectWorkerService.csproj
```

## Installation

From the repository root, with BookonnectCore in place:

```sh
dotnet restore BookonnectWorkerService.sln
dotnet build BookonnectWorkerService.sln --no-restore
```

## Configuration

`Program.cs` registers core options, database access, and services through `AddBookonnectCore(builder.Configuration)`.

The checked-in settings files configure logging only:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

Check BookonnectCore for the required connection strings, service URLs, credentials, and option names. 

Use environment variables or .NET user secrets for sensitive configuration. The project already declares a user-secrets ID. To set a secret, replace the example key and value with those required by BookonnectCore:

```sh
dotnet user-secrets set "Section:Key" "your-local-value" --project BookonnectWorkerService/BookonnectWorkerService.csproj
```

User secrets are loaded by the default host in Development. Environment variables use double underscores for nested keys, such as `Section__Key`.

The two job intervals are fixed at one hour in `Worker.cs`; they are not configuration options.

## Usage

Start the worker locally:

```sh
dotnet run --project BookonnectWorkerService/BookonnectWorkerService.csproj
```

The checked-in launch profile sets `DOTNET_ENVIRONMENT=Development`. On startup, the worker initializes both database services, starts its synchronization loop, and schedules preference processing. Press Ctrl+C to request shutdown.

Preference processing uses an in-memory UTC watermark initialized when the loop starts. Only preferences updated after that watermark are processed. Successful cycles advance it to the cycle's start time; failures leave it unchanged for retry. Restarting resets the watermark, so this worker does not provide persisted catch-up across downtime.

This is a background worker, not an HTTP API; it exposes no routes or Swagger UI.

## Development

```text
BookonnectWorkerService/
|-- Program.cs                    # Host and dependency registration
|-- Worker.cs                     # Initialization and recurring jobs
|-- appsettings.json              # Default logging configuration
|-- appsettings.Development.json  # Development logging configuration
`-- Properties/launchSettings.json
```

Business logic and data-access implementations live in BookonnectCore. The worker resolves `IRelationalOperationsService`, `IVectorOperationsService`, `IDatabaseSyncService`, and `IPreferenceService` from dependency-injection scopes.

## Testing

No test projects or CI workflows are checked into this repository. Use the build command above to check compilation. For an integration smoke test, configure the external services and run the worker, then inspect initialization and synchronization logs. Preference processing first runs after one hour. Running the worker can modify configured databases and user-profile data; use development resources.

## Publishing

```sh
dotnet publish BookonnectWorkerService/BookonnectWorkerService.csproj --configuration Release --output ./artifacts/publish
dotnet ./artifacts/publish/BookonnectWorkerService.dll
```

The default publish is framework-dependent and requires a compatible .NET runtime. Supply the deployment environment's configuration before launching the published worker; launch-profile settings do not apply to the published application.
