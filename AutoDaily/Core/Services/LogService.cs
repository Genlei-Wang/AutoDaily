using System;
using System.IO;
using System.Text;

namespace AutoDaily.Core.Services
{
    public class LogService
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoDaily", "Logs");
        private static readonly string LogFilePath = Path.Combine(LogDirectory, 
            $"AutoDaily_{DateTime.Now:yyyyMMdd}.log");

        static LogService()
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // 忽略日志写入错误
            }
        }

        public static void LogUserAction(string action)
        {
            Log($"用户操作: {action}", LogLevel.UserAction);
        }

        public static void LogScreenAction(string action, int step)
        {
            Log($"屏幕操作步骤 {step}: {action}", LogLevel.ScreenAction);
        }

        public static void LogError(string message, Exception ex = null)
        {
            Log($"{message} {(ex != null ? ex.ToString() : "")}", LogLevel.Error);
        }
    }

    public enum LogLevel
    {
        Info,
        UserAction,
        ScreenAction,
        Error,
        Warning
    }
}

