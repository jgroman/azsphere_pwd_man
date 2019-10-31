/***************************************************************************//**
* @file    main.c
* @version 1.0.0
* @authors Jaroslav Groman
*
* @par Project Name
*     Azure Sphere Password Manager.
*
* @par Description
*    .
*
* @par Target device
*    Azure Sphere MT3620
*
* @par Related hardware
*    Avnet Azure Sphere Starter Kit
*    OLED display 128 x 64
*
* @par Code Tested With
*    1. Silicon: Avnet Azure Sphere Starter Kit
*    2. IDE: Visual Studio 2017
*    3. SDK: Azure Sphere SDK Preview
*
* @par Notes
*    .
*
*******************************************************************************/

#include <stdbool.h>
#include <errno.h>
#include <signal.h>
#include <string.h>
#include <time.h>
#include <stdlib.h>
#include <stdio.h>


// applibs_versions.h defines the API struct versions to use for applibs APIs.
#include "applibs_versions.h"
#include <applibs/log.h>
#include <applibs/gpio.h>

// Import project hardware abstraction from project 
// property "Target Hardware Definition Directory"
#include <hw/project_hardware.h>

// Using a single-thread event loop pattern based on Epoll and timerfd
#include "epoll_timerfd_utilities.h"

#include "azure_iot_utilities.h"
#include <azureiot/iothub_device_client_ll.h>

#include "connection_strings.h"
#include "build_options.h"



#include "lib_u8g2.h"

/*******************************************************************************
*   Macros and #define Constants
*******************************************************************************/

#define I2C_ISU             PROJECT_ISU2_I2C
#define I2C_BUS_SPEED       I2C_BUS_SPEED_STANDARD
#define I2C_TIMEOUT_MS      (100u)

#define I2C_ADDR_OLED       (0x3C)

#define OLED_ROTATION       U8G2_R0


/*******************************************************************************
* Forward declarations of private functions
*******************************************************************************/

/**
 * @brief Application termination handler.
 *
 * Signal handler for termination requests. This handler must be
 * async-signal-safe.
 *
 * @param signal_number
 *
 */
static void
termination_handler(int signal_number);

/**
 * @brief Initialize signal handlers.
 *
 * Set up SIGTERM termination handler.
 *
 * @return 0 on success, -1 otherwise.
 */
static int
init_handlers(void);

/**
 * @brief Initialize peripherals.
 *
 * Initialize all peripherals used by this project.
 *
 * @return 0 on success, -1 otherwise.
 */
static int
init_peripherals(void);

/**
 *
 */
static void
close_peripherals_and_handlers(void);

/**
 * @brief Button1 press handler
 */
static void
handle_button1_press(void);

/**
 * @brief Timer event handler for polling button states
 */
static void
event_handler_timer_button(EventData *event_data);

static void
*SetupHeapMessage(const char *messageFormat, size_t maxLength, ...);

static int
DirectMethodCall(const char *methodName, const char *payload, size_t payloadSize, char **responsePayload, size_t *responsePayloadSize);


/*******************************************************************************
* Global variables
*******************************************************************************/

extern IOTHUB_DEVICE_CLIENT_LL_HANDLE iothubClientHandle;


// Termination state flag
volatile sig_atomic_t gb_is_termination_requested = false;

static int g_fd_epoll = -1;        // Epoll file descriptor
static int g_fd_i2c = -1;          // I2C interface file descriptor
static int g_fd_gpio_button1 = -1; // GPIO button1 file descriptor
static int g_fd_poll_timer_button = -1;    // Poll timer button press file desc.

static GPIO_Value_Type g_state_button1 = GPIO_Value_High;

static EventData g_event_data_button = {          // Button Event data
    .eventHandler = &event_handler_timer_button
};

static u8g2_t g_u8g2;           // OLED device descriptor for u8g2


/*******************************************************************************
* Function definitions
*******************************************************************************/

/// <summary>
///     Application entry point
/// </summary>
int
main(int argc, char *argv[])
{
    Log_Debug("\n*** Starting ***\n");

    gb_is_termination_requested = false;

    // Initialize handlers
    if (init_handlers() != 0)
    {
        // Failed to init handlers
        gb_is_termination_requested = true;
    }

    // Initialize peripherals
    if (!gb_is_termination_requested)
    {
        if (init_peripherals() != 0)
        {
            // Failed to init peripherals
            gb_is_termination_requested = true;
        }
    }

    if (!gb_is_termination_requested)
    {
        // All handlers and peripherals are initialized properly at this point

        u8g2_ClearDisplay(&g_u8g2);

        // Main program loop
        while (!gb_is_termination_requested)
        {
            // Handle timers
            if (WaitForEventAndCallHandler(g_fd_epoll) != 0)
            {
                gb_is_termination_requested = true;
            }

            // Setup the IoT Hub client.
            // Notes:
            // - it is safe to call this function even if the client has already been set up, as in
            //   this case it would have no effect;
            // - a failure to setup the client is a fatal error.
            if (!AzureIoT_SetupClient()) {
                Log_Debug("ERROR: Failed to set up IoT Hub client\n");
                break;
            }

            // AzureIoT_DoPeriodicTasks() needs to be called frequently in order to keep active
            // the flow of data with the Azure IoT Hub
            AzureIoT_DoPeriodicTasks();
        }

        u8g2_ClearDisplay(&g_u8g2);
    }

    close_peripherals_and_handlers();
    Log_Debug("*** Terminated ***\n");
    return 0;
}

/*******************************************************************************
* Private function definitions
*******************************************************************************/

static void
termination_handler(int signal_number)
{
    gb_is_termination_requested = true;
}

static int
init_handlers(void)
{
    int result = -1;

    // Create signal handler
    struct sigaction action;
    memset(&action, 0, sizeof(struct sigaction));
    action.sa_handler = termination_handler;
    result = sigaction(SIGTERM, &action, NULL);
    if (result != 0)
    {
        Log_Debug("ERROR: %s - sigaction: errno=%d (%s)\n",
            __FUNCTION__, errno, strerror(errno));
    }

    // Create epoll
    if (result == 0)
    {
        g_fd_epoll = CreateEpollFd();
        if (g_fd_epoll < 0)
        {
            result = -1;
        }
    }

    // Tell the system about the callback function to call when we receive 
    // a Direct Method message from Azure
    AzureIoT_SetDirectMethodCallback(&DirectMethodCall);


    return result;
}

static int
init_peripherals(void)
{
    int result = -1;

    // Initialize I2C bus
    g_fd_i2c = I2CMaster_Open(I2C_ISU);
    if (g_fd_i2c < 0)
    {
        Log_Debug("ERROR: I2CMaster_Open: errno=%d (%s)\n",
            errno, strerror(errno));
    }
    else
    {
        result = I2CMaster_SetBusSpeed(g_fd_i2c, I2C_BUS_SPEED);
        if (result != 0)
        {
            Log_Debug("ERROR: I2CMaster_SetBusSpeed: errno=%d (%s)\n",
                errno, strerror(errno));
        }
        else
        {
            result = I2CMaster_SetTimeout(g_fd_i2c, I2C_TIMEOUT_MS);
            if (result != 0)
            {
                Log_Debug("ERROR: I2CMaster_SetTimeout: errno=%d (%s)\n",
                    errno, strerror(errno));
            }
        }
    }

    // Initialize 128x64 SSD1306 OLED
    if (result != -1)
    {
        // Set lib_u8g2 I2C interface file descriptor and device address
        lib_u8g2_set_i2c(g_fd_i2c, I2C_ADDR_OLED);

        // Set display type and callbacks
        u8g2_Setup_ssd1306_i2c_128x64_noname_f(&g_u8g2, OLED_ROTATION,
            lib_u8g2_byte_i2c, lib_u8g2_custom_cb);

        // Initialize display descriptor
        u8g2_InitDisplay(&g_u8g2);

        // Wake up display
        u8g2_SetPowerSave(&g_u8g2, 0);
    }

    // Initialize development kit button GPIO
    // -- Open button1 GPIO as input
    if (result != -1)
    {
        g_fd_gpio_button1 = GPIO_OpenAsInput(PROJECT_BUTTON_1);
        if (g_fd_gpio_button1 < 0)
        {
            Log_Debug("ERROR: Could not open button GPIO: %s (%d).\n",
                strerror(errno), errno);
            result = -1;
        }
    }

    // Create timer for button press check
    if (result != -1)
    {
        struct timespec button_press_check_period = { 0, 1000000 };

        g_fd_poll_timer_button = CreateTimerFdAndAddToEpoll(g_fd_epoll,
            &button_press_check_period, &g_event_data_button, EPOLLIN);
        if (g_fd_poll_timer_button < 0)
        {
            Log_Debug("ERROR: Could not create button poll timer: %s (%d).\n",
                strerror(errno), errno);
            result = -1;
        }
    }

    return result;
}

static void
close_peripherals_and_handlers(void)
{
    // Close Epoll fd
    CloseFdAndPrintError(g_fd_epoll, "Epoll");

    // Close I2C
    CloseFdAndPrintError(g_fd_i2c, "I2C");

    // Close button1 GPIO fd
    CloseFdAndPrintError(g_fd_gpio_button1, "Button1 GPIO");
}

static void
handle_button1_press(void)
{
    gb_is_termination_requested = true;

}

static void
event_handler_timer_button(EventData *event_data)
{
    bool b_is_all_ok = true;
    GPIO_Value_Type state_button1_current;

    // Consume timer event
    if (ConsumeTimerFdEvent(g_fd_poll_timer_button) != 0)
    {
        // Failed to consume timer event
        gb_is_termination_requested = true;
        b_is_all_ok = false;
    }

    if (b_is_all_ok)
    {
        // Check for a button1 press
        if (GPIO_GetValue(g_fd_gpio_button1, &state_button1_current) != 0)
        {
            Log_Debug("ERROR: Could not read button GPIO: %s (%d).\n",
                strerror(errno), errno);
            gb_is_termination_requested = true;
            b_is_all_ok = false;
        }
        else if (state_button1_current != g_state_button1)
        {
            if (state_button1_current == GPIO_Value_Low)
            {
                handle_button1_press();
            }
            g_state_button1 = state_button1_current;
        }
    }

    return;
}

/// <summary>
///     Allocates and formats a string message on the heap.
/// </summary>
/// <param name="messageFormat">The format of the message</param>
/// <param name="maxLength">The maximum length of the formatted message string</param>
/// <returns>The pointer to the heap allocated memory.</returns>
static void 
*SetupHeapMessage(const char *messageFormat, size_t maxLength, ...)
{
    va_list args;
    va_start(args, maxLength);
    char *message =
        malloc(maxLength + 1); // Ensure there is space for the null terminator put by vsnprintf.
    if (message != NULL) {
        vsnprintf(message, maxLength, messageFormat, args);
    }
    va_end(args);
    return message;
}

/// <summary>
///     Direct Method callback function, called when a Direct Method call is received from the Azure
///     IoT Hub.
/// </summary>
/// <param name="methodName">The name of the method being called.</param>
/// <param name="payload">The payload of the method.</param>
/// <param name="responsePayload">The response payload content. This must be a heap-allocated
/// string, 'free' will be called on this buffer by the Azure IoT Hub SDK.</param>
/// <param name="responsePayloadSize">The size of the response payload content.</param>
/// <returns>200 HTTP status code if the method name is reconginized and the payload is correctly parsed;
/// 400 HTTP status code if the payload is invalid;</returns>
/// 404 HTTP status code if the method name is unknown.</returns>
static int 
DirectMethodCall(const char *methodName, const char *payload, size_t payloadSize, char **responsePayload, size_t *responsePayloadSize)
{
    Log_Debug("\nDirect Method called %s\n", methodName);

    int result = 404; // HTTP status code.

    if (payloadSize < 32) {

        // Declare a char buffer on the stack where we'll operate on a copy of the payload.  
        char directMethodCallContent[payloadSize + 1];

        // Prepare the payload for the response. This is a heap allocated null terminated string.
        // The Azure IoT Hub SDK is responsible of freeing it.
        *responsePayload = NULL;  // Reponse payload content.
        *responsePayloadSize = 0; // Response payload content size.


        // Look for the haltApplication method name.  This direct method does not require any payload, other than
        // a valid Json argument such as {}.

        if (strcmp(methodName, "haltApplication") == 0) {

            // Log that the direct method was called and set the result to reflect success!
            Log_Debug("haltApplication() Direct Method called\n");
            result = 200;

            // Construct the response message.  This response will be displayed in the cloud when calling the direct method
            static const char resetOkResponse[] =
                "{ \"success\" : true, \"message\" : \"Halting Application\" }";
            size_t responseMaxLength = sizeof(resetOkResponse);
            *responsePayload = SetupHeapMessage(resetOkResponse, responseMaxLength);
            if (*responsePayload == NULL) {
                Log_Debug("ERROR: Could not allocate buffer for direct method response payload.\n");
                abort();
            }
            *responsePayloadSize = strlen(*responsePayload);

            // Set the terminitation flag to true.  When in Visual Studio this will simply halt the application.
            // If this application was running with the device in field-prep mode, the application would halt
            // and the OS services would resetart the application.
            gb_is_termination_requested = true;
            return result;
        }

        // Check to see if the setSensorPollTime direct method was called
        else if (strcmp(methodName, "setSensorPollTime") == 0) {

            // Log that the direct method was called and set the result to reflect success!
            Log_Debug("setSensorPollTime() Direct Method called\n");
            result = 200;

            // The payload should contain a JSON object such as: {"pollTime": 20}
            if (directMethodCallContent == NULL) {
                Log_Debug("ERROR: Could not allocate buffer for direct method request payload.\n");
                abort();
            }

            // Copy the payload into our local buffer then null terminate it.
            memcpy(directMethodCallContent, payload, payloadSize);
            directMethodCallContent[payloadSize] = 0; // Null terminated string.

            JSON_Value *payloadJson = json_parse_string(directMethodCallContent);

            // Verify we have a valid JSON string from the payload
            if (payloadJson == NULL) {
                goto payloadError;
            }

            // Verify that the payloadJson contains a valid JSON object
            JSON_Object *pollTimeJson = json_value_get_object(payloadJson);
            if (pollTimeJson == NULL) {
                goto payloadError;
            }

            // Pull the Key: value pair from the JSON object, we're looking for {"pollTime": <integer>}
            // Verify that the new timer is < 0
            int newPollTime = (int)json_object_get_number(pollTimeJson, "pollTime");
            if (newPollTime < 1) {
                goto payloadError;
            }
            else {

                Log_Debug("New PollTime %d\n", newPollTime);

                // Construct the response message.  This will be displayed in the cloud when calling the direct method
                static const char newPollTimeResponse[] =
                    "{ \"success\" : true, \"message\" : \"New Sensor Poll Time %d seconds\" }";
                size_t responseMaxLength = sizeof(newPollTimeResponse) + strlen(payload);
                *responsePayload = SetupHeapMessage(newPollTimeResponse, responseMaxLength, newPollTime);
                if (*responsePayload == NULL) {
                    Log_Debug("ERROR: Could not allocate buffer for direct method response payload.\n");
                    abort();
                }
                *responsePayloadSize = strlen(*responsePayload);

                /*
                // Define a new timespec variable for the timer and change the timer period
                struct timespec newAccelReadPeriod = { .tv_sec = newPollTime,.tv_nsec = 0 };
                SetTimerFdToPeriod(accelTimerFd, &newAccelReadPeriod);
                */

                return result;
            }
        }
        else {
            result = 404;
            Log_Debug("INFO: Direct Method called \"%s\" not found.\n", methodName);

            static const char noMethodFound[] = "\"method not found '%s'\"";
            size_t responseMaxLength = sizeof(noMethodFound) + strlen(methodName);
            *responsePayload = SetupHeapMessage(noMethodFound, responseMaxLength, methodName);
            if (*responsePayload == NULL) {
                Log_Debug("ERROR: Could not allocate buffer for direct method response payload.\n");
                abort();
            }
            *responsePayloadSize = strlen(*responsePayload);
            return result;
        }

    }
    else {
        Log_Debug("Payload size > 32 bytes, aborting Direct Method execution\n");
        goto payloadError;
    }

    // If there was a payload error, construct the 
    // response message and send it back to the IoT Hub for the user to see
payloadError:


    result = 400; // Bad request.
    Log_Debug("INFO: Unrecognised direct method payload format.\n");

    static const char noPayloadResponse[] =
        "{ \"success\" : false, \"message\" : \"request does not contain an identifiable "
        "payload\" }";

    size_t responseMaxLength = sizeof(noPayloadResponse) + strlen(payload);
    responseMaxLength = sizeof(noPayloadResponse);
    *responsePayload = SetupHeapMessage(noPayloadResponse, responseMaxLength);
    if (*responsePayload == NULL) {
        Log_Debug("ERROR: Could not allocate buffer for direct method response payload.\n");
        abort();
    }
    *responsePayloadSize = strlen(*responsePayload);

    return result;

}


/* [] END OF FILE */
