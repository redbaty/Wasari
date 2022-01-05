<p align="center">
  <img width="256" align="center" src="/assets/logo/logo.png">
</p>
<p align="center">
  Anime Downloader
</p>
<p align="center">  
  <a style="text-decoration:none" href="https://github.com/redbaty/Wasari/actions/workflows/dotnet-core.yml">
    <img src="https://img.shields.io/github/workflow/status/redbaty/wasari/.NET%20Core?style=flat-square" alt="Build Status" />
  </a>
  <a style="text-decoration:none" href="https://github.com/redbaty/Wasari/releases">
    <img src="https://img.shields.io/github/release/redbaty/wasari.svg?label=Latest%20version&style=flat-square" alt="Latest version" />
  </a>
</p>

## Introduction

Wasari hopes to make it easy to download anime shows from popular streaming services.

## Features
* <img src="/assets/icons/cast-connected.svg"> Crunchyroll Support
* <img src="/assets/icons/video-4k-box.svg"> Anime4K Support
* <img src="/assets/icons/subtitles.svg"> Soft subs encoding
* <img src="/assets/icons/download-box.svg"> Download queue
* <img src="/assets/icons/expansion-card.svg"> HEVC Transcoding (With NVIDIA Hardware Acceleration support)

### Prerequisite
* FFmpeg ([Master build](https://github.com/BtbN/FFmpeg-Builds/releases) recommended, for full Anime4K support)
* [YT-DLP](https://github.com/yt-dlp/yt-dlp)

### Usage

To run Wasari, all you need is a shell:

`.\Wasari crunchy <Series-URL> -o <Output-Directory>`

You can check the full supported arguments list using the `--help` argument.

## Credits

* [FFmpeg](https://git.ffmpeg.org/ffmpeg.git) - For video decoding/transcoding
* [YT-DLP](https://github.com/yt-dlp/yt-dlp) - For legacy crunchyroll downloading, and m3u8 streams from crunchyroll API
* [CliFx](https://github.com/Tyrrrz/CliFx) - For arguments parsing

## 🚧 Roadmap

- [ ] Funimation support
- [ ] Nyaa.si support
- [ ] Better Season/Episode tagging