﻿using Microsoft.Extensions.Caching.Memory;
using Quartz;
using TNRD.Zeepkist.GTR.Backend.Extensions;
using TNRD.Zeepkist.GTR.Database;
using TNRD.Zeepkist.GTR.Database.Models;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;

namespace TNRD.Zeepkist.GTR.Backend.Jobs;



internal abstract class PopularityJobBase : IJob
{
    private readonly GTRContext db;
    private readonly IMemoryCache cache;

    private readonly Dictionary<Level, int> levelToCount;
    private readonly Dictionary<Level, Record> levelToWorldRecord;

    protected PopularityJobBase(GTRContext db, IMemoryCache cache)
    {
        this.db = db;
        this.cache = cache;

        levelToCount = new(BasicModelEqualityComparer.Instance);
        levelToWorldRecord = new(BasicModelEqualityComparer.Instance);
    }

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        levelToCount.Clear();
        levelToWorldRecord.Clear();

        DateTime stamp = GetStamp();

        var query = db.Records.AsNoTracking()
            .Include(x => x.UserNavigation)
            .Join(db.Levels.AsNoTracking(), r => r.Level, l => l.Id, (r, l) => new { r, l })
            .Where(t => t.r.DateCreated >= stamp && t.r.IsBest)
            .OrderBy(t => t.r.Level)
            .Select(t => new { Record = t.r, Level = t.l });

        var enumerable = query.AsAsyncEnumerable().WithCancellation(context.CancellationToken);

        await foreach (var item in enumerable)
        {
            levelToCount.TryAdd(item.Level, 0);
            levelToCount[item.Level]++;

            if (item.Record.IsWr)
                levelToWorldRecord[item.Level] = item.Record;
        }

        List<LevelPopularityResponseModel> ordered = levelToCount
            .OrderByDescending(x => x.Value)
            .Select(CreateResponseModel)
            .Take(GetTakeCount())
            .ToList();

        cache.Set(GetCacheKey(), ordered);
    }

    private LevelPopularityResponseModel CreateResponseModel(KeyValuePair<Level, int> item)
    {
        levelToWorldRecord.TryGetValue(item.Key, out Record? record);
        return new LevelPopularityResponseModel()
        {
            Level = item.Key.ToResponseModel(record),
            RecordsCount = item.Value
        };
    }

    protected abstract DateTime GetStamp();

    protected abstract int GetTakeCount();

    protected abstract string GetCacheKey();
}
