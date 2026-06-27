FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0@sha256:ea8bde36c11b6e7eec2656d0e59101d4462f6bd630730f2c8201ed0572b295d5 AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:7644f992230d35cf230017189d4038c0ae0f7388b13f4f7ae1900a155bafb597 AS runtime
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ArchiveTeam.Exporter.ApiService.dll"]