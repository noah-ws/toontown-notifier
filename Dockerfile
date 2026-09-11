FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/ToontownNotifier/ToontownNotifier.csproj src/ToontownNotifier/
RUN dotnet restore src/ToontownNotifier/ToontownNotifier.csproj

COPY src/ToontownNotifier/ src/ToontownNotifier/
RUN dotnet publish src/ToontownNotifier/ToontownNotifier.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

RUN mkdir -p /app/data

COPY --from=build /app/publish .
COPY tasks.yaml /app/tasks.yaml

ENV STATE_PATH=/app/data/state.json
ENV TASKS_YAML_PATH=/app/tasks.yaml
ENV COMPANION_HOST=host.docker.internal
ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ToontownNotifier.dll"]
