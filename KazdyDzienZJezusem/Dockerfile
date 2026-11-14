# ---------- POC: BUILD REAL CORPUS WITHOUT RUNTIME PYTHON ----------
FROM python:3.12-alpine AS poc-corpus
WORKDIR /src
COPY input/ input/
COPY poc/prepare.py /tools/prepare.py
RUN python3 /tools/prepare.py --repo /src --out /out

# ---------- POC: BUNDLE SQLITE WASM WITHOUT RUNTIME NODE ----------
FROM node:24-alpine AS poc-frontend
WORKDIR /poc
COPY poc/frontend/ ./
RUN npm install --no-audit --no-fund
COPY --from=poc-corpus /out/ ./public/
RUN npm run build

# ---------- STAGE 1: BUILD APP ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

COPY KazdyDzienZJezusem/KazdyDzienZJezusem.csproj KazdyDzienZJezusem/
RUN dotnet restore KazdyDzienZJezusem/KazdyDzienZJezusem.csproj

COPY KazdyDzienZJezusem/ KazdyDzienZJezusem/
COPY input/ input/
COPY tags/ tags/
RUN dotnet publish KazdyDzienZJezusem/KazdyDzienZJezusem.csproj -c Release -o /out --no-restore

# Only a secret mount supplies the private repository credential. It is never
# an ARG, an ENV, a URL component or part of the copied repository.
FROM alpine:3.22 AS tag-repository
RUN apk add --no-cache git ca-certificates
COPY scripts/clone-tag-repository.sh /usr/local/bin/clone-tag-repository
# Legacy podman-compose mounts file-backed build secrets as read-only volumes.
# Native Podman and modern Compose supply the same path with a secret mount.
RUN --mount=type=secret,id=github_read_token \
    sh /usr/local/bin/clone-tag-repository

# ---------- STAGE 2: APPLICATION RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine

LABEL org.opencontainers.image.source=https://github.com/tometchy/KazdyDzienZJezusem

WORKDIR /app

RUN apk add --no-cache git ca-certificates icu-libs icu-data-full \
    && mkdir /keys && chown app:app /keys && chmod 700 /keys
COPY --from=build /out/ .
# These are static assets served by the existing ASP.NET Core process.
COPY --from=poc-frontend /poc/dist/ ./wwwroot/POC/
COPY --from=tag-repository --chown=app:app /repository/ /repository/

ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV TagEditing__RepositoryPath=/repository
ENV EditorSessions__KeyPath=/keys

USER app

EXPOSE 8080

ENTRYPOINT ["dotnet", "KazdyDzienZJezusem.dll"]
