FROM alpine:3.19 AS ffmpeg-base
RUN apk update && apk add wget
RUN wget https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linux64-gpl.tar.xz
RUN mkdir /ffmpeg
RUN tar -C /ffmpeg -xvf ffmpeg-master-latest-linux64-gpl.tar.xz --strip-components=1

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
RUN apt update && apt install curl python3 -y
RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp
RUN chmod a+rx /usr/local/bin/yt-dlp
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Wasari.Cli/Wasari.Cli.csproj", "Wasari.Cli/"]
COPY ["Wasari.App/Wasari.App.csproj", "Wasari.App/"]
COPY ["Wasari.App.Abstractions/Wasari.App.Abstractions.csproj", "Wasari.App.Abstractions/"]
COPY ["Wasari.FFmpeg/Wasari.FFmpeg.csproj", "Wasari.FFmpeg/"]
COPY ["Wasari.YoutubeDlp/Wasari.YoutubeDlp.csproj", "Wasari.YoutubeDlp/"]
COPY ["Wasari.Environment/Wasari.Environment.csproj", "Wasari.Environment/"]
COPY ["Wasari.Crunchyroll/Wasari.Crunchyroll.csproj", "Wasari.Crunchyroll/"]
RUN dotnet restore "Wasari.Cli/Wasari.Cli.csproj"
COPY . .
WORKDIR "/src/Wasari.Cli"
RUN dotnet build "Wasari.Cli.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Wasari.Cli.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=ffmpeg-base /ffmpeg/bin/ /usr/local/bin/
RUN chmod a+rx /usr/local/bin/ffmpeg
ENV DEFAULT_OUTPUT_DIRECTORY /output
ENV NVIDIA_DRIVER_CAPABILITIES="compute,video,utility"
ENTRYPOINT ["dotnet", "Wasari.Cli.dll"]
