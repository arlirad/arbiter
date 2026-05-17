# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .

ARG VERSION=0.0.0
RUN dotnet publish src/Arbiter/Arbiter.csproj -c Release -o /app /p:Version=${VERSION}

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final

RUN groupadd -r appgroup && useradd -r -g appgroup appuser

WORKDIR /app
COPY --from=build /app .

USER appuser

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:80/ || exit 1

ENTRYPOINT ["dotnet", "Arbiter.dll"]