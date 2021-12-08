#==> Skip building web app for this DB migration build

#==> Build backend
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out --no-self-contained \
        -r alpine-$([ "$(uname -m)" == aarch64 ] && echo arm64 || echo x64 )


#==> Build the actual app container
FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine
WORKDIR /app

# These are required for transcoding
RUN apk add --no-cache bash ffmpeg fdk-aac && \
    apk add --no-cache fdkaac --repository=http://dl-cdn.alpinelinux.org/alpine/edge/community

# Copy the published app
COPY --from=build-env /app/out .

# Make the app use "appsettings.docker.json"
ENV ASPNETCORE_ENVIRONMENT=docker

ENTRYPOINT ["dotnet", "MCloudServer.dll"]
