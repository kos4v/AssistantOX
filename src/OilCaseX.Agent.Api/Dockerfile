FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/OilCaseX.Agent.Api/OilCaseX.Agent.Api.csproj", "src/OilCaseX.Agent.Api/"]
COPY ["src/OilCaseX.Agent.Application/OilCaseX.Agent.Application.csproj", "src/OilCaseX.Agent.Application/"]
COPY ["src/OilCaseX.Agent.Domain/OilCaseX.Agent.Domain.csproj", "src/OilCaseX.Agent.Domain/"]
COPY ["src/OilCaseX.Agent.Infrastructure/OilCaseX.Agent.Infrastructure.csproj", "src/OilCaseX.Agent.Infrastructure/"]
RUN dotnet restore "src/OilCaseX.Agent.Api/OilCaseX.Agent.Api.csproj"

COPY . .
WORKDIR /src/src/OilCaseX.Agent.Api
RUN dotnet publish "OilCaseX.Agent.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "OilCaseX.Agent.Api.dll"]
