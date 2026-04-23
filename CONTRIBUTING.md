## Contributing
If you'd like to contribute, please keep in mind that:
1. This mod is meant to work in Rain Meadow lobbies even if not everyone has it installed
2. Any potential new features need to be directly related to slugcats reviving or being revived
3. Features that could potentially cause issues in Rain Meadow lobbies can still be added for non-Meadow sessions only (wrap relevant code around an if statement like so: `if (!meadowEnabled || (meadowEnabled && !Meadow.Meadow.IsOnlineSession())`)
4. Due the above points, you should have at least a basic grasp of how Rain Meadow works and what may or may not be an issue in Rain Meadow lobbies

## Required assembly references
* `Rain World\BepInEx\PUBLIC-Assembly-CSharp.dll`
* `Rain World\BepInEx\plugins\HOOKS-Assembly-CSharp.dll`
* `Rain World\BepInEx\core\BepInEx.dll`
* `Rain World\BepInEx\core\MonoMod.RuntimeDetour.dll`
* `Rain World\BepInEx\core\MonoMod.Utils.dll`
* `Rain World\RainWorld_Data\Managed\Mono.Cecil.dll`
* `Rain World\RainWorld_Data\Managed\Unity.Mathematics.dll`
* `Rain World\RainWorld_Data\Managed\UnityEngine.dll`
* `Rain World\RainWorld_Data\Managed\UnityEngine.CoreModule.dll`
* `Rain Meadow.dll`

Rain Meadow.dll can be found in one of these directories:
* `steamapps\workshop\content\312520\3388224007\plugins\ (if installed through Steam Workshop)`
* `Rain World\RainWorld_Data\StreamingAssets\mods\rainmeadow\plugins\ (if installed manually)`
