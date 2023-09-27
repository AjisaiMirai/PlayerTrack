﻿namespace PlayerTrack.Domain;

using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using Dalamud.DrunkenToad.Consumers;
using Dalamud.DrunkenToad.Core;

using Models;
using Models.Integration;

public class VisibilityService
{
    public readonly bool IsVisibilityAvailable;
    private const string Reason = "PlayerTrack";
    private readonly VisibilityConsumer visibilityConsumer;

    public VisibilityService()
    {
        DalamudContext.PluginLog.Verbose("Entering VisibilityService.VisibilityService()");
        this.visibilityConsumer = new VisibilityConsumer(DalamudContext.PluginInterface);
        if (ServiceContext.ConfigService.GetConfig().SyncWithVisibility)
        {
            this.IsVisibilityAvailable = this.visibilityConsumer.IsAvailable();
            DalamudContext.PluginLog.Verbose($"VisibilityService.VisibilityService() - IsVisibilityAvailable: {this.IsVisibilityAvailable}");
            if (this.IsVisibilityAvailable)
            {
                this.SyncWithVisibility();
            }
        }

        ServiceContext.PlayerDataService.PlayerUpdated += this.SyncWithVisibility;
    }

    public void Dispose() => ServiceContext.PlayerDataService.PlayerUpdated -= this.SyncWithVisibility;

    public void SyncWithVisibility(Player player)
    {
        DalamudContext.PluginLog.Verbose($"Entering VisibilityService.SyncWithVisibility(): {player.Name}");
        if (!this.IsVisibilityAvailable)
        {
            DalamudContext.PluginLog.Verbose("VisibilityService.SyncWithVisibility() - Visibility not available");
            return;
        }

        try
        {
            var voidedEntries = this.GetVisibilityPlayers(VisibilityType.Voidlist);
            var whitelistedEntries = this.GetVisibilityPlayers(VisibilityType.Whitelist);
            var visibilityType = PlayerConfigService.GetVisibilityType(player);
            DalamudContext.PluginLog.Verbose($"VisibilityService.SyncWithVisibility() - {player.Name} - {visibilityType}");

            if (visibilityType == VisibilityType.None)
            {
                DalamudContext.PluginLog.Verbose($"VisibilityService.SyncWithVisibility() - {player.Name} - {visibilityType} - Removing from visibility");
                if (voidedEntries.ContainsKey(player.Key))
                {
                    this.visibilityConsumer.RemoveFromVoidList(player.Name, player.WorldId);
                }

                if (whitelistedEntries.ContainsKey(player.Key))
                {
                    this.visibilityConsumer.RemoveFromWhiteList(player.Name, player.WorldId);
                }
            }
            else if (visibilityType == VisibilityType.Voidlist)
            {
                DalamudContext.PluginLog.Verbose($"VisibilityService.SyncWithVisibility() - {player.Name} - {visibilityType} - Adding to void list");
                if (!voidedEntries.ContainsKey(player.Key))
                {
                    this.visibilityConsumer.AddToVoidList(player.Name, player.WorldId, Reason);
                }
            }
            else if (visibilityType == VisibilityType.Whitelist)
            {
                DalamudContext.PluginLog.Verbose($"VisibilityService.SyncWithVisibility() - {player.Name} - {visibilityType} - Adding to white list");
                if (!whitelistedEntries.ContainsKey(player.Key))
                {
                    this.visibilityConsumer.AddToWhiteList(player.Name, player.WorldId, Reason);
                }
            }
            else
            {
                DalamudContext.PluginLog.Warning($"VisibilityService.SyncWithVisibility() - {player.Name} - {visibilityType} - Unhandled");
            }
        }
        catch (Exception ex)
        {
            DalamudContext.PluginLog.Error(ex, $"Failed to sync with visibility for player {player.Name}.");
        }
    }

    public void SyncWithVisibility()
    {
        if (!this.IsVisibilityAvailable)
        {
            DalamudContext.PluginLog.Verbose("VisibilityService.SyncWithVisibility() - Visibility not available");
            return;
        }

        DalamudContext.PluginLog.Verbose("Entering VisibilityService.SyncWithVisibility()");
        try
        {
            var players = ServiceContext.PlayerDataService.GetAllPlayers().ToList();
            var voidedPlayers = players.Where(p => PlayerConfigService.GetVisibilityType(p) == VisibilityType.Voidlist).ToDictionary(p => p.Key, p => p);
            var whitelistedPlayers = players.Where(p => PlayerConfigService.GetVisibilityType(p) == VisibilityType.Whitelist).ToDictionary(p => p.Key, p => p);

            // remove players from void list
            var voidList = this.GetVisibilityPlayers(VisibilityType.Voidlist);
            foreach (var (key, value) in voidList)
            {
                if (!voidedPlayers.ContainsKey(key) && IsSyncedEntry(value.Reason))
                {
                    this.visibilityConsumer.RemoveFromVoidList(value.Name, value.HomeWorldId);
                }
            }

            // remove players from white list
            var whiteList = this.GetVisibilityPlayers(VisibilityType.Whitelist);
            foreach (var (key, value) in whiteList)
            {
                if (!whitelistedPlayers.ContainsKey(key) && IsSyncedEntry(value.Reason))
                {
                    this.visibilityConsumer.RemoveFromWhiteList(value.Name, value.HomeWorldId);
                }
            }

            // add players to void list
            voidList = this.GetVisibilityPlayers(VisibilityType.Voidlist);
            foreach (var (key, value) in voidedPlayers)
            {
                if (!voidList.ContainsKey(key))
                {
                    this.visibilityConsumer.AddToVoidList(value.Name, value.WorldId, Reason);
                }
            }

            // add players to white list
            whiteList = this.GetVisibilityPlayers(VisibilityType.Whitelist);
            foreach (var (key, value) in whitelistedPlayers)
            {
                if (!whiteList.ContainsKey(key))
                {
                    this.visibilityConsumer.AddToWhiteList(value.Name, value.WorldId, Reason);
                }
            }

            // add void list entries to ptrack
            voidList = this.GetVisibilityPlayers(VisibilityType.Voidlist);
            foreach (var (key, value) in voidList)
            {
                if (players.All(p => p.Key != key))
                {
                    PlayerProcessService.CreateNewPlayer(value.Name, value.HomeWorldId);
                    var player = ServiceContext.PlayerDataService.GetPlayer(key);
                    if (player == null)
                    {
                        DalamudContext.PluginLog.Warning($"Failed to create voided player from visibility, key: {key}");
                        continue;
                    }

                    player.PlayerConfig.VisibilityType.Value = VisibilityType.Voidlist;
                    ServiceContext.PlayerDataService.UpdatePlayer(player);
                }
                else
                {
                    var player = players.First(p => p.Key == key);
                    var categoryVisibilityType = PlayerConfigService.GetVisibilityType(player);
                    if (categoryVisibilityType == VisibilityType.None)
                    {
                        player.PlayerConfig.VisibilityType.Value = VisibilityType.Voidlist;
                        ServiceContext.PlayerDataService.UpdatePlayer(player);
                    }
                }
            }

            // add white list entries to ptrack
            whiteList = this.GetVisibilityPlayers(VisibilityType.Whitelist);
            foreach (var (key, value) in whiteList)
            {
                if (players.All(p => p.Key != key))
                {
                    PlayerProcessService.CreateNewPlayer(value.Name, value.HomeWorldId);
                    var player = ServiceContext.PlayerDataService.GetPlayer(key);
                    if (player == null)
                    {
                        DalamudContext.PluginLog.Warning($"Failed to create whitelisted player from visibility, key: {key}");
                        continue;
                    }

                    player.PlayerConfig.VisibilityType.Value = VisibilityType.Whitelist;
                    ServiceContext.PlayerDataService.UpdatePlayer(player);
                }
                else
                {
                    var player = players.First(p => p.Key == key);
                    var categoryVisibilityType = PlayerConfigService.GetVisibilityType(player);
                    if (categoryVisibilityType == VisibilityType.None)
                    {
                        player.PlayerConfig.VisibilityType.Value = VisibilityType.Whitelist;
                        ServiceContext.PlayerDataService.UpdatePlayer(player);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DalamudContext.PluginLog.Error(ex, "Failed to sync with visibility.");
        }
    }

    private static bool IsSyncedEntry(string reason) => reason.Equals(Reason, StringComparison.OrdinalIgnoreCase);

    private Dictionary<string, VisibilityEntry> GetVisibilityPlayers(VisibilityType visibilityType)
    {
        List<string> rawVisibilityEntries;
        switch (visibilityType)
        {
            case VisibilityType.None:
                return new Dictionary<string, VisibilityEntry>();
            case VisibilityType.Voidlist:
                rawVisibilityEntries = this.visibilityConsumer.GetVoidListEntries().ToList();
                break;
            case VisibilityType.Whitelist:
                rawVisibilityEntries = this.visibilityConsumer.GetWhiteListEntries().ToList();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(visibilityType), visibilityType, null);
        }

        Dictionary<string, VisibilityEntry> visibilityEntries = new();
        if (!rawVisibilityEntries.Any())
        {
            return visibilityEntries;
        }

        foreach (var voidListEntry in rawVisibilityEntries)
        {
            try
            {
                var parts = voidListEntry.Split(" ");
                if (parts.Length != 4)
                {
                    continue;
                }

                var visibilityEntry = new VisibilityEntry
                {
                    Name = string.Concat(parts[0], " ", parts[1]), HomeWorldId = Convert.ToUInt32(parts[2]), Reason = parts[3],
                };
                visibilityEntry.Key = PlayerKeyBuilder.Build(visibilityEntry.Name, visibilityEntry.HomeWorldId);
                visibilityEntries.Add(visibilityEntry.Key, visibilityEntry);
            }
            catch (Exception ex)
            {
                DalamudContext.PluginLog.Error(ex, "Failed to load visibility entry.");
            }
        }

        return visibilityEntries;
    }
}
