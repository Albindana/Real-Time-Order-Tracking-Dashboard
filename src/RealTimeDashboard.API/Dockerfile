FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["RealTimeDashboard.API/RealTimeDashboard.API.csproj", "RealTimeDashboard.API/"]
COPY ["RealTimeDashboard.Application/RealTimeDashboard.Application.csproj", "RealTimeDashboard.Application/"]
COPY ["RealTimeDashboard.Domain/RealTimeDashboard.Domain.csproj", "RealTimeDashboard.Domain/"]
COPY ["RealTimeDashboard.Infrastructure/RealTimeDashboard.Infrastructure.csproj", "RealTimeDashboard.Infrastructure/"]
RUN dotnet restore "RealTimeDashboard.API/RealTimeDashboard.API.csproj"
COPY . .
RUN dotnet publish "RealTimeDashboard.API/RealTimeDashboard.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RealTimeDashboard.API.dll"]
