﻿using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using FastEndpoints;
using FluentResults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TNRD.Zeepkist.GTR.Database;
using TNRD.Zeepkist.GTR.Database.Models;
using TNRD.Zeepkist.GTR.Backend.Extensions;
using TNRD.Zeepkist.GTR.Backend.Google;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;

namespace TNRD.Zeepkist.GTR.Backend.Features.Levels.Add;

internal class Endpoint : Endpoint<LevelsAddRequestDTO, GenericIdResponseDTO>
{
    private static readonly ConcurrentDictionary<string, AutoResetEvent> uidToAutoResetEvent = new();

    private readonly IGoogleUploadService googleUploadService;
    private readonly GTRContext context;

    public Endpoint(IGoogleUploadService googleUploadService, GTRContext context)
    {
        this.googleUploadService = googleUploadService;
        this.context = context;
    }

    /// <inheritdoc />
    public override void Configure()
    {
        Post("levels");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(b => b.ExcludeFromDescription());
    }

    /// <inheritdoc />
    public override async Task HandleAsync(LevelsAddRequestDTO req, CancellationToken ct)
    {
        if (!this.TryGetUserId(out int userId))
        {
            Logger.LogCritical("No UserId claim found!");
            ThrowError("Unable to find userid!");
        }

        if (await this.UserIsBanned(context))
        {
            Logger.LogWarning("Banned user tried to submit record");
            ThrowError("You are banned!");
            return;
        }

        Result<Level?> getResult = await AttemptGet(req, ct);
        if (getResult.IsFailed)
        {
            Logger.LogCritical("Failed to get level. Result: {Result}", getResult);
            ThrowError("Failed to get level");
            return;
        }

        if (getResult.Value != null)
        {
            await SendAsync(new GenericIdResponseDTO(getResult.Value.Id), cancellation: ct);
            return;
        }

        AutoResetEvent autoResetEvent = uidToAutoResetEvent.GetOrAdd(req.Uid, new AutoResetEvent(true));
        autoResetEvent.WaitOne();

        try
        {
            await GetOrCreateLevel(userId, req, ct);
        }
        finally
        {
            autoResetEvent.Set();
        }
    }

    private async Task<Result<Level?>> AttemptGet(LevelsAddRequestDTO req, CancellationToken ct)
    {
        Level? level;

        try
        {
            level = await context.Levels
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Uid == req.Uid, ct);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        return level == null ? Result.Ok() : Result.Ok<Level?>(level);
    }

    private async Task<Result<Level?>> AttemptGetWithTracking(LevelsAddRequestDTO req, CancellationToken ct)
    {
        Level? level;

        try
        {
            level = await context.Levels
                .FirstOrDefaultAsync(x => x.Uid == req.Uid, ct);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        return level == null ? Result.Ok() : Result.Ok<Level?>(level);
    }

    private async Task GetOrCreateLevel(int userId, LevelsAddRequestDTO req, CancellationToken ct)
    {
        Result<Level?> getResult = await AttemptGet(req, ct);

        if (getResult.IsFailed)
        {
            Logger.LogCritical("Failed to get level. Result: {Result}", getResult);
            ThrowError("Failed to get level");
            return;
        }

        if (getResult.Value != null)
        {
            await ReturnOrRefreshThumbnail(req, ct, getResult);
            return;
        }

        await CreateLevel(userId, req, ct);
    }

    private async Task ReturnOrRefreshThumbnail(LevelsAddRequestDTO req, CancellationToken ct, Result<Level?> getResult)
    {
        if (!string.IsNullOrEmpty(getResult.Value!.ThumbnailUrl))
        {
            await SendOkAsync(new GenericIdResponseDTO(getResult.Value.Id), ct);
            return;
        }

        getResult = await AttemptGetWithTracking(req, ct);
        if (getResult.IsFailed || getResult.Value == null)
        {
            Logger.LogCritical("Failed to get level. Result: {Result}", getResult);
            ThrowError("Failed to get level");
            return;
        }

        await UpdateThumbnailForLevel(getResult.Value, req, ct);
        await SendOkAsync(new GenericIdResponseDTO(getResult.Value.Id), ct);
    }

    private async Task CreateLevel(int userId, LevelsAddRequestDTO req, CancellationToken ct)
    {
        Result<string> uploadThumbnailResult = string.Empty;
        if (!string.IsNullOrEmpty(req.Thumbnail))
        {
            uploadThumbnailResult = await googleUploadService.UploadThumbnail(req.Uid, req.Thumbnail, ct);
            if (uploadThumbnailResult.IsFailed)
            {
                Logger.LogError("Unable to upload thumbnail: {Result}", uploadThumbnailResult.ToString());
            }
        }

        EntityEntry<Level> entry;

        try
        {
            entry = context.Levels.Add(new Level()
            {
                Uid = req.Uid,
                Wid = req.Wid,
                Name = req.Name,
                Author = RemoveHtmlTags(req.Author),
                TimeAuthor = req.TimeAuthor,
                TimeGold = req.TimeGold,
                TimeSilver = req.TimeSilver,
                TimeBronze = req.TimeBronze,
                ThumbnailUrl = uploadThumbnailResult.IsSuccess ? uploadThumbnailResult.Value : string.Empty,
                CreatedBy = userId,
                IsValid = req.IsValid
            });
        }
        catch (Exception e)
        {
            Logger.LogCritical(e, "Unable to add level to database!");
            ThrowError("Unable to add level to database!");
            return;
        }

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (Exception e)
        {
            Logger.LogCritical(e, "Unable to save level to database!");
            ThrowError("Unable to save level to database!");
            return;
        }

        await SendOkAsync(new GenericIdResponseDTO(entry.Entity.Id), ct);
    }

    private async Task UpdateThumbnailForLevel(Level level, LevelsAddRequestDTO req, CancellationToken ct)
    {
        Result<string> uploadThumbnailResult = await googleUploadService.UploadThumbnail(req.Uid, req.Thumbnail, ct);
        if (uploadThumbnailResult.IsFailed)
        {
            Logger.LogError("Unable to upload thumbnail: {Result}", uploadThumbnailResult.ToString());
            return;
        }

        level.ThumbnailUrl = uploadThumbnailResult.Value;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (Exception e)
        {
            Logger.LogCritical("Unable to save level to database! {Exception}", e);
        }
    }

    private string RemoveHtmlTags(string author)
    {
        return Regex.Replace(author, "<[^>]*>", string.Empty);
    }
}
