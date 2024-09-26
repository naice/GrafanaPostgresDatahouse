FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER 1000
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["GPD/GPD.csproj", "GPD/"]
RUN dotnet restore "GPD/GPD.csproj"
COPY . .
WORKDIR "/src/GPD"
RUN dotnet build "GPD.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GPD.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
## RUN chown -R 1000:1000 ./wwwroot
ENTRYPOINT ["dotnet", "GPD.dll"]
