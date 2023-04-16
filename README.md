<p align="center">
  <img width="256" align="center" src="/assets/logo/logo.png">
</p>
<p align="center">
  Anime Downloader
</p>
<p align="center">  
  <a style="text-decoration:none" href="https://github.com/redbaty/Wasari/actions/workflows/dotnet-core.yml">
    <img src="https://img.shields.io/github/actions/workflow/status/redbaty/wasari/dotnet-core.yml?branch=next" alt="Build Status" />
  </a>
  <a style="text-decoration:none" href="https://github.com/redbaty/Wasari/releases">
    <img src="https://img.shields.io/github/release/redbaty/wasari.svg?label=Latest%20version&style=flat-square" alt="Latest version" />
  </a>
</p>

## Introduction

Wasari hopes to make it easy to download anime shows from popular streaming services.

## Features
* :tv: Crunchyroll Support
* :star: Anime4K Support
* :memo: Soft subs encoding
* :arrow_down: Download queue
* :bullettrain_side: HEVC Transcoding (With NVIDIA Hardware Acceleration support)

## :information_source: Usage

### :warning: Prerequisite
* FFmpeg ([Master build](https://github.com/BtbN/FFmpeg-Builds/releases) recommended, for full Anime4K support)
* [YT-DLP](https://github.com/yt-dlp/yt-dlp)

To run Wasari, all you need is a shell:

`.\Wasari crunchy <Series-URL> -o <Output-Directory>`

You can check the full supported arguments list using the `--help` argument.

## :+1: Credits

* [FFmpeg](https://git.ffmpeg.org/ffmpeg.git) - For video decoding/transcoding
* [YT-DLP](https://github.com/yt-dlp/yt-dlp) - For legacy crunchyroll downloading, and m3u8 streams from crunchyroll API
* [CliFx](https://github.com/Tyrrrz/CliFx) - For arguments parsing

## :construction: Roadmap

- [ ] Funimation support
- [ ] Nyaa.si support
- [ ] Better Season/Episode tagging
