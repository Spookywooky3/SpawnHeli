using Oxide.Core;
using Oxide.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SpawnMini", "qSpooks", "1.0.1"), Description("Spawn a mini!")]
    class SpawnMini : RustPlugin
    {
        private DynamicConfigFile dataFile;
        private SaveData data;
        private PluginConfig config;

        /* EDIT PERMISSIONS HERE */
        private readonly string _spawnMini = "spawnmini.mini";
        private readonly string _noCooldown = "spawnmini.nocd";
        private readonly string _noMini = "spawnmini.nomini";

        private void Loaded() 
        {
            permission.RegisterPermission(_spawnMini, this);
            permission.RegisterPermission(_noCooldown, this);
            permission.RegisterPermission(_noMini, this);

            config = Config.ReadObject<PluginConfig>();

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("SpawnMini"))
            {
                dataFile = Interface.Oxide.DataFileSystem.GetDatafile("SpawnMini");
                dataFile.Save();
            }
            data = Interface.Oxide.DataFileSystem.ReadObject<SaveData>("SpawnMini");
        }

        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject("SpawnMini", data);
        }

        void OnServerShutdown()
        {
            Interface.Oxide.DataFileSystem.WriteObject("SpawnMini", data);
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity.ShortPrefabName == "minicopter.entity")
            {
                MiniCopter mini = entity as MiniCopter;
                if (data.playerMini.ContainsValue(mini.net.ID))
                {
                    string key = data.playerMini.FirstOrDefault(x => x.Value == mini.net.ID).Key;
                    var player = BasePlayer.FindByID(ulong.Parse(key));
                    if (player != null)
                    {
                        player.ChatMessage("Your minicopter has been destroyed!");
                    }
                    data.playerMini.Remove(key);
                }
            }
        }

        [ChatCommand("mini")]
        void GiveMini(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _spawnMini))
            {
                player.ChatMessage("You do not have permission to spawn a minicopter!");
            }
            else
            {
                if (data.playerMini.ContainsKey(player.UserIDString))
                {
                    player.ChatMessage("You already have a minicopter!");
                }
                else if (data.cooldown.ContainsKey(player.UserIDString) && !permission.UserHasPermission(player.UserIDString, _noCooldown))
                {
                    if (data.cooldown[player.UserIDString] > DateTime.Now)
                    {
                        var cooldown = data.cooldown[player.UserIDString];
                        player.ChatMessage($"You have {Math.Round((cooldown - DateTime.Now).TotalMinutes, 2)} minutes until your cooldown ends");
                    }
                    else
                    {
                        data.cooldown.Remove(player.UserIDString);
                        SpawnMinicopter(player);
                    }
                }
                else if (!data.playerMini.ContainsKey(player.UserIDString) && permission.UserHasPermission(player.UserIDString, _noCooldown))
                {
                    SpawnMinicopter(player);
                }
                else
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
                player.ChatMessage("You do not have permission to use this command!");
            }
            else
            {
                if (!data.playerMini.ContainsKey(player.UserIDString))
                {
                    player.ChatMessage("You do not have a minicopter!");
                }
                else
                {
                    BaseNetworkable.serverEntities.Find(data.playerMini[player.UserIDString]).Kill();
                    SpawnMinicopter(player);
                }
            }
        }

        private void SpawnMinicopter(BasePlayer player)
        {
            string miniPrefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab";

            if (!player.IsBuildingBlocked())
            {
                // https://umod.org/plugins/my-mini-copter
                Vector3 forward = player.GetNetworkRotation() * Vector3.forward;
                Vector3 straight = Vector3.Cross(Vector3.Cross(Vector3.up, forward), Vector3.up).normalized;
                Vector3 position = player.transform.position + straight * 5f;
                position.y = player.transform.position.y + 2.5f;
                BaseVehicle vehicleMini = (BaseVehicle)GameManager.server.CreateEntity(miniPrefab, position, new Quaternion());
                BaseEntity miniEntity = vehicleMini as BaseEntity;
                miniEntity.OwnerID = player.userID;
                vehicleMini.Spawn();
                // End

                data.playerMini.Add(player.UserIDString, vehicleMini.net.ID);

                DateTime now = DateTime.Now;
                if (!permission.UserHasPermission(player.UserIDString, _noCooldown))
                {
                    data.cooldown.Add(player.UserIDString, now.AddSeconds(config.cooldownTime));
                }
            }
            else
            {
                player.ChatMessage("Cannot spawn a minicopter because you're building blocked!");
            }
        }

        class SaveData
        {
            public Dictionary<string, uint> playerMini = new Dictionary<string, uint>();
            public Dictionary<string, DateTime> cooldown = new Dictionary<string, DateTime>();
        }

        class PluginConfig
        {
            public float cooldownTime { get; set; }
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                cooldownTime = 86400f
            };
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }
    }
}
