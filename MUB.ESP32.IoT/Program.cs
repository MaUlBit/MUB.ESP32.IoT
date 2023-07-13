using MUB.ESP32.IoT;
using System;
using System.Diagnostics;
using System.Threading;
using System.Device.Gpio;
using System.IO;
using System.IO.Ports;
using System.Device.I2c;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using nanoFramework.Hardware.Esp32;
using nanoFramework.Logging;
using nanoFramework.Logging.Debug;
using nanoFramework.Logging.Serial;
using nanoFramework.Json;
using nanoFramework.Networking;
using Microsoft.Extensions.Logging;
using nanoFramework.M2Mqtt;
using nanoFramework.Azure.Devices.Shared;
using nanoFramework.M2Mqtt.Messages;
using System.Text;
using System.Web;

namespace MUB.ESP32.IoT
{
    public class Program
    {
        // Logger
        private static ILogger _logger;

        // Logger Formatter
        private static LoggerFormatter loggerFormatter = new LoggerFormatter();

        // Azure IoT Hub
        const string DeviceID = "nanoDeepSleep";
        const string IotBrokerAddress = "YOURIOTHUB.azure-devices.net";
        const string SasKey = "a valid SAS token";
        string telemetryTopic = $"devices/{DeviceID}/messages/events/";
        const string TwinReportedPropertiesTopic = "$iothub/twin/PATCH/properties/reported/";
        const string TwinDesiredPropertiesTopic = "$iothub/twin/GET/";
        static ushort messageID = ushort.MaxValue;
        static bool twinReceived = false;
        static bool messageReceived = false;

        // WiFi - will not be part of the code - store in Device Explorer Configuration
        //const string Ssid = "yourWifi";
        //const string Password = "youWifiPassowrd";

        // Timings
        static DateTime allupOperation = DateTime.UtcNow;
        static int sleepTimeMinutes = 60000;
        static int minutesToGoToSleep = 2;

        public static void Main()
        {
            // Configure the serial logger
            ConfigureSerialLogger();

            // Initialize GPIO controller and LED pin
            var gpioController = new GpioController();
            var ledPin = gpioController.OpenPin(13, PinMode.Output);

            // As we are using TLS, we need a valid date & time
            // We will wait maximum 1 minute to get connected and have a valid date
            CancellationTokenSource cs = new CancellationTokenSource(sleepTimeMinutes);
            var success = WifiNetworkHelper.Reconnect(requiresDateTime: true, token: cs.Token);
            if (!success)
            {
                Log($"Can't connect to wifi: {NetworkHelper.Status}");
                if (WifiNetworkHelper.HelperException != null)
                {
                    Log($"WifiNetworkHelper.HelperException");
                }

                // This prevent to debug, once in deep sleep, you won't be able to connect to the device
                // So comment to, start with and check what's happening.
                //GoToSleep();
            }
            else if (success)
            {
                Log("Connected to Wifi");
            }

            // Reset the time counter if the previous date was not valid
            if (allupOperation.Year < 2018)
            {
                allupOperation = DateTime.UtcNow.AddHours(2);
            }

            Log($"Date and time is now {DateTime.UtcNow.AddHours(2)}");

            // nanoFramework socket implementation requires a valid root CA to authenticate with.
            // This can be supplied to the caller (as it's doing on the code bellow) or the Root CA has to be stored in the certificate store
            // Root CA for Azure from here: https://github.com/Azure/azure-iot-sdk-c/blob/master/certs/certs.c
            // We are storing this certificate in the resources

            // Keep in mind the current IoTHub "Hub root certificate" is near to expire 
            // Old Baltimore
            // X509Certificate azureRootCACert = new X509Certificate(Resources.GetBytes(Resources.BinaryResources.BaltimoreCyberTrustRoot));
            // New DigiCert
            X509Certificate azureRootCACert = new X509Certificate(Resources.GetBytes(Resources.BinaryResources.DigiCertGlobalRootG2));

            // Creates MQTT Client with default port 8883 using TLS protocol
            MqttClient mqttc = new MqttClient(
                IotBrokerAddress,
                8883,
                true,
                azureRootCACert,
                null,
                MqttSslProtocols.TLSv1_2);


            // Handler for received messages on the subscribed topics
            mqttc.MqttMsgPublishReceived += ClientMqttMsgReceived;
            // Handler for publisher
            mqttc.MqttMsgPublished += ClientMqttMsgPublished;

            // Now connect the device
            MqttReasonCode code = mqttc.Connect(
                DeviceID,
                $"{IotBrokerAddress}/{DeviceID}/api-version=2020-09-30",
                GetSharedAccessSignature(null, SasKey, $"{IotBrokerAddress}/devices/{DeviceID}", new TimeSpan(24, 0, 0)),
                false,
                MqttQoSLevel.ExactlyOnce,
                false, "$iothub/twin/GET/?$rid=999",
                "Disconnected",
                false,
                60
                );




            while (true)
            {
                // Toggle LED on/off to indicate that the controller is running
                ledPin.Toggle();

                Log("Hello from nanoFramework!");

                // Delay for 1 second
                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// Configure the serial logger
        /// </summary>
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

        /// <summary>
        /// Log a message
        /// </summary>
        /// <param name="message"></param>
        static void Log(string message)
        {
            _logger?.LogDebug(message);
        }

        static void ClientMqttMsgReceived(object sender, MqttMsgPublishEventArgs e)
        {
            try
            {
                Log($"Message received on topic: {e.Topic}");
                string message = Encoding.UTF8.GetString(e.Message, 0, e.Message.Length);
                Log($"and message length: {message.Length}");

                if (e.Topic.StartsWith("$iothub/twin/res/204"))
                {
                    Log("and received confirmation for desired properties.");
                }
                else if (e.Topic.StartsWith("$iothub/twin/"))
                {
                    if (e.Topic.IndexOf("res/400/") > 0 || e.Topic.IndexOf("res/404/") > 0 || e.Topic.IndexOf("res/500/") > 0)
                    {
                        Log("and was in the error queue.");
                    }
                    else
                    {
                        Log("and was in the success queue.");
                        if (message.Length > 0)
                        {
                            // skip if already received in this session
                            if (!twinReceived)
                            {
                                try
                                {
                                    TwinProperties twin = (TwinProperties)JsonConvert.DeserializeObject(message, typeof(TwinProperties));
                                    minutesToGoToSleep = twin.desired.TimeToSleep != 0 ? twin.desired.TimeToSleep : minutesToGoToSleep;
                                    twinReceived = true;
                                }
                                catch
                                {
                                    // We will ignore
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Exception in event: {ex}");
            }
        }

        static void ClientMqttMsgPublished(object sender, MqttMsgPublishedEventArgs e)
        {
            Log($"Response from publish with message id: {e.MessageId}");
            if (e.MessageId == messageID)
            {
                messageReceived = true;
            }
        }

        static string GetSharedAccessSignature(string keyName, string sharedAccessKey, string resource, TimeSpan tokenTimeToLive)
        {
            // http://msdn.microsoft.com/en-us/library/azure/dn170477.aspx
            // the canonical Uri scheme is http because the token is not amqp specific
            // signature is computed from joined encoded request Uri string and expiry string

            var exp = DateTime.UtcNow.ToUnixTimeSeconds() + (long)tokenTimeToLive.TotalSeconds;

            string expiry = exp.ToString();
            string encodedUri = HttpUtility.UrlEncode(resource);

            var hmacsha256 = new HMACSHA256(Convert.FromBase64String(sharedAccessKey));
            byte[] hmac = hmacsha256.ComputeHash(Encoding.UTF8.GetBytes(encodedUri + "\n" + expiry));
            string sig = Convert.ToBase64String(hmac);

            if (keyName != null)
            {
                return String.Format(
                "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}",
                encodedUri,
                HttpUtility.UrlEncode(sig),
                HttpUtility.UrlEncode(expiry),
                HttpUtility.UrlEncode(keyName));
            }
            else
            {
                return String.Format(
                    "SharedAccessSignature sr={0}&sig={1}&se={2}",
                    encodedUri,
                    HttpUtility.UrlEncode(sig),
                    HttpUtility.UrlEncode(expiry));
            }
        }

    }
}
