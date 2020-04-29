## Commands
* /mini - Spawns the minicopter.
* /nomini - Despawns their minicopter and spawns a new one.

## Permissions
* `spawnmini.mini` - /mini command
* `spawnmini.nomini` - /nomini command
* `spawnmini.nocd` - no cooldown on /mini

## Configuration
```
{
  "assetPrefab": "assets/content/vehicles/minicopter/minicopter.entity.prefab", // The asset that you would like to spawn.
  "cooldownTime": 86400.0, // The cooldown timer for the command.
  "maxSpawnDistance": 5.0 // How far a user is allowed to spawn a minicopter away from them.
}
```