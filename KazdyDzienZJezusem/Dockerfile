# ---------- STAGE 1: BUILD APP ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

COPY KazdyDzienZJezusem/KazdyDzienZJezusem.csproj KazdyDzienZJezusem/
RUN dotnet restore KazdyDzienZJezusem/KazdyDzienZJezusem.csproj

COPY KazdyDzienZJezusem/ KazdyDzienZJezusem/
RUN dotnet publish KazdyDzienZJezusem/KazdyDzienZJezusem.csproj -c Release -o /out --no-restore

# ---------- STAGE 2: BUILD REDIS DATA ----------
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS bible-input

RUN apk add --no-cache python3 py3-pip redis

WORKDIR /work

RUN python3 -m venv /opt/venv && \
    /opt/venv/bin/pip install --no-cache-dir redis ijson beautifulsoup4

COPY load-the-bible.py .
COPY input/ data/

RUN mkdir -p /input /out && \
    redis-server --dir /input --save "" --daemonize yes && \
    attempts=0; \
    until redis-cli PING >/dev/null 2>&1; do \
        attempts=$((attempts + 1)); \
        if [ "$attempts" -ge 50 ]; then \
            echo "Redis failed to start while preparing Bible input."; \
            exit 1; \
        fi; \
        sleep 0.1; \
    done && \
    /opt/venv/bin/python load-the-bible.py && \
    redis-cli SAVE && \
    cp /input/dump.rdb /out/dump.rdb

# ---------- STAGE 3: APPLICATION RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine

LABEL org.opencontainers.image.source=https://github.com/tometchy/KazdyDzienZJezusem

RUN apk add --no-cache redis && mkdir -p /input

WORKDIR /app

COPY --from=build /out/ .
COPY --from=bible-input /out/dump.rdb /input/dump.rdb

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

CMD ["sh", "-c", "redis-server --dir /input --save '' --daemonize yes && until redis-cli PING >/dev/null 2>&1; do sleep 0.1; done && exec dotnet KazdyDzienZJezusem.dll"]
