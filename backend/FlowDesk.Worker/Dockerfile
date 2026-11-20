# Contexto de build: raíz del repositorio  →  docker build -f backend/FlowDesk.Worker/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY backend/ backend/
RUN dotnet publish backend/FlowDesk.Worker/FlowDesk.Worker.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER app
ENTRYPOINT ["dotnet", "FlowDesk.Worker.dll"]
