# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# QuestPDF renders via SkiaSharp, which needs fontconfig/freetype on Linux.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["feitoapi.api/feitoapi.api.csproj", "feitoapi.api/"]
RUN dotnet restore "feitoapi.api/feitoapi.api.csproj"
COPY . .
WORKDIR "/src/feitoapi.api"
RUN dotnet build "./feitoapi.api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./feitoapi.api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN useradd -m -u 10001 appuser
USER appuser
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "feitoapi.api.dll"]