using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Microsoft.Extensions.Configuration;

using Microsoft.Azure.Devices;

namespace AzurePasswordManager.Pages
{
    public class Test3Model : PageModel
    {
        private readonly IConfiguration _config;

        private static ServiceClient s_serviceClient;

        public static string Message { get; set; }

        // Connection string for your IoT Hub
        // az iot hub show-connection-string --hub-name {your iot hub name} --policy-name service
        private readonly string connectionString;

        // Invoke the direct method on the device, passing the payload
        private static async Task InvokeMethod() {
            var methodInvocation = new CloudToDeviceMethod("SetTelemetryInterval") { ResponseTimeout = TimeSpan.FromSeconds(30) };
            methodInvocation.SetPayloadJson("10");

            // Invoke the direct method asynchronously and get the response from the simulated device.
            var response = await s_serviceClient.InvokeDeviceMethodAsync("IotTestDevice", methodInvocation);

            Message = response.GetPayloadAsJson();
        }

        public Test3Model(IConfiguration config) {
            _config = config;

            connectionString = _config.GetConnectionString("IotHubService");
        }

        public async Task OnGetAsync() {
            //int retries = 0;
            //bool retry = false;

            // Create a ServiceClient to communicate with service-facing endpoint on your hub.
            s_serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
            InvokeMethod().GetAwaiter().GetResult();

            /*
            try {
            }
            /// Thrown when the operation returned an invalid status code
            /// </exception>
            catch (KeyVaultErrorException keyVaultException) {
                Message = keyVaultException.Message;
            }
            */
        }
    }
}