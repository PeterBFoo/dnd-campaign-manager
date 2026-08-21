FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS restore
WORKDIR /src
COPY global.json Directory.Build.props DndCampaign.slnx ./
COPY apps/api/DndCampaign.Api.csproj apps/api/
COPY src/Modules/Access/DndCampaign.Modules.Access/DndCampaign.Modules.Access.csproj src/Modules/Access/DndCampaign.Modules.Access/
COPY tests/Modules/Access/DndCampaign.Modules.Access.Tests/DndCampaign.Modules.Access.Tests.csproj tests/Modules/Access/DndCampaign.Modules.Access.Tests/
COPY tests/DndCampaign.ArchitectureTests/DndCampaign.ArchitectureTests.csproj tests/DndCampaign.ArchitectureTests/
RUN dotnet restore DndCampaign.slnx

FROM restore AS build
COPY apps/api apps/api
COPY src src
COPY tests tests
RUN dotnet publish apps/api/DndCampaign.Api.csproj -c Release --no-restore -o /app/publish

FROM build AS tests
ENTRYPOINT ["dotnet", "test", "DndCampaign.slnx", "-c", "Release", "--no-restore", "--minimum-expected-tests", "33"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
RUN apk add --no-cache krb5-libs
WORKDIR /app
LABEL org.opencontainers.image.source="https://github.com/PeterBFoo/dnd-campaign-manager"
COPY --from=build /app/publish .
USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DndCampaign.Api.dll"]
