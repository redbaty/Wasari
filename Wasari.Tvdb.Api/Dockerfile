FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Wasari.Tvdb.Api/Wasari.Tvdb.Api.csproj", "Wasari.Tvdb.Api/"]
COPY ["Wasari.Tvdb/Wasari.Tvdb.csproj", "Wasari.Tvdb/"]
COPY ["Wasari.Tvdb.Abstractions/Wasari.Tvdb.Abstractions.csproj", "Wasari.Tvdb.Abstractions/"]
RUN dotnet restore "Wasari.Tvdb.Api/Wasari.Tvdb.Api.csproj"
COPY . .
WORKDIR "/src/Wasari.Tvdb.Api"
RUN dotnet build "Wasari.Tvdb.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Wasari.Tvdb.Api.csproj" -c Release --no-self-contained -p:PublishReadyToRun=true -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Wasari.Tvdb.Api.dll"]
