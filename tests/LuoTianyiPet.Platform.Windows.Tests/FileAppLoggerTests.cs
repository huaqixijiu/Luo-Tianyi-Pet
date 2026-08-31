using LuoTianyiPet.Platform.Windows;

namespace LuoTianyiPet.Platform.Windows.Tests;

public sealed class FileAppLoggerTests
{
    [Fact]
    public void Error_RecordsSafeTargetAndParameterWithoutExceptionMessage()
    {
        string testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"LuoTianyiPet-logger-{Guid.NewGuid():N}");
        try
        {
            FileAppLogger logger = new(new LocalAppPaths(testDirectory));
            ArgumentOutOfRangeException exception = CaptureOutOfRangeException();

            logger.Error("test.failure", exception);

            string logFile = Assert.Single(Directory.GetFiles(
                Path.Combine(testDirectory, "logs"),
                "pet-*.log"));
            string contents = File.ReadAllText(logFile);
            Assert.Contains("ArgumentOutOfRangeException", contents, StringComparison.Ordinal);
            Assert.Contains("Target=", contents, StringComparison.Ordinal);
            Assert.Contains(".ThrowOutOfRange", contents, StringComparison.Ordinal);
            Assert.Contains("Parameter=peakLevel", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("private diagnostic value", contents, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    private static ArgumentOutOfRangeException CaptureOutOfRangeException()
    {
        try
        {
            ThrowOutOfRange();
            throw new InvalidOperationException("Expected the helper to throw.");
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return exception;
        }
    }

    private static void ThrowOutOfRange() =>
        throw new ArgumentOutOfRangeException("peakLevel", "private diagnostic value");
}
