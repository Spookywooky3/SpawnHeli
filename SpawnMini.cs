using Oxide.Core;   
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Spawn Mini", "SpooksAU", "0.2.4"), Description("Spawn a mini!")]
    class SpawnMini : RustPlugin
    {
        private SaveData _data;
        private PluginConfig _config;

        /* EDIT PERMISSIONS HERE */
        private readonly string _spawnMini = "spawnmini.mini";
        private readonly string _noCooldown = "spawnmini.nocd";
        private readonly string _noMini = "spawnmini.nomini";
        private readonly string _noFuel = "spawnmini.unlimitedfuel";
        private readonly string _noDecay = "spawnmini.nodecay";

        #region Hooks

        private void Loaded() 
        {
            _config = Config.ReadObject<PluginConfig>();

            permission.RegisterPermission(_spawnMini, this);
            permission.RegisterPermission(_noCooldown, this);
            permission.RegisterPermission(_noMini, this);
            permission.RegisterPermission(_noFuel, this);
            permission.RegisterPermission(_noDecay, this);

            foreach (var perm in _config.cooldowns)
                permission.RegisterPermission(perm.Key, this);

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("SpawnMini"))
                Interface.Oxide.DataFileSystem.GetDatafile("SpawnMini").Save();

            _data = Interface.Oxide.DataFileSystem.ReadObject<SaveData>("SpawnMini");
        }

        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject("SpawnMini", _data);
        }

        void OnEntityKill(MiniCopter mini)
        {
            if (_data.playerMini.ContainsValue(mini.net.ID))
            {
                string key = _data.playerMini.FirstOrDefault(x => x.Value == mini.net.ID).Key;

                BasePlayer player = BasePlayer.FindByID(ulong.Parse(key));

                if (player != null)
                    player.ChatMessage(lang.GetMessage("mini_destroyed", this, player.UserIDString));

                _data.playerMini.Remove(key);
            }
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null)
                return null;

            // TODO: Look for better option later 
            if (_data.playerMini.ContainsValue(entity.net.ID))
                if (permission.UserHasPermission(entity.OwnerID.ToString(), _noDecay))
                    info.damageTypes.Scale(Rust.DamageType.Decay, 0);

            return null;
        }

        #endregion

        #region Commands

        [ChatCommand("mymini")]
        void GiveMini(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _spawnMini))
            {
                player.ChatMessage(lang.GetMessage("mini_perm", this, player.UserIDString));
            }
            else
            {
                if (_data.playerMini.ContainsKey(player.UserIDString))
                {
                    player.ChatMessage(lang.GetMessage("mini_current", this, player.UserIDString));
                }
                else if (_data.cooldown.ContainsKey(player.UserIDString) && !permission.UserHasPermission(player.UserIDString, _noCooldown))
                {
                    DateTime cooldown = _data.cooldown[player.UserIDString];
                    if (cooldown > DateTime.Now)
                    {
                        player.ChatMessage(string.Format(lang.GetMessage("mini_timeleft", this, player.UserIDString),
                            Math.Round((cooldown - DateTime.Now).TotalMinutes, 2)));
                    }
                    else
                    {
                        _data.cooldown.Remove(player.UserIDString);
                        SpawnMinicopter(player);
                    }
                }
                else if (!_data.playerMini.ContainsKey(player.UserIDString))
                {
                    SpawnMinicopter(player);
                }
            }
        }        

        [ChatCommand("nomini")]
        void NoMini(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _noMini))
            {
                player.ChatMessage(lang.GetMessage("mini_perm", this, player.UserIDString));
            }
            else
            {
                if (!_data.playerMini.ContainsKey(player.UserIDString))
                {
                    player.ChatMessage(lang.GetMessage("mini_notcurrent", this, player.UserIDString));
                }
                else if (_data.playerMini.ContainsKey(player.UserIDString))
                {
                    MiniCopter mini = BaseNetworkable.serverEntities.Find(_data.playerMini[player.UserIDString]) as MiniCopter;

                    if (mini == null)
                        return;

                    if (!mini.AnyMounted())
                    {
                        BaseNetworkable.serverEntities.Find(_data.playerMini[player.UserIDString])?.Kill();

                        if (_config.noMiniRespawn)
                            SpawnMinicopter(player);
                    }
                    else
                    {
                        player.ChatMessage(lang.GetMessage("mini_mounted", this, player.UserIDString));
                    }
                }
            }
        }

        #endregion

        #region Helpers/Functions

        void SpawnMinicopter(BasePlayer player)
        {
            if (!player.IsBuildingBlocked() || _config.canSpawnBuildingBlocked)
            {
                RaycastHit hit;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity, 
                    LayerMask.GetMask("Construction", "Default", "Deployed", "Resource", "Terrain", "Water", "World")))
                {
                    if (hit.distance > _config.maxSpawnDistance)
                    {
                        player.ChatMessage(lang.GetMessage("mini_distance", this, player.UserIDString));
                    }
                    else
                    {
                        Vector3 position = hit.point + Vector3.up * 2f;
                        BaseVehicle miniEntity = (BaseVehicle)GameManager.server.CreateEntity(_config.assetPrefab, position, new Quaternion());
                        miniEntity.OwnerID = player.userID;
                        miniEntity.health = _config.spawnHealth;
                        miniEntity.Spawn();

                        // Credit Original MyMinicopter Plugin
                        if (permission.UserHasPermission(player.UserIDString, _noFuel))
                        {
                            MiniCopter minicopter = miniEntity as MiniCopter;

                            if (minicopter == null)
                                return;

                            minicopter.fuelPerSec = 0f;

                            StorageContainer fuelContainer = minicopter.fuelStorageInstance.Get(true).GetComponent<StorageContainer>();
                            ItemManager.CreateByItemID(-946369541, 1)?.MoveToContainer(fuelContainer.inventory);
                            fuelContainer.SetFlag(BaseEntity.Flags.Locked, true);
                        }

                        _data.playerMini.Add(player.UserIDString, miniEntity.net.ID);

                        if (!permission.UserHasPermission(player.UserIDString, _noCooldown))
                        {
                            foreach (var perm in _config.cooldowns)
                            {
                                if (_data.cooldown.ContainsKey(player.UserIDString))
                                    break;

                                if (permission.UserHasPermission(player.UserIDString, perm.Key))
                                    _data.cooldown.Add(player.UserIDString, DateTime.Now.AddSeconds(_config.cooldowns[perm.Key]));
                            }

                            /* Incase players don't have any cooldown permission default to one day */
                            if (!_data.cooldown.ContainsKey(player.UserIDString))
                                _data.cooldown.Add(player.UserIDString, DateTime.Now.AddDays(1));
                        }
                    }
                }
                else
                {
                    player.ChatMessage(lang.GetMessage("mini_terrain", this, player.UserIDString));
                }
            }
            else
            {
                player.ChatMessage(lang.GetMessage("mini_priv", this, player.UserIDString));
            }
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

        #endregion

        #region Classes And Overrides

        class SaveData
        {
            public Dictionary<string, uint> playerMini = new Dictionary<string, uint>();
            public Dictionary<string, DateTime> cooldown = new Dictionary<string, DateTime>();
        }

        class PluginConfig
        {
            [JsonProperty("AssetPrefab")]
            public string assetPrefab { get; set; }

            [JsonProperty("MaxSpawnDistance")]
            public float maxSpawnDistance { get; set; }

            [JsonProperty("SpawnHealth")]
            public float spawnHealth { get; set; }

            [JsonProperty("CanSpawnBuildingBlocked")]
            public bool canSpawnBuildingBlocked { get; set; }

            [JsonProperty("NoMiniRespawn")]
            public bool noMiniRespawn { get; set; }

            [JsonProperty("PermissionCooldowns")]
            public Dictionary<string, float> cooldowns { get; set; }
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                maxSpawnDistance = 5f,
                spawnHealth = 750f,
                assetPrefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                noMiniRespawn = true,
                canSpawnBuildingBlocked = false,
                cooldowns = new Dictionary<string, float>()
                {
                    ["spawnmini.tier1"] = 86400f,
                    ["spawnmini.tier2"] = 43200f,
                    ["spawnmini.tier3"] = 21600f,
                }
            };
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["mini_destroyed"] = "Your minicopter has been destroyed!",
                ["mini_perm"] = "You do not have permission to use this command!",
                ["mini_current"] = "You already have a minicopter!",
                ["mini_notcurrent"] = "You do not have a minicopter!",
                ["mini_priv"] = "Cannot spawn a minicopter because you're building blocked!",
                ["mini_timeleft"] = "You have {0} minutes until your cooldown ends",
                ["mini_distance"] = "You're trying to spawn the minicopter too far away!",
                ["mini_terrain"] = "Trying to spawn minicopter outside of terrain!",
                ["mini_mounted"] = "A player is currenty mounted on the minicopter!"
            }, this, "en");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["mini_destroyed"] = "Ваш миникоптер был уничтожен!",
                ["mini_perm"] = "У вас нет разрешения на использование этой команды!",
                ["mini_current"] = "У вас уже есть мини-вертолет!",
                ["mini_notcurrent"] = "У вас нет мини-вертолета!",
                ["mini_priv"] = "Невозможно вызвать мини-вертолет, потому что ваше здание заблокировано!",
                ["mini_timeleft"] = "У вас есть {0} минута, пока ваше время восстановления не закончится.",
                ["mini_distance"] = "Вы пытаетесь породить миникоптер слишком далеко!",
                ["mini_terrain"] = "Попытка породить мини-вертолет вне местности!",
                ["mini_mounted"] = "Игрок в данный момент сидит в миникоптере!"
            }, this, "ru");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["mini_destroyed"] = "Ihr minikopter wurde zerstört!",
                ["mini_perm"] = "Du habe keine keine berechtigung diesen befehl zu verwenden!",
                ["mini_current"] = "Du hast bereits einen minikopter!",
                ["mini_notcurrent"] = "Du hast keine minikopter!",
                ["mini_priv"] = "Ein minikopter kann nicht hervorgebracht, da das bauwerk ist verstopft!",
                ["mini_timeleft"] = "Du hast {0} minuten, bis ihre abklingzeit ende",
                ["mini_distance"] = "Du bist versuchen den minikopter zu weit weg zu spawnen!",
                ["mini_terrain"] = "Du versucht laichen einen minikopter außerhalb des geländes!",
                ["mini_mounted"] = "Ein spieler ist derzeit am minikopter montiert!"
            }, this, "de");
        }

        #endregion
    }
}
