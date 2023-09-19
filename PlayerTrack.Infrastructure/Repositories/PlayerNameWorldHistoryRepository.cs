﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using AutoMapper;
using Dalamud.Logging;
using Dapper;
using FluentDapperLite.Repository;
using PlayerTrack.Models;

namespace PlayerTrack.Infrastructure;

public class PlayerNameWorldHistoryRepository : BaseRepository
{
    public PlayerNameWorldHistoryRepository(IDbConnection connection, IMapper mapper)
        : base(connection, mapper)
    {
    }

    public int CreatePlayerNameWorldHistory(PlayerNameWorldHistory playerNameWorldHistory)
    {
        PluginLog.LogVerbose("Entering PlayerNameWorldHistoryRepository.CreatePlayerNameWorldHistory()");
        using var transaction = this.Connection.BeginTransaction();
        try
        {
            const string sql = @"
            INSERT INTO player_name_world_histories (created, updated, is_migrated, player_name, world_id, player_id)
            VALUES (@created, @updated, @is_migrated, @player_name, @world_id, @player_id)";

            var historyDto = this.Mapper.Map<PlayerNameWorldHistoryDTO>(playerNameWorldHistory);
            SetCreateTimestamp(historyDto);

            this.Connection.Execute(sql, historyDto, transaction);

            const string retrieveSql = "SELECT id FROM player_name_world_histories WHERE created = @created";
            var newId = this.Connection.ExecuteScalar<int>(retrieveSql, new { historyDto.created }, transaction);

            transaction.Commit();
            return newId;
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, "Failed to create PlayerNameWorldHistory.", playerNameWorldHistory);
            transaction.Rollback();
            return 0;
        }
    }

    public int UpdatePlayerId(int oldestPlayerId, int newPlayerId)
    {
        PluginLog.LogVerbose("Entering PlayerNameWorldHistoryRepository.UpdatePlayerId()");
        try
        {
            const string updateSql = "UPDATE player_name_world_histories SET player_id = @newPlayerId WHERE player_id = @oldestPlayerId";

            var numberOfUpdatedRecords = this.Connection.Execute(updateSql, new { newPlayerId, oldestPlayerId });
            return numberOfUpdatedRecords;
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, $"Failed to update playerIds from {oldestPlayerId} to {newPlayerId}.");
            return 0;
        }
    }

    public string[]? GetHistoricalNames(int playerId)
    {
        PluginLog.LogVerbose($"Entering PlayerNameWorldHistoryRepository.GetHistoricalNames(): {playerId}");
        try
        {
            const string sql = "SELECT player_name FROM player_name_world_histories WHERE player_id = @player_id ORDER BY updated DESC";
            return this.Connection.Query<string>(sql, new { player_id = playerId }).ToArray();
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, $"Failed to get historical names for PlayerID {playerId}.");
            return null;
        }
    }

    public IEnumerable<uint>? GetHistoricalWorlds(int playerId)
    {
        PluginLog.LogVerbose($"Entering PlayerNameWorldHistoryRepository.GetHistoricalWorlds(): {playerId}");
        try
        {
            const string sql = "SELECT world_id FROM player_name_world_histories WHERE player_id = @player_id ORDER BY updated DESC";
            return this.Connection.Query<uint>(sql, new { player_id = playerId }).ToArray();
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, $"Failed to get historical worlds for PlayerID {playerId}.");
            return null;
        }
    }

    public bool DeleteNameWorldHistory(int playerId)
    {
        PluginLog.LogVerbose($"Entering PlayerNameWorldHistoryRepository.DeleteNameWorldHistory(): {playerId}");
        try
        {
            const string sql = "DELETE FROM player_name_world_histories WHERE player_id = @player_id";
            this.Connection.Execute(sql, new { player_id = playerId });
            return true;
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, $"Failed to delete NameWorldHistory for PlayerID {playerId}");
            return false;
        }
    }

    public bool CreatePlayerNameWorldHistories(IEnumerable<PlayerNameWorldHistory> playerNameWorldHistoriesList)
    {
        PluginLog.LogVerbose("Entering PlayerNameWorldHistoryRepository.CreatePlayerNameWorldHistories()");
        using var transaction = this.Connection.BeginTransaction();
        try
        {
            const string sql = @"
        INSERT INTO player_name_world_histories (created, updated, is_migrated, player_name, world_id, player_id)
        VALUES (@created, @updated, @is_migrated, @player_name, @world_id, @player_id)";

            var historyDTOs = playerNameWorldHistoriesList.Select(history => new PlayerNameWorldHistoryDTO
            {
                player_id = history.PlayerId,
                player_name = history.PlayerName,
                world_id = history.WorldId,
                is_migrated = history.IsMigrated,
                created = history.Created,
                updated = history.Updated,
            }).ToList();

            this.Connection.Execute(sql, historyDTOs, transaction);

            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, "Failed to create PlayerNameWorldHistories.");
            transaction.Rollback();
            return false;
        }
    }
}
