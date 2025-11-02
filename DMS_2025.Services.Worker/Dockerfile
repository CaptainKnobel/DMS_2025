## See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.
#
## This stage is used when running from VS in fast mode (Default for Debug configuration)
#FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
#USER $APP_UID
#WORKDIR /app
#
#
## This stage is used to build the service project
#FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
#ARG BUILD_CONFIGURATION=Release
#WORKDIR /src
#COPY ["DMS_2025.Services.Worker/DMS_2025.Services.Worker.csproj", "DMS_2025.Services.Worker/"]
#RUN dotnet restore "./DMS_2025.Services.Worker/DMS_2025.Services.Worker.csproj"
#COPY . .
#WORKDIR "/src/DMS_2025.Services.Worker"
#RUN dotnet build "./DMS_2025.Services.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build
#
## This stage is used to publish the service project to be copied to the final stage
#FROM build AS publish
#ARG BUILD_CONFIGURATION=Release
#RUN dotnet publish "./DMS_2025.Services.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false
#
## This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
#FROM base AS final
#WORKDIR /app
#COPY --from=publish /app/publish .
#ENTRYPOINT ["dotnet", "DMS_2025.Services.Worker.dll"]


# ---------- custom

# ---- Build Stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "DMS_2025.Services.Worker/DMS_2025.Services.Worker.csproj"
RUN dotnet publish "DMS_2025.Services.Worker/DMS_2025.Services.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime Stage ----
FROM mcr.microsoft.com/dotnet/runtime:8.0

# Install Ghostscript + Tesseract + Sprachpakete
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ghostscript \
      tesseract-ocr \
      tesseract-ocr-deu \
      tesseract-ocr-eng \
 && rm -rf /var/lib/apt/lists/*

ENV TESSDATA_PREFIX=/usr/share/tesseract-ocr/4.00/tessdata

WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "DMS_2025.Services.Worker.dll"]
