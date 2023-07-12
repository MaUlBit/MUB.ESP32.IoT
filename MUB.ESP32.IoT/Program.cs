using System;
using System.Diagnostics;
using System.Threading;
using System.Device.Gpio;
using System.IO.Ports;
using System.Device.I2c;
using nanoFramework.Hardware.Esp32;
using nanoFramework.Logging;
using nanoFramework.Logging.Debug;
using nanoFramework.Logging.Serial;
using nanoFramework.Json;
using Microsoft.Extensions.Logging;

namespace MUB.ESP32.IoT
{
    public class Program
    {
        // Logger
        private static ILogger _logger;

        // Logger Formatter
        private static LoggerFormatter loggerFormatter = new LoggerFormatter();

        // Allup Operation
        DateTime allupOperation = DateTime.UtcNow;

        public static void Main()
        {
            ConfigureSerialLogger();

            // Initialize GPIO controller and LED pin
            var gpioController = new GpioController();
            var ledPin = gpioController.OpenPin(13, PinMode.Output);

            // ToDo: pico version des ESP32 mal ausprobier
            // ToDo: C:\Users\Dell\Documents\NanoFrameworkSamples\samples\AzureMQTTTwinsBMP280Sleep durchgehen

            while (true)
            {
                // Toggle LED on/off to indicate that the controller is running
                ledPin.Toggle();

                Log("Hello from nanoFramework!");

                // Delay for 1 second
                Thread.Sleep(1000);
            }
        }

        private static void ConfigureSerialLogger()
        {
            // Configure ESP32 pins for serial logging
            Configuration.SetPinFunction(16, DeviceFunction.COM2_RX);
            Configuration.SetPinFunction(17, DeviceFunction.COM2_TX);

            // Use the Factory to create a logger
            LogDispatcher.LoggerFactory = new SerialLoggerFactory("COM2");
            _logger = LogDispatcher.LoggerFactory.CreateLogger("Debug");
            LoggerExtensions.MessageFormatter = typeof(LoggerFormatter).GetType().GetMethod("MessageFormatterStatic");
            //_logger.LogDebug("Starting Device ...");

            //logger = new SerialLogger("COM2");
            Log("Device Started");
            Log("Logger Configured");

        }

        static void Log(string message)
        {
            _logger?.LogDebug(message);
        }
    }
}
