# MusicCloudServer

The server of [MusicCloud](https://github.com/lideming/MusicCloud) implemented in C# with ASP.NET Core.

## Run in Docker [![Docker Image Size](https://img.shields.io/docker/image-size/yuuza/musiccloud/latest?label=yuuza%2Fmusiccloud%3Alatest&logo=docker)](https://hub.docker.com/r/yuuza/musiccloud)

It works out of box (with transcoding).

### Run simply

```shell
docker run -d yuuza/musiccloud
```

### With custom data location

```shell
docker run -d --name mc -v /PATH_TO_DATA:/app/data yuuza/musiccloud
```

### With docker-compose

Create file `docker-compose.yml` in a directory:

```compose
version: "2"

services:
    app:
        image: yuuza/musiccloud
        volumes:
            - ./data:/app/data
        ports:
            - "80:80"
```

Start the app:

```shell
docker-compose up -d
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
