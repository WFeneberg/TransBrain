# Build context: repository root.  docker build -f src/TransBrain.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props nuget.config ./
COPY src/TransBrain.Domain/TransBrain.Domain.csproj src/TransBrain.Domain/
COPY src/TransBrain.Application/TransBrain.Application.csproj src/TransBrain.Application/
COPY src/TransBrain.Infrastructure/TransBrain.Infrastructure.csproj src/TransBrain.Infrastructure/
COPY src/TransBrain.ServiceDefaults/TransBrain.ServiceDefaults.csproj src/TransBrain.ServiceDefaults/
COPY src/TransBrain.Api/TransBrain.Api.csproj src/TransBrain.Api/
RUN dotnet restore src/TransBrain.Api/TransBrain.Api.csproj
COPY src/TransBrain.Domain src/TransBrain.Domain
COPY src/TransBrain.Application src/TransBrain.Application
COPY src/TransBrain.Infrastructure src/TransBrain.Infrastructure
COPY src/TransBrain.ServiceDefaults src/TransBrain.ServiceDefaults
COPY src/TransBrain.Api src/TransBrain.Api
RUN dotnet publish src/TransBrain.Api/TransBrain.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "TransBrain.Api.dll"]
