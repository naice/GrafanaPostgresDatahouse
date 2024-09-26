public class Configuration
{
    public const string SECTION_KEY = nameof(Configuration);
    public string PgSqlConnectionString { get; set; } = string.Empty;
}