FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
RUN apk add curl python3 ffmpeg icu-libs
RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp
RUN chmod a+rx /usr/local/bin/yt-dlp
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["Wasari.Daemon/Wasari.Daemon.csproj", "Wasari.Daemon/"]
COPY ["Wasari.App/Wasari.App.csproj", "Wasari.App/"]
COPY ["Wasari.App.Abstractions/Wasari.App.Abstractions.csproj", "Wasari.App.Abstractions/"]
COPY ["Wasari.FFmpeg/Wasari.FFmpeg.csproj", "Wasari.FFmpeg/"]
COPY ["Wasari.Environment/Wasari.Environment.csproj", "Wasari.Environment/"]
COPY ["Wasari.YoutubeDlp/Wasari.YoutubeDlp.csproj", "Wasari.YoutubeDlp/"]
COPY ["Wasari.Crunchyroll/Wasari.Crunchyroll.csproj", "Wasari.Crunchyroll/"]
COPY ["Wasari.Tvdb.Api.Client/Wasari.Tvdb.Api.Client.csproj", "Wasari.Tvdb.Api.Client/"]
COPY ["Wasari.Tvdb.Abstractions/Wasari.Tvdb.Abstractions.csproj", "Wasari.Tvdb.Abstractions/"]
RUN dotnet restore "Wasari.Daemon/Wasari.Daemon.csproj"
COPY . .
WORKDIR "/src/Wasari.Daemon"
RUN dotnet build "Wasari.Daemon.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Wasari.Daemon.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "Wasari.Daemon.dll"]
