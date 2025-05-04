#if !ENABLE_VECERDI_LOGGING
using System;
using System.Diagnostics;

// ReSharper disable once CheckNamespace
namespace Vecerdi.Logging {
    internal enum LogLevel {
        Trace,
        Debug,
        Information,
        Warning,
        Error,
        Critical,
        None,
    }

    [AttributeUsage(AttributeTargets.Field)]
    internal class LogCategoryAttribute : Attribute {
        public LogLevel DefaultLogLevel { get; }

        public LogCategoryAttribute(LogLevel defaultLogLevel = LogLevel.Information) {
            DefaultLogLevel = defaultLogLevel;
        }
    }

    internal static class Log {
        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Exception(Exception? exception, ReadOnlySpan<char> category, object? context = null, LogLevel logLevel = LogLevel.Error) { }

        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Exception(Exception? exception, string category, object? context = null, LogLevel logLevel = LogLevel.Error) { }

        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Exception(Exception? exception, object? context = null, LogLevel logLevel = LogLevel.Error) { }

        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Trace(ReadOnlySpan<char> message, ReadOnlySpan<char> category, object? context = null) { }

        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Trace(string message, string category = "", object? context = null) { }

        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Debug(ReadOnlySpan<char> message, ReadOnlySpan<char> category = default, object? context = null) { }

        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Debug(string message, string category = "", object? context = null) { }

        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Information(ReadOnlySpan<char> message, ReadOnlySpan<char> category = default, object? context = null) { }

        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Information(string message, string category = "", object? context = null) { }

        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Warning(ReadOnlySpan<char> message, ReadOnlySpan<char> category = default, object? context = null) { }

        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Warning(string message, string category = "", object? context = null) { }

        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Error(ReadOnlySpan<char> message, ReadOnlySpan<char> category = default, object? context = null) { }

        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Error(string message, string category = "", object? context = null) { }

        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Critical(ReadOnlySpan<char> message, ReadOnlySpan<char> category, object? context = null) { }

        [Conditional("ENABLE_VECERDI_LOGGING")]
        public static void Critical(string message, string category = "", object? context = null) { }
    }
}
#endif
