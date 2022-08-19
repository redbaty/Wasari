FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Wasari.Cli/Wasari.Cli.csproj", "Wasari.Cli/"]
RUN dotnet restore "Wasari.Cli/Wasari.Cli.csproj"
COPY . .
WORKDIR "/src/Wasari.Cli"
RUN dotnet build "Wasari.Cli.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Wasari.Cli.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Wasari.Cli.dll"]
