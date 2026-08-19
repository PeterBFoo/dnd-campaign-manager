FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS restore
WORKDIR /src
COPY global.json Directory.Build.props DndCampaign.slnx ./
COPY apps/api/DndCampaign.Api.csproj apps/api/
COPY apps/api-tests/DndCampaign.Api.Tests.csproj apps/api-tests/
RUN dotnet restore DndCampaign.slnx

FROM restore AS build
COPY apps/api apps/api
COPY apps/api-tests apps/api-tests
RUN dotnet publish apps/api/DndCampaign.Api.csproj -c Release --no-restore -o /app/publish

FROM build AS tests
ENTRYPOINT ["dotnet", "test", "apps/api-tests/DndCampaign.Api.Tests.csproj", "-c", "Release", "--no-restore"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
RUN apk add --no-cache krb5-libs
WORKDIR /app
LABEL org.opencontainers.image.source="https://github.com/PeterBFoo/dnd-campaign-manager"
COPY --from=build /app/publish .
USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DndCampaign.Api.dll"]
