﻿using FastEndpoints;
using Newtonsoft.Json;
using TNRD.Zeepkist.GTR.Backend.Extensions;
using TNRD.Zeepkist.GTR.Database;
using TNRD.Zeepkist.GTR.Database.Models;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Backend.Features.Stats.Submit;

public class Endpoint : Endpoint<UsersUpdateStatsRequestDTO>
{
    private readonly GTRContext context;

    public Endpoint(GTRContext context)
    {
        this.context = context;
    }

    public override void Configure()
    {
        Post("stats");
        Description(x => x.ExcludeFromDescription());
    }

    public override async Task HandleAsync(UsersUpdateStatsRequestDTO req, CancellationToken ct)
    {
        if (!this.TryGetUserId(out int userId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (!await context.Users.AnyAsync(x => x.Id == userId, ct))
        {
            await SendNotFoundAsync(ct);
            return;
        }

        Logger.LogInformation("Updating stats for user {UserId}; Existing stats: {SubmittedStats}",
            userId,
            JsonConvert.SerializeObject(req, Formatting.Indented));

        Stat? existingStat = await context.Stats.FirstOrDefaultAsync(x =>
                x.User == userId && x.Month == DateTime.UtcNow.Month && x.Year == DateTime.UtcNow.Year,
            ct);

        if (existingStat == null)
        {
            Stat stat = CreateNewStat(req, userId);
            Logger.LogInformation("Newly created stats for user {UserId}: {NewStats}",
                userId,
                JsonConvert.SerializeObject(stat, Formatting.Indented));

            await context.Stats.AddAsync(stat, ct);
            await context.SaveChangesAsync(ct);
        }
        else
        {
            Logger.LogInformation("Existing stats for user {UserId} before: {ExistingStats}",
                userId,
                JsonConvert.SerializeObject(existingStat, Formatting.Indented));

            UpdateStats(existingStat, req);

            Logger.LogInformation("Existing stats for user {UserId} after: {ExistingStats}",
                userId,
                JsonConvert.SerializeObject(existingStat, Formatting.Indented));

            await context.SaveChangesAsync(ct);
        }

        await SendOkAsync(ct);
    }

    private static Stat CreateNewStat(UsersUpdateStatsRequestDTO req, int userId)
    {
        Stat stat = new()
        {
            CrashTotal = req.CrashTotal,
            CrashRegular = req.CrashRegular,
            CrashEye = req.CrashEye,
            CrashGhost = req.CrashGhost,
            CrashSticky = req.CrashSticky,
            DistanceArmsUp = req.DistanceArmsUp,
            DistanceBraking = req.DistanceBraking,
            DistanceGrounded = req.DistanceGrounded,
            DistanceInAir = req.DistanceInAir,
            DistanceOnNoWheels = req.DistanceOnNoWheels,
            DistanceOnOneWheel = req.DistanceOnOneWheel,
            DistanceOnTwoWheels = req.DistanceOnTwoWheels,
            DistanceOnThreeWheels = req.DistanceOnThreeWheels,
            DistanceOnFourWheels = req.DistanceOnFourWheels,
            DistanceRagdoll = req.DistanceRagdoll,
            DistanceWithNoWheels = req.DistanceWithNoWheels,
            DistanceWithOneWheel = req.DistanceWithOneWheel,
            DistanceWithTwoWheels = req.DistanceWithTwoWheels,
            DistanceWithThreeWheels = req.DistanceWithThreeWheels,
            DistanceWithFourWheels = req.DistanceWithFourWheels,
            DistanceOnRegular = req.DistanceOnRegular,
            DistanceOnGrass = req.DistanceOnGrass,
            DistanceOnIce = req.DistanceOnIce,
            TimeArmsUp = req.TimeArmsUp,
            TimeBraking = req.TimeBraking,
            TimeGrounded = req.TimeGrounded,
            TimeInAir = req.TimeInAir,
            TimeOnNoWheels = req.TimeOnNoWheels,
            TimeOnOneWheel = req.TimeOnOneWheel,
            TimeOnTwoWheels = req.TimeOnTwoWheels,
            TimeOnThreeWheels = req.TimeOnThreeWheels,
            TimeOnFourWheels = req.TimeOnFourWheels,
            TimeRagdoll = req.TimeRagdoll,
            TimeWithNoWheels = req.TimeWithNoWheels,
            TimeWithOneWheel = req.TimeWithOneWheel,
            TimeWithTwoWheels = req.TimeWithTwoWheels,
            TimeWithThreeWheels = req.TimeWithThreeWheels,
            TimeWithFourWheels = req.TimeWithFourWheels,
            TimeOnRegular = req.TimeOnRegular,
            TimeOnGrass = req.TimeOnGrass,
            TimeOnIce = req.TimeOnIce,
            TimesFinished = req.TimesFinished,
            TimesStarted = req.TimesStarted,
            WheelsBroken = req.WheelsBroken,
            CheckpointsCrossed = req.CheckpointsCrossed,
            User = userId,
            Month = DateTime.UtcNow.Month,
            Year = DateTime.UtcNow.Year,
        };

        return stat;
    }

    private static void UpdateStats(Stat stats, UsersUpdateStatsRequestDTO req)
    {
        stats.CrashTotal += req.CrashTotal;
        stats.CrashRegular += req.CrashRegular;
        stats.CrashEye += req.CrashEye;
        stats.CrashGhost += req.CrashGhost;
        stats.CrashSticky += req.CrashSticky;
        stats.DistanceArmsUp += req.DistanceArmsUp;
        stats.DistanceBraking += req.DistanceBraking;
        stats.DistanceGrounded += req.DistanceGrounded;
        stats.DistanceInAir += req.DistanceInAir;
        stats.DistanceOnNoWheels += req.DistanceOnNoWheels;
        stats.DistanceOnOneWheel += req.DistanceOnOneWheel;
        stats.DistanceOnTwoWheels += req.DistanceOnTwoWheels;
        stats.DistanceOnThreeWheels += req.DistanceOnThreeWheels;
        stats.DistanceOnFourWheels += req.DistanceOnFourWheels;
        stats.DistanceRagdoll += req.DistanceRagdoll;
        stats.DistanceWithNoWheels += req.DistanceWithNoWheels;
        stats.DistanceWithOneWheel += req.DistanceWithOneWheel;
        stats.DistanceWithTwoWheels += req.DistanceWithTwoWheels;
        stats.DistanceWithThreeWheels += req.DistanceWithThreeWheels;
        stats.DistanceWithFourWheels += req.DistanceWithFourWheels;
        stats.DistanceOnRegular += req.DistanceOnRegular;
        stats.DistanceOnGrass += req.DistanceOnGrass;
        stats.DistanceOnIce += req.DistanceOnIce;
        stats.TimeArmsUp += req.TimeArmsUp;
        stats.TimeBraking += req.TimeBraking;
        stats.TimeGrounded += req.TimeGrounded;
        stats.TimeInAir += req.TimeInAir;
        stats.TimeOnNoWheels += req.TimeOnNoWheels;
        stats.TimeOnOneWheel += req.TimeOnOneWheel;
        stats.TimeOnTwoWheels += req.TimeOnTwoWheels;
        stats.TimeOnThreeWheels += req.TimeOnThreeWheels;
        stats.TimeOnFourWheels += req.TimeOnFourWheels;
        stats.TimeRagdoll += req.TimeRagdoll;
        stats.TimeWithNoWheels += req.TimeWithNoWheels;
        stats.TimeWithOneWheel += req.TimeWithOneWheel;
        stats.TimeWithTwoWheels += req.TimeWithTwoWheels;
        stats.TimeWithThreeWheels += req.TimeWithThreeWheels;
        stats.TimeWithFourWheels += req.TimeWithFourWheels;
        stats.TimeOnRegular += req.TimeOnRegular;
        stats.TimeOnGrass += req.TimeOnGrass;
        stats.TimeOnIce += req.TimeOnIce;
        stats.TimesFinished += req.TimesFinished;
        stats.TimesStarted += req.TimesStarted;
        stats.WheelsBroken += req.WheelsBroken;
        stats.CheckpointsCrossed += req.CheckpointsCrossed;
    }
}
