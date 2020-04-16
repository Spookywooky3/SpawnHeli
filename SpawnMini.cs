using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SpawnMini", "Spooks © 2020", 1.0), Description("Spawn a mini!")]
    class SpawnMini : CovalencePlugin
    {
        private DynamicConfigFile dataFile;
        private SaveData data;

        private void Loaded()
        {
            permission.RegisterPermission("spawnmini.mini", this);
            permission.RegisterPermission("spawnmini.nocd", this);
            permission.RegisterPermission("spawnmini.nomini", this);
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("SpawnMini"))
            {
                dataFile = Interface.Oxide.DataFileSystem.GetDatafile("SpawnMini");
                dataFile.Save();
            }
            data = Interface.Oxide.DataFileSystem.ReadObject<SaveData>("SpawnMini");
            Subscribe(nameof(OnEntityKill));
            Subscribe(nameof(OnServerShutdown));
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
                    BasePlayer.FindByID(ulong.Parse(key)).ChatMessage("Your minicopter has been destroyed!");
                    data.playerMini.Remove(key);
                }
            }
        }

        [Command("mini")]
        void GiveMini(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission("spawnmini.mini"))
            {
                player.Reply("You do not have permission to spawn a minicopter!");
            }
            else
            {
                if (data.playerMini.ContainsKey(player.Id))
                {
                    player.Reply("You already have a minicopter!");
                }
                else if (data.cooldown.ContainsKey(player.Id) && !player.HasPermission("spawnmini.nocd"))
                {
                    var cooldown = data.cooldown[player.Id];
                    player.Reply($"You have {Math.Round((cooldown - DateTime.Now).TotalMinutes, 2)} minutes until your cooldown ends");
                }
                else if (!data.playerMini.ContainsKey(player.Id) && player.HasPermission("spawnmini.nocd"))
                {
                    SpawnMinicopter(player);
                }
                else
                {
                    SpawnMinicopter(player);
                }
            }
        }

        [Command("nomini")]
        void NoMini(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission("spawnmini.nomini"))
            {
                player.Reply("You do not have permission to use this command!");
            }
            else
            {
                if (!data.playerMini.ContainsKey(player.Id))
                {
                    player.Reply("You do not have a minicopter!");
                }
                else
                {
                    BaseNetworkable.serverEntities.Find(data.playerMini[player.Id]).Kill();
                    SpawnMinicopter(player);
                }
            }
        }

        private void SpawnMinicopter(IPlayer player)
        {
            string miniPrefab= "assets/content/vehicles/minicopter/minicopter.entity.prefab";

            BasePlayer p = BasePlayer.FindByID(ulong.Parse(player.Id));

            if (!p.IsBuildingBlocked())
            {
                // https://umod.org/plugins/my-mini-copter
                Vector3 forward = p.GetNetworkRotation() * Vector3.forward;
                Vector3 straight = Vector3.Cross(Vector3.Cross(Vector3.up, forward), Vector3.up).normalized;
                Vector3 position = p.transform.position + straight * 5f;
                position.y = p.transform.position.y + 2.5f;
                BaseVehicle vehicleMini = (BaseVehicle)GameManager.server.CreateEntity(miniPrefab, position, new Quaternion());
                BaseEntity miniEntity = vehicleMini as BaseEntity;
                miniEntity.OwnerID = p.userID;
                vehicleMini.Spawn();
                // End

                data.playerMini.Add(p.UserIDString, vehicleMini.net.ID);

                DateTime now = DateTime.Now;
                if (!player.HasPermission("spawnmini.nocd"))
                {
                    data.cooldown.Add(p.UserIDString, now.AddDays(1));
                    timer.Once(86400, () =>
                    {
                        data.cooldown.Remove(p.UserIDString);
                    });
                }
            }
            else
            {
                p.ChatMessage("Cannot spawn a minicopter because you're building blocked!");
            }
        }

        class SaveData
        {
            public Dictionary<string, uint> playerMini = new Dictionary<string, uint>();
            public Dictionary<string, DateTime> cooldown = new Dictionary<string, DateTime>();
        }
    }
}
