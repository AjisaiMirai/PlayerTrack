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

public class PlayerRepository : BaseRepository
{
    public PlayerRepository(IDbConnection connection, IMapper mapper)
        : base(connection, mapper)
    {
    }

    public IEnumerable<Player> GetAllPlayersWithRelations()
    {
        PluginLog.LogVerbose("Entering PlayerRepository.GetAllPlayersWithRelations()");
        var players = new Dictionary<int, Player>();

        try
        {
            const string playerSql = "SELECT * FROM players";
            var playerDTOs = this.Connection.Query<PlayerDTO>(playerSql);
            foreach (var dto in playerDTOs)
            {
                var player = this.Mapper.Map<Player>(dto);
                player.AssignedTags = new List<Tag>();
                player.AssignedCategories = new List<Category>();
                player.PlayerConfig = new PlayerConfig(PlayerConfigType.Player)
                {
                    PlayerId = player.Id,
                };
                players.Add(player.Id, player);
            }

            var tagDict = this.Connection.Query<TagDTO>("SELECT * FROM tags")
                                         .ToDictionary<TagDTO, int, Tag>(t => t.id, t => this.Mapper.Map<Tag>(t));

            var categoryDict = this.Connection.Query<CategoryDTO>("SELECT * FROM categories")
                                              .ToDictionary<CategoryDTO, int, Category>(c => c.id, c => this.Mapper.Map<Category>(c));

            const string tagSql = "SELECT * FROM player_tags";
            var playerTagDTOs = this.Connection.Query<PlayerTagDTO>(tagSql);
            foreach (var dto in playerTagDTOs)
            {
                if (players.TryGetValue(dto.player_id, out var player))
                {
                    player.AssignedTags.Add(tagDict[dto.tag_id]);
                }
            }

            const string categoryConfigSql = "SELECT * FROM player_config WHERE category_id IS NOT NULL";
            var categoryConfigDTOs = this.Connection.Query<PlayerConfigDTO>(categoryConfigSql);
            var categoryConfigDict = categoryConfigDTOs
                .Where(dto => dto.category_id.HasValue)
                .ToDictionary(
                    dto => dto.category_id!.Value,
                    dto => this.Mapper.Map<PlayerConfig>(dto));

            const string categorySql = "SELECT * FROM player_categories";
            var playerCategoryDTOs = this.Connection.Query<PlayerCategoryDTO>(categorySql);
            foreach (var dto in playerCategoryDTOs)
            {
                if (players.TryGetValue(dto.player_id, out var player))
                {
                    var category = categoryDict[dto.category_id];
                    if (categoryConfigDict.TryGetValue(category.Id, out var config))
                    {
                        category.PlayerConfig = config;
                    }

                    player.AssignedCategories.Add(category);
                }
            }

            const string configSql = "SELECT * FROM player_config";
            var playerConfigDTOs = this.Connection.Query<PlayerConfigDTO>(configSql);
            foreach (var dto in playerConfigDTOs)
            {
                if (dto.player_id is null or 0)
                {
                    continue;
                }

                if (players.TryGetValue((int)dto.player_id, out var player))
                {
                    var config = this.Mapper.Map<PlayerConfig>(dto);
                    player.PlayerConfig = config;
                }
            }
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, "Failed to fetch all players with relations.");
            return Enumerable.Empty<Player>();
        }

        return players.Values;
    }

    public bool UpdatePlayer(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerRepository.UpdatePlayer(): {player.Id}");
        try
        {
            var playerDto = this.Mapper.Map<PlayerDTO>(player);
            SetUpdateTimestamp(playerDto);
            const string sql = @"
        UPDATE players
        SET
            key = @key,
            last_alert_sent = @last_alert_sent,
            last_seen = @last_seen,
            customize = @customize,
            seen_count = @seen_count,
            lodestone_status = @lodestone_status,
            lodestone_verified_on = @lodestone_verified_on,
            free_company_state = @free_company_state,
            free_company_tag = @free_company_tag,
            name = @name,
            notes = @notes,
            lodestone_id = @lodestone_id,
            object_id = @object_id,
            world_id = @world_id,
            last_territory_type = @last_territory_type,
            updated = @updated
        WHERE id = @id";
            this.Connection.Execute(sql, playerDto);
            return true;
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, $"Failed to update player with PlayerID {player.Id}.", player);
            return false;
        }
    }

    public List<Player>? GetPlayersByLodestoneId(uint lodestoneId)
    {
        PluginLog.LogVerbose($"Entering PlayerRepository.GetPlayersByLodestoneId(): {lodestoneId}");
        try
        {
            const string sql = @"SELECT * FROM players WHERE lodestone_id = @lodestone_id";
            var playerDTOs = this.Connection.Query<PlayerDTO>(sql, new { lodestone_id = lodestoneId }).ToList();
            return playerDTOs.Select(dto => this.Mapper.Map<Player>(dto)).ToList();
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, $"Failed to fetch players by LodestoneID {lodestoneId}.");
            return null;
        }
    }

    public bool DeletePlayer(int playerId)
    {
        PluginLog.LogVerbose($"Entering PlayerRepository.DeletePlayer(): {playerId}");
        try
        {
            const string sql = @"DELETE FROM players WHERE id = @player_id";
            this.Connection.Execute(sql, new { player_id = playerId });
            return true;
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, $"Failed to delete player with PlayerID {playerId}.");
            return false;
        }
    }

    public int CreatePlayer(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerRepository.CreatePlayer(): {player.Key}");
        using var transaction = this.Connection.BeginTransaction();
        try
        {
            const string checkExistenceSql = "SELECT id FROM players WHERE key = @key";
            var existingId = this.Connection.ExecuteScalar<int?>(checkExistenceSql, new { key = player.Key }, transaction);

            if (existingId.HasValue)
            {
                PluginLog.LogVerbose($"CreatePlayer(): Player with Key {player.Key} already exists.");
                return existingId.Value;
            }

            var playerDto = this.Mapper.Map<PlayerDTO>(player);
            SetCreateTimestamp(playerDto);

            const string sql = @"
        INSERT INTO players (
            created,
            updated,
            last_alert_sent,
            last_seen,
            customize,
            seen_count,
            lodestone_status,
            lodestone_verified_on,
            free_company_state,
            free_company_tag,
            key,
            name,
            notes,
            lodestone_id,
            object_id,
            world_id,
            last_territory_type)
        VALUES (
            @created,
            @updated,
            @last_alert_sent,
            @last_seen,
            @customize,
            @seen_count,
            @lodestone_status,
            @lodestone_verified_on,
            @free_company_state,
            @free_company_tag,
            @key,
            @name,
            @notes,
            @lodestone_id,
            @object_id,
            @world_id,
            @last_territory_type)";

            this.Connection.Execute(sql, playerDto, transaction);

            const string retrieveSql = "SELECT id FROM players WHERE created = @created";
            var newId = this.Connection.ExecuteScalar<int>(retrieveSql, new { playerDto.created }, transaction);

            transaction.Commit();
            return newId;
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, $"Failed to create new player with Key {player.Key}.", player);
            transaction.Rollback();
            return 0;
        }
    }

    public bool CreatePlayers(IEnumerable<Player> players)
    {
        PluginLog.LogVerbose($"Entering PlayerRepository.CreatePlayers()");
        using var transaction = this.Connection.BeginTransaction();
        try
        {
            const string sql = @"
                INSERT INTO players (
                    created,
                    updated,
                    last_alert_sent,
                    last_seen,
                    customize,
                    seen_count,
                    lodestone_status,
                    lodestone_verified_on,
                    free_company_state,
                    free_company_tag,
                    key,
                    name,
                    notes,
                    lodestone_id,
                    object_id,
                    world_id,
                    last_territory_type)
                VALUES (
                    @created,
                    @updated,
                    @last_alert_sent,
                    @last_seen,
                    @customize,
                    @seen_count,
                    @lodestone_status,
                    @lodestone_verified_on,
                    @free_company_state,
                    @free_company_tag,
                    @key,
                    @name,
                    @notes,
                    @lodestone_id,
                    @object_id,
                    @world_id,
                    @last_territory_type)";
            var playerDTOs = players.Select(this.Mapper.Map<PlayerDTO>).ToList();
            this.Connection.Execute(sql,  playerDTOs, transaction);
            transaction.Commit();

            return true;
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, "Failed to migrate players.");
            transaction.Rollback();
            return false;
        }
    }
}
