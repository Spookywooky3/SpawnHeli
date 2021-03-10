## Permissions

* `spawnmini.mini`  -- Allows player to spawn a minicopter with `/mymini`
* `spawnmini.nocd` -- Gives player no cooldown for `/mymini` chat command
* `spawnmini.nomini` -- Allows player to use `/nomini` chat command
* `spawnmini.nodecay` -- Doesn't allow the player's minicopter to decay
* `spawnmini.unlimitedfuel` -- Gives the player unlimited fuel when they spawn their minicopter
* `spawnmini.fmini` -- Allows player to use `/fmini` chat command
* `spawnmini.fuel.<amount>` -- Determines the amount of fuel the player's minicopter spawns with, overriding the `FuelAmount` config value
  * To use this, you must first add the desired amount in the `FuelAmountsRequiringPermission` config option

## Chat Commands

* `/mymini` -- Spawn a minicopter
* `/nomini` -- Despawn your minicopter
* `/fmini` -- Fetch your minicopter

## Server Commands

* `spawnmini.give <name or steamid>` -- Spawn a minicopter for a specific player using their name or steamid64
* `spawnmini.give <name or steamid> <x> <y> <z>` -- Spawn a minicopter for a specific player at the designated coordinates

## For Developers

```csharp
void SpawnMinicopter(BasePlayer);
float GetDistance(BasePlayer, MiniCopter);
bool IsPlayerOwned(MiniCopter);
```

## Configuration

Default configuration:
```json
{
  "AssetPrefab": "assets/content/vehicles/minicopter/minicopter.entity.prefab",
  "CanDespawnWhileOccupied": false,
  "CanFetchWhileOccupied": false,
  "CanSpawnBuildingBlocked": false,
  "FuelAmount": 0,
  "FuelAmountsRequiringPermission": [],
  "MaxNoMiniDistance": -1.0,
  "MaxSpawnDistance": 5.0,
  "UseFixedSpawnDistance": true,
  "OwnerAndTeamCanMount": false,
  "DefaultCooldown": 86400.0,
  "PermissionCooldowns": {
    "spawnmini.tier1": 43200.0,
    "spawnmini.tier2": 21600.0,
    "spawnmini.tier3": 10800.0
  },
  "SpawnHealth": 750.0,
  "DestroyOnDisconnect": false
}
```

Options explained:
* `CanDespawnWhileOccupied` (`true` or `false`) -- Whether to allow players to use `/nomini` while their minicopter is mounted. Regardless of this setting, players cannot despawn their minicopter while they are mounted on it.
* `CanFetchWhileOccupied` (`true` or `false`) -- Whether to allow players to use `/fmini` while the minicopter is mounted. Mounted players will be dismounted automatically. Regardless of this setting, players cannot fetched their minicopter while they are mounted on it.
* `CanSpawnBuildingBlocked` (`true` or `false`) -- Whether to allow players to spawn a minicopter while building blocked.
* `FuelAmount` -- Amount of low grade fuel to add to minicopters when spawned. Set to `-1` for max stack size (which depends on the server, but is 500 in vanilla). Does not apply to minicopters spawned for players who have the `spawnmini.unlimitedfuel` permission.
* `FuelAmountsRequiringPermission` -- Use this to customize the fuel amount for different player groups. Simply add the desire fuel amounts like `[100, 200]`, and reload the plugin to generate permissions of the format `spawnmini.fuel.<amount>`. Granting one to a player will cause their mini to spawn with that amount of low grade fuel, overriding the default `FuelAmount`. If you grant multiple of these permission to a player, the last will apply, based on the order in the config.
* `MaxNoMiniDistance` -- The maximum distance players can be from their minicopter to use `/nomini` or `/fmini`. Set to `-1` to allow those commands at unlimited distance.
* `MaxSpawnDistance` -- The maximum distance away that players are allowed to spawn their minicopter.
* `UseFixedSpawnDistance` (`true` or `false`) -- Set to `true` to cause minicopters to spawn directly in front of players at a fixed distance, disregarding the `MaxSpawnDistance` setting. Performs no terrain checks.
* `OwnerAndTeamCanMount` (`true` or `false`) -- Set to `true` to only allow the owner and their team members to be able to mount the minicopter.
* `DefaultCooldown` -- The default spawn cooldown that will apply to players who have not been granted any permissions in `PermissionCooldowns`.
* `PermissionCooldowns` -- Use these settings to customize cooldowns for different player groups. For example, set `"spawnmini.tier1": 3600.0` and then grant the `spawnmini.tier1` permission to a group of players to assign them a 1 hour cooldown for spawning their minicopters.
  * If a player has multiple cooldown permissions, the lowest is used.
  * If a player has no cooldown permissions, `DefaultCooldown` will be used for them.
  * You can add as many cooldown tiers as you would like, but you should prefix them all with `spawnmini.` to prevent warnings in the server logs.
* `SpawnHealth` -- The health minicopters will spawn with.
* `DestroyOnDisconnect` (`true` or `false`) -- Set to `true` to destroy each spawned minicopter when its owner disconnects from the server. If the minicopter is mounted, it will be destroyed when fully dismounted.

## Localization

Spawn Mini supports English, Russian, and German; and you can also add more languages.

## Future Plans

* `/findmini` command that displays the bearing and distance of the minicopter

If you have any ideas/suggestions I would love to hear them. If you have any issues use the Plugin Support section.

## Developer Hooks

```csharp
object OnMyMiniSpawn(BasePlayer player)
```
- Called when a player uses `/mymini`
- Returning `false` will prevent spawning the minicopter
- Returning `null` will result in the default behavior

```csharp
object OnMyMiniFetch(BasePlayer player, MiniCopter mini)
```
- Called when a player uses `/fmini`
- Returning `false` will prevent fetching the minicopter
- Returning `null` will result in the default behavior

```csharp
object OnMyMiniDespawn(BasePlayer player, MiniCopter mini)
```
- Called when a player uses `/nomini`
- Returning `false` will prevent despawning the minicopter
- Returning `null` will result in the default behavior

## Credits

* **SpooksAU**, the current maintainer
* **WhiteThunder**, put in heaps of work while i was unable to
* **BuzZ**, the original author of MyMiniCopter
* **rfc1920**, for helping maintain the plugin
* **Texas/Alice**, for the German translations
* **Glasiore**, for the Russian translations
