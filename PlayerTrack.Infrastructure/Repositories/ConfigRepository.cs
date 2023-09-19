﻿using System;
using System.Data;
using System.Linq;
using AutoMapper;
using Dalamud.Logging;
using Dapper;
using FluentDapperLite.Repository;
using PlayerTrack.Models;

namespace PlayerTrack.Infrastructure;

public class ConfigRepository : BaseRepository
{
    public ConfigRepository(IDbConnection connection, IMapper mapper)
        : base(connection, mapper)
    {
    }

    public PluginConfig? GetPluginConfig()
    {
        PluginLog.LogVerbose("Entering ConfigRepository.GetPluginConfig()");
        try
        {
            const string sql = "SELECT * FROM configs";
            var configEntryDTOs = this.Connection.Query<ConfigEntryDTO>(sql).ToArray();
            return configEntryDTOs.Length == 0 ? null : ConfigMapper.ToModel(configEntryDTOs);
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, "Failed to get plugin configuration from the database.");
            return null;
        }
    }

    public bool UpdatePluginConfig(PluginConfig pluginConfig)
    {
        PluginLog.LogVerbose("Entering ConfigRepository.UpdatePluginConfig()");
        var configEntryDTOs = ConfigMapper.ToDTOs(pluginConfig);

        using var transaction = this.Connection.BeginTransaction();
        try
        {
            foreach (var configEntryDTO in configEntryDTOs)
            {
                const string sql = "INSERT OR REPLACE INTO configs (key, value) VALUES (@key, @value)";
                this.Connection.Execute(sql, new { configEntryDTO.key, configEntryDTO.value }, transaction: transaction);
            }

            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            PluginLog.LogError(ex, "Failed to update config.", pluginConfig);
            transaction.Rollback();
            return false;
        }
    }
}
