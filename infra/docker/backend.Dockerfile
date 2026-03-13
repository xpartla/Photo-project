# =============================================================================
# Dev target: hot reload with dotnet watch
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS dev

WORKDIR /app/src

# Copy solution and project files for restore
COPY src/backend/DogPhoto.sln ./
COPY src/backend/DogPhoto.Api/DogPhoto.Api.csproj ./DogPhoto.Api/
COPY src/backend/DogPhoto.SharedKernel/DogPhoto.SharedKernel.csproj ./DogPhoto.SharedKernel/
COPY src/backend/DogPhoto.Infrastructure/DogPhoto.Infrastructure.csproj ./DogPhoto.Infrastructure/

RUN dotnet restore

# Copy all source (overridden by volume mount in dev)
COPY src/backend/ ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "watch", "run", "--project", "DogPhoto.Api", "--urls", "http://+:8080"]

# =============================================================================
# Build target: restore + publish
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build

WORKDIR /src

COPY src/backend/DogPhoto.sln ./
COPY src/backend/DogPhoto.Api/DogPhoto.Api.csproj ./DogPhoto.Api/
COPY src/backend/DogPhoto.SharedKernel/DogPhoto.SharedKernel.csproj ./DogPhoto.SharedKernel/
COPY src/backend/DogPhoto.Infrastructure/DogPhoto.Infrastructure.csproj ./DogPhoto.Infrastructure/

RUN dotnet restore

COPY src/backend/ ./

RUN dotnet publish DogPhoto.Api -c Release -o /app/publish --no-restore

# =============================================================================
# Prod target: minimal runtime image
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS prod

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

WORKDIR /app

COPY --from=build /app/publish .

USER appuser

EXPOSE 8080

ENTRYPOINT ["dotnet", "DogPhoto.Api.dll"]
