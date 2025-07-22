## Features

- Allows players to spawn, fetch and despawn personal helicopters
- Supports Minicopters, Scrap Transport Helicopters, and Attack Helicopters
- Allows configurable cooldowns using permissions
- Allows preventing decay damage to personal helicopters
- Allows spawning personal helicopters with configurable fuel
- Allows personal helicopters to function without fuel
- Allows personal helicopters to be auto mounted when spawned or fetched
- Allows personal helicopters to takeoff instantly
- Allows configuring multiple spawn/fetch/despawn commands
- Allows automatically fetching helicopters, as well as optionally repairing them, when using the spawn command, to simplify the experience for players

## Quick start

### Allow all players to spawn Minicopters

1. To allow all players to spawn a Minicopter, run the command `o.grant group default spawnheli.minicopter.spawn`.
2. To allow all players to fetch their existing Minicopter, run the command `o.grant group default spawnheli.minicopter.fetch`.
3. To allow all players to despawn their existing Minicopter, run the command `o.grant group default spawnheli.minicopter.despawn`.
4. To make it so the `/mymini` command automatically fetches the player's existing Minicopter if they have one, set `Minicopter` -> `Auto fetch` to `true` in the config and reload the plugin.
5. To make it so fetching an existing Minicopter automatically repairs it, set `Minicopter` -> `Repair on fetch` to `true` in the config and reload the plugin.

Following the above steps, players will be able to spawn their Minicopter as often as every hour by default, but that can be changed in the configuration at `Minicopter` -> `Spawn cooldowns` -> `Default cooldown (seconds)`.

## Migrating from v2 to v3

This plugin has recently been redesigned to support spawning multiple types of helicopters. Previously it only allowed spawning Minicopters. Here are the steps you should take to update to v3.

1. Delete plugins/SpawnMini.cs
2. Copy config/SpawnMini.json to config/SpawnHeli.json
3. Add plugins/SpawnHeli.cs
4. Review config/SpawnHeli.json to verify everything looks right, make changes as necessary, and reload SpawnHeli if you make changes
5. Grant permissions as necessary

All of the plugin permission have been renamed. Here are the highlights.

- `spawnmini.mini` -> `spawnheli.minicopter.spawn`
- `spawnmini.nocd` -> `spawnheli.minicopter.nocooldown`
- `spawnmini.nomini` -> `spawnheli.minicopter.despawn`
- `spawnmini.nodecay` -> `spawnheli.minicopter.nodecay`
- `spawnmini.unlimitedfuel` -> `spawnheli.minicopter.unlimitedfuel`
- `spawnmini.fmini` -> `spawnheli.minicopter.fetch`
- `spawnmini.fuel.<amount>` -> `spawnheli.minicopter.fuel.<amount>` (replace `<amount>` with a number like `100`)
- Spawn/fetch cooldown permissions are more complicated. Previously, you could define an arbitrary permission like `spawnmini.<whatever>`, which will now be `spawnheli.minicopter.cooldown.spawn.<whatever>`. or `spawnheli.minicopter.cooldown.fetch.<whatever>`.

**Note:** Some servers dynamically grant permissions to players or groups via other plugins or external systems. If you are doing that, you will need to update the configurations of those plugins or external systems to refer to the new permissions.

**Caution:** If you use a system or plugin (like Timed Permissions) to grant permissions to **specific players** (less of a concern for assigning permissions to groups) for a limited time, it might not be straight forward for you to back-fill the new permissions to the players who have been temporarily assigned the old permissions. If you are concerned about this case, you should plan to delay the v3 migration until an upcoming server wipe (assuming the temporary permissions would be revoked when the server is wiped).

## Permissions

### Basic permissions

Allow the player to spawn a helicopter (e.g., `/mymini`, `/myheli`, `/myattack`):
- `spawnheli.all.spawn`
- `spawnheli.minicopter.spawn`
- `spawnheli.scraptransport.spawn`
- `spawnheli.attackhelicopter.spawn`

Allow the player to fetch their existing helicopter (e.g,. `/fmini`, `/fheli`, `/fattack`):
- `spawnheli.all.fetch`
- `spawnheli.minicopter.fetch`
- `spawnheli.scraptransport.fetch`
- `spawnheli.attackhelicopter.fetch`

Note: The fetch permissions are required to make the spawn command fetch the player's existing helicopter when using the `"Auto fetch": true` configuration option.

Allow the player to despawn their existing helicopter (e.g., `/nomini`, `/noheli`, `/noattack`):
- `spawnheli.all.despawn`
- `spawnheli.minicopter.despawn`
- `spawnheli.scraptransport.despawn`
- `spawnheli.attackhelicopter.despawn`

Allow the player's helicopter to function without fuel:
- `spawnheli.all.unlimitedfuel`
- `spawnheli.minicopter.unlimitedfuel`
- `spawnheli.scraptransport.unlimitedfuel`
- `spawnheli.attackhelicopter.unlimitedfuel`

Disable decay damage to the player's helicopter:
- `spawnheli.all.nodecay`
- `spawnheli.minicopter.nodecay`
- `spawnheli.scraptransport.nodecay`
- `spawnheli.attackhelicopter.nodecay`

Allow the player to spawn their helicopter as often as they would like:
- `spawnheli.all.nocooldown`
- `spawnheli.minicopter.nocooldown`
- `spawnheli.scraptransport.nocooldown`
- `spawnheli.attackhelicopter.nocooldown`

**Caution:** Allowing helicopters to be respawned too frequently could be abused by players to reduce server performance. Alternatively, you can rely on the default cooldown configuration option, and/or grant non-zero cooldowns using permissions (see below).

Allow the player to auto mount their helicopter when spawning or fetching it (requires feature to be enabled in the config, and permissions are only necessary if configured to require permission):
- `spawnheli.all.automount`
- `spawnheli.minicopter.automount`
- `spawnheli.scraptransport.automount`
- `spawnheli.attackhelicopter.automount`

Allow the player's helicopter to takeoff instantly, bypassing engine startup time (requires feature to be enabled in the config, and permissions are only necessary if configured to require permission):
- `spawnheli.all.instanttakeoff`
- `spawnheli.minicopter.instanttakeoff`
- `spawnheli.scraptransport.instanttakeoff`
- `spawnheli.attackhelicopter.instanttakeoff`

### Fuel permissions

The following permissions come with the plugin's default configuration. Each one determines how much fuel the helicopter spawns with.

Minicopters:
- `spawnheli.minicopter.fuel.100` -- 100 fuel.
- `spawnheli.minicopter.fuel.500` -- 500 fuel.
- `spawnheli.minicopter.fuel.1000` -- 1000 fuel.

Scrap Transport Helicopters:
- `spawnheli.scraptransport.fuel.100` -- 100 fuel.
- `spawnheli.scraptransport.fuel.500` -- 500 fuel.
- `spawnheli.scraptransport.fuel.1000` -- 1000 fuel.

Attack Helicopters:
- `spawnheli.attackhelicopter.fuel.100` -- 100 fuel.
- `spawnheli.attackhelicopter.fuel.500` -- 500 fuel.
- `spawnheli.attackhelicopter.fuel.1000` -- 1000 fuel.

Additional permissions may be defined by adding them to `Fuel profiles requiring permission` per vehicle type in the configuration.

**Note:** If a player is granted multiple fuel permissions for a given helicopter type, the last one will apply, according to the profile order in the configuration.

**Caution:** Allowing helicopters to spawn with fuel could be abused to generate lots of fuel if the player is granted a low spawn cooldown. As an alternative to spawning helicopters with fuel, consider granting the `spawnheli.minicopter.unlimitedfuel` permission.

### Spawn cooldowns

The following permissions come with the plugin's default configuration. Each one determines how often players can spawn helicopters.

Minicopters:
- `spawnheli.minicopter.cooldown.spawn.1hr` -- 1 hour.
- `spawnheli.minicopter.cooldown.spawn.10m` -- 10 minutes.
- `spawnheli.minicopter.cooldown.spawn.10s` -- 10 seconds.

Scrap Transport Helicopters:
- `spawnheli.scraptransport.cooldown.spawn.1hr` -- 1 hour.
- `spawnheli.scraptransport.cooldown.spawn.10m` -- 10 minutes.
- `spawnheli.scraptransport.cooldown.spawn.10s` -- 10 seconds.

Attack Helicopters:
- `spawnheli.attackhelicopter.cooldown.spawn.1hr` -- 1 hour.
- `spawnheli.attackhelicopter.cooldown.spawn.10m` -- 10 minutes.
- `spawnheli.attackhelicopter.cooldown.spawn.10s` -- 10 seconds.

Additional permissions may be defined by adding them to `Spawn Cooldowns` -> `Cooldown profiles requiring permission` per vehicle type in the configuration.

**Note:** If a player is granted multiple spawn cooldown permissions for a given helicopter type, the last one will apply, according to the profile order in the configuration.

**Caution:** Allowing helicopters to be respawned too frequently could be abused by players to reduce server performance. Instead of allowing helicopters to be spawned frequently, consider granting the fetch permission, setting a reasonable fetch cooldown, enabling `"Auto fetch": true`, and enabling `"Repair on fetch": true`.

### Fetch cooldowns

The following permissions come with the plugin's default configuration. Each one determines how often players can fetch their existing helicopters.

Minicopters:
- `spawnheli.minicopter.cooldown.fetch.1hr` -- 1 hour.
- `spawnheli.minicopter.cooldown.fetch.10m` -- 10 minutes.
- `spawnheli.minicopter.cooldown.fetch.10s` -- 10 seconds.

Scrap Transport Helicopters:
- `spawnheli.scraptransport.cooldown.fetch.1hr` -- 1 hour.
- `spawnheli.scraptransport.cooldown.fetch.10m` -- 10 minutes.
- `spawnheli.scraptransport.cooldown.fetch.10s` -- 10 seconds.

Attack Helicopters:
- `spawnheli.attackhelicopter.cooldown.fetch.1hr` -- 1 hour.
- `spawnheli.attackhelicopter.cooldown.fetch.10m` -- 10 minutes.
- `spawnheli.attackhelicopter.cooldown.fetch.10s` -- 10 seconds.

Additional permissions may be defined by adding them to `Fetch Cooldowns` -> `Cooldown profiles requiring permission` per vehicle type in the configuration.

**Note:** If a player is granted multiple fetch cooldown permissions, the last one will apply, according to the profile order in the configuration.

## Commands

The following commands will spawn your helicopter. If `Auto fetch` is enabled for that helicopter type, these commands will fetch your existing helicopter if you have one.

- `mymini` -- Spawn your Minicopter.
- `myheli` -- Spawn your Scrap Transport Helicopter.
- `myattack` -- Spawn your Attack Helicopter.

The following commands will fetch your existing helicopter.

- `fmini` -- Fetch your Minicopter.
- `fheli` -- Fetch your Scrap Transport Helicopter.
- `fattack` -- Fetch your Attack Helicopter.

The following commands will despawn your existing helicopter.

- `nomini` -- Despawn your Minicopter.
- `noheli` -- Despawn your Scrap Transport Helicopter.
- `noattack` -- Despawn your Attack Helicopter.

The spawn/fetch/despawn commands can be changed in the configuration. Additionally, you can define multiple commands to perform the same function in case players are accustomed to different commands from other servers.

## Server Commands

The following server commands can be used to spawn a helicopter for a specific player using their name or 64-bit Steam ID (recommended).

- `spawnheli.minicopter.give <name or steamid>`
- `spawnheli.scraptransport.give <name or steamid>`
- `spawnheli.attackhelicopter.give <name or steamid>`

The following variations allow spawning the helicopter at designated coordinates.

- `spawnheli.minicopter.give <name or steamid> <x> <y> <z>`
- `spawnheli.scraptransport.give <name or steamid> <x> <y> <z>`
- `spawnheli.attackhelicopter.give <name or steamid> <x> <y> <z>`

**Note:** The `spawnheli.minicopter.give` command has the alternative name `spawnmini.give` for backwards compatibility.

**Caution:** Integrating these commands with a custom shop could potentially lead to players wasting a purchase if they already have the helicopter spawned because this command will have no effect in that case. You can advise players to try to fetch their helicopter before attempting to purchase one to avoid this issue (e.g., in case they forgot they currently have one spawned).

## Configuration

Default configuration:

```json
{
  "Admin debug bounds": false,
  "Limit players to one helicopter type at a time": false,
  "Try to auto despawn other helicopter types": false,
  "Minicopter": {
    "Spawn commands": [
      "mymini"
    ],
    "Fetch commands": [
      "fmini"
    ],
    "Despawn commands": [
      "nomini"
    ],
    "Can despawn while occupied": false,
    "Can fetch while occupied": false,
    "Can spawn while building blocked": false,
    "Can fetch while building blocked": false,
    "Auto fetch": false,
    "Repair on fetch": false,
    "Max spawn distance": 5.0,
    "Max fetch distance": -1.0,
    "Max despawn distance": -1.0,
    "Fixed spawn distance": {
      "Enabled": true,
      "Distance from player": 3.0,
      "Helicopter rotation angle": 90.0
    },
    "Auto mount": {
      "Enabled": false,
      "Require permission": false
    },
    "Instant takeoff": {
      "Enabled": false,
      "Require permission": false
    },
    "Only owner and team can mount": false,
    "Spawn health": 750.0,
    "Destroy on disconnect": false,
    "Fuel": {
      "Default fuel amount": 0,
      "Fuel profiles requiring permission": [
        {
          "Fuel amount": 100,
          "Permission suffix": "100"
        },
        {
          "Fuel amount": 500,
          "Permission suffix": "500"
        },
        {
          "Fuel amount": 1000,
          "Permission suffix": "1000"
        }
      ]
    },
    "Spawn cooldowns": {
      "Default cooldown (seconds)": 3600.0,
      "Cooldown profiles requiring permission": [
        {
          "Cooldown (seconds)": 3600.0,
          "Permission suffix": "1hr"
        },
        {
          "Cooldown (seconds)": 600.0,
          "Permission suffix": "10m"
        },
        {
          "Cooldown (seconds)": 10.0,
          "Permission suffix": "10s"
        }
      ]
    },
    "Fetch cooldowns": {
      "Default cooldown (seconds)": 10.0,
      "Cooldown profiles requiring permission": [
        {
          "Cooldown (seconds)": 3600.0,
          "Permission suffix": "1hr"
        },
        {
          "Cooldown (seconds)": 600.0,
          "Permission suffix": "10m"
        },
        {
          "Cooldown (seconds)": 10.0,
          "Permission suffix": "10s"
        }
      ]
    }
  },
  "ScrapTransportHelicopter": {
    "Spawn commands": [
      "myheli"
    ],
    "Fetch commands": [
      "fheli"
    ],
    "Despawn commands": [
      "noheli"
    ],
    "Can despawn while occupied": false,
    "Can fetch while occupied": false,
    "Can spawn while building blocked": false,
    "Can fetch while building blocked": false,
    "Auto fetch": false,
    "Repair on fetch": false,
    "Max spawn distance": 5.0,
    "Max fetch distance": -1.0,
    "Max despawn distance": -1.0,
    "Fixed spawn distance": {
      "Enabled": true,
      "Distance from player": 3.0,
      "Helicopter rotation angle": 90.0
    },
    "Auto mount": {
      "Enabled": false,
      "Require permission": false
    },
    "Instant takeoff": {
      "Enabled": false,
      "Require permission": false
    },
    "Only owner and team can mount": false,
    "Spawn health": 1000.0,
    "Destroy on disconnect": false,
    "Fuel": {
      "Default fuel amount": 0,
      "Fuel profiles requiring permission": [
        {
          "Fuel amount": 100,
          "Permission suffix": "100"
        },
        {
          "Fuel amount": 500,
          "Permission suffix": "500"
        },
        {
          "Fuel amount": 1000,
          "Permission suffix": "1000"
        }
      ]
    },
    "Spawn cooldowns": {
      "Default cooldown (seconds)": 3600.0,
      "Cooldown profiles requiring permission": [
        {
          "Cooldown (seconds)": 3600.0,
          "Permission suffix": "1hr"
        },
        {
          "Cooldown (seconds)": 600.0,
          "Permission suffix": "10m"
        },
        {
          "Cooldown (seconds)": 10.0,
          "Permission suffix": "10s"
        }
      ]
    },
    "Fetch cooldowns": {
      "Default cooldown (seconds)": 10.0,
      "Cooldown profiles requiring permission": [
        {
          "Cooldown (seconds)": 3600.0,
          "Permission suffix": "1hr"
        },
        {
          "Cooldown (seconds)": 600.0,
          "Permission suffix": "10m"
        },
        {
          "Cooldown (seconds)": 10.0,
          "Permission suffix": "10s"
        }
      ]
    }
  },
  "AttackHelicopter": {
    "Spawn commands": [
      "myattack"
    ],
    "Fetch commands": [
      "fattack"
    ],
    "Despawn commands": [
      "noattack"
    ],
    "Can despawn while occupied": false,
    "Can fetch while occupied": false,
    "Can spawn while building blocked": false,
    "Can fetch while building blocked": false,
    "Auto fetch": false,
    "Repair on fetch": false,
    "Max spawn distance": 5.0,
    "Max fetch distance": -1.0,
    "Max despawn distance": -1.0,
    "Fixed spawn distance": {
      "Enabled": true,
      "Distance from player": 3.0,
      "Helicopter rotation angle": 90.0
    },
    "Auto mount": {
      "Enabled": false,
      "Require permission": false
    },
    "Instant takeoff": {
      "Enabled": false,
      "Require permission": false
    },
    "Only owner and team can mount": false,
    "Spawn health": 850.0,
    "Destroy on disconnect": false,
    "Fuel": {
      "Default fuel amount": 0,
      "Fuel profiles requiring permission": [
        {
          "Fuel amount": 100,
          "Permission suffix": "100"
        },
        {
          "Fuel amount": 500,
          "Permission suffix": "500"
        },
        {
          "Fuel amount": 1000,
          "Permission suffix": "1000"
        }
      ]
    },
    "Spawn cooldowns": {
      "Default cooldown (seconds)": 3600.0,
      "Cooldown profiles requiring permission": [
        {
          "Cooldown (seconds)": 3600.0,
          "Permission suffix": "1hr"
        },
        {
          "Cooldown (seconds)": 600.0,
          "Permission suffix": "10m"
        },
        {
          "Cooldown (seconds)": 10.0,
          "Permission suffix": "10s"
        }
      ]
    },
    "Fetch cooldowns": {
      "Default cooldown (seconds)": 10.0,
      "Cooldown profiles requiring permission": [
        {
          "Cooldown (seconds)": 3600.0,
          "Permission suffix": "1hr"
        },
        {
          "Cooldown (seconds)": 600.0,
          "Permission suffix": "10m"
        },
        {
          "Cooldown (seconds)": 10.0,
          "Permission suffix": "10s"
        }
      ]
    }
  }
}
```

The following options are global, not tied to a specific helicopter type.

- `Admin debug bounds` (`true` or `false`) -- Determines whether to display visual bounding boxes when an admin attempts to spawn a helicopter. The bounding boxes are only visible to the admin who attempted to spawn the helicopter. This can be useful when debugging "Not enough space" messages.
- `Limit players to one helicopter type at a time` (`true` or `false`) -- Determines whether each player must first destroy their other helicopter(s) before they can spawn a new one. For example, while `true`, a player with a Minicopter will not be able to spawn an Attack Helicopter or Scrap Transport Helicopter until the Minicopter is destroyed.
- `Try to auto despawn other helicopter types`  (`true` or `false`) -- Determines whether attempting to spawn a helicopter will automatically try to despawn the player's other helicopter(s). For example, while `true`, when a player with a Minicopter attempts to spawn an Attack Helicopter, their existing Minicopter will be automatically despawned if possible. In some cases, despawning the existing helicopter is not possible (e.g., if that helicopter is occupied, and despawning that helicopter type while occupied is disallowed according to the plugin configuration), in which case the player will simply be informed that they must first destroy their existing helicopter(s).
  - Note: This option only takes effect while `Limit players to one helicopter type at a time` is also `true`.

Each vehicle section (`Minicopter`, `ScrapTransportHelicopter`, and `AttackHelicopter`) has the following options.

- `Spawn commands` -- Determines which commands can be used to spawn the heli.
- `Fetch commands` -- Determines which commands can be used to fetch the heli.
- `Despawn commands` -- Determines which commands can be used to despawn the heli.
- `Can despawn while occupied` (`true` or `false`) -- Determines whether players can despawn their helicopter while it is occupied. A helicopter is considered occupied if a player is mounted to it, or if passengers are in the transport bay (i.e., for Scrap Transport Helicopters).
- `Can fetch while occupied` (`true` or `false`) -- Determines whether players can fetch their helicopter while it is occupied. While `true`, fetching the heli will automatically dismount all players. Regardless of this setting, players cannot fetched their helicopter while they are occupying it.
- `Can spawn while building blocked` (`true` or `false`) -- Determines whether players can spawn their helicopter while building blocked.
- `Can fetch while building blocked` (`true` or `false`) -- Determines whether players can fetch their helicopter while building blocked.
- `Auto fetch` (`true` or `false`) -- Determines whether your existing helicopter will be automatically fetched when using the spawn command. This feature only applies to players who have the fetch permission, and it is subject to the player's fetch cooldown.
- `Repair on fetch` (`true` or `false`) -- Determines whether to repair the helicopter when fetched. Note: It is advised to set a reasonable fetch cooldown to prevent abuse.
- `Max spawn distance` -- Determines the maximum distance away that players can spawn their helicopter. This only applies while `Fixed spawn distance` is **disabled** which is enabled by default.
- `Max fetch distance` -- Determines the maximum distance at which players can be from their helicopter and still be able to fetch it. Set to `-1` for unlimited distance.
- `Max despawn distance` -- Determines the maximum distance at which players can be from their helicopter and still be able to despawn it. Set to `-1` for unlimited distance.
- `Fixed spawn distance` -- When enabled, fixed spawn distance causes the helicopter to be spawned and fetched in front of the player at a consistent distance, regardless of where the player is aiming. While **disabled**, players have to aim at a valid nearby surface in order to spawn the helicopter.
  - `Enabled` (`true` or `false`) -- Determines whether fixed spawn distance is enabled.
  - `Distance from player` -- Determines how far away from the player the helicopter will be spawned.
  - `Helicopter rotation angle` -- Determines how the helicopter will be rotated relative to the player when spawned or fetched.
- `Auto mount` -- When enabled, spawning or fetching a helicopter will automatically mount the owner player to the pilot seat. Note: Giving a player a helicopter using admin/server commands will skip auto mount.
  - `Enabled` (`true` or `false`) -- Determines whether auto mount is enabled.
  - `Require permission` (`true` or `false`) -- Determines whether players need permission for the auto mount feature.
- `Instant takeoff` -- When enabled, helicopters spawned by this plugin can takeoff instantly, bypassing engine startup time.
  - `Enabled` (`true` or `false`) -- Determines whether instant takeoff is enabled.
  - `Require permission` (`true` or `false`) -- Determines whether the owner of the helicopter needs permission for this feature to work.
- `Only owner and team can mount` (`true` or `false`) -- Set to `true` to only allow the owner and their team members to be able to mount the helicopter.
- `Destroy on disconnect` (`true` or `false`) -- Determines whether player helicopters will be despawned when the owner disconnects from the server. Note: If a player is mounted to the helicopter when the owner disconnects, the despawn will be delayed until no more players are mounted to the it.
- `Fuel` -- Determines how much fuel helicopters will spawn with. Note: Fuel configuration does not apply to players that have the unlimited fuel permission.
  - `Default fuel amount` -- Determines the amount of low grade fuel to add to helicopters when spawned. Set to `-1` for max stack size (which depends on the server, but is 500 in vanilla).
  - `Fuel profiles requiring permission` -- Use this section to customize the fuel amount for different players. The plugin will generate a permission for each fuel profile of the format `spawnheli.<heli-type>.fuel.<suffix>`. For example, `100` for Minicopter would generate the permission `spawnheli.minicopter.fuel.100`. Granting one of those permissions to a player will cause their helicopter to spawn with that amount of low grade fuel, overriding the `Default fuel amount`. Note: If multiple such permissions are granted to a player, the last one will apply, based on the order in the config.
    - `Fuel amount` -- Determines the amount of fuel to add when the helicopter is spawned, if this profile is assigned to a player.
    - `Permission suffix` -- Determines the permission generated, like `spawnheli.<heli-type>.fuel.<suffix>`.
- `SpawnHealth` -- Determines the amount of health with which helicopters will spawn. Set to 0 or less to have no effect (i.e., to make helicopters spawn with vanilla health). Note: Collisions cause helicopters to take damage equal to a percentage of their health, so changing this will not effect how many collisions helicopters can take before being destroyed.
- `Spawn cooldowns` -- Spawn cooldowns determine how often players can spawn the helicopter.
  - `Default cooldown (seconds)` -- The default spawn cooldown applies to players who have not been granted any permissions in `Cooldown profiles requiring permission`.
  - `Cooldown profiles requiring permission` -- Use this section to customize spawn cooldowns for different players. The plugin will generate a permission for each profile of the format `spawnheli.<heli-type>.cooldown.spawn.<suffix>`. Note: If multiple such permissions are granted to a player, the last one will apply, based on the order in the config.
    - `Cooldown (seconds)`
    - `Permission suffix` -- Determines the permission generated, like `spawnheli.<heli-type>.cooldown.spawn.<suffix>`. -- Determines the number of seconds the player must wait after spawning their helicopter before they can spawn it again.
- `Fetch cooldowns` -- Fetch cooldowns determine how often players can fetch their helicopter.
  - `Default cooldown (seconds)` -- The default fetch cooldown applies to players who have not been granted any permissions in `Cooldown profiles requiring permission`.
  - `Cooldown profiles requiring permission` -- Use this section to customize fetch cooldowns for different players. The plugin will generate a permission for each profile of the format `spawnheli.<heli-type>.cooldown.fetch.<suffix>`. Note: If multiple such permissions are granted to a player, the last one will apply, based on the order in the config.
    - `Cooldown (seconds)` -- Determines the number of seconds the player must wait after fetching their helicopter before they can fetch it again.
    - `Permission suffix` -- Determines the permission generated, like `spawnheli.<heli-type>.cooldown.fetch.<suffix>`.

## Localization

## Developer Hooks

### Spawn hooks

```csharp
object OnMyMiniSpawn(BasePlayer player)
object OnMyScrapHeliSpawn(BasePlayer player)
object OnMyAttackHeliSpawn(BasePlayer player)
```

- Called when a player tries to spawn a helicopter
- Returning `false` will prevent spawning the helicopter
- Returning `null` will allow the helicopter to be spawned, unless blocked by another plugin

### Fetch hooks

```csharp
object OnMyMiniFetch(BasePlayer player, Minicopter heli)
object OnMyScrapHeliFetch(BasePlayer player, ScrapTransportHelicopter heli)
object OnMyAttackHeliFetch(BasePlayer player, AttackHelicopter heli)
```

- Called when a player tries to fetch their helicopter
- Returning `false` will prevent fetching the helicopter
- Returning `null` will allow the helicopter to be fetched, unless blocked by another plugin

### Despawn hooks

```csharp
object OnMyMiniDespawn(BasePlayer player, MiniCopter heli)
object OnMyScrapHeliDespawn(BasePlayer player, ScrapTransportHelicopter heli)
object OnMyAttackHeliDespawn(BasePlayer player, AttackHelicopter heli)
```

- Called when a player tries to despawn their helicopter
- Returning `false` will prevent despawning the minicopter
- Returning `null` will allow the helicopter to be despawned, unless blocked by another plugin

## Credits

* **SpooksAU**, the current maintainer
* **WhiteThunder**, put in heaps of work while i was unable to
* **BuzZ**, the original author of MyMiniCopter
* **rfc1920**, for helping maintain the plugin
