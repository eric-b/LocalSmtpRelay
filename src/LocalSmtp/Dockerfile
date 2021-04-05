#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0-buster-slim AS base
WORKDIR /app
EXPOSE 25

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["LocalSmtpRelay.csproj", ""]
RUN dotnet restore "./LocalSmtpRelay.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "LocalSmtpRelay.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LocalSmtpRelay.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LocalSmtpRelay.dll"]