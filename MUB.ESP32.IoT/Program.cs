using Microsoft.Extensions.Logging;
using nanoFramework.Logging.Debug;
using nanoFramework.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Device.Gpio;

namespace MUB.ESP32.IoT
{
    public class Program
    {
        // Logger
        private static ILogger _logger;

        // Logger Formatter
        private static LoggerFormatter loggerFormatter = new LoggerFormatter();

        public static void Main()
        {
            // Configure the logger
            LogDispatcher.LoggerFactory = new DebugLoggerFactory();
            _logger = LogDispatcher.LoggerFactory.CreateLogger("Debug");
            LoggerExtensions.MessageFormatter = typeof(LoggerFormatter).GetType().GetMethod("MessageFormatterStatic");
            _logger.LogDebug("Starting Device ...");

            // Initialize GPIO controller and LED pin
            var gpioController = new GpioController();
            var ledPin = gpioController.OpenPin(13, PinMode.Output);

            while (true)
            {
                // Toggle LED on/off to indicate that the controller is running
                ledPin.Toggle();

                // Delay for 1 second
                Thread.Sleep(1000);
            }
        }
    }
}
