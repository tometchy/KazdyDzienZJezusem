# ---------- STAGE 1: BUILD APP ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

COPY KazdyDzienZJezusem/KazdyDzienZJezusem.csproj KazdyDzienZJezusem/
RUN dotnet restore KazdyDzienZJezusem/KazdyDzienZJezusem.csproj

COPY KazdyDzienZJezusem/ KazdyDzienZJezusem/
COPY output/ output/
RUN dotnet publish KazdyDzienZJezusem/KazdyDzienZJezusem.csproj -c Release -o /out --no-restore

# ---------- STAGE 2: APPLICATION RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine

LABEL org.opencontainers.image.source=https://github.com/tometchy/KazdyDzienZJezusem

WORKDIR /app

COPY --from=build /out/ .

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "KazdyDzienZJezusem.dll"]
