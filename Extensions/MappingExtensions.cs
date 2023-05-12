﻿using TNRD.Zeepkist.GTR.Backend.Database.Models;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;

namespace TNRD.Zeepkist.GTR.Backend.Extensions;

internal static class MappingExtensions
{
    public static LevelResponseModel ToResponseModel(this Level level)
    {
        return new LevelResponseModel
        {
            Id = level.Id,
            Author = level.Author,
            Name = level.Name,
            IsValid = level.IsValid,
            ThumbnailUrl = level.ThumbnailUrl,
            TimeAuthor = level.TimeAuthor,
            TimeBronze = level.TimeBronze,
            TimeGold = level.TimeGold,
            TimeSilver = level.TimeSilver,
            UniqueId = level.Uid,
            WorkshopId = level.Wid
        };
    }

    public static UserResponseModel ToResponseModel(this User user)
    {
        return new UserResponseModel
        {
            Id = user.Id,
            DiscordId = user.DiscordId,
            SteamId = user.SteamId,
            SteamName = user.SteamName
        };
    }
}
