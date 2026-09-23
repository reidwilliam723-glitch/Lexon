using System.Diagnostics;
using System.Text;
using System.Reflection;

namespace Lexon.Core;

/// <summary>
/// Crash reporting system for capturing and logging application crashes
/// </summary>
public class CrashReporter
{
    private readonly string _logDirectory;
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public CrashReporter(string? applicationName = null)
    {
        var appName = applicationName ?? "Lexon";
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appName,
            "Logs"
        );

        Directory.CreateDirectory(_logDirectory);
        _logFilePath = Path.Combine(_logDirectory, $"crash_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");
    }

    /// <summary>
    /// Set up global exception handling for the current application
    /// </summary>
    public void SetupGlobalExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        
        // Windows Forms thread exception handling (only available in Windows Forms apps)
        try
        {
            var applicationType = Type.GetType("System.Windows.Forms.Application, System.Windows.Forms");
            if (applicationType != null)
            {
                var threadExceptionEvent = applicationType.GetEvent("ThreadException");
                if (threadExceptionEvent != null)
                {
                    var onThreadExceptionMethod = GetType().GetMethod("OnThreadException", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (onThreadExceptionMethod != null)
                    {
                        threadExceptionEvent.AddEventHandler(null, Delegate.CreateDelegate(threadExceptionEvent.EventHandlerType!, this, onThreadExceptionMethod));
                    }
                }
            }
        }
        catch
        {
            // Windows Forms not available, skip thread exception handling
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogCrash(exception, "UnhandledException", e.IsTerminating);
        }
    }

    private void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
    {
        LogCrash(e.Exception, "ThreadException", isTerminating: false);
    }

    /// <summary>
    /// Log a crash with detailed information
    /// </summary>
    public void LogCrash(Exception exception, string context = "Unknown", bool isTerminating = false)
    {
        try
        {
            var crashReport = BuildCrashReport(exception, context, isTerminating);

            lock (_lock)
            {
                File.WriteAllText(_logFilePath, crashReport);
            }

            // Also log to the legacy crash.log file for backward compatibility
            var legacyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Lexon",
                "crash.log"
            );

            Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
            File.WriteAllText(legacyPath, crashReport);
        }
        catch
        {
            // If crash reporting fails, there's not much we can do
        }
    }

    private string BuildCrashReport(Exception exception, string context, bool isTerminating)
    {
        var report = new StringBuilder();

        report.AppendLine("=== Lexon Crash Report ===");
        report.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine($"Context: {context}");
        report.AppendLine($"Is Terminating: {isTerminating}");
        report.AppendLine($"Version: {AppVersion.Current}");
        report.AppendLine($"OS: {Environment.OSVersion}");
        report.AppendLine($"Machine Name: {Environment.MachineName}");
        report.AppendLine($"User Name: {Environment.UserName}");
        report.AppendLine();

        report.AppendLine("=== Exception Details ===");
        report.AppendLine($"Type: {exception.GetType().FullName}");
        report.AppendLine($"Message: {exception.Message}");
        report.AppendLine($"Source: {exception.Source}");
        report.AppendLine($"Stack Trace:\n{exception.StackTrace}");
        report.AppendLine();

        if (exception.InnerException != null)
        {
            report.AppendLine("=== Inner Exception ===");
            report.AppendLine($"Type: {exception.InnerException.GetType().FullName}");
            report.AppendLine($"Message: {exception.InnerException.Message}");
            report.AppendLine($"Stack Trace:\n{exception.InnerException.StackTrace}");
            report.AppendLine();
        }

        report.AppendLine("=== System Information ===");
        report.AppendLine($"Processor Count: {Environment.ProcessorCount}");
        report.AppendLine($"Working Set: {Environment.WorkingSet / 1024 / 1024} MB");
        report.AppendLine($"GC Memory: {GC.GetTotalMemory(false) / 1024 / 1024} MB");
        report.AppendLine($"Is 64-bit OS: {Environment.Is64BitOperatingSystem}");
        report.AppendLine($"Is 64-bit Process: {Environment.Is64BitProcess}");
        report.AppendLine();

        report.AppendLine("=== Running Processes ===");
        try
        {
            var processes = Process.GetProcesses()
                .OrderBy(p => p.ProcessName)
                .Take(20);

            foreach (var process in processes)
            {
                try
                {
                    report.AppendLine($"- {process.ProcessName} (PID: {process.Id}, Memory: {process.WorkingSet64 / 1024 / 1024} MB)");
                }
                catch
                {
                    // Skip processes we can't access
                }
            }
        }
        catch
        {
            report.AppendLine("Unable to enumerate processes");
        }

        report.AppendLine();
        report.AppendLine("=== Environment Variables (Security-related) ===");
        try
        {
            var securityVars = new[] { "COMPUTERNAME", "USERNAME", "USERDOMAIN", "PROCESSOR_ARCHITECTURE" };
            foreach (var varName in securityVars)
            {
                var value = Environment.GetEnvironmentVariable(varName);
                report.AppendLine($"{varName}: {value}");
            }
        }
        catch
        {
            report.AppendLine("Unable to read environment variables");
        }

        report.AppendLine();
        report.AppendLine("=== End of Crash Report ===");

        return report.ToString();
    }

    /// <summary>
    /// Get all crash log files
    /// </summary>
    public IEnumerable<string> GetCrashLogs()
    {
        if (!Directory.Exists(_logDirectory))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.GetFiles(_logDirectory, "crash_*.log")
            .OrderByDescending(File.GetCreationTime);
    }

    /// <summary>
    /// Clean up old crash logs (older than specified days)
    /// </summary>
    public void CleanupOldLogs(int daysToKeep = 30)
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                return;
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
            var oldLogs = Directory.GetFiles(_logDirectory, "crash_*.log")
                .Where(f => File.GetCreationTime(f) < cutoffDate);

            foreach (var oldLog in oldLogs)
            {
                try
                {
                    File.Delete(oldLog);
                }
                catch
                {
                    // Skip files we can't delete
                }
            }
        }
        catch
        {
            // If cleanup fails, continue silently
        }
    }

    /// <summary>
    /// Get the most recent crash log content
    /// </summary>
    public string? GetMostRecentCrashLog()
    {
        try
        {
            var recentLogs = GetCrashLogs().ToList();
            if (recentLogs.Any())
            {
                return File.ReadAllText(recentLogs.First());
            }
        }
        catch
        {
            // If reading fails, return null
        }

        return null;
    }
}