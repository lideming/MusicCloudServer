#==> Web 前端程序构建阶段
FROM node:alpine AS build-webapp-env
WORKDIR /app
RUN apk add git
COPY webapp ./
RUN rm .git
COPY .git/modules/webapp/ .git/
RUN sed -i '/\Wworktree = .*/d' .git/config && \
    npm i -g pnpm && \
    pnpm i --frozen-lockfile && \
    pnpm run build

#==> 后端程序构建阶段
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env
WORKDIR /app
COPY *.csproj ./
RUN dotnet restore
COPY . ./
# 生成目标平台为 linux-arm64 的二进制：
RUN dotnet publish -c Release -o out --no-self-contained -r linux-arm64
# 从前端构建阶段复制前端文件到输出目录：
COPY --from=build-webapp-env /app/bundle.js /app/index.html /app/out/webapp/

#==> 构建目标容器镜像
FROM mcr.microsoft.com/dotnet/aspnet:5.0
WORKDIR /app
# 从构建阶段复制程序：
COPY --from=build-env /app/out .
# 设置环境变量，使程序读取 "appsettings.docker.json" 作为默认配置：
ENV ASPNETCORE_ENVIRONMENT=docker
# 设置容器入口点：
ENTRYPOINT ["dotnet", "MCloudServer.dll"]
