# PoE Asset Updater

PoE Asset Updater is a CLI tool built specifically to update the PoE asset files of the [PoE Overlay](https://github.com/PoE-Overlay-Community/PoE-Overlay-Community-Fork)

<!-- TOC -->
- [Usage](#usage)
- [Development](#development)
- [License](#license)
- [Acknowledgments](#acknowledgments)
<!-- /TOC -->

## Usage

Use a Steam installation (because it has an unpacked Content.ggpk)
Download the CLI tool and run it using the following command:  
```powershell
> PoEAssetUpdater.exe "C:\Steam PoE Folder\Bundles2" "C:\output" "C:\local-static-poe" "C:\Repos\PoE-Asset-Updater\Resources\stable.py"
```

## Development

The project is written in C# and outputs an executable CLI.

You'll need Visual Studio or VSCode and .NET 5 (exactly this version),
which you can get [here](https://dotnet.microsoft.com/download/dotnet).
Get the SDK, not the Runtime.

**Run tests**

```powershell
> dotnet test
```

**Compile the app**

```powershell
> dotnet build
```

Check `bin\` for binaries.

**Run the app**

Use a Steam installation (because it has an unpacked Content.ggpk)

```powershell
> PoEAssetUpdater.exe "C:\Steam PoE Folder\Bundles2" "C:\output" "C:\local-static-poe" "C:\Repos\PoE-Asset-Updater\Resources\stable.py"
```

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details

## Acknowledgments

* [Grinding Gear Games](https://www.pathofexile.com/) the game
* [libggpk](https://github.com/MuxaJIbI4/libggpk) parsing PoE's Content.ggpk
* [libOoz](https://github.com/zao/ooz) parsing PoE's Bundle format
* [Omega2K - PyPoE](https://github.com/OmegaK2/PyPoE) for providing the initial .dat definitions
* [brather1ng - PyPoE fork](https://github.com/brather1ng/PyPoE) for providing .dat definitions
* [PoE Tool Devs](https://github.com/poe-tool-dev/dat-schema) for providing .dat definitions
