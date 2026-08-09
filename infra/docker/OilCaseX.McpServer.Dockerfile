FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/OilCaseX.McpServer/OilCaseX.McpServer.csproj", "src/OilCaseX.McpServer/"]
RUN dotnet restore "src/OilCaseX.McpServer/OilCaseX.McpServer.csproj"

COPY . .
WORKDIR "/src/src/OilCaseX.McpServer"
RUN dotnet publish "OilCaseX.McpServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=5 CMD curl --fail http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "OilCaseX.McpServer.dll"]
