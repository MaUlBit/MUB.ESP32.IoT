using Microsoft.Extensions.Logging;
using System;

namespace MUB.ESP32.IoT
{
    public class LoggerFormatter
    {
        public string MessageFormatterStatic(string className, LogLevel logLevel, EventId eventId, string state, Exception exception)
        {
            string logstr = string.Empty;
            switch (logLevel)
            {
                case LogLevel.Trace:
                    logstr = "TRACE: ";
                    break;
                case LogLevel.Debug:
                    logstr = "DEBUG: ";
                    break;
                case LogLevel.Warning:
                    logstr = "WARNING: ";
                    break;
                case LogLevel.Error:
                    logstr = "ERROR: ";
                    break;
                case LogLevel.Critical:
                    logstr = "CRITICAL:";
                    break;
                case LogLevel.None:
                case LogLevel.Information:
                default:
                    break;
            }

            string eventstr = eventId.Id != 0 ? $" Event ID:{eventId}, " : string.Empty;
            //string msg = $"[{className}]{eventstr}{logstr}{state}";
            string date = DateTime.UtcNow.AddHours(2).ToString("HH:mm:ss");
            string msg = $"[{className}] {date} {eventstr}{state}";

            if (exception != null)
            {
                msg += $" {exception}";
            }

            return msg;
        }
    }
}
