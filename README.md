# ⚠️ Crunchyroll has apparently gone DRM only, this project is not working at the moment.

<p align="center">
  <img width="256" align="center" src="/assets/logo/logo.png">
</p>
<p align="center">
  Anime Downloader
</p>
<p align="center">  
  <a style="text-decoration:none" href="https://github.com/redbaty/Wasari/actions/workflows/docker-wasari-cli.yml">
    <img src="https://img.shields.io/github/actions/workflow/status/redbaty/wasari/docker-wasari-cli.yml?branch=next" alt="Build Status" />
  </a>
  <a style="text-decoration:none" href="https://github.com/redbaty/Wasari/releases">
    <img src="https://img.shields.io/github/release/redbaty/wasari.svg?label=Latest%20version&style=flat-square" alt="Latest version" />
  </a>
</p>

## Introduction

Wasari is a tool for downloading anime from various sources, and transcoding them to a format that is supported by most media players.

## Features
* :tv: Crunchyroll Support
* :star: Anime4K Support
* :memo: Soft subs encoding
* :arrow_down: Download queue
* :bullettrain_side: HEVC Transcoding (With NVIDIA and AMD AMF Hardware Acceleration support)

## :information_source: Getting Started

### :warning: Prerequisite
* FFmpeg ([Master build](https://github.com/BtbN/FFmpeg-Builds/releases) recommended, for full Anime4K support)
* [YT-DLP](https://github.com/yt-dlp/yt-dlp)

Wasari can be used as a CLI tool, or as a Docker container (as an API).

To use the CLI tool, you can download the latest release from the [releases page](https://github.com/redbaty/Wasari/releases) and run it using the following command:

`.\Wasari.Cli <Series-URL> -o <Output-Directory>`

You can check the full supported arguments list using the `--help` argument.

To utilize the Docker container, you can retrieve the most recent image from Docker Hub. By exposing port 80, you can access the API through the `/media/download` endpoint. To test the API, you can utilize Swagger UI, which is accessible at the `/swagger` endpoint.

## :+1: Credits

* [FFmpeg](https://git.ffmpeg.org/ffmpeg.git) - For video decoding/transcoding
* [YT-DLP](https://github.com/yt-dlp/yt-dlp) - For video downloading
* [CliFx](https://github.com/Tyrrrz/CliFx) - For arguments parsing
