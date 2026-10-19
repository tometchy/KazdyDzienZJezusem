# ---------- STAGE 1: BUILD APP ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

COPY KazdyDzienZJezusem/KazdyDzienZJezusem.csproj KazdyDzienZJezusem/
RUN dotnet restore KazdyDzienZJezusem/KazdyDzienZJezusem.csproj

COPY KazdyDzienZJezusem/ KazdyDzienZJezusem/
COPY input/ input/
COPY tags/ tags/
RUN dotnet publish KazdyDzienZJezusem/KazdyDzienZJezusem.csproj -c Release -o /out --no-restore

# The remote ref is a build-cache input: a new commit invalidates the clone layer.
# No token is needed to clone this public repository.
FROM alpine:3.22 AS tag-repository
RUN apk add --no-cache git ca-certificates
ADD https://api.github.com/repos/tometchy/KazdyDzienZJezusem/git/ref/heads/master /tmp/tag-ref.json
RUN git clone --depth=1 --single-branch --branch master \
    https://github.com/tometchy/KazdyDzienZJezusem.git /repository \
    && git -C /repository config kdj.editorClone true

# ---------- STAGE 2: APPLICATION RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine

LABEL org.opencontainers.image.source=https://github.com/tometchy/KazdyDzienZJezusem

WORKDIR /app

RUN apk add --no-cache git ca-certificates \
    && mkdir /keys && chown app:app /keys && chmod 700 /keys
COPY --from=build /out/ .
COPY --from=tag-repository --chown=app:app /repository/ /repository/

ENV ASPNETCORE_HTTP_PORTS=8080
ENV TagEditing__RepositoryPath=/repository
ENV EditorSessions__KeyPath=/keys

USER app

EXPOSE 8080

ENTRYPOINT ["dotnet", "KazdyDzienZJezusem.dll"]
