﻿using FastEndpoints;
using TNRD.Zeepkist.GTR.Backend.Extensions;
using TNRD.Zeepkist.GTR.Database;
using TNRD.Zeepkist.GTR.Database.Models;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;

namespace TNRD.Zeepkist.GTR.Backend.Features.Users.Discord;

internal class Endpoint : Endpoint<UsersUpdateDiscordIdRequestDTO>
{
    private readonly GTRContext context;

    /// <inheritdoc />
    public Endpoint(GTRContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public override void Configure()
    {
        Post("users/discord");
        Description(x => x.ExcludeFromDescription());
    }

    /// <inheritdoc />
    public override async Task HandleAsync(UsersUpdateDiscordIdRequestDTO req, CancellationToken ct)
    {
        if (!this.TryGetUserId(out int userId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        User? user = await context.Users
            .FirstOrDefaultAsync(x => x.Id == userId, ct);

        if (user == null)
        {
            await SendNotFoundAsync(ct);
        }
        else
        {
            user.DiscordId = req.DiscordId;
            await context.SaveChangesAsync(ct);
            await SendOkAsync(ct);
        }
    }
}
