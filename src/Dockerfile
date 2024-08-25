#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app
EXPOSE 25

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/LocalSmtpRelay/LocalSmtpRelay.csproj", "src/LocalSmtpRelay/"]
RUN dotnet restore "src/LocalSmtpRelay/LocalSmtpRelay.csproj"
COPY src/ src
WORKDIR "/src"
RUN dotnet build "src/LocalSmtpRelay/LocalSmtpRelay.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src/LocalSmtpRelay/LocalSmtpRelay.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LocalSmtpRelay.dll"]