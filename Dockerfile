# ---------- STAGE 1 ----------
FROM python:3.12-alpine AS builder

RUN apk add --no-cache redis

WORKDIR /app

COPY load-the-bible.py .
COPY data/ data/
COPY Biblia_przeklad_Torunski.epub .
COPY UBG_2025.epub .

RUN pip install --no-cache-dir redis ijson beautifulsoup4

RUN mkdir -p /data && \
    redis-server --dir /data --save "" --daemonize yes && \
    sleep 1 && \
    python load-the-bible.py && \
    redis-cli SAVE && \
    cp /data/dump.rdb /app/dump.rdb

# ---------- STAGE 2 ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src
COPY app/ .

RUN dotnet publish -c Release -o /out

# ---------- STAGE 3 ----------
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine

RUN apk add --no-cache redis

WORKDIR /app

RUN mkdir -p /data
COPY --from=builder /app/dump.rdb /data/dump.rdb
COPY --from=build /out .

RUN cat << 'EOF' > /app/entrypoint.sh
#!/bin/sh
set -e

redis-server --dir /data --save "" --daemonize yes

# 🔥 Czekamy aż Redis ZAŁADUJE dump (100% pewne)
while true; do
  LOADING=$(redis-cli INFO persistence | grep loading: | cut -d: -f2 | tr -d '\r')
  if [ "$LOADING" = "0" ]; then
    break
  fi
  sleep 0.1
done

dotnet App.dll "$@"
EOF

RUN chmod +x /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]
