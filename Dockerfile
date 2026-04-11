# ---------- STAGE 1: build data into redis dump ----------
FROM python:3.12-alpine AS builder

RUN apk add --no-cache redis

WORKDIR /app

COPY load-textus-receptus.py .
COPY data/ data/

RUN pip install --no-cache-dir redis ijson

# uruchom redis + załaduj dane + zapisz dump
RUN mkdir -p /data && \
    redis-server --dir /data --daemonize yes && \
    sleep 1 && \
    python load-textus-receptus.py && \
    redis-cli SAVE && \
    cp /data/dump.rdb /app/dump.rdb

# ---------- STAGE 2: build .NET app ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src
COPY app/ .

RUN dotnet publish -c Release -o /out

# ---------- STAGE 3: final ----------
FROM alpine:3.20

RUN apk add --no-cache redis dotnet-runtime-10

WORKDIR /app

# Redis data
RUN mkdir -p /data
COPY --from=builder /app/dump.rdb /data/dump.rdb

# App
COPY --from=build /out .

# entrypoint
RUN echo '#!/bin/sh
set -e
redis-server --dir /data --daemonize yes
sleep 1
dotnet App.dll "$@"
' > /app/entrypoint.sh && chmod +x /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]
