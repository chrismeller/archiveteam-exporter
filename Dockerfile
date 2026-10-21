FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca AS runtime
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ArchiveTeam.Exporter.ApiService.dll"]