using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Spawn Mini", "SpooksAU", "2.11.0"), Description("Spawn a mini!")]
    class SpawnMini : RustPlugin
    {
        private SaveData _data;
        private PluginConfig _config;

        /* EDIT PERMISSIONS HERE */
        private readonly string _spawnMini = "spawnmini.mini";
        private readonly string _noCooldown = "spawnmini.nocd";
        private readonly string _noMini = "spawnmini.nomini";
        private readonly string _fetchMini = "spawnmini.fmini";
        private readonly string _noFuel = "spawnmini.unlimitedfuel";
        private readonly string _noDecay = "spawnmini.nodecay";
        private readonly string _permissionFuelFormat = "spawnmini.fuel.{0}";

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(_spawnMini, this);
            permission.RegisterPermission(_noCooldown, this);
            permission.RegisterPermission(_noMini, this);
            permission.RegisterPermission(_fetchMini, this);
            permission.RegisterPermission(_noFuel, this);
            permission.RegisterPermission(_noDecay, this);

            foreach (var perm in _config.cooldowns)
                permission.RegisterPermission(perm.Key, this);

            foreach (var fuelAmount in _config.fuelAmountsRequiringPermission)
                permission.RegisterPermission(GetFuelPermission(fuelAmount), this);

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
                Interface.Oxide.DataFileSystem.GetDatafile(Name).Save();

            LoadSaveData();

            if (!_config.ownerOnly)
                Unsubscribe(nameof(CanMountEntity));

            if (!_config.destroyOnDisconnect)
            {
                Unsubscribe(nameof(OnPlayerDisconnected));
                Unsubscribe(nameof(OnEntityDismounted));
            }
        }

        void OnServerInitialized()
        {
            foreach (var mini in BaseNetworkable.serverEntities.OfType<MiniCopter>())
            {
                if (IsPlayerOwned(mini) && mini.OwnerID != 0 && permission.UserHasPermission(mini.OwnerID.ToString(), _noFuel))
                {
                    EnableUnlimitedFuel(mini);
                }
            }
        }

        void Unload() => WriteSaveData();

        void OnServerSave() => WriteSaveData();

        void OnNewSave()
        {
            _data.playerMini.Clear();
            _data.cooldown.Clear();
            WriteSaveData();
        }

        void OnEntityKill(MiniCopter mini)
        {
            if (_data.playerMini.ContainsValue(mini.net.ID))
            {
                string key = _data.playerMini.FirstOrDefault(x => x.Value == mini.net.ID).Key;

                ulong result;
                ulong.TryParse(key, out result);
                BasePlayer player = BasePlayer.FindByID(result);

                if (player != null)
                    player.ChatMessage(lang.GetMessage("mini_destroyed", this, player.UserIDString));

                _data.playerMini.Remove(key);
            }
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || entity.OwnerID == 0)
                return null;

            if (_data.playerMini.ContainsValue(entity.net.ID))
                if (permission.UserHasPermission(entity.OwnerID.ToString(), _noDecay) && info.damageTypes.Has(Rust.DamageType.Decay))
                    return true;

            return null;
        }

        object CanMountEntity(BasePlayer player, BaseVehicleMountPoint entity)
        {
            if (player == null || entity == null)
                return null;

            var mini = entity.GetVehicleParent() as MiniCopter;
            if (mini == null || mini is ScrapTransportHelicopter || mini.OwnerID == 0 || !IsPlayerOwned(mini)) return null;

            if (mini.OwnerID != player.userID)
            {
                if (player.Team != null && player.Team.members.Contains(mini.OwnerID))
                    return null;

                player.ChatMessage(lang.GetMessage("mini_canmount", this, player.UserIDString));
                return false;
            }
            return null;
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null)
                return;

            uint miniNetId;
            if (!_data.playerMini.TryGetValue(player.UserIDString, out miniNetId))
                return;

            var mini = BaseNetworkable.serverEntities.Find(miniNetId) as MiniCopter;
            if (mini == null)
                return;

            NextTick(() =>
            {
                if (mini == null)
                    return;

                // Despawn minicopter when the owner disconnects
                // If mounted, we will despawn it later when all players dismount
                if (!mini.AnyMounted())
                    mini.Kill();
            });
        }

        void OnEntityDismounted(BaseVehicleSeat seat)
        {
            if (seat == null)
                return;

            var mini = seat.GetVehicleParent() as MiniCopter;
            if (mini == null || mini.OwnerID == 0 || !IsPlayerOwned(mini) || mini.AnyMounted())
                return;

            // Despawn minicopter when fully dismounted, if the owner player has disconnected
            var ownerPlayer = BasePlayer.FindByID(mini.OwnerID);
            if (ownerPlayer == null || !ownerPlayer.IsConnected)
                mini.Kill();
        }

        void CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container == null || !container.IsLocked())
                return;

            var mini = container.GetParentEntity() as MiniCopter;
            if (mini == null || !IsPlayerOwned(mini))
                return;

            if (permission.UserHasPermission(mini.OwnerID.ToString(), _noFuel))
                player.ChatMessage(lang.GetMessage("mini_unlimited_fuel", this, player.UserIDString));
        }

        #endregion

        #region Commands

        [ChatCommand("mymini")]
        private void MyMiniCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _spawnMini))
            {
                player.ChatMessage(lang.GetMessage("mini_perm", this, player.UserIDString));
                return;
            }

            if (FindPlayerMini(player) != null)
            {
                player.ChatMessage(lang.GetMessage("mini_current", this, player.UserIDString));
                return;
            }

            if (IsLocationRestricted(player.transform.position))
            {
                player.ChatMessage(lang.GetMessage("mini_location_restricted", this, player.UserIDString));
                return;
            }

            if (SpawnWasBlocked(player))
                return;

            if (_data.cooldown.ContainsKey(player.UserIDString) && !permission.UserHasPermission(player.UserIDString, _noCooldown))
            {
                DateTime lastSpawned = _data.cooldown[player.UserIDString];
                TimeSpan timeRemaining = CeilingTimeSpan(lastSpawned.AddSeconds(GetPlayerCooldownSeconds(player)) - DateTime.Now);
                if (timeRemaining.TotalSeconds > 0)
                {
                    player.ChatMessage(string.Format(lang.GetMessage("mini_timeleft_new", this, player.UserIDString), timeRemaining.ToString("g")));
                    return;
                }

                _data.cooldown.Remove(player.UserIDString);
            }

            SpawnMinicopter(player);
        }

        [ChatCommand("fmini")]
        private void FetchMinicopter(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _fetchMini))
            {
                player.ChatMessage(lang.GetMessage("mini_perm", this, player.UserIDString));
                return;
            }

            var mini = FindPlayerMini(player);
            if (mini == null)
            {
                player.ChatMessage(lang.GetMessage("mini_notcurrent", this, player.UserIDString));
                return;
            }

            if (!_config.canFetchBuildlingBlocked && player.IsBuildingBlocked())
            {
                player.ChatMessage(lang.GetMessage("mini_buildingblocked", this, player.UserIDString));
                return;
            }

            bool isMounted = mini.AnyMounted();
            if (isMounted && (!_config.canFetchWhileOccupied || player.GetMountedVehicle() == mini))
            {
                player.ChatMessage(lang.GetMessage("mini_mounted", this, player.UserIDString));
                return;
            }

            if (IsMiniBeyondMaxDistance(player, mini))
            {
                player.ChatMessage(lang.GetMessage("mini_current_distance", this, player.UserIDString));
                return;
            }

            if (IsLocationRestricted(player.transform.position))
            {
                player.ChatMessage(lang.GetMessage("mini_location_restricted", this, player.UserIDString));
                return;
            }

            if (FetchWasBlocked(player, mini))
                return;

            if (isMounted)
            {
                // mini.DismountAllPlayers() doesn't work so we have to enumerate the mount points
                foreach (var mountPoint in mini.mountPoints)
                    mountPoint.mountable?.DismountAllPlayers();
            }

            mini.transform.SetPositionAndRotation(GetIdealFixedPositionForPlayer(player), GetIdealRotationForPlayer(player));
        }

        [ChatCommand("nomini")]
        private void NoMinicopter(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _noMini))
            {
                player.ChatMessage(lang.GetMessage("mini_perm", this, player.UserIDString));
                return;
            }

            var mini = FindPlayerMini(player);
            if (mini == null)
            {
                player.ChatMessage(lang.GetMessage("mini_notcurrent", this, player.UserIDString));
                return;
            }

            if (mini.AnyMounted() && (!_config.canDespawnWhileOccupied || player.GetMountedVehicle() == mini))
            {
                player.ChatMessage(lang.GetMessage("mini_mounted", this, player.UserIDString));
                return;
            }

            if (IsMiniBeyondMaxDistance(player, mini))
            {
                player.ChatMessage(lang.GetMessage("mini_current_distance", this, player.UserIDString));
                return;
            }

            if (DespawnWasBlocked(player, mini))
                return;

            BaseNetworkable.serverEntities.Find(_data.playerMini[player.UserIDString])?.Kill();
        }

        [ConsoleCommand("spawnmini.give")]
        private void GiveMiniConsole(ConsoleSystem.Arg arg)
        {
            if (arg.IsClientside || !arg.IsRcon)
                return;

            var args = arg.Args;
            if (args == null || args.Length == 0)
            {
                Puts("Syntax: spawnmini.give <name or steamid>");
                return;
            }

            var player = BasePlayer.Find(args[0]);
            if (player == null)
            {
                PrintError($"No player found matching '{args[0]}'");
                return;
            }

            if (args.Length > 1)
            {
                float x, y, z;
                if (args.Length < 4 ||
                    !float.TryParse(args[1], out x) ||
                    !float.TryParse(args[2], out y) ||
                    !float.TryParse(args[3], out z))
                {
                    Puts($"Syntax: spawnmini.give <name or steamid> <x> <y> <z>");
                    return;
                }

                GiveMinicopter(player, new Vector3(x, y, z), useCustomPosition: true);
            }
            else
            {
                GiveMinicopter(player);
            }
        }

        #endregion

        #region Helpers/Functions

        private bool SpawnWasBlocked(BasePlayer player)
        {
            object hookResult = Interface.CallHook("OnMyMiniSpawn", player);
            return hookResult is bool && (bool)hookResult == false;
        }

        private bool FetchWasBlocked(BasePlayer player, MiniCopter mini)
        {
            object hookResult = Interface.CallHook("OnMyMiniFetch", player, mini);
            return hookResult is bool && (bool)hookResult == false;
        }

        private bool DespawnWasBlocked(BasePlayer player, MiniCopter mini)
        {
            object hookResult = Interface.CallHook("OnMyMiniDespawn", player, mini);
            return hookResult is bool && (bool)hookResult == false;
        }

        private TimeSpan CeilingTimeSpan(TimeSpan timeSpan) =>
            new TimeSpan((long)Math.Ceiling(1.0 * timeSpan.Ticks / 10000000) * 10000000);

        private bool IsLocationRestricted(Vector3 position)
        {
            // Disallow spawning in underground train tunnels
            return position.y < -100;
        }

        private bool IsMiniBeyondMaxDistance(BasePlayer player, MiniCopter mini) =>
            _config.noMiniDistance >= 0 && GetDistance(player, mini) > _config.noMiniDistance;

        private Vector3 GetIdealFixedPositionForPlayer(BasePlayer player)
        {
            Vector3 forward = player.GetNetworkRotation() * Vector3.forward;
            forward.y = 0;
            return player.transform.position + forward.normalized * 3f + Vector3.up * 2f;
        }

        private Quaternion GetIdealRotationForPlayer(BasePlayer player) =>
            Quaternion.Euler(0, player.GetNetworkRotation().eulerAngles.y - 135, 0);

        private MiniCopter FindPlayerMini(BasePlayer player)
        {
            uint miniNetId;
            if (!_data.playerMini.TryGetValue(player.UserIDString, out miniNetId))
                return null;

            var mini = BaseNetworkable.serverEntities.Find(miniNetId) as MiniCopter;

            // Fix a potential data file desync where the mini doesn't exist anymore
            // Desyncs should be rare but are not possible to 100% prevent
            // They can happen if the mini is destroyed while the plugin is unloaded
            // Or if someone edits the data file manually
            if (mini == null)
                _data.playerMini.Remove(player.UserIDString);

            return mini;
        }

        private void SpawnMinicopter(BasePlayer player)
        {
            if (!_config.canSpawnBuildingBlocked && player.IsBuildingBlocked())
            {
                player.ChatMessage(lang.GetMessage("mini_buildingblocked", this, player.UserIDString));
                return;
            }

            Vector3 position;

            if (_config.useFixedSpawnDistance)
            {
                position = GetIdealFixedPositionForPlayer(player);
            }
            else
            {
                RaycastHit hit;
                if (!Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity,
                    LayerMask.GetMask("Construction", "Default", "Deployed", "Resource", "Terrain", "Water", "World")))
                {
                    player.ChatMessage(lang.GetMessage("mini_terrain", this, player.UserIDString));
                    return;
                }

                if (hit.distance > _config.maxSpawnDistance)
                {
                    player.ChatMessage(lang.GetMessage("mini_sdistance", this, player.UserIDString));
                    return;
                }

                position = hit.point + Vector3.up * 2f;
            }

            MiniCopter mini = GameManager.server.CreateEntity(_config.assetPrefab, position, GetIdealRotationForPlayer(player)) as MiniCopter;
            if (mini == null) return;

            mini.OwnerID = player.userID;
            mini.health = _config.spawnHealth;
            mini.Spawn();

            // Credit Original MyMinicopter Plugin
            if (permission.UserHasPermission(player.UserIDString, _noFuel))
                EnableUnlimitedFuel(mini);
            else
                AddInitialFuel(mini, player.UserIDString);

            _data.playerMini.Add(player.UserIDString, mini.net.ID);

            if (!permission.UserHasPermission(player.UserIDString, _noCooldown))
            {
                _data.cooldown.Add(player.UserIDString, DateTime.Now);
            }
        }

        private void GiveMinicopter(BasePlayer player, Vector3 customPosition = default(Vector3), bool useCustomPosition = false)
        {
            if (FindPlayerMini(player) != null)
            {
                player.ChatMessage(lang.GetMessage("mini_current", this, player.UserIDString));
                return;
            }

            var position = useCustomPosition ? customPosition : GetIdealFixedPositionForPlayer(player);
            var rotation = useCustomPosition ? Quaternion.identity : GetIdealRotationForPlayer(player);

            MiniCopter mini = GameManager.server.CreateEntity(_config.assetPrefab, position, rotation) as MiniCopter;
            if (mini == null) return;

            mini.OwnerID = player.userID;
            mini.Spawn();

            _data.playerMini.Add(player.UserIDString, mini.net.ID);

            if (permission.UserHasPermission(player.UserIDString, _noFuel))
                EnableUnlimitedFuel(mini);
            else
                AddInitialFuel(mini, player.UserIDString);
        }

        private float GetPlayerCooldownSeconds(BasePlayer player)
        {
            var grantedCooldownPerms = _config.cooldowns
                .Where(entry => permission.UserHasPermission(player.UserIDString, entry.Key));

            // Default cooldown to 1 day if they don't have any specific permissions
            return grantedCooldownPerms.Any() ? grantedCooldownPerms.Min(entry => entry.Value) : _config.defaultCooldown;
        }

        private void AddInitialFuel(MiniCopter minicopter, string userId)
        {
            var fuelAmount = GetPlayerAllowedFuel(userId);
            if (fuelAmount == 0)
                return;

            StorageContainer fuelContainer = minicopter.GetFuelSystem().GetFuelContainer();
            if (fuelAmount < 0)
            {
                // Value of -1 is documented to represent max stack size
                fuelAmount = fuelContainer.allowedItem.stackable;
            }
            fuelContainer.inventory.AddItem(fuelContainer.allowedItem, fuelAmount);
        }

        private void EnableUnlimitedFuel(MiniCopter minicopter)
        {
            minicopter.fuelPerSec = 0f;

            StorageContainer fuelContainer = minicopter.GetFuelSystem().GetFuelContainer();
            fuelContainer.inventory.AddItem(fuelContainer.allowedItem, 1);
            fuelContainer.SetFlag(BaseEntity.Flags.Locked, true);
        }

        private float GetDistance(BasePlayer player, MiniCopter mini)
        {
            float distance = Vector3.Distance(player.transform.position, mini.transform.position);
            return distance;
        }

        private bool IsPlayerOwned(MiniCopter mini)
        {
            if (mini != null && _data.playerMini.ContainsValue(mini.net.ID))
                return true;

            return false;
        }

        private string GetFuelPermission(int fuelAmount) => String.Format(_permissionFuelFormat, fuelAmount);

        #endregion

        #region Data & Configuration

        private int GetPlayerAllowedFuel(string userIdString)
        {
            if (_config.fuelAmountsRequiringPermission == null || _config.fuelAmountsRequiringPermission.Length == 0)
                return _config.fuelAmount;

            for (var i = _config.fuelAmountsRequiringPermission.Length - 1; i >= 0; i--)
            {
                var fuelAmount = _config.fuelAmountsRequiringPermission[i];
                if (permission.UserHasPermission(userIdString, String.Format(_permissionFuelFormat, fuelAmount)))
                    return fuelAmount;
            }

            return _config.fuelAmount;
        }

        private SaveData LoadSaveData()
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<SaveData>(Name);
            if (_data == null)
            {
                PrintWarning($"Data file {Name}.json is invalid. Creating new data file.");
                _data = new SaveData();
                WriteSaveData();
            }
            return _data;
        }

        private void WriteSaveData() =>
            Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        class SaveData
        {
            public Dictionary<string, uint> playerMini = new Dictionary<string, uint>();
            public Dictionary<string, DateTime> cooldown = new Dictionary<string, DateTime>();
        }

        class PluginConfig : SerializableConfiguration
        {
            [JsonProperty("AssetPrefab")]
            public string assetPrefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab";

            [JsonProperty("CanDespawnWhileOccupied")]
            public bool canDespawnWhileOccupied = false;

            [JsonProperty("CanFetchWhileOccupied")]
            public bool canFetchWhileOccupied = false;

            [JsonProperty("CanSpawnBuildingBlocked")]
            public bool canSpawnBuildingBlocked = false;

            [JsonProperty("CanFetchBuildingBlocked")]
            public bool canFetchBuildlingBlocked = true;

            [JsonProperty("FuelAmount")]
            public int fuelAmount = 0;

            [JsonProperty("FuelAmountsRequiringPermission")]
            public int[] fuelAmountsRequiringPermission = new int[0];

            [JsonProperty("MaxNoMiniDistance")]
            public float noMiniDistance = -1;

            [JsonProperty("MaxSpawnDistance")]
            public float maxSpawnDistance = 5f;

            [JsonProperty("UseFixedSpawnDistance")]
            public bool useFixedSpawnDistance = true;

            [JsonProperty("OwnerAndTeamCanMount")]
            public bool ownerOnly = false;

            [JsonProperty("DefaultCooldown")]
            public float defaultCooldown = 86400f;

            [JsonProperty("PermissionCooldowns")]
            public Dictionary<string, float> cooldowns = new Dictionary<string, float>()
            {
                ["spawnmini.tier1"] = 43200f,
                ["spawnmini.tier2"] = 21600f,
                ["spawnmini.tier3"] = 10800f,
            };

            [JsonProperty("SpawnHealth")]
            public float spawnHealth = 750f;

            [JsonProperty("DestroyOnDisconnect")]
            public bool destroyOnDisconnect = false;
        }

        private PluginConfig GetDefaultConfig() => new PluginConfig();

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["mini_destroyed"] = "Your minicopter has been destroyed!",
                ["mini_perm"] = "You do not have permission to use this command!",
                ["mini_current"] = "You already have a minicopter!",
                ["mini_notcurrent"] = "You do not have a minicopter!",
                ["mini_buildingblocked"] = "Cannot do that while building blocked!",
                ["mini_timeleft_new"] = "You have <color=red>{0}</color> until your cooldown ends",
                ["mini_sdistance"] = "You're trying to spawn the minicopter too far away!",
                ["mini_terrain"] = "Trying to spawn minicopter outside of terrain!",
                ["mini_mounted"] = "A player is currenty mounted on the minicopter!",
                ["mini_current_distance"] = "The minicopter is too far!",
                ["mini_canmount"] = "You are not the owner of this Minicopter or in the owner's team!",
                ["mini_unlimited_fuel"] = "That minicopter doesn't need fuel!",
                ["mini_location_restricted"] = "You cannot do that here!",
            }, this, "en");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["mini_destroyed"] = "Ваш миникоптер был уничтожен!",
                ["mini_perm"] = "У вас нет разрешения на использование этой команды!",
                ["mini_current"] = "У вас уже есть мини-вертолет!",
                ["mini_notcurrent"] = "У вас нет мини-вертолета!",
                ["mini_timeleft_new"] = "У вас есть <color=red>{0}</color>, пока ваше время восстановления не закончится.",
                ["mini_sdistance"] = "Вы пытаетесь породить миникоптер слишком далеко!",
                ["mini_terrain"] = "Попытка породить мини-вертолет вне местности!",
                ["mini_mounted"] = "Игрок в данный момент сидит на миникоптере или это слишком далеко!",
                ["mini_current_distance"] = "Мини-вертолет слишком далеко!",
                ["mini_canmount"] = "Вы не являетесь владельцем этого Minicopter или в команде владельца!"
            }, this, "ru");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["mini_destroyed"] = "Ihr minikopter wurde zerstört!",
                ["mini_perm"] = "Du habe keine keine berechtigung diesen befehl zu verwenden!",
                ["mini_current"] = "Du hast bereits einen minikopter!",
                ["mini_notcurrent"] = "Du hast keine minikopter!",
                ["mini_timeleft_new"] = "Du hast <color=red>{0}</color>, bis ihre abklingzeit ende",
                ["mini_sdistance"] = "Du bist versuchen den minikopter zu weit weg zu spawnen!",
                ["mini_terrain"] = "Du versucht laichen einen minikopter außerhalb des geländes!",
                ["mini_mounted"] = "Ein Spieler ist gerade am Minikopter montiert oder es ist zu weit!",
                ["mini_current_distance"] = "Der Minikopter ist zu weit!",
                ["mini_rcon"] = "Dieser Befehl kann nur von RCON ausgeführt werden!",
                ["mini_canmount"] = "Sie sind nicht der Besitzer dieses Minicopters oder im Team des Besitzers!"
            }, this, "de");
        }

        #endregion

        #region Configuration Boilerplate

        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        internal static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        // Don't update nested keys since the cooldown tiers might be customized
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion
    }
}
