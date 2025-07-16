FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DataArchival.Main/DataArchival.Main.csproj", "DataArchival.Main/"]
COPY ["DataArchival.Core/DataArchival.Core.csproj", "DataArchival.Core/"]
RUN dotnet restore "DataArchival.Main/DataArchival.Main.csproj"
COPY . .
WORKDIR "/src/DataArchival.Main"
RUN dotnet build "./DataArchival.Main.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DataArchival.Main.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DataArchival.Main.dll"]
