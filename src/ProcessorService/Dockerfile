# Build from the repository root: docker compose build (or docker build -f src/ProcessorService/Dockerfile .)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Contracts/Contracts.csproj src/Contracts/
COPY src/BlobStorage/BlobStorage.csproj src/BlobStorage/
COPY src/ProcessorService/ProcessorService.csproj src/ProcessorService/
RUN dotnet restore src/ProcessorService/ProcessorService.csproj
COPY src/Contracts/ src/Contracts/
COPY src/BlobStorage/ src/BlobStorage/
COPY src/ProcessorService/ src/ProcessorService/
RUN dotnet publish src/ProcessorService/ProcessorService.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
# Mount point for the shared "blobs" volume (one folder, mounted into both services). The image runs as the non-root
# "app" user; a new named volume takes this directory's owner, so the services can write to it.
RUN mkdir -p /data/blobs && chown app:app /data/blobs
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProcessorService.dll"]
