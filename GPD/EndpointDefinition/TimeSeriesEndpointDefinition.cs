using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace GPD.EndpointDefinition;

/// <summary>
/// TimeSeries
/// </summary>
/// <param name="T">Timestamp</param>
/// <param name="Sn">SeriesName</param>
/// <param name="V">Value</param>
public record TimeSeries(DateTimeOffset T, string Sn, decimal? V);

[UsedImplicitly]
public class TimeSeriesEndpointDefinition(Configuration configuration) : IEndpointDefinition
{
    static async Task TryCreateTimeSeriesTable(NpgsqlDataSource dataSource, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Table name cant be empty or null.", nameof(name));
        }

        var sql = @$"
            CREATE TABLE IF NOT EXISTS {name} (
                id timestamp with time zone NOT NULL UNIQUE,
                value numeric,
                PRIMARY KEY (id)
        )";
        await using var cmd = dataSource.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync();
    }
    
    static async Task RetainTimeSeriesForTable(NpgsqlDataSource dataSource, string name, DateTimeOffset timestamp)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Table name cant be empty or null.", nameof(name));
        }
        var sql = $"DELETE FROM {name} WHERE id < @t";
        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("@t", timestamp);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task TryCreateTableAndAddTimeSeries(TimeSeries dp)
    {
        await using var dataSource = NpgsqlDataSource.Create(configuration.PgSqlConnectionString);
        await TryCreateTimeSeriesTable(dataSource, dp.Sn);
        await AddTimeSeries(dataSource, dp);
        await RetainTimeSeriesForTable(dataSource, dp.Sn, DateTimeOffset.Now.AddYears(-configuration.MaxTimeSeriesRetentionInYears));
    }
    
    private static async Task AddTimeSeries(NpgsqlDataSource dataSource, TimeSeries dp)
    {
        var sql = $"INSERT INTO {dp.Sn} (id, value) VALUES (@t,@v)";
        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("@t", dp.T);
        cmd.Parameters.AddWithValue("@v", dp.V!);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task AddTimeSeries(IEnumerable<TimeSeries> inputDps)
    {
        await using var dataSource = NpgsqlDataSource.Create(configuration.PgSqlConnectionString);
        var groupedByTableName = inputDps.GroupBy(x => x.Sn).ToArray();
        foreach (var byTableName in groupedByTableName)
        {
            await TryCreateTimeSeriesTable(dataSource, byTableName.Key);
            foreach (var dps in byTableName.Batch(500))
            {
                var data = dps.ToArray();
                var name = data.First().Sn;
                await using var conn = await dataSource.OpenConnectionAsync();
                await using var writer = await conn.BeginBinaryImportAsync(
                    $"COPY {name} (id, value) FROM STDIN (FORMAT BINARY)");
                
                foreach (var dp in data)
                {
                    await writer.StartRowAsync();
                    await writer.WriteAsync(dp.T.ToUniversalTime(), NpgsqlDbType.TimestampTz);
                    await writer.WriteAsync(dp.V, NpgsqlDbType.Numeric);
                }

                await writer.CompleteAsync();

                await conn.CloseAsync();
                await RetainTimeSeriesForTable(dataSource, name, DateTimeOffset.Now.AddYears(-configuration.MaxTimeSeriesRetentionInYears).ToUniversalTime());
            }
        }
        
    }
    
    public void DefineEndpoints(WebApplication app)
    {
        app.MapPost("/timeserie", ([FromBody] TimeSeries timeSeries) => TryCreateTableAndAddTimeSeries(timeSeries))
            .WithTags("Time Series")
            .WithOpenApi();
        app.MapGet("/timeserie", ([FromQuery] DateTimeOffset t, [FromQuery] string tn, [FromQuery] decimal? v) =>
            TryCreateTableAndAddTimeSeries(new TimeSeries(t, tn, v)))
            .WithTags("Time Series")
            .WithOpenApi();
        app.MapPost("/timeseries", ([FromBody] TimeSeries[] timeSeries) => AddTimeSeries(timeSeries))
            .WithTags("Time Series")
            .WithOpenApi();
    }
}