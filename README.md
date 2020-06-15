# PoE Asset Updater

PoE Asset Updater is a CLI tool built specifically to update the PoE asset files of the [PoE Overlay](https://github.com/PoE-Overlay-Community/PoE-Overlay-Community-Fork)

<!-- TOC -->
- [Usage](#usage)
- [Development](#development)
- [License](#license)
- [Acknowledgments](#acknowledgments)
<!-- /TOC -->

## Usage

Download the CLI tool and run it using the following command:  
`PoEAssetUpdater <path-to-Content.ggpk> <asset-output-directory>`

## Development

The project is written in C# and outputs an executable CLI.

You'll need Visual Studio or VSCode and .NET Framework v4.7.1 (exactly this version),
which you can get [here](https://dotnet.microsoft.com/download/dotnet-framework).
Get the Developer Pack, not the Runtime.

Note: We should port this from .NET Framework to .NET Core for better multiplatform support.

**Run tests**

```powershell
> dotnet build
```

**Compile the app**

```powershell
> dotnet build
```

Check `bin\` for binaries.

**Run the app**

```powershell
> mkdir output
> PoEAssetUpdater.exe G:\PoE\Content.ggpk output
```

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details

## Acknowledgments

* [Grinding Gear Games](https://www.pathofexile.com/) the game
* [libggpk](https://github.com/MuxaJIbI4/libggpk) parsing content.ggpk