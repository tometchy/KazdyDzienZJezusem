# ---------- STAGE 1: BUILD APP ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

COPY KazdyDzienZJezusem/KazdyDzienZJezusem.csproj KazdyDzienZJezusem/
RUN dotnet restore KazdyDzienZJezusem/KazdyDzienZJezusem.csproj

COPY KazdyDzienZJezusem/ KazdyDzienZJezusem/
RUN dotnet publish KazdyDzienZJezusem/KazdyDzienZJezusem.csproj -c Release -o /out --no-restore

# ---------- STAGE 2: BUILD REDIS DATA ----------
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS bible-data

RUN apk add --no-cache python3 py3-pip redis

WORKDIR /work

RUN python3 -m venv /opt/venv && \
    /opt/venv/bin/pip install --no-cache-dir redis ijson beautifulsoup4

COPY load-the-bible.py .
COPY data/ data/

RUN mkdir -p /data /out && \
    redis-server --dir /data --save "" --daemonize yes && \
    attempts=0; \
    until redis-cli PING >/dev/null 2>&1; do \
        attempts=$((attempts + 1)); \
        if [ "$attempts" -ge 50 ]; then \
            echo "Redis failed to start while preparing Bible data."; \
            exit 1; \
        fi; \
        sleep 0.1; \
    done && \
    /opt/venv/bin/python load-the-bible.py && \
    redis-cli SAVE && \
    cp /data/dump.rdb /out/dump.rdb

# ---------- STAGE 3: SHARED QUARTZ + RUNTIME TOOLS ----------
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS generation-base

RUN apk add --no-cache coreutils git nodejs npm redis

WORKDIR /opt/quartz-site

RUN git clone --depth 1 --branch v5 https://github.com/jackyzha0/quartz.git . && \
    npm ci && \
    npx quartz create \
        --template default \
        --strategy new \
        --baseUrl localhost \
        --links shortest && \
    config_file="$(find . -maxdepth 2 -name 'quartz.config.*' -print -quit)" && \
    if [ -n "$config_file" ]; then \
        sed -i 's/pageTitle: "Quartz 5"/pageTitle: "Każdy dzień z Jezusem"/' "$config_file"; \
        npx quartz plugin install --from-config; \
    fi

# ---------- STAGE 4: GENERATE NT HTML ----------
FROM generation-base AS nt-html

WORKDIR /work

COPY --from=build /out /app
COPY --from=bible-data /out/dump.rdb /data/dump.rdb

RUN mkdir -p /data /data-out && \
    redis-server --dir /data --save "" --daemonize yes && \
    attempts=0; \
    until redis-cli EXISTS gnt:John:1:1 >/dev/null 2>&1; do \
        attempts=$((attempts + 1)); \
        if [ "$attempts" -ge 100 ]; then \
            echo "Redis did not load Bible data for NT generation."; \
            exit 1; \
        fi; \
        sleep 0.1; \
    done && \
    KAZDY_DZIEN_INCLUDE_TOPICS_LINK=1 dotnet /app/KazdyDzienZJezusem.dll --all

# ---------- STAGE 5: GENERATE TOPICS HTML ----------
FROM generation-base AS topics-html

WORKDIR /work

COPY --from=build /out /app
COPY --from=bible-data /out/dump.rdb /data/dump.rdb
COPY Topics/ Topics/

RUN mkdir -p /data /data-out && \
    redis-server --dir /data --save "" --daemonize yes && \
    attempts=0; \
    until redis-cli PING >/dev/null 2>&1; do \
        attempts=$((attempts + 1)); \
        if [ "$attempts" -ge 100 ]; then \
            echo "Redis did not load Bible data for topic generation."; \
            exit 1; \
        fi; \
        sleep 0.1; \
    done && \
    dotnet /app/KazdyDzienZJezusem.dll

# ---------- STAGE 6: FINAL IMAGE ----------
FROM node:22-alpine

LABEL org.opencontainers.image.source=https://github.com/tometchy/KazdyDzienZJezusem

RUN apk add --no-cache coreutils
RUN mkdir -p /opt/prebuilt/IndexHtml /data-out

WORKDIR /KazdyDzienZJezusem

COPY KazdyDzienZJezusem/entrypoint.sh /KazdyDzienZJezusem/entrypoint.sh
COPY --from=nt-html /data-out/IndexHtml/ /opt/prebuilt/IndexHtml/
COPY --from=topics-html /data-out/IndexHtml/Topics/ /opt/prebuilt/IndexHtml/Topics/

RUN chmod +x /KazdyDzienZJezusem/entrypoint.sh

EXPOSE 8080

ENTRYPOINT ["/KazdyDzienZJezusem/entrypoint.sh"]
