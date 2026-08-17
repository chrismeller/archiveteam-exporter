FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS build
ARG TARGETARCH
WORKDIR /src

COPY src/ArchiveTeam.Exporter.ApiService/ArchiveTeam.Exporter.ApiService.csproj src/ArchiveTeam.Exporter.ApiService/
COPY src/ArchiveTeam.Exporter.ServiceDefaults/ArchiveTeam.Exporter.ServiceDefaults.csproj src/ArchiveTeam.Exporter.ServiceDefaults/
RUN dotnet restore src/ArchiveTeam.Exporter.ApiService/ArchiveTeam.Exporter.ApiService.csproj -a $TARGETARCH

COPY . .
RUN dotnet publish src/ArchiveTeam.Exporter.ApiService/ArchiveTeam.Exporter.ApiService.csproj \
    -a $TARGETARCH \
    --no-restore \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS runtime
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ArchiveTeam.Exporter.ApiService.dll"]