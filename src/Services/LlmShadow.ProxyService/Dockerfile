FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files for layer-cache-friendly restore
COPY ["src/Services/LlmShadow.ProxyService/LlmShadow.ProxyService.csproj",       "src/Services/LlmShadow.ProxyService/"]
COPY ["src/Shared/LlmShadow.Common/LlmShadow.Common.csproj",                     "src/Shared/LlmShadow.Common/"]
COPY ["src/Shared/LlmShadow.Models/LlmShadow.Models.csproj",                     "src/Shared/LlmShadow.Models/"]
COPY ["src/Shared/LlmShadow.DataLayer/LlmShadow.DataLayer.csproj",               "src/Shared/LlmShadow.DataLayer/"]
COPY ["src/Shared/LlmShadow.Inference/LlmShadow.Inference.csproj",               "src/Shared/LlmShadow.Inference/"]
COPY ["src/Shared/LlmShadow.Messaging/LlmShadow.Messaging.csproj",               "src/Shared/LlmShadow.Messaging/"]
RUN dotnet restore "src/Services/LlmShadow.ProxyService/LlmShadow.ProxyService.csproj"

COPY . .
WORKDIR "/src/src/Services/LlmShadow.ProxyService"
RUN dotnet build "LlmShadow.ProxyService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "LlmShadow.ProxyService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Run as non-root
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

ENTRYPOINT ["dotnet", "LlmShadow.ProxyService.dll"]
