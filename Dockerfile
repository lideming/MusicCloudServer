#==> Build web app
FROM node:alpine AS build-webapp-env
WORKDIR /app

# Git is required to generate the build info.
RUN apk add git

COPY webapp ./

# Copy the git submodule from the parent git repo.
RUN rm .git
COPY .git/modules/webapp/ .git/

# Remove "worktree" in git config to make git work.
RUN sed -i '/\Wworktree = .*/d' .git/config && \
    npm i && \
    npm run build


#==> Build backend
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out --no-self-contained \
        -r alpine-$([ "$(uname -m)" == aarch64 ] && echo arm64 || echo x64 )

COPY --from=build-webapp-env /app/bundle.js /app/index.html /app/out/webapp/


#==> Build the actual app container
FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine
WORKDIR /app

# These are required for transcoding
RUN apk add --no-cache bash ffmpeg fdk-aac && \
    apk add --no-cache fdkaac --repository=http://dl-cdn.alpinelinux.org/alpine/edge/testing

# Copy the published app
COPY --from=build-env /app/out .

# Make the app use "appsettings.docker.json"
ENV ASPNETCORE_ENVIRONMENT=docker

ENTRYPOINT ["dotnet", "MCloudServer.dll"]