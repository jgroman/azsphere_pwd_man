﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// This application uses the Azure IoT Hub device SDK for .NET
// For samples see: https://github.com/Azure/azure-iot-sdk-csharp/tree/master/iothub/device/samples

using System;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;

namespace sphere_simulator
{
    class SphereSimulator
    {
        private static DeviceClient s_deviceClient;

        // The device connection string to authenticate the device with your IoT hub.
        // Using the Azure CLI:
        // az iot hub device-identity show-connection-string --hub-name {YourIoTHubName} --device-id IotTestDevice --output table
        private readonly static string s_connectionString = "HostName=JGTestIoTHub.azure-devices.net;DeviceId=SphereSimulator;SharedAccessKey=KTGeyIUBbC8j8xWQ3Nlye/DY+sl0GuFc/1ix8Y/llCM=";

        private static int s_telemetryInterval = 100; // Seconds

        // Handle the direct method call
        private static Task<MethodResponse> SetTelemetryInterval(MethodRequest methodRequest, object userContext)
        {
            var data = Encoding.UTF8.GetString(methodRequest.Data);
            Console.WriteLine("{0} > Received message: {1}", DateTime.Now, data);

            // Check the payload is a single integer value
            if (Int32.TryParse(data, out s_telemetryInterval))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Telemetry interval set to {0} seconds", data);
                Console.ResetColor();

                // Acknowlege the direct method call with a 200 success message
                string result = "{\"result\":\"Executed direct method: " + methodRequest.Name + "\"}";
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
            }
            else
            {
                // Acknowlege the direct method call with a 400 error message
                string result = "{\"result\":\"Invalid parameter\"}";
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 400));
            }
        }

        private static Task<MethodResponse> SetSiteLoginData(MethodRequest methodRequest, object userContext)
        {
            var data = Encoding.UTF8.GetString(methodRequest.Data);
            Console.WriteLine("{0} > Received message: {1}", DateTime.Now, data);

            string result = "{\"result\":\"Item loaded.\"}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }


        // Async method to send simulated telemetry
        private static async void SendDeviceToCloudMessagesAsync()
        {
            // Initial telemetry values
            double minTemperature = 20;
            double minHumidity = 60;
            Random rand = new Random();

            while (true)
            {
                double currentTemperature = minTemperature + rand.NextDouble() * 15;
                double currentHumidity = minHumidity + rand.NextDouble() * 20;

                // Create JSON message
                var telemetryDataPoint = new
                {
                    temperature = currentTemperature,
                    humidity = currentHumidity
                };
                var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
                var message = new Message(Encoding.ASCII.GetBytes(messageString));

                // Add a custom application property to the message.
                // An IoT hub can filter on these properties without access to the message body.
                message.Properties.Add("temperatureAlert", (currentTemperature > 30) ? "true" : "false");

                // Send the telemetry message
                //await s_deviceClient.SendEventAsync(message);
                //Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, messageString);

                await Task.Delay(s_telemetryInterval * 1000);
            }
        }
        private static void Main(string[] args)
        {
            Console.WriteLine("Azure Sphere Simulated Device. Ctrl-C to exit.\n");

            // Connect to the IoT hub using the MQTT protocol
            s_deviceClient = DeviceClient.CreateFromConnectionString(s_connectionString, TransportType.Mqtt);

            // Create a handler for the direct method call
            s_deviceClient.SetMethodHandlerAsync("SetTelemetryInterval", SetTelemetryInterval, null).Wait();

            // Create a handler for the direct method call
            s_deviceClient.SetMethodHandlerAsync("SetSiteLoginData", SetSiteLoginData, null).Wait();


            SendDeviceToCloudMessagesAsync();
            Console.ReadLine();
        }
    }
}
