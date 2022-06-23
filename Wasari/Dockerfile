FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine AS base
WORKDIR /app
RUN apk add python3 ffmpeg curl

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build
WORKDIR /src
COPY . .

FROM build AS publish
RUN dotnet publish "./Wasari/Wasari.csproj" --configuration Release --self-contained -r linux-x64 -p:PublishSingleFile=true -p:PublishReadyToRun=true -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp
RUN chmod a+rx /usr/local/bin/yt-dlp
ENTRYPOINT ["./Wasari"]