# ---------- STAGE 1 ----------
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS builder

RUN apk add --no-cache redis python3 py3-pip

WORKDIR /KazdyDzienZJezusem

COPY load-the-bible.py .
COPY data/ data/
# COPY Biblia_przeklad_Torunski.epub .
# COPY UBG_2025.epub .

RUN python3 -m venv /opt/venv && \
    /opt/venv/bin/pip install --no-cache-dir redis ijson beautifulsoup4

RUN mkdir -p /data && \
    redis-server --dir /data --save "" --daemonize yes && \
    sleep 1 && \
    /opt/venv/bin/python load-the-bible.py && \
    redis-cli SAVE && \
    cp /data/dump.rdb /KazdyDzienZJezusem/dump.rdb

# ---------- STAGE 2 ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src
COPY KazdyDzienZJezusem/ .

RUN dotnet publish -c Release -o /out

# ---------- STAGE 3 ----------
FROM node:22-alpine AS node

# ---------- STAGE 4 ----------
FROM node:22-alpine AS quartz

RUN apk add --no-cache git coreutils

WORKDIR /opt/quartz-site

# Non-interactive Quartz project bootstrap used by the C# app at runtime.
RUN git clone --depth 1 --branch v5 https://github.com/jackyzha0/quartz.git . && \
    npm ci && \
    npx quartz create \
    --template default \
    --strategy new \
    --baseUrl localhost \
    --links shortest && \
    npx quartz plugin install --from-config

# ---------- STAGE 5 ----------
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine

RUN apk add --no-cache redis

WORKDIR /KazdyDzienZJezusem

RUN mkdir -p /data && chmod 777 /data
COPY --from=builder /KazdyDzienZJezusem/dump.rdb /data/dump.rdb
COPY --from=build /out .
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/bin/npm /usr/local/bin/npm
COPY --from=node /usr/local/bin/npx /usr/local/bin/npx
COPY --from=node /usr/local/lib/node_modules /usr/local/lib/node_modules
COPY --from=quartz /opt/quartz-site /opt/quartz-site
COPY KazdyDzienZJezusem/entrypoint.sh /KazdyDzienZJezusem/entrypoint.sh
RUN chmod +x /KazdyDzienZJezusem/entrypoint.sh

ENTRYPOINT ["/KazdyDzienZJezusem/entrypoint.sh"]
