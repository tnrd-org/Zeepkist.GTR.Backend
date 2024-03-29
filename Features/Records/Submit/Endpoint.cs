﻿using System.Net;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TNRD.Zeepkist.GTR.Backend.Extensions;
using TNRD.Zeepkist.GTR.Backend.Rabbit;
using TNRD.Zeepkist.GTR.Database;
using TNRD.Zeepkist.GTR.Database.Models;
using TNRD.Zeepkist.GTR.DTOs.Rabbit;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Backend.Features.Records.Submit;

internal class Endpoint : Endpoint<RecordsSubmitRequestDTO>
{
    private readonly GTRContext context;
    private readonly IRabbitPublisher publisher;

    private static readonly string[] bannedLevels = new[]
    {
        "BE6DBC63CD48A2B1B0B14E7F337FD4BF0813DD6C" // NYE KICK OR CLUTCH VOTING MAP Decorated by Fred (ioi8)
    };

    public Endpoint(GTRContext context, IRabbitPublisher publisher)
    {
        this.context = context;
        this.publisher = publisher;
    }

    /// <inheritdoc />
    public override void Configure()
    {
        Post("records/submit");
        AuthSchemes(JwtBearerDefaults.AuthenticationScheme);
        Description(b => b.ExcludeFromDescription());
    }

    /// <inheritdoc />
    public override async Task HandleAsync(RecordsSubmitRequestDTO req, CancellationToken ct)
    {
        if (bannedLevels.Contains(req.Level, StringComparer.OrdinalIgnoreCase))
        {
            await SendOkAsync(ct);
            return;
        }

        if (!this.TryGetUserId(out int userId))
        {
            Logger.LogCritical("No UserId claim found!");
            ThrowError("Unable to find user id!");
            return;
        }

        if (await this.UserIsBanned(context))
        {
            Logger.LogWarning("Banned user tried to submit record");
            ThrowError("You are banned!");
            return;
        }

        if (userId != req.User)
        {
            Logger.LogCritical("UserId claim does not match request!");
            ThrowError("User id does not match!");
            return;
        }

        if (await DoesRecordExist(req, ct))
        {
            Logger.LogWarning("Double record submission detected!");
            await SendAsync(null, (int)HttpStatusCode.AlreadyReported, ct);
            return;
        }

        EntityEntry<Record> entry = context.Records.Add(new Record()
        {
            Level = req.Level,
            User = req.User,
            Time = req.Time,
            Splits = string.Join('|', req.Splits),
            IsValid = req.IsValid,
            GameVersion = req.GameVersion,
            ModVersion = req.ModVersion,
            DateCreated = DateTime.UtcNow
        });

        await context.SaveChangesAsync(ct);

        publisher.Publish("records",
            new RecordId
            {
                Id = entry.Entity.Id
            });

        publisher.Publish("media",
            new UploadRecordMediaRequest
            {
                Id = entry.Entity.Id,
                GhostData = req.GhostData,
                ScreenshotData = req.ScreenshotData
            });

        publisher.Publish("pb",
            new ProcessPersonalBestRequest
            {
                Record = entry.Entity.Id,
                User = userId,
                Level = req.Level,
                Time = entry.Entity.Time
            });

        publisher.Publish("wr",
            new ProcessWorldRecordRequest()
            {
                Record = entry.Entity.Id,
                User = userId,
                Level = req.Level,
                Time = entry.Entity.Time
            });

        await SendOkAsync(ct);
    }

    private async Task<bool> DoesRecordExist(RecordsSubmitRequestDTO req, CancellationToken ct)
    {
        string joinedSplits = string.Join('|', req.Splits);

        Record? existingRecord = await context.Records.AsNoTracking()
            .Where(r => r.User == req.User && r.Level == req.Level && Math.Abs(r.Time - req.Time) < 0.001f &&
                        r.Splits == joinedSplits)
            .FirstOrDefaultAsync(ct);

        if (existingRecord == null)
            return false;

        TimeSpan a = DateTime.Now - existingRecord.DateCreated;
        TimeSpan b = DateTime.UtcNow - existingRecord.DateCreated;

        return a < TimeSpan.FromMinutes(1) || b < TimeSpan.FromMinutes(1);
    }
}
