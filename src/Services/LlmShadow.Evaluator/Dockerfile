FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Services/LlmShadow.Evaluator/LlmShadow.Evaluator.csproj",     "src/Services/LlmShadow.Evaluator/"]
COPY ["src/Shared/LlmShadow.Common/LlmShadow.Common.csproj",             "src/Shared/LlmShadow.Common/"]
COPY ["src/Shared/LlmShadow.Models/LlmShadow.Models.csproj",             "src/Shared/LlmShadow.Models/"]
COPY ["src/Shared/LlmShadow.DataLayer/LlmShadow.DataLayer.csproj",       "src/Shared/LlmShadow.DataLayer/"]
COPY ["src/Shared/LlmShadow.Evaluation/LlmShadow.Evaluation.csproj",     "src/Shared/LlmShadow.Evaluation/"]
RUN dotnet restore "src/Services/LlmShadow.Evaluator/LlmShadow.Evaluator.csproj"

COPY . .
WORKDIR "/src/src/Services/LlmShadow.Evaluator"
RUN dotnet build "LlmShadow.Evaluator.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "LlmShadow.Evaluator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

ENTRYPOINT ["dotnet", "LlmShadow.Evaluator.dll"]
