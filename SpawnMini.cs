//   Copyright 2020 SpooksAU aka Spookywooky3
//	
//   Licensed under the Apache License, Version 2.0 (the "License");	
//   you may not use this file except in compliance with the License.	
//   You may obtain a copy of the License at	
//	
//       http://www.apache.org/licenses/LICENSE-2.0	
//	
//   Unless required by applicable law or agreed to in writing, software	
//   distributed under the License is distributed on an "AS IS" BASIS,	
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.	
//   See the License for the specific language governing permissions and	
//   limitations under the License.

using Oxide.Core;
using Oxide.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Spawn Mini", "SpooksAU", "1.0.5"), Description("Spawn a mini!")]
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

        void OnEntityKill(MiniCopter mini)
        {
            if (data.playerMini.ContainsValue(mini.net.ID))
            {
                string key = data.playerMini.FirstOrDefault(x => x.Value == mini.net.ID).Key;

                var player = BasePlayer.FindByID(ulong.Parse(key));

                if (player != null)
                    player.ChatMessage(lang.GetMessage("mini_destroyed", this, player.UserIDString));

                data.playerMini.Remove(key);
            }
        }

        [ChatCommand("mini")]
        void GiveMini(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _spawnMini))
            {
                player.ChatMessage(lang.GetMessage("mini_perm", this, player.UserIDString));
            }
            else
            {
                
                if (data.playerMini.ContainsKey(player.UserIDString))
                {
                    player.ChatMessage(lang.GetMessage("mini_current", this, player.UserIDString));
                }
                else if (data.cooldown.ContainsKey(player.UserIDString) && !permission.UserHasPermission(player.UserIDString, _noCooldown))
                {
                    var cooldown = data.cooldown[player.UserIDString];
                    if (cooldown > DateTime.Now)
                    {
                        player.ChatMessage(string.Format(lang.GetMessage("mini_timeleft", this, player.UserIDString),
                            Math.Round((cooldown - DateTime.Now).TotalMinutes, 2)));
                    }
                    else
                    {
                        data.cooldown.Remove(player.UserIDString);
                        SpawnMinicopter(player);
                    }
                }
                else if (!data.playerMini.ContainsKey(player.UserIDString))
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
                if (!data.playerMini.ContainsKey(player.UserIDString))
                {
                    player.ChatMessage(lang.GetMessage("mini_notcurrent", this, player.UserIDString));
                }
                else
                {
                    var heli = BaseNetworkable.serverEntities.Find(data.playerMini[player.UserIDString]);

                    if (heli != null)
                        heli.Kill();

                    SpawnMinicopter(player);
                }
            }
        }

        private void SpawnMinicopter(BasePlayer player)
        {
            if (!player.IsBuildingBlocked())
            {
                RaycastHit hit;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity, 
                    LayerMask.GetMask("Construction", "Default", "Deployed", "Resource", "Terrain", "Water", "World")))
                {
                    if (hit.distance > config.maxSpawnDistance)
                    {
                        player.ChatMessage(lang.GetMessage("mini_distance", this, player.UserIDString));
                    }
                    else
                    {
                        Vector3 position = hit.point + Vector3.up * 2f;
                        BaseVehicle mini = (BaseVehicle)GameManager.server.CreateEntity(config.assetPrefab, position, new Quaternion());
                        mini.OwnerID = player.userID;
                        mini.Spawn();

                        data.playerMini.Add(player.UserIDString, mini.net.ID);

                        if (!permission.UserHasPermission(player.UserIDString, _noCooldown))
                        {
                            data.cooldown.Add(player.UserIDString, DateTime.Now.AddSeconds(config.cooldownTime));
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

        class SaveData
        {
            public Dictionary<string, uint> playerMini = new Dictionary<string, uint>();
            public Dictionary<string, DateTime> cooldown = new Dictionary<string, DateTime>();
        }

        class PluginConfig
        {
            public float cooldownTime { get; set; }
            public float maxSpawnDistance { get; set; }
            public string assetPrefab { get; set; }
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                cooldownTime = 86400f,
                maxSpawnDistance = 5f,
                assetPrefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab"
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
                ["mini_terrain"] = "Trying to spawn minicopter outside of terrain!"
            }, this, "en");
        }
    }
}
