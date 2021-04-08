### Build web app
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
    npm i rollup && \
    npm run build


### Build backend
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out -r alpine-x64 --no-self-contained

COPY --from=build-webapp-env /app/bundle.js /app/index.html /app/out/webapp/


FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine
WORKDIR /app
RUN apk add ffmpeg fdk-aac && \
    apk add fdkaac --repository=http://dl-cdn.alpinelinux.org/alpine/edge/testing
COPY --from=build-env /app/out .
ENV ASPNETCORE_ENVIRONMENT=docker
ENTRYPOINT ["dotnet", "MCloudServer.dll"]
