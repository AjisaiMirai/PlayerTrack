﻿using System;
using PlayerTrack.Infrastructure;
using PlayerTrack.Models;
using PlayerTrack.Models.Structs;

namespace PlayerTrack.Domain;

using System.Collections.Generic;
using Dalamud.Logging;

public class PlayerConfigService
{
    public static ExtractedProperty<T> ExtractProperty<T>(
        PlayerConfigSet playerConfigSet,
        Func<PlayerConfig, ConfigValue<T>> propertySelector)
    {
        switch (playerConfigSet.PlayerConfigType)
        {
            case PlayerConfigType.Default:
                return new ExtractedProperty<T>
                {
                    PlayerConfigType = PlayerConfigType.Default,
                    PropertyValue = ExtractDefaultProperty(GetDefaultConfig(), propertySelector).PropertyValue,
                };
            case PlayerConfigType.Category:
                return ExtractCategoryProperty(GetDefaultConfig(), playerConfigSet.CurrentPlayerConfig, propertySelector);
            case PlayerConfigType.Player:
                return ExtractPlayerProperty(GetDefaultConfig(), playerConfigSet.CurrentPlayerConfig, playerConfigSet.CategoryPlayerConfigs, propertySelector);
            default:
                throw new ArgumentOutOfRangeException(nameof(playerConfigSet.PlayerConfigType), "Invalid player configuration type.");
        }
    }

    public static VisibilityType GetVisibilityType(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetVisibilityType(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.VisibilityType).PropertyValue;
    }

    public static uint GetNameColor(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetNameColor(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.PlayerListNameColor).PropertyValue;
    }

    public static char GetIcon(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetIcon(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.PlayerListIcon).PropertyValue;
    }

    public static bool GetIsProximityAlertEnabled(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetIsProximityAlertEnabled(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.AlertProximity).PropertyValue;
    }

    public static bool GetIsWorldTransferAlertEnabled(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetIsWorldTransferAlertEnabled(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.AlertWorldTransfer).PropertyValue;
    }

    public static bool GetIsNameChangeAlertEnabled(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetIsNameChangeAlertEnabled(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.AlertNameChange).PropertyValue;
    }

    public static bool GetNameplateShowInOverworld(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetNameplateShowInOverworld(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateShowInOverworld).PropertyValue;
    }

    public static bool GetNameplateShowInContent(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetNameplateShowInContent(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateShowInContent).PropertyValue;
    }

    public static bool GetNameplateShowInHighEndContent(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetNameplateShowInHighEndContent(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateShowInHighEndContent).PropertyValue;
    }

    public static bool GetNameplateShowColor(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetNameplateColorShowColor(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateShowColor).PropertyValue;
    }

    public static uint GetNameplateColor(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetNameplateColor(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateColor).PropertyValue;
    }

    public static bool GetNameplateShowColorIfDead(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetNameplateShowColorIfDead(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateDisableColorIfDead).PropertyValue;
    }

    public static bool GetNameplateUseCategory(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetNameplateUseCategory(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateUseCategoryName).PropertyValue;
    }

    public static bool GetNameplateUseCustomTitle(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetNameplateUseCustomTitle(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateUseCustomTitle).PropertyValue;
    }

    public static string GetNameplateCustomTitle(Player player)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetNameplateCustomTitle(): {player.Name}");
        return ExtractPlayerProperty(
            GetDefaultConfig(),
            player.PlayerConfig,
            player.GetCategoryPlayerConfigs(),
            x => x.NameplateCustomTitle).PropertyValue;
    }

    public static uint GetCategoryColor(Category category)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.GetCategoryColor(): {category.Name}");
        return ExtractCategoryProperty(
            GetDefaultConfig(),
            category.PlayerConfig,
            x => x.PlayerListNameColor).PropertyValue;
    }

    public static void UpdateCategoryConfig(int categoryId, PlayerConfig config)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.UpdateConfig(): {categoryId}, {config.Id}");
        var category = ServiceContext.CategoryService.GetCategory(categoryId);
        if (category == null)
        {
            return;
        }

        if (category.PlayerConfig.Id == 0)
        {
            category.PlayerConfig.CategoryId = category.Id;
            config.Id = RepositoryContext.PlayerConfigRepository.CreatePlayerConfig(config);
        }
        else
        {
            config.Id = category.PlayerConfig.Id;
            RepositoryContext.PlayerConfigRepository.UpdatePlayerConfig(config);
        }

        category.PlayerConfig = config;
        ServiceContext.CategoryService.UpdateCategory(category);
    }

    public static void DeletePlayerConfig(int playerId) => RepositoryContext.PlayerConfigRepository.DeletePlayerConfigByPlayerId(playerId);

    public static void UpdateConfig(int playerId, PlayerConfig config)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.UpdateConfig(): {playerId}, {config.Id}");
        var player = ServiceContext.PlayerDataService.GetPlayer(playerId);
        if (player == null)
        {
            return;
        }

        if (player.PlayerConfig.Id == 0)
        {
            config.PlayerId = playerId;
            RepositoryContext.PlayerConfigRepository.CreatePlayerConfig(config);
            config.Id = RepositoryContext.PlayerConfigRepository.GetIdByPlayerId(playerId) ?? 0;
        }
        else
        {
            config.Id = player.PlayerConfig.Id;
            RepositoryContext.PlayerConfigRepository.UpdatePlayerConfig(config);
        }

        player.PlayerConfig = config;
        ServiceContext.PlayerDataService.UpdatePlayer(player);
    }

    public static void DeletePlayerConfigByCategoryId(int categoryId) => RepositoryContext.PlayerConfigRepository.DeletePlayerConfigByCategoryId(categoryId);

    public static void ResetPlayerConfig(int playerId)
    {
        PluginLog.LogVerbose($"Entering PlayerConfigService.ResetPlayerConfig(): {playerId}");
        var player = ServiceContext.PlayerDataService.GetPlayer(playerId);
        if (player == null)
        {
            return;
        }

        player.PlayerConfig = new PlayerConfig
        {
            PlayerId = playerId,
        };

        ServiceContext.PlayerDataService.UpdatePlayer(player);
    }

    private static ExtractedProperty<T> ExtractPlayerProperty<T>(
        PlayerConfig defaultPlayerConfig,
        PlayerConfig playerPlayerConfig,
        List<PlayerConfig> categoryPlayerConfigs,
        Func<PlayerConfig, ConfigValue<T>> propertySelector)
    {
        var playerProperty = propertySelector(playerPlayerConfig);
        if (playerProperty.InheritOverride == InheritOverride.Override)
        {
            return new ExtractedProperty<T> { PlayerConfigType = PlayerConfigType.Player, PropertyValue = playerProperty.Value };
        }

        foreach (var categoryPlayerConfig in categoryPlayerConfigs)
        {
            var categoryProperty = propertySelector(categoryPlayerConfig);
            if (categoryProperty.InheritOverride == InheritOverride.Override)
            {
                return new ExtractedProperty<T> { PlayerConfigType = PlayerConfigType.Category, PropertyValue = categoryProperty.Value, CategoryId = categoryPlayerConfig.CategoryId ?? 0 };
            }
        }

        return ExtractDefaultProperty(defaultPlayerConfig, propertySelector);
    }

    private static ExtractedProperty<T> ExtractCategoryProperty<T>(
        PlayerConfig defaultPlayerConfig,
        PlayerConfig categoryPlayerConfig,
        Func<PlayerConfig, ConfigValue<T>> propertySelector)
    {
        var categoryProperty = propertySelector(categoryPlayerConfig);
        if (categoryProperty.InheritOverride == InheritOverride.Override)
        {
            return new ExtractedProperty<T> { PlayerConfigType = PlayerConfigType.Category, PropertyValue = categoryProperty.Value, CategoryId = categoryPlayerConfig.CategoryId ?? 0 };
        }

        return ExtractDefaultProperty(defaultPlayerConfig, propertySelector);
    }

    private static ExtractedProperty<T> ExtractDefaultProperty<T>(
        PlayerConfig defaultPlayerConfig,
        Func<PlayerConfig, ConfigValue<T>> propertySelector) => new() { PlayerConfigType = PlayerConfigType.Default, PropertyValue = propertySelector(defaultPlayerConfig).Value };

    private static PlayerConfig GetDefaultConfig() => ServiceContext.ConfigService.GetConfig().PlayerConfig;
}
