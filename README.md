## Permissions

* `spawnmini.mini`  -- Allows player to spawn a minicopter with `/mymini`
* `spawnmini.nocd` -- Gives player no cooldown for `/mymini` chat command
* `spawnmini.nomini` -- Allows player to use `/nomini` chat command 
* `spawnmini.nodecay` -- Doesn't allow the player's minicopter to decay
* `spawnmini.unlimitedfuel` -- Gives the player unlimited fuel when they spawn their minicopter
* `spawnmini.fmini` -- Allows player to use `/fmini` chat command
## Chat Commands

* `/mymini` -- Spawn a minicopter
* `/nomini` -- Despawn your minicopter
* `/fmini` -- Fetch your minicopter

## For Developers

```csharp
void SpawnMinicopter(BasePlayer/string);
float GetDistance(BasePlayer, MiniCopter);
bool IsPlayerOwned(MiniCopter);
```

## Configuration

```json
{
  "AssetPrefab": "assets/content/vehicles/minicopter/minicopter.entity.prefab", -- Prefab you would like to spawn
  "CanSpawnBuildingBlocked": false,  -- Can player spawn a minicopter while building blocked
  "MaxNoMiniDistance": 300.0, -- The maximum distance the player can be from the minicopter when using /nomini and /fmini
  "MaxSpawnDistance": 5.0, -- How far away can the player spawn a minicopter
  "OwnerAndTeamCanMount": false, -- If you want only the owner and their team members to be able to mount the mini set this to true
  "PermissionCooldowns": { -- These are the cooldown tiers feel free to add/change as many as you like just make sure users only have one for now
    "spawnmini.tier1": 86400.0,
    "spawnmini.tier2": 43200.0,
    "spawnmini.tier3": 21600.0
  },
  "SpawnHealth": 750.0 -- The health the minicopter spawns with
}
```

## Localization

Spawn Mini supports English, Russian, and German; and you can also add more languages.

## Future Plans

* `/findmini` command that displays the bearing and distance of the minicopter

If you have any ideas/suggestions I would love to hear them. Use the Plugin Support or add me on discord Spooks#1328.

## Credits

* **SpooksAU**, the current maintainer
* **BuzZ**, the original author of MyMiniCopter
* **rfc1920**, for helping maintain the plugin
* **Texas/Alice**, for the German translations
* **Glasiore**, for the Russian translations
