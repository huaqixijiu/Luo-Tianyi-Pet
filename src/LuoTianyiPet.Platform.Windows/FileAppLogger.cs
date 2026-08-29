using System.Globalization;
using System.Text;
using LuoTianyiPet.Core;

namespace LuoTianyiPet.Platform.Windows;

public sealed class FileAppLogger : IAppLogger
{
    private readonly LocalAppPaths _paths;
    private readonly object _sync = new();

    public FileAppLogger(LocalAppPaths paths)
    {
        _paths = paths;
    }

    public void Info(string eventName, string message)
    {
        Write("INFO", eventName, Sanitize(message));
    }

    public void Error(string eventName, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Write("ERROR", eventName, exception.GetType().Name);
    }

    private void Write(string level, string eventName, string message)
    {
        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(_paths.LogsDirectory);
                string logFile = Path.Combine(
                    _paths.LogsDirectory,
                    $"pet-{DateTime.UtcNow:yyyyMMdd}.log");
                string line = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{DateTimeOffset.UtcNow:O}\t{level}\t{Sanitize(eventName)}\t{message}{Environment.NewLine}");
                File.AppendAllText(logFile, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Logging is best-effort and must never crash the desktop pet.
        }
    }

    private static string Sanitize(string value)
    {
        string singleLine = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        return singleLine.Length <= 512 ? singleLine : singleLine[..512];
    }
}
