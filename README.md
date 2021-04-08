# MusicCloudServer

The server of MusicCloud implemented in C# with ASP.NET Core.

## Run in Docker [![Docker Cloud Build Status](https://img.shields.io/docker/cloud/build/yuuza/musiccloud)](https://hub.docker.com/r/yuuza/musiccloud)

```shell
docker run -d --name mc yuuza/musiccloud
```

### With custom data location

```shell
docker run -d --name mc -v /PATH_TO_DATA:/app/data yuuza/musiccloud
```

## Build / Run Manually

### Requirements

* [.NET SDK 5](https://dotnet.microsoft.com/download/dotnet-core/3.1)
* [PostgreSQL](https://www.postgresql.org/) (optional)

### Build

```
dotnet build
```
or
```
dotnet build -c Release
```

### Configure Back-end

Edit file `appsettings.json`.

(Please ensure `staticdir` is configured correctly.)

### Run

```
dotnet run
```
or
```
dotnet run -c Release
```

Now you can access http://localhost:5000/
