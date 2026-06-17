# ---------- STAGE 1 ----------
FROM python:3.12-alpine AS builder

RUN apk add --no-cache redis

WORKDIR /KazdyDzienZJezusem

COPY load-the-bible.py .
COPY data/ data/
# COPY Biblia_przeklad_Torunski.epub .
# COPY UBG_2025.epub .

RUN pip install --no-cache-dir redis ijson beautifulsoup4

RUN mkdir -p /data && \
    redis-server --dir /data --save "" --daemonize yes && \
    sleep 1 && \
    python load-the-bible.py && \
    redis-cli SAVE && \
    cp /data/dump.rdb /KazdyDzienZJezusem/dump.rdb

# ---------- STAGE 2 ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src
COPY KazdyDzienZJezusem/ .

RUN dotnet publish -c Release -o /out

# ---------- STAGE 3 ----------
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine

RUN apk add --no-cache redis

WORKDIR /KazdyDzienZJezusem

RUN mkdir -p /data && chmod 777 /data
COPY --from=builder /KazdyDzienZJezusem/dump.rdb /data/dump.rdb
COPY --from=build /out .
COPY KazdyDzienZJezusem/entrypoint.sh /KazdyDzienZJezusem/entrypoint.sh
RUN chmod +x /KazdyDzienZJezusem/entrypoint.sh

ENTRYPOINT ["/KazdyDzienZJezusem/entrypoint.sh"]
