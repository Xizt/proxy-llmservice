FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Services/LlmShadow.CompareService/LlmShadow.CompareService.csproj", "src/Services/LlmShadow.CompareService/"]
COPY ["src/Shared/LlmShadow.Common/LlmShadow.Common.csproj",                   "src/Shared/LlmShadow.Common/"]
COPY ["src/Shared/LlmShadow.Models/LlmShadow.Models.csproj",                   "src/Shared/LlmShadow.Models/"]
COPY ["src/Shared/LlmShadow.DataLayer/LlmShadow.DataLayer.csproj",             "src/Shared/LlmShadow.DataLayer/"]
RUN dotnet restore "src/Services/LlmShadow.CompareService/LlmShadow.CompareService.csproj"

COPY . .
WORKDIR "/src/src/Services/LlmShadow.CompareService"
RUN dotnet build "LlmShadow.CompareService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "LlmShadow.CompareService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

ENTRYPOINT ["dotnet", "LlmShadow.CompareService.dll"]
