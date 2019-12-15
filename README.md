# MusicCloudServer

The server of MusicCloud implemented in C# with ASP.NET Core.

## Build/Run Requirements

* [.NET Core SDK 3.0+](https://dotnet.microsoft.com/download/dotnet-core/3.1)
* [PostgreSQL](https://www.postgresql.org/) (optional)

## Build

```
dotnet build
```
or
```
dotnet build -c Release
```

## Configure Back-end

Edit file `appsettings.json`.

(Please ensure `staticdir` is configured correctly.)

## Run

```
dotnet run
```
or
```
dotnet run -c Release
```

Now you can access http://localhost:5000/
