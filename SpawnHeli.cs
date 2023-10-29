using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Facepunch;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Spawn Heli", "SpooksAU", "3.0.0")]
    [Description("Allows players to spawn helicopters")]
    internal class SpawnHeli : CovalencePlugin
    {
        #region Fields

        private const string LegacyPluginName = "SpawnMini";
        private const string LegacyPermissionPrefix = "spawnmini.";

        private const string PermissionMinicopter = "minicopter";
        private const string PermissionScrapHeli = "scraptransport";
        private const string PermissionAttackHeli = "attackhelicopter";

        private const int SpawnPointLayerMask = Rust.Layers.Solid | Rust.Layers.Mask.Water;
        private const int SpaceCheckLayerMask = Rust.Layers.Solid;

        private const float VerticalSpawnOffset = 1;

        private SaveData _data;
        private Configuration _config;
        private readonly VehicleInfoManager _vehicleInfoManager;

        private readonly object True = true;
        private readonly object False = false;

        public SpawnHeli()
        {
            _vehicleInfoManager = new VehicleInfoManager(this);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            _data = SaveData.Load();
            _config.Init();
            _vehicleInfoManager.Init();

            if (!_vehicleInfoManager.AnyOwnerOnly)
            {
                Unsubscribe(nameof(CanMountEntity));
            }

            if (!_vehicleInfoManager.AnyDespawnOnDisconnect)
            {
                Unsubscribe(nameof(OnPlayerDisconnected));
                Unsubscribe(nameof(OnEntityDismounted));
            }
        }

        private void OnServerInitialized()
        {
            if (plugins.PluginManager.GetPlugin(LegacyPluginName) != null)
            {
                LogWarning($"Detected conflicting plugin {LegacyPluginName}. Please remove that plugin to avoid issues.");
            }

            _vehicleInfoManager.OnServerInitialized();

            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                var heli = networkable as PlayerHelicopter;
                if (heli == null)
                    continue;

                var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(heli);
                if (vehicleInfo == null || !vehicleInfo.Data.HasVehicle(heli))
                    continue;

                if (heli.OwnerID != 0 && permission.UserHasPermission(heli.OwnerID.ToString(), vehicleInfo.Permissions.UnlimitedFuel))
                {
                    EnableUnlimitedFuel(heli);
                }
            }

            _data.Clean();
        }

        private void Unload()
        {
            _data.SaveIfChanged();
        }

        private void OnServerSave()
        {
            _data.SaveIfChanged();
        }

        private void OnNewSave()
        {
            _data.Reset();
            _data.SaveIfChanged();
        }

        private void OnEntityKill(PlayerHelicopter heli)
        {
            var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(heli);
            if (vehicleInfo == null)
                return;

            var ownerIdString = heli.OwnerID.ToString();
            var playerVehicle = vehicleInfo.Data.GetVehicle(ownerIdString);
            if (playerVehicle == null || playerVehicle != heli)
                return;

            _data.UnregisterVehicle(vehicleInfo, ownerIdString);

            var basePlayer = BasePlayer.FindByID(heli.OwnerID);
            if (basePlayer != null)
            {
                basePlayer.ChatMessage(GetMessage(basePlayer.UserIDString, vehicleInfo.Messages.Destroyed));
            }
        }

        private object OnEntityTakeDamage(PlayerHelicopter heli, HitInfo info)
        {
            if (heli == null || info == null || heli.OwnerID == 0)
                return null;

            VehicleInfo vehicleInfo;
            if (!IsPlayerVehicle(heli, out vehicleInfo))
                return null;

            if (info.damageTypes.Has(Rust.DamageType.Decay)
                && permission.UserHasPermission(heli.OwnerID.ToString(), vehicleInfo.Permissions.NoDecay))
                return True;

            return null;
        }

        private object CanMountEntity(BasePlayer player, BaseVehicleMountPoint mountPoint)
        {
            if (player == null || mountPoint == null)
                return null;

            VehicleInfo vehicleInfo;
            var heli = mountPoint.GetParentEntity() as PlayerHelicopter;
            if (heli == null || heli.OwnerID == 0 || !IsPlayerVehicle(heli, out vehicleInfo))
                return null;

            // Vehicle owner is allowed to mount.
            if (heli.OwnerID == player.userID)
                return null;

            // Team members are allowed to mount.
            if (player.Team != null && player.Team.members.Contains(heli.OwnerID))
                return null;

            player.ChatMessage(GetMessage(player.UserIDString, LangEntry.ErrorCannotMount));
            return False;
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null)
                return;

            if (_config.Minicopter.DespawnOnDisconnect)
            {
                var heli = _data.Minicopter.GetVehicle(player.UserIDString);
                if (heli != null)
                {
                    ScheduleDespawnVehicleIfUnmounted(heli);
                }
            }

            if (_config.ScrapTransportHelicopter.DespawnOnDisconnect)
            {
                var heli = _data.ScrapTransportHelicopter.GetVehicle(player.UserIDString);
                if (heli != null)
                {
                    ScheduleDespawnVehicleIfUnmounted(heli);
                }
            }

            if (_config.AttackHelicopter.DespawnOnDisconnect)
            {
                var heli = _data.AttackHelicopter.GetVehicle(player.UserIDString);
                if (heli != null)
                {
                    ScheduleDespawnVehicleIfUnmounted(heli);
                }
            }
        }

        private void ScheduleDespawnVehicleIfUnmounted(PlayerHelicopter heli)
        {
            NextTick(() =>
            {
                // Despawn vehicle when the owner disconnects.
                // If mounted, we will despawn it later when all players dismount.
                if (heli == null || heli.AnyMounted())
                    return;

                heli.Kill();
            });
        }

        private void OnEntityDismounted(BaseVehicleSeat seat)
        {
            if (seat == null)
                return;

            VehicleInfo vehicleInfo;
            var heli = seat.GetParentEntity() as PlayerHelicopter;
            if (heli == null
                || !heli.AnyMounted()
                || !IsPlayerVehicle(heli, out vehicleInfo)
                || !vehicleInfo.Config.DespawnOnDisconnect)
                return;

            // Despawn minicopter when fully dismounted, if the owner player has disconnected.
            var ownerPlayer = BasePlayer.FindByID(heli.OwnerID);
            if (ownerPlayer != null && ownerPlayer.IsConnected)
                return;

            heli.Kill();
        }

        private void CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container == null || !container.IsLocked())
                return;

            VehicleInfo vehicleInfo;
            var heli = container.GetParentEntity() as PlayerHelicopter;
            if (heli == null || !IsPlayerVehicle(heli, out vehicleInfo))
                return;

            if (!permission.UserHasPermission(heli.OwnerID.ToString(), vehicleInfo.Permissions.UnlimitedFuel))
                return;

            player.ChatMessage(GetMessage(player.UserIDString, LangEntry.ErrorUnlimitedFuel));
        }

        #endregion

        #region Commands

        private void CommandSpawnMinicopter(IPlayer player, string cmd, string[] args) =>
            SpawnCommandInternal(_vehicleInfoManager.Minicopter, player, cmd, args);

        private void CommandSpawnScrapTransportHelicopter(IPlayer player, string cmd, string[] args) =>
            SpawnCommandInternal(_vehicleInfoManager.ScrapTransportHelicopter, player, cmd, args);

        private void CommandSpawnAttackHelicopter(IPlayer player, string cmd, string[] args) =>
            SpawnCommandInternal(_vehicleInfoManager.AttackHelicopter, player, cmd, args);

        private void SpawnCommandInternal(VehicleInfo vehicleInfo, IPlayer player, string command, string[] args)
        {
            BasePlayer basePlayer;
            if (vehicleInfo == null
                || !VerifyPlayer(player, out basePlayer)
                || !VerifyPermission(player, vehicleInfo.Permissions.Spawn))
                return;

            var heli = FindPlayerVehicle(vehicleInfo, basePlayer);
            if (heli != null)
            {
                if (vehicleInfo.Config.AutoFetch && permission.UserHasPermission(player.Id, vehicleInfo.Permissions.Fetch))
                {
                    FetchVehicle(vehicleInfo, player, basePlayer, heli);
                }
                else
                {
                    player.Reply(GetMessage(player.Id, vehicleInfo.Messages.AlreadySpawned));
                }

                return;
            }

            if (_config.LimitPlayersToOneHelicopterType)
            {
                foreach (var otherVehicleInfo in _vehicleInfoManager.AllVehicles)
                {
                    if (otherVehicleInfo == vehicleInfo)
                        continue;

                    var otherVehicle = otherVehicleInfo.Data.GetVehicle(player.Id);
                    if (otherVehicle == null || otherVehicle.IsDestroyed)
                        continue;

                    if (!TryDespawnHeli(otherVehicleInfo, otherVehicle, basePlayer))
                    {
                        player.Reply(GetMessage(player.Id, LangEntry.ErrorConflictingHeli));
                        return;
                    }

                    otherVehicle.Kill();
                }
            }

            Vector3 position;
            Quaternion rotation;
            if (!VerifyOffCooldown(vehicleInfo, basePlayer, vehicleInfo.Config.SpawnCooldowns, vehicleInfo.Data.SpawnCooldowns)
                || !vehicleInfo.Config.CanSpawnBuildingBlocked && !VerifyNotBuildingBlocked(player, basePlayer)
                || SpawnWasBlocked(vehicleInfo, basePlayer)
                || !VerifyValidSpawnOrFetchPosition(vehicleInfo, basePlayer, out position, out rotation))
                return;

            heli = SpawnVehicle(vehicleInfo, basePlayer, position, rotation);
            if (heli == null)
                return;

            if (!permission.UserHasPermission(basePlayer.UserIDString, vehicleInfo.Permissions.NoCooldown))
            {
                _data.StartSpawnCooldown(vehicleInfo, basePlayer);
            }
        }

        private void CommandFetchMinicopter(IPlayer player, string cmd, string[] args) =>
            FetchCommandInternal(_vehicleInfoManager.Minicopter, player, cmd, args);

        private void CommandFetchScrapTransportHelicopter(IPlayer player, string cmd, string[] args) =>
            FetchCommandInternal(_vehicleInfoManager.ScrapTransportHelicopter, player, cmd, args);

        private void CommandFetchAttackHelicopter(IPlayer player, string cmd, string[] args) =>
            FetchCommandInternal(_vehicleInfoManager.AttackHelicopter, player, cmd, args);

        private void FetchCommandInternal(VehicleInfo vehicleInfo, IPlayer player, string command, string[] args)
        {
            BasePlayer basePlayer;
            PlayerHelicopter heli;
            if (vehicleInfo == null
                || !VerifyPlayer(player, out basePlayer)
                || !VerifyPermission(player, vehicleInfo.Permissions.Fetch)
                || !VerifyVehicleExists(player, basePlayer, vehicleInfo, out heli))
                return;

            FetchVehicle(vehicleInfo, player, basePlayer, heli);
        }

        private void CommandDespawnMinicopter(IPlayer player, string cmd, string[] args) =>
            DespawnCommandInternal(_vehicleInfoManager.Minicopter, player, cmd, args);

        private void CommandDespawnScrapTransportHelicopter(IPlayer player, string cmd, string[] args) =>
            DespawnCommandInternal(_vehicleInfoManager.ScrapTransportHelicopter, player, cmd, args);

        private void CommandDespawnAttackHelicopter(IPlayer player, string cmd, string[] args) =>
            DespawnCommandInternal(_vehicleInfoManager.AttackHelicopter, player, cmd, args);

        private void DespawnCommandInternal(VehicleInfo vehicleInfo, IPlayer player, string command, string[] args)
        {
            BasePlayer basePlayer;
            PlayerHelicopter heli;
            if (vehicleInfo == null
                || !VerifyPlayer(player, out basePlayer)
                || !VerifyPermission(player, vehicleInfo.Permissions.Despawn)
                || !VerifyVehicleExists(player, basePlayer, vehicleInfo, out heli))
                return;

            if (!vehicleInfo.Config.CanDespawnWhileOccupied && IsHeliOccupied(heli))
            {
                player.Reply(GetMessage(player.Id, LangEntry.ErrorHeliOccupied));
                return;
            }

            if (!VerifyVehicleWithinDistance(player, basePlayer, heli, vehicleInfo.Config.MaxDespawnDistance)
                || DespawnWasBlocked(vehicleInfo, basePlayer, heli))
                return;

            heli.Kill();
        }

        // Old command for backwards compatibility.
        [Command("spawnmini.give")]
        private void CommandGiveMinicopter(IPlayer player, string cmd, string[] args) =>
            GiveCommandInternal(_vehicleInfoManager.Minicopter, player, cmd, args);

        private void CommandGiveScrapTransportHelicopter(IPlayer player, string cmd, string[] args) =>
            GiveCommandInternal(_vehicleInfoManager.ScrapTransportHelicopter, player, cmd, args);

        private void CommandGiveAttackHelicopter(IPlayer player, string cmd, string[] args) =>
            GiveCommandInternal(_vehicleInfoManager.AttackHelicopter, player, cmd, args);

        private void GiveCommandInternal(VehicleInfo vehicleInfo, IPlayer player, string cmd, string[] args)
        {
            if (!player.IsServer)
                return;

            if (args.Length < 1)
            {
                PrintError($"Syntax: {cmd} <name or steamid>");
                return;
            }

            var recipientPlayer = BasePlayer.Find(args[0]);
            if (recipientPlayer == null)
            {
                PrintError($"{cmd}: No player found matching '{args[0]}'");
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
                    Puts($"Syntax: {cmd} <name or steamid> <x> <y> <z>");
                    return;
                }

                GiveVehicle(vehicleInfo, recipientPlayer, new Vector3(x, y, z));
                return;
            }

            GiveVehicle(vehicleInfo, recipientPlayer);
        }

        #endregion

        #region Helpers/Functions

        private static class StringUtils
        {
            public static string StripPrefix(string subject, string prefix)
            {
                return subject.StartsWith(prefix) ? subject.Substring(prefix.Length) : subject;
            }

            public static string StripPrefixes(string subject, params string[] prefixes)
            {
                foreach (var prefix in prefixes)
                {
                    subject = StripPrefix(subject, prefix);
                }

                return subject;
            }
        }

        public static void LogWarning(string message) => Interface.Oxide.LogWarning($"[Spawn Heli] {message}");
        public static void LogError(string message) => Interface.Oxide.LogError($"[Spawn Heli] {message}");

        private static bool VerifyPlayer(IPlayer player, out BasePlayer basePlayer)
        {
            if (player.IsServer)
            {
                basePlayer = null;
                return false;
            }

            basePlayer = player.Object as BasePlayer;
            return true;
        }

        private bool VerifyPermission(IPlayer player, string perm)
        {
            if (permission.UserHasPermission(player.Id, perm))
                return true;

            player.Reply(GetMessage(player.Id, LangEntry.ErrorNoPermission));
            return false;
        }

        private bool VerifyVehicleExists(IPlayer player, BasePlayer basePlayer, VehicleInfo vehicleInfo, out PlayerHelicopter heli)
        {
            heli = FindPlayerVehicle(vehicleInfo, basePlayer);
            if (heli != null)
                return true;

            player.Reply(GetMessage(player.Id, vehicleInfo.Messages.NotFound));
            return false;
        }

        private bool VerifyVehicleWithinDistance(IPlayer player, BasePlayer basePlayer, PlayerHelicopter heli, float maxDistance)
        {
            if (maxDistance < 0 || Vector3.Distance(basePlayer.transform.position, heli.transform.position) < maxDistance)
                return true;

            player.Reply(GetMessage(player.Id, LangEntry.ErrorHeliDistance));
            return false;
        }

        private bool VerifyValidSpawnOrFetchPosition(VehicleInfo vehicleInfo, BasePlayer player, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (vehicleInfo.Config.FixedSpawnDistanceConfig.Enabled)
            {
                position = GetFixedPositionForPlayer(vehicleInfo, player);
                rotation = GetFixedRotationForPlayer(vehicleInfo, player);
            }
            else
            {
                RaycastHit hit;
                if (!Physics.Raycast(player.eyes.HeadRay(), out hit, Mathf.Infinity, SpawnPointLayerMask))
                {
                    player.ChatMessage(GetMessage(player.UserIDString, LangEntry.ErrorNoSpawnLocationFound));
                    return false;
                }

                if (hit.distance > vehicleInfo.Config.MaxSpawnDistance)
                {
                    player.ChatMessage(GetMessage(player.UserIDString, LangEntry.ErrorSpawnDistance));
                    return false;
                }

                position = hit.point + Vector3.up * VerticalSpawnOffset;
            }

            var extents = vehicleInfo.Bounds.extents;
            var boundsCenter = position + rotation * vehicleInfo.Bounds.center;

            if (Physics.CheckBox(boundsCenter, extents, rotation, SpaceCheckLayerMask, QueryTriggerInteraction.Ignore)
                || Physics.CheckBox(position + Vector3.down * VerticalSpawnOffset / 2f, extents.WithY(VerticalSpawnOffset / 2f), rotation, Rust.Layers.Mask.Player_Server, QueryTriggerInteraction.Ignore))
            {
                player.ChatMessage(GetMessage(player.UserIDString, LangEntry.InsufficientSpace));
                return false;
            }

            return true;
        }

        private bool VerifyOffCooldown(VehicleInfo vehicleInfo, BasePlayer player, CooldownConfig cooldownConfig, Dictionary<string, DateTime> cooldownMap)
        {
            DateTime cooldownStart;
            if (!cooldownMap.TryGetValue(player.UserIDString, out cooldownStart)
                || permission.UserHasPermission(player.UserIDString, vehicleInfo.Permissions.NoCooldown))
                return true;

            var timeRemaining = CeilingTimeSpan(cooldownStart.AddSeconds(GetPlayerCooldownSeconds(cooldownConfig, player)) - DateTime.Now);
            if (timeRemaining.TotalSeconds <= 0)
            {
                _data.RemoveCooldown(cooldownMap, player);
                return true;
            }

            player.ChatMessage(GetMessage(player.UserIDString, LangEntry.ErrorOnCooldown, timeRemaining.ToString("g")));
            return false;
        }

        private bool VerifyNotBuildingBlocked(IPlayer player, BasePlayer basePlayer)
        {
            if (!basePlayer.IsBuildingBlocked())
                return true;

            player.Reply(GetMessage(basePlayer.UserIDString, LangEntry.ErrorBuildingBlocked));
            return false;
        }

        private static bool SpawnWasBlocked(VehicleInfo vehicleInfo, BasePlayer player)
        {
            var hookResult = Interface.CallHook(vehicleInfo.Hooks.Spawn, player);
            return hookResult is bool && !(bool)hookResult;
        }

        private static bool FetchWasBlocked(VehicleInfo vehicleInfo, BasePlayer player, PlayerHelicopter heli)
        {
            var hookResult = Interface.CallHook(vehicleInfo.Hooks.Fetch, player, heli);
            return hookResult is bool && !(bool)hookResult;
        }

        private static bool DespawnWasBlocked(VehicleInfo vehicleInfo, BasePlayer player, PlayerHelicopter heli)
        {
            var hookResult = Interface.CallHook(vehicleInfo.Hooks.Despawn, player, heli);
            return hookResult is bool && !(bool)hookResult;
        }

        private static TimeSpan CeilingTimeSpan(TimeSpan timeSpan)
        {
            return new TimeSpan((long)Math.Ceiling(1.0 * timeSpan.Ticks / 10000000) * 10000000);
        }

        private static Vector3 GetFixedPositionForPlayer(VehicleInfo vehicleInfo, BasePlayer player)
        {
            var forward = player.GetNetworkRotation() * Vector3.forward;
            forward.y = 0;
            return player.transform.position + forward.normalized * vehicleInfo.Config.FixedSpawnDistanceConfig.Distance + Vector3.up * VerticalSpawnOffset;
        }

        private static Quaternion GetFixedRotationForPlayer(VehicleInfo vehicleInfo, BasePlayer player)
        {
            return Quaternion.Euler(0, player.GetNetworkRotation().eulerAngles.y - vehicleInfo.Config.FixedSpawnDistanceConfig.RotationAngle, 0);
        }

        private static void EnableUnlimitedFuel(PlayerHelicopter heli)
        {
            var fuelSystem = heli.GetFuelSystem();
            fuelSystem.cachedHasFuel = true;
            fuelSystem.nextFuelCheckTime = float.MaxValue;
            fuelSystem.GetFuelContainer().SetFlag(BaseEntity.Flags.Locked, true);
        }

        private static bool AnyParentedPlayers(PlayerHelicopter heli)
        {
            foreach (var entity in heli.children)
            {
                if (entity is BasePlayer)
                    return true;
            }

            return false;
        }

        private static bool IsHeliOccupied(PlayerHelicopter heli)
        {
            return heli.AnyMounted() || AnyParentedPlayers(heli);
        }

        private static void UnparentPlayers(PlayerHelicopter heli)
        {
            var tempList = Pool.GetList<BasePlayer>();

            try
            {
                foreach (var entity in heli.children)
                {
                    var player = entity as BasePlayer;
                    if (player == null)
                        continue;

                    tempList.Add(player);
                }

                foreach (var player in tempList)
                {
                    player.SetParent(null, worldPositionStays: true);
                }
            }
            finally
            {
                Pool.FreeList(ref tempList);
            }
        }

        private bool IsPlayerVehicle(PlayerHelicopter heli, out VehicleInfo vehicleInfo)
        {
            vehicleInfo = _vehicleInfoManager.GetVehicleInfo(heli);
            return vehicleInfo != null && vehicleInfo.Data.HasVehicle(heli);
        }

        private PlayerHelicopter FindPlayerVehicle(VehicleInfo vehicleInfo, BasePlayer player)
        {
            ulong heliNetId;
            if (!vehicleInfo.Data.Vehicles.TryGetValue(player.UserIDString, out heliNetId))
                return null;

            var heli = BaseNetworkable.serverEntities.Find(new NetworkableId(heliNetId)) as PlayerHelicopter;
            if (heli == null)
            {
                _data.UnregisterVehicle(vehicleInfo, player.UserIDString);
            }

            return heli;
        }

        private void FetchVehicle(VehicleInfo vehicleInfo, IPlayer player, BasePlayer basePlayer, PlayerHelicopter heli)
        {
            var isOccupied = IsHeliOccupied(heli);
            if (isOccupied && (!vehicleInfo.Config.CanFetchWhileOccupied
                               || basePlayer.GetMountedVehicle() == heli
                               || basePlayer.GetParentEntity() == heli))
            {
                basePlayer.ChatMessage(GetMessage(basePlayer.UserIDString, LangEntry.ErrorHeliOccupied));
                return;
            }

            Vector3 position;
            Quaternion rotation;
            if (!VerifyVehicleWithinDistance(player, basePlayer, heli, vehicleInfo.Config.MaxFetchDistance)
                || !VerifyOffCooldown(vehicleInfo, basePlayer, vehicleInfo.Config.FetchCooldowns, vehicleInfo.Data.FetchCooldowns)
                || !vehicleInfo.Config.CanFetchBuildingBlocked && !VerifyNotBuildingBlocked(player, basePlayer)
                || FetchWasBlocked(vehicleInfo, basePlayer, heli)
                || !VerifyValidSpawnOrFetchPosition(vehicleInfo, basePlayer, out position, out rotation))
                return;

            if (isOccupied)
            {
                foreach (var mountPoint in heli.mountPoints)
                {
                    mountPoint.mountable?.DismountAllPlayers();
                }
            }

            if (AnyParentedPlayers(heli))
            {
                UnparentPlayers(heli);
            }

            if (vehicleInfo.Config.RepairOnFetch && vehicleInfo.Config.SpawnHealth > 0)
            {
                heli.SetHealth(Math.Max(heli.Health(), vehicleInfo.Config.SpawnHealth));
            }

            heli.rigidBody.velocity = Vector3.zero;
            heli.transform.SetPositionAndRotation(position, rotation);
            heli.UpdateNetworkGroup();
            heli.SendNetworkUpdateImmediate();

            if (!permission.UserHasPermission(basePlayer.UserIDString, vehicleInfo.Permissions.NoCooldown))
            {
                _data.StartFetchCooldown(vehicleInfo, basePlayer);
            }
        }

        private PlayerHelicopter SpawnVehicle(VehicleInfo vehicleInfo, BasePlayer player, Vector3 position, Quaternion rotation)
        {
            var heli = GameManager.server.CreateEntity(vehicleInfo.PrefabPath, position, rotation) as PlayerHelicopter;
            if (heli == null)
                return null;

            heli.OwnerID = player.userID;
            if (vehicleInfo.Config.SpawnHealth > 0)
            {
                heli.startHealth = vehicleInfo.Config.SpawnHealth;
            }

            heli.Spawn();

            if (permission.UserHasPermission(player.UserIDString, vehicleInfo.Permissions.UnlimitedFuel))
            {
                EnableUnlimitedFuel(heli);
            }
            else
            {
                AddInitialFuel(vehicleInfo, heli, player);
            }

            _data.RegisterVehicle(vehicleInfo, player.UserIDString, heli);
            return heli;
        }

        private void GiveVehicle(VehicleInfo vehicleInfo, BasePlayer player, Vector3? customPosition = null)
        {
            // Note: The give command does not auto fetch, but that could be changed in the future.
            if (FindPlayerVehicle(vehicleInfo, player) != null)
            {
                player.ChatMessage(GetMessage(player.UserIDString, vehicleInfo.Messages.AlreadySpawned));
                return;
            }

            var position = customPosition ?? GetFixedPositionForPlayer(vehicleInfo, player);
            var rotation = customPosition.HasValue ? Quaternion.identity : GetFixedRotationForPlayer(vehicleInfo, player);
            SpawnVehicle(vehicleInfo, player, position, rotation);
        }

        private bool TryDespawnHeli(VehicleInfo vehicleInfo, PlayerHelicopter heli, BasePlayer basePlayer)
        {
            if (!_config.AutoDespawnOtherHelicopterTypes)
                return false;

            if (!vehicleInfo.Config.CanDespawnWhileOccupied && IsHeliOccupied(heli))
                return false;

            if (DespawnWasBlocked(vehicleInfo, basePlayer, heli))
                return false;

            return true;
        }

        private float GetPlayerCooldownSeconds(CooldownConfig cooldownConfig, BasePlayer player)
        {
            var profileList = cooldownConfig.CooldownProfiles;
            if (profileList != null)
            {
                for (var i = profileList.Length - 1; i >= 0; i--)
                {
                    var profile = profileList[i];
                    if (profile.Permission != null
                        && permission.UserHasPermission(player.UserIDString, profile.Permission))
                        return profile.CooldownSeconds;
                }
            }

            return cooldownConfig.DefaultCooldown;
        }

        private int GetPlayerAllowedFuel(VehicleInfo vehicleInfo, BasePlayer player)
        {
            var fuelConfig = vehicleInfo.Config.FuelConfig;
            var profileList = fuelConfig.FuelProfiles;
            if (profileList != null)
            {
                for (var i = profileList.Length - 1; i >= 0; i--)
                {
                    var profile = profileList[i];
                    if (profile.Permission != null
                        && permission.UserHasPermission(player.UserIDString, profile.Permission))
                        return profile.FuelAmount;
                }
            }

            return fuelConfig.DefaultFuelAmount;
        }

        private void AddInitialFuel(VehicleInfo vehicleInfo, PlayerHelicopter heli, BasePlayer player)
        {
            var fuelAmount = GetPlayerAllowedFuel(vehicleInfo, player);
            if (fuelAmount == 0)
                return;

            var fuelContainer = heli.GetFuelSystem().GetFuelContainer();
            if (fuelAmount < 0)
            {
                // Value of -1 is documented to represent max stack size.
                fuelAmount = fuelContainer.allowedItem.stackable;
            }

            fuelContainer.inventory.AddItem(fuelContainer.allowedItem, fuelAmount);
        }

        #endregion

        #region Vehicle Info

        private class VehicleInfo
        {
            public class PermissionSet
            {
                public string Spawn;
                public string Fetch;
                public string Despawn;
                public string UnlimitedFuel;
                public string NoDecay;
                public string NoCooldown;
            }

            public class HookSet
            {
                public string Spawn;
                public string Fetch;
                public string Despawn;
            }

            public class MessageSet
            {
                public LangEntry Destroyed;
                public LangEntry AlreadySpawned;
                public LangEntry NotFound;
            }

            public string VehicleName { private get; set; }
            public string PrefabPath;
            public Bounds Bounds;
            public string GiveCommand  { get; private set; }
            public VehicleConfig Config;
            public VehicleData Data;
            public uint PrefabId { get; private set; }
            public PermissionSet Permissions;
            public HookSet Hooks;
            public MessageSet Messages;

            public void Init(SpawnHeli plugin)
            {
                GiveCommand = $"{nameof(SpawnHeli)}.{VehicleName}.give".ToLower();

                Permissions = new PermissionSet
                {
                    Spawn = $"{nameof(SpawnHeli)}.{VehicleName}.spawn".ToLower(),
                    Fetch = $"{nameof(SpawnHeli)}.{VehicleName}.fetch".ToLower(),
                    Despawn = $"{nameof(SpawnHeli)}.{VehicleName}.despawn".ToLower(),
                    UnlimitedFuel = $"{nameof(SpawnHeli)}.{VehicleName}.unlimitedfuel".ToLower(),
                    NoDecay = $"{nameof(SpawnHeli)}.{VehicleName}.nodecay".ToLower(),
                    NoCooldown = $"{nameof(SpawnHeli)}.{VehicleName}.nocooldown".ToLower(),
                };

                plugin.permission.RegisterPermission(Permissions.Spawn, plugin);
                plugin.permission.RegisterPermission(Permissions.Fetch, plugin);
                plugin.permission.RegisterPermission(Permissions.Despawn, plugin);
                plugin.permission.RegisterPermission(Permissions.UnlimitedFuel, plugin);
                plugin.permission.RegisterPermission(Permissions.NoDecay, plugin);
                plugin.permission.RegisterPermission(Permissions.NoCooldown, plugin);

                if (Config.FuelConfig.FuelProfiles != null)
                {
                    foreach (var profile in Config.FuelConfig.FuelProfiles)
                    {
                        if (profile.Permission != null)
                        {
                            plugin.permission.RegisterPermission(profile.Permission, plugin);
                        }
                    }
                }

                if (Config.SpawnCooldowns.CooldownProfiles != null)
                {
                    foreach (var profile in Config.SpawnCooldowns.CooldownProfiles)
                    {
                        if (profile.Permission != null)
                        {
                            plugin.permission.RegisterPermission(profile.Permission, plugin);
                        }
                    }
                }

                if (Config.FetchCooldowns.CooldownProfiles != null)
                {
                    foreach (var profile in Config.FetchCooldowns.CooldownProfiles)
                    {
                        if (profile.Permission != null)
                        {
                            plugin.permission.RegisterPermission(profile.Permission, plugin);
                        }
                    }
                }
            }

            public void OnServerInitialized()
            {
                PrefabId = GameManager.server.FindPrefab(PrefabPath)?.GetComponent<BaseEntity>()?.prefabID ?? 0;
            }
        }

        private class VehicleInfoManager
        {
            public VehicleInfo Minicopter { get; private set; }
            public VehicleInfo ScrapTransportHelicopter { get; private set; }
            public VehicleInfo AttackHelicopter { get; private set; }
            public VehicleInfo[] AllVehicles { get; private set; }

            private readonly SpawnHeli _plugin;
            private readonly Dictionary<uint, VehicleInfo> _prefabIdToVehicleInfo = new Dictionary<uint, VehicleInfo>();

            public bool AnyOwnerOnly => AllVehicles.Any(vehicleInfo => vehicleInfo.Config.OnlyOwnerAndTeamCanMount);
            public bool AnyDespawnOnDisconnect => AllVehicles.Any(vehicleInfo => vehicleInfo.Config.DespawnOnDisconnect);

            private Configuration _config => _plugin._config;
            private SaveData _data => _plugin._data;

            public VehicleInfoManager(SpawnHeli plugin)
            {
                _plugin = plugin;
            }

            public void Init()
            {
                AllVehicles = new[]
                {
                    Minicopter = new VehicleInfo
                    {
                        VehicleName = PermissionMinicopter,
                        PrefabPath = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                        Config = _config.Minicopter,
                        Data = _data.Minicopter,
                        Hooks = new VehicleInfo.HookSet
                        {
                            Spawn = "OnMyMiniSpawn",
                            Fetch = "OnMyMiniFetch",
                            Despawn = "OnMyMiniDespawn",
                        },
                        Messages = new VehicleInfo.MessageSet
                        {
                            Destroyed = LangEntry.MiniDestroyed,
                            AlreadySpawned = LangEntry.ErrorMiniExists,
                            NotFound = LangEntry.ErrorMiniNotFound,
                        },
                        Bounds = new Bounds
                        {
                            center = new Vector3(-0.001f, 1.114f, -0.36f),
                            extents = new Vector3(1.32f, 0.87f, 2.16f),
                        },
                    },
                    ScrapTransportHelicopter = new VehicleInfo
                    {
                        VehicleName = PermissionScrapHeli,
                        PrefabPath = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
                        Config = _config.ScrapTransportHelicopter,
                        Data = _data.ScrapTransportHelicopter,
                        Hooks = new VehicleInfo.HookSet
                        {
                            Spawn = "OnMyScrapHeliSpawn",
                            Fetch = "OnMyScrapHeliFetch",
                            Despawn = "OnMyScrapHeliDespawn",
                        },
                        Messages = new VehicleInfo.MessageSet
                        {
                            Destroyed = LangEntry.ScrapHeliDestroyed,
                            AlreadySpawned = LangEntry.ErrorScrapHeliExist,
                            NotFound = LangEntry.ErrorScrapHeliNotFound,
                        },
                        Bounds = new Bounds
                        {
                            center = new Vector3(0, 2.25f, -1.25f),
                            extents = new Vector3(2.2f, 2.25f, 6f),
                        },
                    },
                    AttackHelicopter = new VehicleInfo
                    {
                        VehicleName = PermissionAttackHeli,
                        PrefabPath = "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab",
                        Config = _config.AttackHelicopter,
                        Data = _data.AttackHelicopter,
                        Hooks = new VehicleInfo.HookSet
                        {
                            Spawn = "OnMyAttackHeliSpawn",
                            Fetch = "OnMyAttackHeliFetch",
                            Despawn = "OnMyAttackHeliDespawn",
                        },
                        Messages = new VehicleInfo.MessageSet
                        {
                            Destroyed = LangEntry.AttackHeliDestroyed,
                            AlreadySpawned = LangEntry.ErrorAttackHeliExists,
                            NotFound = LangEntry.ErrorAttackHeliNotFound,
                        },
                        Bounds = new Bounds
                        {
                            center = new Vector3(0, 1.678f, -1.676f),
                            extents = new Vector3(1.303f, 1.678f, 5.495f),
                        },
                    },
                };

                foreach (var vehicleInfo in AllVehicles)
                {
                    vehicleInfo.Init(_plugin);
                }

                _plugin.AddCovalenceCommand(Minicopter.Config.SpawnCommands, nameof(CommandSpawnMinicopter));
                _plugin.AddCovalenceCommand(Minicopter.Config.FetchCommands, nameof(CommandFetchMinicopter));
                _plugin.AddCovalenceCommand(Minicopter.Config.DespawnCommands, nameof(CommandDespawnMinicopter));
                _plugin.AddCovalenceCommand(Minicopter.GiveCommand, nameof(CommandGiveMinicopter));

                _plugin.AddCovalenceCommand(ScrapTransportHelicopter.Config.SpawnCommands, nameof(CommandSpawnScrapTransportHelicopter));
                _plugin.AddCovalenceCommand(ScrapTransportHelicopter.Config.FetchCommands, nameof(CommandFetchScrapTransportHelicopter));
                _plugin.AddCovalenceCommand(ScrapTransportHelicopter.Config.DespawnCommands, nameof(CommandDespawnScrapTransportHelicopter));
                _plugin.AddCovalenceCommand(ScrapTransportHelicopter.GiveCommand, nameof(CommandGiveScrapTransportHelicopter));

                _plugin.AddCovalenceCommand(AttackHelicopter.Config.SpawnCommands, nameof(CommandSpawnAttackHelicopter));
                _plugin.AddCovalenceCommand(AttackHelicopter.Config.FetchCommands, nameof(CommandFetchAttackHelicopter));
                _plugin.AddCovalenceCommand(AttackHelicopter.Config.DespawnCommands, nameof(CommandDespawnAttackHelicopter));
                _plugin.AddCovalenceCommand(AttackHelicopter.GiveCommand, nameof(CommandGiveAttackHelicopter));
            }

            public void OnServerInitialized()
            {
                foreach (var vehicleInfo in AllVehicles)
                {
                    vehicleInfo.OnServerInitialized();

                    if (vehicleInfo.PrefabId != 0)
                    {
                        _prefabIdToVehicleInfo[vehicleInfo.PrefabId] = vehicleInfo;
                    }
                    else
                    {
                        LogError($"Unable to determine Prefab ID for prefab: {vehicleInfo.PrefabPath}");
                    }
                }
            }

            public VehicleInfo GetVehicleInfo(BaseEntity entity)
            {
                VehicleInfo vehicleInfo;
                return _prefabIdToVehicleInfo.TryGetValue(entity.prefabID, out vehicleInfo)
                    ? vehicleInfo
                    : null;
            }
        }

        #endregion

        #region Data

        [JsonObject(MemberSerialization.OptIn)]
        private class LegacySaveData
        {
            private const string Filename = LegacyPluginName;

            public static LegacySaveData LoadIfExists()
            {
                return Interface.Oxide.DataFileSystem.ExistsDatafile(Filename)
                    ? Interface.Oxide.DataFileSystem.ReadObject<LegacySaveData>(Filename)
                    : null;
            }

            [JsonProperty("playerMini")]
            public Dictionary<string, ulong> playerMini = new Dictionary<string, ulong>();

            [JsonProperty("spawnCooldowns")]
            public Dictionary<string, DateTime> spawnCooldowns = new Dictionary<string, DateTime>();

            [JsonProperty("cooldown")]
            private Dictionary<string, DateTime> deprecatedCooldown
            {
                set { spawnCooldowns = value; }
            }

            [JsonProperty("fetchCooldowns")]
            public Dictionary<string, DateTime> fetchCooldowns = new Dictionary<string, DateTime>();

            public void Delete()
            {
                Interface.Oxide.DataFileSystem.DeleteDataFile(Filename);
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class VehicleData
        {
            [JsonProperty("Vehicles")]
            public Dictionary<string, ulong> Vehicles = new Dictionary<string, ulong>();

            [JsonProperty("SpawnCooldowns")]
            public Dictionary<string, DateTime> SpawnCooldowns = new Dictionary<string, DateTime>();

            [JsonProperty("FetchCooldowns")]
            public Dictionary<string, DateTime> FetchCooldowns = new Dictionary<string, DateTime>();

            public PlayerHelicopter GetVehicle(string playerId)
            {
                ulong netId;
                return Vehicles.TryGetValue(playerId, out netId)
                    ? BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as PlayerHelicopter
                    : null;
            }

            public bool HasVehicle(BaseEntity vehicle)
            {
                return Vehicles.ContainsValue(vehicle.net.ID.Value);
            }

            public bool RegisterVehicle(string playerId, ulong netId)
            {
                return Vehicles.TryAdd(playerId, netId);
            }

            public bool UnregisterVehicle(string playerId)
            {
                return Vehicles.Remove(playerId);
            }

            public void SetSpawnCooldown(string playerId, DateTime dateTime)
            {
                SpawnCooldowns[playerId] = dateTime;
            }

            public void SetFetchCooldown(string playerId, DateTime dateTime)
            {
                FetchCooldowns[playerId] = dateTime;
            }

            public bool Clean()
            {
                if (Vehicles.Count == 0)
                    return false;

                var changed = false;

                foreach (var entry in Vehicles.ToList())
                {
                    var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(entry.Value)) as PlayerHelicopter;
                    if (entity != null)
                        continue;

                    Vehicles.Remove(entry.Key);
                    changed = true;
                }

                return changed;
            }

            public bool Reset()
            {
                var result = Vehicles.Count > 0 || SpawnCooldowns.Count > 0 || FetchCooldowns.Count > 0;
                Vehicles.Clear();
                SpawnCooldowns.Clear();
                FetchCooldowns.Clear();
                return result;
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class SaveData
        {
            private const string Filename = nameof(SpawnHeli);

            public static SaveData Load()
            {
                var exists = Interface.Oxide.DataFileSystem.ExistsDatafile(Filename);
                var data = Interface.Oxide.DataFileSystem.ReadObject<SaveData>(Filename) ?? new SaveData();
                if (!exists)
                {
                    var legacyData = LegacySaveData.LoadIfExists();
                    if (legacyData != null)
                    {
                        data.Minicopter.Vehicles = legacyData.playerMini;
                        data.Minicopter.SpawnCooldowns = legacyData.spawnCooldowns;
                        data.Minicopter.FetchCooldowns = legacyData.fetchCooldowns;
                        data.SaveIfChanged();
                        legacyData.Delete();
                    }
                }

                return data;
            }

            private bool _dirty;

            [JsonProperty("Minicopter")]
            public VehicleData Minicopter = new VehicleData();

            [JsonProperty("ScrapTransportHelicopter")]
            public VehicleData ScrapTransportHelicopter = new VehicleData();

            [JsonProperty("AttackHelicopter")]
            public VehicleData AttackHelicopter = new VehicleData();

            public void Clean()
            {
                _dirty |= Minicopter.Clean();
                _dirty |= ScrapTransportHelicopter.Clean();
                _dirty |= AttackHelicopter.Clean();
            }

            public void Reset()
            {
                _dirty |= Minicopter.Reset();
                _dirty |= ScrapTransportHelicopter.Reset();
                _dirty |= AttackHelicopter.Reset();
            }

            public void SaveIfChanged()
            {
                if (!_dirty)
                    return;

                Interface.Oxide.DataFileSystem.WriteObject(Filename, this);
                _dirty = false;
            }

            public void StartSpawnCooldown(VehicleInfo vehicleInfo, BasePlayer player)
            {
                vehicleInfo.Data.SetSpawnCooldown(player.UserIDString, DateTime.Now);
                _dirty = true;
            }

            public void StartFetchCooldown(VehicleInfo vehicleInfo, BasePlayer player)
            {
                vehicleInfo.Data.SetFetchCooldown(player.UserIDString, DateTime.Now);
                _dirty = true;
            }

            public void RegisterVehicle(VehicleInfo vehicleInfo, string playerId, PlayerHelicopter heli)
            {
                vehicleInfo.Data.RegisterVehicle(playerId, heli.net.ID.Value);
                _dirty = true;
            }

            public void UnregisterVehicle(VehicleInfo vehicleInfo, string playerId)
            {
                vehicleInfo.Data.UnregisterVehicle(playerId);
                _dirty = true;
            }

            public void RemoveCooldown(Dictionary<string, DateTime> cooldownMap, BasePlayer player)
            {
                cooldownMap.Remove(player.UserIDString);
                _dirty = true;
            }
        }

        #endregion

        #region Configuration

        [JsonObject(MemberSerialization.OptIn)]
        private class BasePermissionAmount
        {
            [JsonProperty("Permission suffix")]
            protected string PermissionSuffix;

            [JsonIgnore]
            public string Permission { get; protected set; }

            public void Init(string permissionInfix)
            {
                if (!string.IsNullOrWhiteSpace(PermissionSuffix))
                {
                    Permission = $"{nameof(SpawnHeli)}.{permissionInfix}.{PermissionSuffix}".ToLower();
                }
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class FuelProfile : BasePermissionAmount
        {
            [JsonProperty("Fuel amount")]
            public int FuelAmount;

            // Default constructor for JSON, necessary because there's another constructor.
            public FuelProfile() { }

            public FuelProfile(string permissionSuffix, int fuelAmount)
            {
                PermissionSuffix = permissionSuffix;
                FuelAmount = fuelAmount;
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class FuelConfig
        {
            [JsonProperty("Default fuel amount")]
            public int DefaultFuelAmount;

            [JsonProperty("Fuel profiles requiring permission")]
            public FuelProfile[] FuelProfiles =
            {
                new FuelProfile("100", 100),
                new FuelProfile("500", 500),
                new FuelProfile("1000", 1000),
            };

            public void Init(string vehicleName)
            {
                if (FuelProfiles != null)
                {
                    foreach (var profile in FuelProfiles)
                    {
                        profile.Init($"{vehicleName}.fuel");
                    }
                }
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class CooldownProfile : BasePermissionAmount
        {
            [JsonProperty("Cooldown (seconds)")]
            public float CooldownSeconds;

            // Default constructor for JSON, necessary because there's another constructor.
            public CooldownProfile() { }

            public CooldownProfile(string permissionSuffix, float cooldownSeconds)
            {
                PermissionSuffix = permissionSuffix;
                CooldownSeconds = cooldownSeconds;
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class CooldownConfig
        {
            [JsonProperty("Default cooldown (seconds)")]
            public float DefaultCooldown;

            [JsonProperty("Cooldown profiles requiring permission")]
            public CooldownProfile[] CooldownProfiles;

            public void Init(string vehicleName, string cooldownType)
            {
                if (CooldownProfiles != null)
                {
                    foreach (var profile in CooldownProfiles)
                    {
                        profile.Init($"{vehicleName}.cooldown.{cooldownType}");
                    }
                }
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class FixedSpawnDistanceConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;

            [JsonProperty("Distance from player")]
            public float Distance = 3;

            [JsonProperty("Helicopter rotation angle")]
            public float RotationAngle = 90;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class VehicleConfig
        {
            [JsonProperty("Spawn commands")]
            public string[] SpawnCommands;

            [JsonProperty("Fetch commands")]
            public string[] FetchCommands;

            [JsonProperty("Despawn commands")]
            public string[] DespawnCommands;

            [JsonProperty("Can despawn while occupied")]
            public bool CanDespawnWhileOccupied;

            [JsonProperty("Can fetch while occupied")]
            public bool CanFetchWhileOccupied;

            [JsonProperty("Can spawn while building blocked")]
            public bool CanSpawnBuildingBlocked;

            [JsonProperty("Can fetch while building blocked")]
            public bool CanFetchBuildingBlocked;

            [JsonProperty("Auto fetch")]
            public bool AutoFetch;

            [JsonProperty("Repair on fetch")]
            public bool RepairOnFetch;

            [JsonProperty("Max spawn distance")]
            public float MaxSpawnDistance = 5f;

            [JsonProperty("Max fetch distance")]
            public float MaxFetchDistance = -1;

            [JsonProperty("Max despawn distance")]
            public float MaxDespawnDistance = -1;

            [JsonProperty("Fixed spawn distance")]
            public FixedSpawnDistanceConfig FixedSpawnDistanceConfig = new FixedSpawnDistanceConfig();

            [JsonProperty("Only owner and team can mount")]
            public bool OnlyOwnerAndTeamCanMount;

            [JsonProperty("Spawn health")]
            public float SpawnHealth;

            [JsonProperty("Destroy on disconnect")]
            public bool DespawnOnDisconnect;

            [JsonProperty("Fuel")]
            public FuelConfig FuelConfig = new FuelConfig();

            [JsonProperty("Spawn cooldowns")]
            public CooldownConfig SpawnCooldowns = new CooldownConfig
            {
                DefaultCooldown = 3600f,
                CooldownProfiles = new[]
                {
                    new CooldownProfile("1hr", 3600),
                    new CooldownProfile("10m", 600),
                    new CooldownProfile("10s", 10),
                },
            };

            [JsonProperty("Fetch cooldowns")]
            public CooldownConfig FetchCooldowns = new CooldownConfig
            {
                DefaultCooldown = 10f,
                CooldownProfiles = new[]
                {
                    new CooldownProfile("1hr", 3600),
                    new CooldownProfile("10m", 600),
                    new CooldownProfile("10s", 10),
                },
            };

            public void Init(string vehicleName)
            {
                FuelConfig?.Init(vehicleName);
                SpawnCooldowns?.Init(vehicleName, "spawn");
                FetchCooldowns?.Init(vehicleName, "fetch");
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("Limit players to one helicopter type at a time")]
            public bool LimitPlayersToOneHelicopterType;

            [JsonProperty("Try to auto despawn other helicopter types")]
            public bool AutoDespawnOtherHelicopterTypes;

            [JsonProperty("Minicopter")]
            public VehicleConfig Minicopter = new VehicleConfig
            {
                SpawnCommands = new[] { "mymini" },
                FetchCommands = new[] { "fmini" },
                DespawnCommands = new[] { "nomini" },
                SpawnHealth = 750,
            };

            [JsonProperty("ScrapTransportHelicopter")]
            public VehicleConfig ScrapTransportHelicopter = new VehicleConfig
            {
                SpawnCommands = new[] { "myheli" },
                FetchCommands = new[] { "fheli" },
                DespawnCommands = new[] { "noheli" },
                SpawnHealth = 1000,
            };

            [JsonProperty("AttackHelicopter")]
            public VehicleConfig AttackHelicopter = new VehicleConfig
            {
                SpawnCommands = new[] { "myattack" },
                FetchCommands = new[] { "fattack" },
                DespawnCommands = new[] { "noattack" },
                SpawnHealth = 850,
            };

            public bool Migrate()
            {
                var changed = false;

                if (DeprecatedCanDespawnWhileOccupied)
                {
                    Minicopter.CanDespawnWhileOccupied = DeprecatedCanDespawnWhileOccupied;
                    DeprecatedCanDespawnWhileOccupied = false;
                    changed = true;
                }

                if (DeprecatedCanFetchWhileOccupied)
                {
                    Minicopter.CanFetchWhileOccupied = DeprecatedCanFetchWhileOccupied;
                    DeprecatedCanFetchWhileOccupied = false;
                    changed = true;
                }

                if (DeprecatedCanSpawnBuildingBlocked)
                {
                    Minicopter.CanSpawnBuildingBlocked = DeprecatedCanSpawnBuildingBlocked;
                    DeprecatedCanSpawnBuildingBlocked = DeprecatedCanFetchWhileOccupied;
                    changed = true;
                }

                if (!DeprecatedCanFetchBuildingBlocked)
                {
                    Minicopter.CanFetchBuildingBlocked = DeprecatedCanFetchBuildingBlocked;
                    DeprecatedCanFetchBuildingBlocked = true;
                    changed = true;
                }

                if (DeprecatedAutoFetch)
                {
                    Minicopter.AutoFetch = DeprecatedAutoFetch;
                    DeprecatedAutoFetch = false;
                    changed = true;
                }

                if (DeprecatedRepairOnFetch)
                {
                    Minicopter.RepairOnFetch = DeprecatedRepairOnFetch;
                    DeprecatedRepairOnFetch = false;
                    changed = true;
                }

                if (DeprecatedFuelAmount != 0)
                {
                    Minicopter.FuelConfig.DefaultFuelAmount = DeprecatedFuelAmount;
                    DeprecatedFuelAmount = 0;
                    changed = true;
                }

                if (DeprecatedFuelAmountsRequiringPermission != null)
                {
                    Minicopter.FuelConfig.FuelProfiles = DeprecatedFuelAmountsRequiringPermission
                        .Select(amount => new FuelProfile(amount.ToString(), amount))
                        .ToArray();

                    DeprecatedFuelAmountsRequiringPermission = null;
                    changed = true;
                }

                if (DeprecatedNoMiniDistance != 0)
                {
                    Minicopter.MaxDespawnDistance = DeprecatedNoMiniDistance;
                    Minicopter.MaxFetchDistance = DeprecatedNoMiniDistance;
                    DeprecatedNoMiniDistance = 0;
                    changed = true;
                }

                if (DeprecatedMaxSpawnDistance != 0)
                {
                    Minicopter.MaxSpawnDistance = DeprecatedMaxSpawnDistance;
                    DeprecatedMaxSpawnDistance = 0;
                    changed = true;
                }

                if (!DeprecatedUseFixedSpawnDistance)
                {
                    Minicopter.FixedSpawnDistanceConfig.Enabled = false;
                    DeprecatedUseFixedSpawnDistance = true;
                    changed = true;
                }

                if (DeprecatedFixedSpawnDistance != 0)
                {
                    Minicopter.FixedSpawnDistanceConfig.Distance = DeprecatedFixedSpawnDistance;
                    DeprecatedFixedSpawnDistance = 0;
                    changed = true;
                }

                if (DeprecatedFixedSpawnRotationAngle != 0)
                {
                    Minicopter.FixedSpawnDistanceConfig.RotationAngle = DeprecatedFixedSpawnRotationAngle;
                    DeprecatedFixedSpawnRotationAngle = 0;
                    changed = true;
                }

                if (DeprecatedOwnerOnly)
                {
                    Minicopter.OnlyOwnerAndTeamCanMount = DeprecatedOwnerOnly;
                    DeprecatedOwnerOnly = false;
                    changed = true;
                }

                if (DeprecatedDefaultSpawnCooldown != 0)
                {
                    Minicopter.SpawnCooldowns.DefaultCooldown = DeprecatedDefaultSpawnCooldown;
                    DeprecatedDefaultSpawnCooldown = 0;
                    changed = true;
                }

                if (DeprecatedSpawnPermissionCooldowns != null)
                {
                    Minicopter.SpawnCooldowns.CooldownProfiles = DeprecatedSpawnPermissionCooldowns
                        .Select(entry => new CooldownProfile(StringUtils.StripPrefixes(entry.Key, LegacyPermissionPrefix), entry.Value))
                        .ToArray();

                    DeprecatedSpawnPermissionCooldowns = null;
                    changed = true;
                }

                if (DeprecatedDefaultFetchCooldown != 0)
                {
                    Minicopter.FetchCooldowns.DefaultCooldown = DeprecatedDefaultFetchCooldown;
                    DeprecatedDefaultFetchCooldown = 0;
                    changed = true;
                }

                if (DeprecatedFetchPermissionCooldowns != null)
                {
                    Minicopter.FetchCooldowns.CooldownProfiles = DeprecatedFetchPermissionCooldowns
                        .Select(entry => new CooldownProfile(StringUtils.StripPrefixes(entry.Key, LegacyPermissionPrefix, "fetch."), entry.Value))
                        .ToArray();

                    DeprecatedFetchPermissionCooldowns = null;
                    changed = true;
                }

                if (DeprecatedSpawnHealth != 0)
                {
                    Minicopter.SpawnHealth = DeprecatedSpawnHealth;
                    DeprecatedSpawnHealth = 0;
                    changed = true;
                }

                if (DeprecatedDespawnOnDisconnect)
                {
                    Minicopter.DespawnOnDisconnect = DeprecatedDespawnOnDisconnect;
                    DeprecatedDespawnOnDisconnect = false;
                    changed = true;
                }

                return changed;
            }

            [JsonProperty("CanDespawnWhileOccupied", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private bool DeprecatedCanDespawnWhileOccupied;

            [JsonProperty("CanFetchWhileOccupied", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private bool DeprecatedCanFetchWhileOccupied;

            [JsonProperty("CanSpawnBuildingBlocked", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private bool DeprecatedCanSpawnBuildingBlocked;

            [JsonProperty("CanFetchBuildingBlocked", DefaultValueHandling = DefaultValueHandling.Ignore)]
            [DefaultValue(true)]
            private bool DeprecatedCanFetchBuildingBlocked = true;

            [JsonProperty("AutoFetch", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private bool DeprecatedAutoFetch;

            [JsonProperty("RepairOnFetch", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private bool DeprecatedRepairOnFetch;

            [JsonProperty("FuelAmount", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private int DeprecatedFuelAmount;

            [JsonProperty("FuelAmountsRequiringPermission", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private int[] DeprecatedFuelAmountsRequiringPermission;

            [JsonProperty("MaxNoMiniDistance", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private float DeprecatedNoMiniDistance;

            [JsonProperty("MaxSpawnDistance", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private float DeprecatedMaxSpawnDistance;

            [JsonProperty("UseFixedSpawnDistance", DefaultValueHandling = DefaultValueHandling.Ignore)]
            [DefaultValue(true)]
            private bool DeprecatedUseFixedSpawnDistance = true;

            [JsonProperty("FixedSpawnDistance", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private float DeprecatedFixedSpawnDistance;

            [JsonProperty("FixedSpawnRotationAngle", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private float DeprecatedFixedSpawnRotationAngle;

            [JsonProperty("OwnerAndTeamCanMount", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private bool DeprecatedOwnerOnly;

            [JsonProperty("DefaultSpawnCooldown", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private float DeprecatedDefaultSpawnCooldown;

            [JsonProperty("PermissionSpawnCooldowns", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private Dictionary<string, float> DeprecatedSpawnPermissionCooldowns;

            [JsonProperty("DefaultFetchCooldown", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private float DeprecatedDefaultFetchCooldown;

            [JsonProperty("PermissionFetchCooldowns", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private Dictionary<string, float> DeprecatedFetchPermissionCooldowns;

            [JsonProperty("SpawnHealth", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private float DeprecatedSpawnHealth;

            [JsonProperty("DestroyOnDisconnect", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private bool DeprecatedDespawnOnDisconnect;

            public void Init()
            {
                Minicopter.Init(PermissionMinicopter);
                ScrapTransportHelicopter.Init(PermissionScrapHeli);
                AttackHelicopter.Init(PermissionAttackHeli);
            }
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #region Configuration Helpers

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
            var changed = false;

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
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                    throw new JsonException();

                if (MaybeUpdateConfig(_config) | _config.Migrate())
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                PrintError(e.Message);
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

        #endregion

        #region Localization

        private class LangEntry
        {
            public enum Lang
            {
                en,
            }

            public static readonly List<LangEntry> AllLangEntries = new List<LangEntry>();

            public static readonly LangEntry ErrorNoPermission = new LangEntry("error_no_permission", new Dictionary<Lang, string>
            {
                [Lang.en] = "You do not have permission to use this command.",
            });
            public static readonly LangEntry ErrorBuildingBlocked = new LangEntry("error_building_blocked", new Dictionary<Lang, string>
            {
                [Lang.en] = "Cannot do that while building blocked.",
            });
            public static readonly LangEntry ErrorOnCooldown = new LangEntry("error_on_cooldown", new Dictionary<Lang, string>
            {
                [Lang.en] = "You have <color=red>{0}</color> until your cooldown ends.",
            });
            public static readonly LangEntry InsufficientSpace = new LangEntry("error_insufficient_space", new Dictionary<Lang, string>
            {
                [Lang.en] = "Not enough space.",
            });
            public static readonly LangEntry ErrorConflictingHeli = new LangEntry("error_conflicting_heli", new Dictionary<Lang, string>
            {
                [Lang.en] = "You must first destroy your other helicopter(s) before you can spawn a new one.",
            });

            public static readonly LangEntry ErrorSpawnDistance = new LangEntry("error_spawn_distance", new Dictionary<Lang, string>
            {
                [Lang.en] = "You cannot spawn the helicopter that far away.",
            });
            public static readonly LangEntry ErrorNoSpawnLocationFound = new LangEntry("error_spawn_location", new Dictionary<Lang, string>
            {
                [Lang.en] = "No suitable spawn location found.",
            });
            public static readonly LangEntry ErrorHeliOccupied = new LangEntry("error_heli_occupied", new Dictionary<Lang, string>
            {
                [Lang.en] = "The helicopter is currently occupied.",
            });
            public static readonly LangEntry ErrorHeliDistance = new LangEntry("error_heli_distance", new Dictionary<Lang, string>
            {
                [Lang.en] = "The helicopter is too far away.",
            });
            public static readonly LangEntry ErrorCannotMount = new LangEntry("error_cannot_mount", new Dictionary<Lang, string>
            {
                [Lang.en] = "You are not the owner of this helicopter or in the owner's team.",
            });
            public static readonly LangEntry ErrorUnlimitedFuel = new LangEntry("error_unlimited_fuel", new Dictionary<Lang, string>
            {
                [Lang.en] = "That helicopter doesn't need fuel.",
            });

            public static readonly LangEntry MiniDestroyed = new LangEntry("info_mini_destroyed", new Dictionary<Lang, string>
            {
                [Lang.en] = "Your Minicopter has been destroyed.",
            });
            public static readonly LangEntry ScrapHeliDestroyed = new LangEntry("info_scrap_heli_destroyed", new Dictionary<Lang, string>
            {
                [Lang.en] = "Your Scrap Heli has been destroyed.",
            });
            public static readonly LangEntry AttackHeliDestroyed = new LangEntry("info_attack_heli_destroyed", new Dictionary<Lang, string>
            {
                [Lang.en] = "Your Attack Heli has been destroyed.",
            });

            public static readonly LangEntry ErrorMiniExists = new LangEntry("error_mini_exists", new Dictionary<Lang, string>
            {
                [Lang.en] = "You already have a Minicopter.",
            });
            public static readonly LangEntry ErrorScrapHeliExist = new LangEntry("error_scrap_heli_exists", new Dictionary<Lang, string>
            {
                [Lang.en] = "You already have a Scrap Heli.",
            });
            public static readonly LangEntry ErrorAttackHeliExists = new LangEntry("error_attack_heli_exists", new Dictionary<Lang, string>
            {
                [Lang.en] = "You already have an Attack Heli.",
            });

            public static readonly LangEntry ErrorMiniNotFound = new LangEntry("error_mini_not_found", new Dictionary<Lang, string>
            {
                [Lang.en] = "You do not have a Minicopter.",
            });
            public static readonly LangEntry ErrorScrapHeliNotFound = new LangEntry("error_scrap_heli_not_found", new Dictionary<Lang, string>
            {
                [Lang.en] = "You do not have a Scrap Heli.",
            });
            public static readonly LangEntry ErrorAttackHeliNotFound = new LangEntry("error_attack_heli_not_found", new Dictionary<Lang, string>
            {
                [Lang.en] = "You do not have an Attack Heli.",
            });

            public readonly string Name;
            public readonly Dictionary<Lang, string> PhrasesByLanguage;

            private LangEntry(string name, Dictionary<Lang, string> phrasesByLanguage)
            {
                Name = name;
                PhrasesByLanguage = phrasesByLanguage;

                AllLangEntries.Add(this);
            }
        }

        private string GetMessage(string playerId, LangEntry langEntry) =>
            lang.GetMessage(langEntry.Name, this, playerId);

        private string GetMessage(string playerId, LangEntry langEntry, object arg1) =>
            string.Format(GetMessage(playerId, langEntry), arg1);

        protected override void LoadDefaultMessages()
        {
            var langKeysByLanguage = new Dictionary<string, Dictionary<string, string>>();

            foreach (var langEntry in LangEntry.AllLangEntries)
            {
                foreach (var phraseEntry in langEntry.PhrasesByLanguage)
                {
                    var langName = phraseEntry.Key.ToString();
                    Dictionary<string, string> langKeys;
                    if (!langKeysByLanguage.TryGetValue(langName, out langKeys))
                    {
                        langKeys = new Dictionary<string, string>();
                        langKeysByLanguage[langName] = langKeys;
                    }

                    langKeys[langEntry.Name] = phraseEntry.Value;
                }
            }

            foreach (var langKeysEntry in langKeysByLanguage)
            {
                lang.RegisterMessages(langKeysEntry.Value, this, langKeysEntry.Key);
            }
        }

        #endregion
    }
}
