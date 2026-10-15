# CosmeticArmor

Remember Cosmetic Armor from Minecraft? It's in Valheim now. An add-on for ExtraSlots with 6 cosmetic slots (helmet, chest, legs, cape, utility, trinket). Keep your best armor's stats and show any look you like. Please report bugs in Issues.

A Valheim mod by Onononoki. Get it on Thunderstore: https://thunderstore.io/c/valheim/p/Onononoki/CosmeticArmor/

The full description, settings and changelog are in [package/README.md](package/README.md) and [package/CHANGELOG.md](package/CHANGELOG.md).

## Bug reports and feedback

Bug reports and ideas: open an issue at https://github.com/Ononononoki/CosmeticArmor/issues. If something breaks, please add your mod list and the BepInEx log (`BepInEx/LogOutput.log`).

## Building

Needs the .NET SDK, Valheim and a mod manager profile with BepInEx and ExtraSlots.

```
dotnet build -c Release -p:GamePath="<Valheim folder>" -p:ProfilePath="<mod manager profile folder>"
```

The dll is built into `bin/Release/net48` and copied to `package/`.

## License

MIT, see [LICENSE](LICENSE).
