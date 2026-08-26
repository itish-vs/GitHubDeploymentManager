namespace GitHubDeploymentManager.Services;

public interface IAppLogger
{
    string LogDirectory { get; }
    string BeginOperation(string name, params (string Key, object? Value)[] properties);
    void Info(string message, params (string Key, object? Value)[] properties);
    void Info(string operationId, string message, params (string Key, object? Value)[] properties);
    void Error(string message, Exception? ex = null, params (string Key, object? Value)[] properties);
    void Error(string operationId, string message, Exception? ex = null, params (string Key, object? Value)[] properties);
}
