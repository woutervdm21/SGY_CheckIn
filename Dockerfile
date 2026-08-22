# syntax=docker/dockerfile:1
#
# The SDK image is pinned to a specific patch version rather than floating on "10.0".
# Two things were needed to get a working interactive Blazor Server build out of this
# Dockerfile, found by testing directly against the built image (not just a clean build):
#   1. Copying the full source tree BEFORE restoring/publishing, in one pass — restoring
#      against just the .csproj (a common Docker layer-caching trick) and then publishing
#      with `--no-restore` left the static web assets manifest missing
#      `_framework/blazor.web.js` (the framework's own interactive-circuit script), which
#      silently breaks all interactivity — no build error, the app just never becomes
#      interactive. This project is small enough that skipping that caching trick costs
#      nothing.
#   2. Pinning the SDK to 10.0.201 rather than the floating "10.0" tag (10.0.400 as of
#      writing) as extra insurance against SDK-version drift affecting the same manifest.
# If you bump this version, republish and check that `_framework/blazor.web.js` appears in
# the running container's `_framework/` route list before trusting the build.
FROM mcr.microsoft.com/dotnet/sdk:10.0.201 AS build
WORKDIR /src

COPY . .
RUN dotnet publish "src/SGY.CheckIn/SGY.CheckIn.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# SQLite and the auth cookie's Data Protection keys both live on the mounted volume
# (see docker-compose.yml) so data and logins both survive container rebuilds/updates.
RUN mkdir -p /data
ENV ConnectionStrings__Default="Data Source=/data/sgy_checkin.db"
ENV DataProtection__KeyPath=/data/keys
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
ENTRYPOINT ["dotnet", "SGY.CheckIn.dll"]
