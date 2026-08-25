# Opus Music Collection Converter

## Introduction and purpose

Ever had a large collection of music (or other audio like audiobooks) and wanted to copy it over to another device for portable listening, to your phone or laptop, but encountered these problems?:

* The collection is too large to copy over
* Even after using a convertor program (like FFMPEG or in my case, foobar2000), the contents of the output is missing files like covers and other misc files and only contains the bare audio files you wanted
* The output isn't in the same directory format as the input?

Well, I have the solution for you!

This script, which I whipped up after giving up with manually converting music and copying to my phone, lets you target a directory full of all your music and related files and convert them over (in OPUS format) to an output directory of your choosing.

## Features

- **Maintains Folder Structure**: Replicates your source directory tree exactly, ensuring albums and artists stay organised in line with your input directory.
- **Copies Non-Audio Files**: Album artwork (`.jpg`, `.png`), cue sheets, and text files are preserved and copied to the destination. 


## How to run

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [FFmpeg](https://ffmpeg.org/download.html) installed and added to your system PATH.

### Execution
Open your terminal or Developer PowerShell and run the program using the .NET CLI:

```
dotnet run -- "C:\Path\To\SourceMusic" "C:\Path\To\Destination" --bitrate 128k
```
*(Note: The `--` is required so the CLI knows which arguments belong to the application rather than the dotnet tool.)*

**Or, run the compiled executable directly:**

```
.\MusicCollectionConverter.exe "C:\Path\To\SourceMusic" "C:\Path\To\Destination" -b 128k
```
