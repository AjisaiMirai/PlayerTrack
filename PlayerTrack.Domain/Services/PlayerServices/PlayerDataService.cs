﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PlayerTrack.Domain.Common;
using PlayerTrack.Infrastructure;
using PlayerTrack.Models;

namespace PlayerTrack.Domain;

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Timers;
using Dalamud.DrunkenToad.Caching;
using Dalamud.DrunkenToad.Collections;
using Dalamud.DrunkenToad.Core;
using Dalamud.DrunkenToad.Core.Models;
using Dalamud.DrunkenToad.Extensions;
using Dalamud.DrunkenToad.Helpers;
using Dalamud.Interface;

using Models.Comparers;
using Newtonsoft.Json;

public class PlayerDataService : SortedCacheService<Player>
{
    public readonly ConcurrentDictionary<int, long> RecentPlayersExpiry = new();
    public Action<Player>? PlayerUpdated;
    private const long NinetyDaysInMilliseconds = 7776000000;
    private const int MaxBatchSize = 500;
    private readonly Timer recentPlayerTimer;
    private readonly ConcurrentDictionary<int, ThreadSafeSortedCollection<Player>> categoryPlayersCache = new();
    private readonly ConcurrentDictionary<int, ThreadSafeSortedCollection<Player>> tagPlayersCache = new();
    private bool isFirstLoad = true;
    private ThreadSafeSortedCollection<Player> currentPlayersCache = new(LockRecursionPolicy.SupportsRecursion);
    private ThreadSafeSortedCollection<Player> recentPlayersCache = new(LockRecursionPolicy.SupportsRecursion);

    public PlayerDataService()
    {
        this.recentPlayerTimer = new Timer(30000);
        this.recentPlayerTimer.Elapsed += this.OnRecentPlayerTimerOnElapsed;
        this.recentPlayerTimer.AutoReset = true;
        this.recentPlayerTimer.Start();
    }

    public new void Dispose() => this.recentPlayerTimer.Stop();

    public Player? GetPlayer(int playerId) => this.cache.FindFirst(p => p.Id == playerId);

    public Player? GetPlayer(string name, uint worldId) => this.cache.FindFirst(p => p.Key.Equals(PlayerKeyBuilder.Build(name, worldId), StringComparison.Ordinal));

    public Player? GetPlayer(string playerKey) => this.cache.FindFirst(p => p.Key == playerKey);

    public Player? GetPlayer(uint playerObjectId) => this.cache.FindFirst(p => p.ObjectId == playerObjectId);

    public IEnumerable<Player> GetAllPlayers() => this.cache.GetSortedItems();

    public void DeletePlayer(int playerId)
    {
        DalamudContext.PluginLog.Verbose($"PlayerDataService.DeletePlayer(): {playerId}");

        PlayerChangeService.DeleteCustomizeHistory(playerId);
        PlayerChangeService.DeleteNameWorldHistory(playerId);
        PlayerCategoryService.DeletePlayerCategoryByPlayerId(playerId);
        PlayerConfigService.DeletePlayerConfig(playerId);
        PlayerTagService.DeletePlayerTagsByPlayerId(playerId);
        PlayerLodestoneService.DeleteLookupsByPlayer(playerId);
        PlayerEncounterService.DeletePlayerEncountersByPlayer(playerId);
        this.DeletePlayerFromCacheAndRepository(playerId);
    }

    public void UpdatePlayer(Player player)
    {
        DalamudContext.PluginLog.Verbose($"PlayerDataService.UpdatePlayer(): {player.Id}");
        this.UpdatePlayerInCacheAndRepository(player);
    }

    public void AddPlayer(Player player)
    {
        DalamudContext.PluginLog.Verbose($"PlayerDataService.AddPlayer(): {player.Id}");
        var categoryId = player.PrimaryCategoryId;
        this.AddPlayerToCacheAndRepository(player);
        PlayerLodestoneService.CreateLodestoneLookup(player.Id, player.Name, player.WorldId);
        if (categoryId != 0)
        {
            PlayerCategoryService.AssignCategoryToPlayer(player.Id, categoryId);
        }
        else
        {
            DalamudContext.PluginLog.Verbose($"PlayerDataService.AddPlayer(): No category assigned to player: {player.Id}");
        }
    }

    public void ClearCategoryFromPlayers(int categoryId)
    {
        try
        {
            var ranks = ServiceContext.CategoryService.GetCategoryRanks();
            foreach (var player in this.cache.FindAll(p => p.AssignedCategories.Any(c => c.Id == categoryId)))
            {
                player.AssignedCategories.RemoveAll(c => c.Id == categoryId);
                player.PrimaryCategoryId = 0;
                PopulateDerivedFields(player, ranks);
                this.UpdatePlayerInCacheAndRepository(player);
            }
        }
        catch (Exception ex)
        {
            DalamudContext.PluginLog.Debug(ex, $"PlayerDataService.ClearCategoryFromPlayers(): {categoryId}");
        }
    }

    public void RefreshAllPlayers()
    {
        DalamudContext.PluginLog.Verbose("PlayerDataService.RefreshAllPlayers()");
        Task.Run(this.ReloadPlayerCacheAsync);
    }

    public void RecalculatePlayerRankings()
    {
        DalamudContext.PluginLog.Verbose($"PlayerDataService.RecalculatePlayerRankings()");
        Task.Run(() =>
        {
            try
            {
                var comparer = new PlayerComparer(ServiceContext.CategoryService.GetCategoryRanks());
                this.cache.Resort(comparer);
                this.currentPlayersCache.Resort(comparer);
                this.recentPlayersCache.Resort(comparer);
                foreach (var entry in this.categoryPlayersCache)
                {
                    entry.Value.Resort(comparer);
                }
                
                foreach (var entry in this.tagPlayersCache)
                {
                    entry.Value.Resort(comparer);
                }
                
                this.OnCacheUpdated();
            }
            catch (Exception ex)
            {
                DalamudContext.PluginLog.Debug(ex, "PlayerDataService.RecalculatePlayerRankings()");
            }
        });
    }

    public void MergePlayers(Player oldestPlayer, int newPlayerId)
    {
        DalamudContext.PluginLog.Verbose($"PlayerDataService.MergePlayer(): {oldestPlayer.Id} -> {newPlayerId}");
        var newPlayer = this.cache.FindFirst(p => p.Id == newPlayerId);
        if (newPlayer == null)
        {
            return;
        }

        // save state before changing
        var oldestPlayerString = JsonConvert.SerializeObject(oldestPlayer);
        var newPlayerString = JsonConvert.SerializeObject(newPlayer);
        var isCurrent = newPlayer.IsCurrent;
        var payloads = ServiceContext.PlayerAlertService.CreatePlayerNameWorldChangeAlert(oldestPlayer, newPlayer);

        // remove players from cache
        ServiceContext.PlayerProcessService.RemoveCurrentPlayer(newPlayer.ObjectId);
        this.cache.Remove(newPlayer);
        this.cache.Remove(oldestPlayer);
        this.currentPlayersCache.Remove(newPlayer);
        this.currentPlayersCache.Remove(oldestPlayer);
        this.recentPlayersCache.Remove(newPlayer);
        this.recentPlayersCache.Remove(oldestPlayer);
        foreach (var entry in this.categoryPlayersCache)
        {
            entry.Value.Remove(newPlayer);
            entry.Value.Remove(oldestPlayer);
        }
        foreach (var entry in this.tagPlayersCache)
        {
            entry.Value.Remove(newPlayer);
            entry.Value.Remove(oldestPlayer);
        }
        this.OnCacheUpdated();

        // create records
        PlayerChangeService.HandleNameWorldChange(oldestPlayer, newPlayer);
        PlayerChangeService.HandleCustomizeChange(oldestPlayer, newPlayer);

        // re-parent records
        PlayerChangeService.UpdatePlayerId(newPlayer.Id, oldestPlayer.Id);
        PlayerEncounterService.UpdatePlayerId(newPlayer.Id, oldestPlayer.Id);

        // delete records
        PlayerConfigService.DeletePlayerConfig(newPlayer.Id);
        PlayerCategoryService.DeletePlayerCategoryByPlayerId(newPlayer.Id);
        PlayerTagService.DeletePlayerTagsByPlayerId(newPlayer.Id);
        PlayerLodestoneService.DeleteLookupsByPlayer(newPlayer.Id);
        RepositoryContext.PlayerRepository.DeletePlayer(newPlayer.Id);

        // merge data into original
        oldestPlayer.Merge(newPlayer);

        // recalculate derived fields
        PopulateDerivedFields(oldestPlayer, ServiceContext.CategoryService.GetCategoryRanks());

        // update player in repo & cache
        RepositoryContext.PlayerRepository.UpdatePlayer(oldestPlayer);
        this.cache.Add(oldestPlayer);
        if (oldestPlayer.IsCurrent)
        {
            this.currentPlayersCache.Add(oldestPlayer);
        }
        
        if (oldestPlayer.IsRecent)
        {
            this.recentPlayersCache.Add(oldestPlayer);
        }
        
        foreach (var entry in this.categoryPlayersCache)
        {
            if (oldestPlayer.AssignedCategories.Select(c => c.Id).Contains(entry.Key))
            {
                entry.Value.Add(oldestPlayer);
            }
        }
        
        foreach(var entry in this.tagPlayersCache)
        {
            if (oldestPlayer.AssignedTags.Select(t => t.Id).Contains(entry.Key))
            {
                entry.Value.Add(oldestPlayer);
            }
        }
        
        this.OnCacheUpdated();

        // add to current players if needed
        oldestPlayer.IsCurrent = isCurrent;
        if (oldestPlayer.IsCurrent)
        {
            ServiceContext.PlayerProcessService.RegisterCurrentPlayer(oldestPlayer);
        }

        // send alert
        if (!payloads.Any())
        {
            DalamudContext.PluginLog.Warning("Skipping empty alert for name/world change.");
            DalamudContext.PluginLog.Warning($"Oldest Player: {oldestPlayerString}");
            DalamudContext.PluginLog.Warning($"New Player: {newPlayerString}");
            return;
        }

        PlayerAlertService.SendNameWorldChangeAlert(payloads);
    }

    public int GetAllPlayersCount() => this.cache.Count;

    public int GetAllPlayersCount(string name, SearchType searchType) => this.cache.GetFilteredItemsCount(GetSearchFilter(name, searchType));

    public int GetCurrentPlayersCount() => this.currentPlayersCache.Count;

    public int GetCurrentPlayersCount(string name, SearchType searchType) => this.currentPlayersCache.GetFilteredItemsCount(p => GetSearchFilter(name, searchType)(p));

    public int GetRecentPlayersCount() => this.recentPlayersCache.Count;

    public int GetRecentPlayersCount(string name, SearchType searchType) => this.recentPlayersCache.GetFilteredItemsCount(p => GetSearchFilter(name, searchType)(p));

    public int GetCategoryPlayersCount(int categoryId) => this.categoryPlayersCache[categoryId].GetFilteredItemsCount(p => p.PrimaryCategoryId == categoryId);

    public int GetTagPlayersCount(int tagId) => this.tagPlayersCache[tagId].Count;

    public int GetCategoryPlayersCount(int categoryId, string name, SearchType searchType) => this.categoryPlayersCache[categoryId].GetFilteredItemsCount(p => p.PrimaryCategoryId == categoryId && GetSearchFilter(name, searchType)(p));

    public int GetTagPlayersCount(int tagId, string name, SearchType searchType) => this.tagPlayersCache[tagId].GetFilteredItemsCount(p => GetSearchFilter(name, searchType)(p));

    public List<Player> GetAllPlayers(int start, int count) => this.cache.GetSortedItems(start, count);

    public List<Player> GetAllPlayers(int start, int count, string name, SearchType searchType) => this.cache.GetFilteredSortedItems(GetSearchFilter(name, searchType), start, count);

    public List<Player> GetCurrentPlayers(int start, int count) => this.currentPlayersCache.GetSortedItems(start, count);

    public List<Player> GetCurrentPlayers(int start, int count, string name, SearchType searchType) => this.currentPlayersCache.GetFilteredSortedItems(
        p => GetSearchFilter(name, searchType)(p),
        start,
        count);

    public List<Player> GetRecentPlayers(int start, int count) => this.recentPlayersCache.GetSortedItems(start, count);

    public List<Player> GetRecentPlayers(int start, int count, string name, SearchType searchType) => this.recentPlayersCache.GetFilteredSortedItems(
        p => GetSearchFilter(name, searchType)(p),
        start,
        count);

    public List<Player> GetCategoryPlayers(int categoryId, int start, int count) => this.categoryPlayersCache[categoryId].GetFilteredSortedItems(p => p.PrimaryCategoryId == categoryId, start, count);

    public List<Player> GetCategoryPlayers(int categoryId, int start, int count, string name, SearchType searchType) => this.categoryPlayersCache[categoryId].GetFilteredSortedItems(
        p => GetSearchFilter(name, searchType)(p) && p.PrimaryCategoryId == categoryId, start, count);

    public List<Player> GetTagPlayers(int tagId, int start, int count) => this.tagPlayersCache[tagId].GetSortedItems(start, count);

    public List<Player> GetTagPlayers(int tagId, int start, int count, string name, SearchType searchType) => this.tagPlayersCache[tagId].GetFilteredSortedItems(
        p => GetSearchFilter(name, searchType)(p), start, count);

    public int GetPlayerConfigCount() => this.cache.GetFilteredItemsCount(p => p.PlayerConfig.Id != 0);

    public int GetPlayersForDeletionCount() => this.GetPlayersForDeletion().Count;

    public int GetPlayerConfigsForDeletionCount() => this.GetPlayerConfigsForDeletion().Count;

    public void DeletePlayers()
    {
        var players = this.GetPlayersForDeletion();
        var playerIds = players.Select(p => p.Id).ToList();

        for (var i = 0; i < playerIds.Count; i += MaxBatchSize)
        {
            var currentBatch = playerIds.Skip(i).Take(MaxBatchSize).ToList();
            RepositoryContext.PlayerRepository.DeletePlayersWithRelations(currentBatch);
        }

        RepositoryContext.RunMaintenanceChecks(true);
        this.RefreshAllPlayers();
    }

    public void DeletePlayerConfigs()
    {
        var playerConfigs = this.GetPlayerConfigsForDeletion();
        var playerConfigIds = playerConfigs.Select(p => p.Id).ToList();

        for (var i = 0; i < playerConfigIds.Count; i += MaxBatchSize)
        {
            var currentBatch = playerConfigIds.Skip(i).Take(MaxBatchSize).ToList();
            RepositoryContext.PlayerConfigRepository.DeletePlayerConfigs(currentBatch);
        }

        RepositoryContext.RunMaintenanceChecks(true);
        this.RefreshAllPlayers();
    }

    public void ReloadPlayerCache() => this.ExecuteReloadCache(() =>
    {
        DalamudContext.PluginLog.Verbose($"Entering PlayerDataService.ReloadPlayerCacheAsync()");
        this.ReloadPlayers();
    });

    public void UpdatePlayerNotes(int playerId, string notes)
    {
        DalamudContext.PluginLog.Verbose($"PlayerDataService.UpdatePlayerNotes(): {playerId}");
        var player = this.cache.FindFirst(p => p.Id == playerId);
        if (player == null)
        {
            return;
        }

        player.Notes = notes;
        this.UpdatePlayerInCacheAndRepository(player);
    }

    private static void PopulateDerivedFields(Player player, Dictionary<int, int> categoryRanks)
    {
        PlayerCategoryService.SetPrimaryCategoryId(player, categoryRanks);
        var colorId = PlayerConfigService.GetNameColor(player);
        var color = DalamudContext.DataManager.UIColors.TryGetValue(colorId, out var uiColor) ? uiColor : new ToadUIColor();
        player.PlayerListNameColor = color.Foreground.ToVector4();
        player.PlayerListIconString = ((FontAwesomeIcon)PlayerConfigService.GetIcon(player)).ToIconString();
    }

    private static Func<Player, bool> GetSearchFilter(string name, SearchType searchType)
    {
        return Filter;

        bool Filter(Player player)
        {
            return searchType switch
            {
                SearchType.Contains => player.Name.Contains(name, StringComparison.OrdinalIgnoreCase),
                SearchType.StartsWith => player.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase),
                SearchType.Exact => player.Name.Equals(name, StringComparison.OrdinalIgnoreCase),
                _ => throw new ArgumentException($"Invalid search type: {searchType}"),
            };
        }
    }

    private void OnRecentPlayerTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        var threshold = UnixTimestampHelper.CurrentTime() - ServiceContext.ConfigService.GetConfig().RecentPlayersThreshold;
        var playerIds = this.RecentPlayersExpiry.Keys.ToList();

        foreach (var playerId in playerIds)
        {
            if (!this.RecentPlayersExpiry.TryGetValue(playerId, out var timestamp) || timestamp > threshold) continue;
            if (!this.RecentPlayersExpiry.TryRemove(playerId, out _)) continue;
            var player = this.cache.FindFirst(p => p.Id == playerId);
            if (player == null) continue;
            player.IsRecent = false;
            ServiceContext.PlayerDataService.UpdatePlayer(player);
        }
    }

    private List<Player> GetPlayersForDeletion()
    {
        var playersWithEncounters = RepositoryContext.PlayerEncounterRepository.GetPlayersWithEncounters();
        var currentTimeUnix = UnixTimestampHelper.CurrentTime();
        var options = ServiceContext.ConfigService.GetConfig().PlayerDataActionOptions;
        return this.cache.GetFilteredSortedItems(p =>
            (!options.KeepPlayersWithNotes || string.IsNullOrEmpty(p.Notes)) &&
            (!options.KeepPlayersWithCategories || !p.AssignedCategories.Any()) &&
            (!options.KeepPlayersWithAnySettings || p.PlayerConfig.Id == 0) &&
            (!options.KeepPlayersWithEncounters || !playersWithEncounters.Contains(p.Id)) &&
            (!options.KeepPlayersSeenInLast90Days || currentTimeUnix - p.LastSeen > NinetyDaysInMilliseconds) &&
            (!options.KeepPlayersVerifiedOnLodestone || p.LodestoneStatus != LodestoneStatus.Verified));
    }

    private List<PlayerConfig> GetPlayerConfigsForDeletion()
    {
        var playersWithEncounters = RepositoryContext.PlayerEncounterRepository.GetPlayersWithEncounters();
        var currentTimeUnix = UnixTimestampHelper.CurrentTime();
        var options = ServiceContext.ConfigService.GetConfig().PlayerSettingsDataActionOptions;
        return this.cache.GetFilteredSortedItems(p =>
            p.PlayerConfig.Id != 0 &&
            (!options.KeepSettingsForPlayersWithNotes || string.IsNullOrEmpty(p.Notes)) &&
            (!options.KeepSettingsForPlayersWithCategories || !p.AssignedCategories.Any()) &&
            (!options.KeepSettingsForPlayersWithAnySettings || p.PlayerConfig.Id == 0) &&
            (!options.KeepSettingsForPlayersWithEncounters || !playersWithEncounters.Contains(p.Id)) &&
            (!options.KeepSettingsForPlayersSeenInLast90Days || currentTimeUnix - p.LastSeen > NinetyDaysInMilliseconds) &&
            (!options.KeepSettingsForPlayersVerifiedOnLodestone || p.LodestoneStatus != LodestoneStatus.Verified)).Select(p => p.PlayerConfig).ToList();
    }

    private void UpdateCurrentPlayerCache(Player player)
    {
        if (player.IsCurrent)
        {
            this.currentPlayersCache.AddOrUpdate(player);
        }
    }

    private void UpdateRecentPlayerCache(Player player)
    {
        if (player.IsRecent)
        {
            this.recentPlayersCache.AddOrUpdate(player);
        }
    }

    private void UpdateCategoryPlayersCache(Player player)
    {
        foreach (var entry in this.categoryPlayersCache)
        {
            if (player.AssignedCategories.Select(c => c.Id).Contains(entry.Key))
            {
                entry.Value.AddOrUpdate(player);
            }
            else
            {
                entry.Value.Remove(player);
            }
        }

        if (player.AssignedCategories.Count == 0)
        {
            this.categoryPlayersCache[0].AddOrUpdate(player);
        }
    }

    private void UpdateTagPlayersCache(Player player)
    {
        foreach (var entry in this.tagPlayersCache)
        {
            if (player.AssignedTags.Select(t => t.Id).Contains(entry.Key))
            {
                entry.Value.AddOrUpdate(player);
            }
            else
            {
                entry.Value.Remove(player);
            }
        }
        
        if (player.AssignedTags.Count == 0)
        {
            this.tagPlayersCache[0].AddOrUpdate(player);
        }
    }
    
    private void UpdatePlayerInCacheAndRepository(Player player)
    {
        PopulateDerivedFields(player, ServiceContext.CategoryService.GetCategoryRanks());
        this.cache.Update(player);
        UpdateCurrentPlayerCache(player);
        UpdateRecentPlayerCache(player);
        UpdateCategoryPlayersCache(player);
        UpdateTagPlayersCache(player);
        RepositoryContext.PlayerRepository.UpdatePlayer(player);
        this.OnCacheUpdated();
        this.PlayerUpdated?.Invoke(player);
    }

    private void AddPlayerToCacheAndRepository(Player player)
    {
        player.Id = RepositoryContext.PlayerRepository.CreatePlayer(player);
        player.PlayerConfig.PlayerId = player.Id;
        PopulateDerivedFields(player, ServiceContext.CategoryService.GetCategoryRanks());
        this.cache.Add(player);
        UpdateCurrentPlayerCache(player);
        UpdateRecentPlayerCache(player);
        UpdateCategoryPlayersCache(player);
        UpdateTagPlayersCache(player);
        this.OnCacheUpdated();
    }

    private void DeletePlayerFromCacheAndRepository(Player player)
    {
        this.cache.Remove(player);
        this.currentPlayersCache.Remove(player);
        this.recentPlayersCache.Remove(player);
        foreach (var entry in this.categoryPlayersCache)
        {
            entry.Value.Remove(player);
        }
        foreach (var entry in this.tagPlayersCache)
        {
            entry.Value.Remove(player);
        }
        RepositoryContext.PlayerRepository.DeletePlayer(player.Id);
        this.OnCacheUpdated();
    }

    private void DeletePlayerFromCacheAndRepository(int playerId)
    {
        var player = this.cache.FindFirst(p => p.Id == playerId);
        if (player == null)
        {
            return;
        }

        this.DeletePlayerFromCacheAndRepository(player);
    }

    private async Task ReloadPlayerCacheAsync() => await this.ExecuteReloadCacheAsync(() =>
    {
        DalamudContext.PluginLog.Verbose($"Entering PlayerDataService.ReloadPlayerCacheAsync()");
        this.ReloadPlayers();
        return Task.CompletedTask;
    });

    private void ReloadPlayers()
    {
        // load current and recent player ids from cache (if not first load)
        HashSet<int>? currentPlayerIds = null;
        HashSet<int>? recentPlayerIds = null;
        
        if (!this.isFirstLoad)
        {
            currentPlayerIds = new HashSet<int>(this.cache.GetFilteredSortedItems(p => p.IsCurrent).Select(p => p.Id));
            recentPlayerIds = new HashSet<int>(this.cache.GetFilteredSortedItems(p => p.IsRecent).Select(p => p.Id));
        }
        
        // load category and tag data to determine caches and sorting
        var categoryRanks = ServiceContext.CategoryService.GetCategoryRanks();
        var tags = ServiceContext.TagService.GetAllTags();

        // load players from repo
        var players = RepositoryContext.PlayerRepository.GetAllPlayersWithRelations().ToList();
        
        // setup new lists for caches
        var currentPlayers = new List<Player>();
        var recentPlayers = new List<Player>();
        var categoryPlayers = new ConcurrentDictionary<int, List<Player>>();
        var tagPlayers = new ConcurrentDictionary<int, List<Player>>();
        
        // add empty lists for each category
        categoryPlayers.TryAdd(0, new List<Player>());
        for (var i = 0; i < categoryRanks.Count; i++)
        {
            categoryPlayers.TryAdd(categoryRanks.ElementAt(i).Key, new List<Player>());
        }

        // add empty lists for each tag
        tagPlayers.TryAdd(0, new List<Player>());
        foreach (var tag in tags)
        {
            tagPlayers.TryAdd(tag.Id, new List<Player>());
        }
        
        // populate caches
        if (currentPlayerIds != null && recentPlayerIds != null)
        {
            foreach (var player in players)
            {
                PopulateDerivedFields(player, categoryRanks);
                player.IsCurrent = currentPlayerIds.Contains(player.Id);
                player.IsRecent = recentPlayerIds.Contains(player.Id);

                if (player.IsCurrent)
                {
                    currentPlayers.Add(player);
                }
                
                if (player.IsRecent)
                {
                    recentPlayers.Add(player);
                }

                if (player.AssignedCategories.Count == 0)
                {
                    categoryPlayers[0].Add(player);
                }
                else
                {
                    foreach (var category in player.AssignedCategories)
                    {
                        if (!categoryPlayers.ContainsKey(category.Id))
                        {
                            categoryPlayers.TryAdd(category.Id, new List<Player>());
                        }

                        categoryPlayers[category.Id].Add(player);
                    }
                }

                if (player.AssignedTags.Count == 0)
                {
                    tagPlayers[0].Add(player);
                }
                else
                {
                    foreach (var tag in player.AssignedTags)
                    {
                        if (!tagPlayers.ContainsKey(tag.Id))
                        {
                            tagPlayers.TryAdd(tag.Id, new List<Player>());
                        }

                        tagPlayers[tag.Id].Add(player);
                    }
                }
            }
        }
        else
        {
            foreach (var player in players)
            {
                PopulateDerivedFields(player, categoryRanks);
                if (player.AssignedCategories.Count == 0)
                {
                    categoryPlayers[0].Add(player);
                }
                else
                {
                    foreach (var category in player.AssignedCategories)
                    {
                        if (!categoryPlayers.ContainsKey(category.Id))
                        {
                            categoryPlayers.TryAdd(category.Id, new List<Player>());
                        }

                        categoryPlayers[category.Id].Add(player);
                    } 
                }
                
                if (player.AssignedTags.Count == 0)
                {
                    tagPlayers[0].Add(player);
                }
                else
                {
                    foreach (var tag in player.AssignedTags)
                    {
                        if (!tagPlayers.ContainsKey(tag.Id))
                        {
                            tagPlayers.TryAdd(tag.Id, new List<Player>());
                        }

                        tagPlayers[tag.Id].Add(player);
                    }
                }
            }
        }
        
        // create caches with sorted collections
        var comparer = new PlayerComparer(ServiceContext.CategoryService.GetCategoryRanks());
        this.cache = new ThreadSafeSortedCollection<Player>(players, comparer, LockRecursionPolicy.SupportsRecursion);
        this.currentPlayersCache = new ThreadSafeSortedCollection<Player>(currentPlayers, comparer, LockRecursionPolicy.SupportsRecursion);
        this.recentPlayersCache = new ThreadSafeSortedCollection<Player>(recentPlayers, comparer, LockRecursionPolicy.SupportsRecursion);
        
        foreach (var entry in categoryPlayers)
        {
            this.categoryPlayersCache.TryAdd(entry.Key, new ThreadSafeSortedCollection<Player>(entry.Value, comparer, LockRecursionPolicy.SupportsRecursion));
        }
        
        foreach (var entry in tagPlayers)
        {
            this.tagPlayersCache.TryAdd(entry.Key, new ThreadSafeSortedCollection<Player>(entry.Value, comparer, LockRecursionPolicy.SupportsRecursion));
        }
        
        this.OnCacheUpdated();
        this.isFirstLoad = false;
    }
}
