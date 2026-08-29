FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS restore
RUN apk add --no-cache krb5-libs
WORKDIR /src
COPY global.json Directory.Build.props DndCampaign.slnx ./
COPY apps/api/DndCampaign.Api.csproj apps/api/
COPY apps/api/Modules/Access/DndCampaign.Modules.Access/DndCampaign.Modules.Access.csproj apps/api/Modules/Access/DndCampaign.Modules.Access/
COPY apps/api/Modules/AdventureCatalog/DndCampaign.Modules.AdventureCatalog/DndCampaign.Modules.AdventureCatalog.csproj apps/api/Modules/AdventureCatalog/DndCampaign.Modules.AdventureCatalog/
COPY apps/api/Modules/Campaigns/DndCampaign.Modules.Campaigns/DndCampaign.Modules.Campaigns.csproj apps/api/Modules/Campaigns/DndCampaign.Modules.Campaigns/
COPY apps/api/Modules/Characters/DndCampaign.Modules.Characters/DndCampaign.Modules.Characters.csproj apps/api/Modules/Characters/DndCampaign.Modules.Characters/
COPY apps/api/Modules/Combat/DndCampaign.Modules.Combat/DndCampaign.Modules.Combat.csproj apps/api/Modules/Combat/DndCampaign.Modules.Combat/
COPY apps/api/Modules/Journal/DndCampaign.Modules.Journal/DndCampaign.Modules.Journal.csproj apps/api/Modules/Journal/DndCampaign.Modules.Journal/
COPY apps/api/Modules/Missions/DndCampaign.Modules.Missions/DndCampaign.Modules.Missions.csproj apps/api/Modules/Missions/DndCampaign.Modules.Missions/
COPY tests/Modules/Access/DndCampaign.Modules.Access.Tests/DndCampaign.Modules.Access.Tests.csproj tests/Modules/Access/DndCampaign.Modules.Access.Tests/
COPY tests/Modules/AdventureCatalog/DndCampaign.Modules.AdventureCatalog.Tests/DndCampaign.Modules.AdventureCatalog.Tests.csproj tests/Modules/AdventureCatalog/DndCampaign.Modules.AdventureCatalog.Tests/
COPY tests/Modules/Campaigns/DndCampaign.Modules.Campaigns.Tests/DndCampaign.Modules.Campaigns.Tests.csproj tests/Modules/Campaigns/DndCampaign.Modules.Campaigns.Tests/
COPY tests/Modules/Characters/DndCampaign.Modules.Characters.Tests/DndCampaign.Modules.Characters.Tests.csproj tests/Modules/Characters/DndCampaign.Modules.Characters.Tests/
COPY tests/Modules/Combat/DndCampaign.Modules.Combat.Tests/DndCampaign.Modules.Combat.Tests.csproj tests/Modules/Combat/DndCampaign.Modules.Combat.Tests/
COPY tests/Modules/Journal/DndCampaign.Modules.Journal.Tests/DndCampaign.Modules.Journal.Tests.csproj tests/Modules/Journal/DndCampaign.Modules.Journal.Tests/
COPY tests/Modules/Missions/DndCampaign.Modules.Missions.Tests/DndCampaign.Modules.Missions.Tests.csproj tests/Modules/Missions/DndCampaign.Modules.Missions.Tests/
COPY tests/DndCampaign.ArchitectureTests/DndCampaign.ArchitectureTests.csproj tests/DndCampaign.ArchitectureTests/
RUN dotnet restore DndCampaign.slnx

FROM restore AS build
COPY apps/api apps/api
COPY tests tests
RUN dotnet publish apps/api/DndCampaign.Api.csproj -c Release --no-restore -o /app/publish

FROM build AS tests
ENTRYPOINT ["dotnet", "test", "DndCampaign.slnx", "-c", "Release", "--no-restore", "--max-parallel-test-modules", "1", "--minimum-expected-tests", "35"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
RUN apk add --no-cache krb5-libs
WORKDIR /app
LABEL org.opencontainers.image.source="https://github.com/PeterBFoo/dnd-campaign-manager"
COPY --from=build /app/publish .
USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DndCampaign.Api.dll"]
