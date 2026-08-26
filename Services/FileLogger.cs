using System.Text;

namespace GitHubDeploymentManager.Services;

public sealed class FileLogger : IAppLogger
{
    private static readonly object SyncRoot = new();
    private readonly string _logDirectory;

    public FileLogger(string? logFolderPath = null)
    {
        var configuredPath = string.IsNullOrWhiteSpace(logFolderPath) ? "Logs" : logFolderPath.Trim();
        _logDirectory = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));

        Directory.CreateDirectory(_logDirectory);
    }

    public string LogDirectory => _logDirectory;

    public string BeginOperation(string name, params (string Key, object? Value)[] properties)
    {
        var operationId = Guid.NewGuid().ToString("N");
        Info(operationId, $"Operation started: {name}", properties);
        return operationId;
    }

    public void Info(string message, params (string Key, object? Value)[] properties) =>
        Write("INFO", null, message, null, properties);

    public void Info(string operationId, string message, params (string Key, object? Value)[] properties) =>
        Write("INFO", operationId, message, null, properties);

    public void Error(string message, Exception? ex = null, params (string Key, object? Value)[] properties) =>
        Write("ERROR", null, message, ex, properties);

    public void Error(string operationId, string message, Exception? ex = null, params (string Key, object? Value)[] properties) =>
        Write("ERROR", operationId, message, ex, properties);

    private void Write(string level, string? operationId, string message, Exception? ex, params (string Key, object? Value)[] properties)
    {
        try
        {
            var file = Path.Combine(_logDirectory, $"deployment-{DateTime.Now:yyyy-MM-dd}.log");
            var entry = BuildEntry(level, operationId, message, ex, properties);

            lock (SyncRoot)
            {
                File.AppendAllText(file, entry);
            }
        }
        catch
        {
            // Logging must never crash the application.
        }
    }

    private static string BuildEntry(string level, string? operationId, string message, Exception? ex, params (string Key, object? Value)[] properties)
    {
        var builder = new StringBuilder();
        builder.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ");
        builder.Append('[').Append(level).Append("] ");

        if (!string.IsNullOrWhiteSpace(operationId))
            builder.Append("[OperationId=").Append(operationId).Append("] ");

        builder.Append(message);

        if (properties.Length > 0)
            builder.Append(" | ").Append(string.Join(", ", properties.Select(x => $"{x.Key}={FormatValue(x.Value)}")));

        if (ex is not null)
            builder.AppendLine().Append(ex);

        builder.AppendLine();
        return builder.ToString();
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "null",
        DateTimeOffset dto => dto.ToString("O"),
        DateTime dt => dt.ToString("O"),
        _ => value.ToString() ?? string.Empty
    };
}
