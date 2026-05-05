FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY O11yParty.csproj ./
RUN dotnet restore O11yParty.csproj

COPY . ./
RUN dotnet publish O11yParty.csproj -c Release -o /app/publish
COPY newrelic.config /app/publish/newrelic/newrelic.config

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# New Relic buzz integration — override at runtime via env vars or appsettings
# Example: NewRelic__AccountId=1234567  NewRelic__UserApiKey=your-user-api-key
ENV NewRelic__AccountId= \
    NewRelic__UserApiKey=

# New Relic APM — set NEW_RELIC_LICENSE_KEY at runtime to enable instrumentation
ENV NEW_RELIC_LICENSE_KEY=

# CORECLR_PROFILER_PATH is set at container startup by docker-entrypoint.sh
# based on the detected CPU architecture (x86_64 → linux-x64, aarch64 → linux-arm64).
ENV CORECLR_ENABLE_PROFILING=1 \
    CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
    CORECLR_NEWRELIC_HOME=/app/newrelic \
    NEW_RELIC_APP_NAME="O11yParty" \
    NEW_RELIC_LOG_LEVEL=info

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/ || exit 1

COPY --from=build --chown=app:app /app/publish .
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

USER app

ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
