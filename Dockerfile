# syntax=docker/dockerfile:1
# Stage 1: Angular production build
FROM node:20-bookworm-slim AS frontend-build
WORKDIR /src/front
COPY Src/NAN.GitBackupper.Front/package.json ./
RUN npm install
COPY Src/NAN.GitBackupper.Front/ ./
RUN npm run build -- --configuration production

# Stage 2: publish ASP.NET API + static wwwroot
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS api-build
WORKDIR /src
COPY Src/NAN.GitBackupper.sln ./
COPY Src/NAN.Git/ Src/NAN.Git/
COPY Src/NAN.GitBackupper.Core/ Src/NAN.GitBackupper.Core/
COPY Src/NAN.GitBackupper.Api/ Src/NAN.GitBackupper.Api/
RUN dotnet publish Src/NAN.GitBackupper.Api/NAN.GitBackupper.Api.csproj -c Release -o /app/publish

# Copy Angular browser bundle into wwwroot (Angular application builder output)
COPY --from=frontend-build /src/front/dist/nan-gitbackupper-front/browser/ /app/publish/wwwroot/

# Stage 3: runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends git \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=api-build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "NAN.GitBackupper.Api.dll"]
