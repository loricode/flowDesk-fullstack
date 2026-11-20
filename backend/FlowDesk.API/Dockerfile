# Contexto de build: raíz del repositorio  →  docker build -f backend/FlowDesk.API/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY backend/ backend/
RUN dotnet publish backend/FlowDesk.API/FlowDesk.API.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
USER app
# Health check: GET /health (configurarlo en el balanceador o en la definición de tarea de ECS).
ENTRYPOINT ["dotnet", "FlowDesk.API.dll"]
