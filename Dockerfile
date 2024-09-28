﻿FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim-amd64 AS build
WORKDIR /
# Copy everything..
COPY ./ ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish /GPD/GPD.csproj -c Release -o app
# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "./GPD.dll"]