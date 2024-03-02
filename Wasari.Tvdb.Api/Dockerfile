FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
RUN apk add icu-libs
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["Wasari.Tvdb.Api/Wasari.Tvdb.Api.csproj", "Wasari.Tvdb.Api/"]
COPY ["Wasari.Tvdb/Wasari.Tvdb.csproj", "Wasari.Tvdb/"]
COPY ["Wasari.Tvdb.Abstractions/Wasari.Tvdb.Abstractions.csproj", "Wasari.Tvdb.Abstractions/"]
RUN dotnet restore "Wasari.Tvdb.Api/Wasari.Tvdb.Api.csproj"
COPY . .
WORKDIR "/src/Wasari.Tvdb.Api"
RUN dotnet build "Wasari.Tvdb.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Wasari.Tvdb.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "Wasari.Tvdb.Api.dll"]
