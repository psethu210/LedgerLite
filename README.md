# LedgerLite

A demo ledger for learning production-style backend work in a payments setting. No real money and no real customer data.

## Prerequisites

- .NET SDK 9.0 (this repo pins `9.0.305` in `global.json`, and rolls forward to a newer 9.0 patch)
- Docker, for SQL Server and Redpanda

## Run local dependencies

```bash
docker compose up
```

- SQL Server Developer: `localhost,1433`, user `sa`, password `LedgerLite_Dev1!` (override with `MSSQL_SA_PASSWORD`)
- Redpanda Kafka: `localhost:19092`

## Build and test

```bash
dotnet build
dotnet test
```
