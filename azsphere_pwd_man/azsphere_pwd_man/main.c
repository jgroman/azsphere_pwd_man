/***************************************************************************//**
* @file    main.c
* @version 1.0.0
* @authors Microsoft - Azure Sphere Examples
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

// Azure IoT 
#include "azure_iot_utilities.h"
#include "connection_strings.h"
#include "build_options.h"

// OLED display support library
#include "lib_u8g2.h"

/*******************************************************************************
*   Macros and #define Constants
*******************************************************************************/

#define I2C_ISU                 PROJECT_ISU2_I2C
#define I2C_BUS_SPEED           I2C_BUS_SPEED_STANDARD
#define I2C_TIMEOUT_MS          (100u)

#define I2C_ADDR_OLED           (0x3C)
#define I2C_ADDR_USB_KEYBOARD   (0x08)

#define OLED_ROTATION           U8G2_R0

#define DIRECT_METHOD_CALL_PAYLOAD_MAX      400

#define JSON_NAME_LENGTH        30
#define JSON_USERNAME_LENGTH    50
#define JSON_PASSWORD_LENGTH    50

#define JSON_NAME_NAME          "Name"
#define JSON_USERNAME_NAME      "Username"
#define JSON_USERNAMEENTER_NAME "UsernameEnter"
#define JSON_PASSWORD_NAME      "Password"
#define JSON_PASSWORDENTER_NAME "PasswordEnter"
#define JSON_TABJOIN_NAME       "UnameTabPass"
#define JSON_LOADSEND_NAME      "LoadAndSend"

#define KEY_RETURN              (0xB0)
#define STRING_CR               "\n"
#define STRING_TAB              "\t"
#define STRING_USERNAME         "Username"
#define STRING_PASSWORD         "Password"
#define STRING_UNAMETABPASS     "User & Pass"
#define STRING_BUTTON1          "B1: "
#define STRING_BUTTON2          "B2: "
#define STRING_3DOT             "..."

// How long the loaded item will be available
#define PERIOD_TO_FORGET_SEC      (5 * 60)

typedef struct item_data_s
{
    unsigned char name[JSON_NAME_LENGTH + 1];
    unsigned char username[JSON_USERNAME_LENGTH + 1];
    unsigned char password[JSON_PASSWORD_LENGTH + 1];
    bool send_username_enter;
    bool send_password_enter;
    bool send_uname_tab_pass;
    bool send_immediately;
} item_data_t;

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
 * @brief Close all opened peripherals.
 */
static void
close_peripherals_and_handlers(void);

/**
 * @brief Button1 press handler
 */
static void
handle_button1_press(void);

/**
 * @brief Button2 press handler
 */
static void
handle_button2_press(void);

static void
send_string_to_usb_keyboard(const unsigned char* p_string);

/**
 * @brief Timer event handler for polling button states
 */
static void
event_handler_timer_button(EventData *event_data);

static void
setup_item_sender(void);

static void
show_standby_state(void);

/**
 * @brief Allocates and formats a string message on the heap.
 *
 * @param p_message_fmt The format of the message.
 * @param msg_length_max The maximum length of the formatted message string.
 *
 * @return The pointer to the heap allocated memory.
 */
static void
*setup_heap_message(const char *p_message_fmt, size_t msg_length_max, ...);

/**
 * @brief Direct Method callback function, called when a Direct Method call 
 * is received from the Azure IoT Hub.
 *
 * @param p_method_name The name of the method being called.
 * @param p_payload The payload of the method.
 * @param payload_size Direct method payload size.
 * @param pp_response_payload The response payload content. This must be 
 *    a heap-allocated string, 'free' will be called on this buffer 
 *    by the Azure IoT Hub SDK.
 * @param p_response_payload_size The size of the response payload content.
 *
 * @return 
 *    200 HTTP status code if the method name is reconginized and 
 *        the payload is correctly parsed;
 *    400 HTTP status code if the payload is invalid;
 *    404 HTTP status code if the method name is unknown.
 */
static int
cb_direct_method_call(const char* p_method_name,
    const char* p_payload, size_t payload_size,
    char** pp_response_payload, size_t* p_response_payload_size);

/*******************************************************************************
* Global variables
*******************************************************************************/

// Termination state flag
volatile sig_atomic_t gb_is_termination_requested = false;

static int g_fd_epoll = -1;        // Epoll file descriptor
static int g_fd_i2c = -1;          // I2C interface file descriptor
static int g_fd_gpio_button1 = -1; // GPIO button1 file descriptor
static int g_fd_gpio_button2 = -1; // GPIO button2 file descriptor
static int g_fd_poll_timer_button = -1;    // Poll timer button press file desc.

static GPIO_Value_Type g_state_button1 = GPIO_Value_High;
static GPIO_Value_Type g_state_button2 = GPIO_Value_High;

static EventData g_event_data_button = {          // Button Event data
    .eventHandler = &event_handler_timer_button
};

static u8g2_t g_u8g2;           // OLED device descriptor for u8g2

static item_data_t g_item_data;

static struct timespec g_time;
static long g_time_to_forget;

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

        strcpy(g_item_data.name, "Item Name");
        strcpy(g_item_data.username, "username\n");
        strcpy(g_item_data.password, "longpassword32characters12345678");
        g_item_data.send_uname_tab_pass = false;
        g_item_data.send_password_enter = false;
        g_item_data.send_username_enter = true;

        setup_item_sender();
        //show_standby_state();

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
            // - it is safe to call this function even if the client has already
            //   been set up, as in this case it would have no effect;
            // - a failure to setup the client is a fatal error.
            if (!AzureIoT_SetupClient()) {
                Log_Debug("ERROR: Failed to set up IoT Hub client\n");
                gb_is_termination_requested = true;
                break;
            }

            // AzureIoT_DoPeriodicTasks() needs to be called frequently in order
            // to keep active the flow of data with the Azure IoT Hub
            AzureIoT_DoPeriodicTasks();

            // Check if time to keep item loaded expired
            if (clock_gettime(CLOCK_REALTIME, &g_time) == -1) {
                Log_Debug("ERROR: clock_gettime failed with error code: %s (%d).\n",
                    strerror(errno), errno);
                gb_is_termination_requested = true;
            }
            else if ((g_time.tv_sec > g_time_to_forget) &&
                (strlen(g_item_data.password) > 0))
            {
                // Erase item
                strcpy(g_item_data.username, "");
                strcpy(g_item_data.password, "");

                //show_standby_state();
            }
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

    // Create SIGTERM signal handler
    struct sigaction term_action;
    memset(&term_action, 0, sizeof(struct sigaction));
    term_action.sa_handler = termination_handler;
    result = sigaction(SIGTERM, &term_action, NULL);
    if (result != 0)
    {
        Log_Debug("ERROR: %s - SIGTERM: errno=%d (%s)\n",
            __FUNCTION__, errno, strerror(errno));
    }

    // Create SIGABRT signal handler
    struct sigaction abort_action;
    memset(&abort_action, 0, sizeof(struct sigaction));
    abort_action.sa_handler = termination_handler;
    result = sigaction(SIGTERM, &abort_action, NULL);
    if (result != 0)
    {
        Log_Debug("ERROR: %s - SIGABRT: errno=%d (%s)\n",
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
    AzureIoT_SetDirectMethodCallback(&cb_direct_method_call);

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

    // Initialize development kit button GPIO
    // -- Open button2 GPIO as input
    if (result != -1)
    {
        g_fd_gpio_button2 = GPIO_OpenAsInput(PROJECT_BUTTON_2);
        if (g_fd_gpio_button2 < 0)
        {
            Log_Debug("ERROR: Could not open button GPIO: %s (%d).\n",
                strerror(errno), errno);
            result = -1;
        }
    }

    // Create timer for button press check poll
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

    // Close button2 GPIO fd
    CloseFdAndPrintError(g_fd_gpio_button2, "Button2 GPIO");
}

static void
handle_button1_press(void)
{
    char data[2];
    data[0] = 0x10;

    if (strlen(g_item_data.username) > 0)
    {
        send_string_to_usb_keyboard(g_item_data.username);
        if (g_item_data.send_username_enter)
        {
            //send_string_to_usb_keyboard(STRING_CR);
            I2CMaster_Write(g_fd_i2c, I2C_ADDR_USB_KEYBOARD, data, 1);
        }
        else if (g_item_data.send_uname_tab_pass)
        {
            send_string_to_usb_keyboard(STRING_TAB);
            if (strlen(g_item_data.password) > 0)
            {
                send_string_to_usb_keyboard(g_item_data.password);
                if (g_item_data.send_password_enter)
                {
                    send_string_to_usb_keyboard(STRING_CR);
                }
            }
        }
    }
}

static void
handle_button2_press(void)
{
    if (strlen(g_item_data.password) > 0)
    {
        send_string_to_usb_keyboard(g_item_data.password);
        if (g_item_data.send_password_enter)
        {
            send_string_to_usb_keyboard(STRING_CR STRING_CR);
        }
    }
}

static void 
send_string_to_usb_keyboard(const unsigned char *p_string)
{
    if ((p_string == NULL) || (strlen(p_string) == 0))
    {
        return;
    }

    const struct timespec sleep_time = { 0, 150 * 1000000 };    // 150 ms

    int string_length = (int)strlen(p_string);
    Log_Debug("String length %d.\n", string_length);
    int length_to_send;

    // Send string to I2c in 32-byte chunks since receiving Arduino's Wire
    // library has 32 byte buffer
    for (int i = 0; i < string_length; i += 32)
    {
        if ((string_length - i) > 32)
        {
            length_to_send = 32;
        }
        else
        {
            length_to_send = string_length - i;
        }

        Log_Debug("Sending %d chars.\n", length_to_send);

        if (I2CMaster_Write(g_fd_i2c, I2C_ADDR_USB_KEYBOARD, p_string + i, (size_t)length_to_send) == -1)
        {
            Log_Debug("ERROR Sending data to USB keyboard via I2C.\n");
        }

        // Delay before sending the next chunk
        nanosleep(&sleep_time, NULL);
    }

    return;
}

static void
event_handler_timer_button(EventData *event_data)
{
    bool b_is_all_ok = true;
    GPIO_Value_Type state_button1_current;
    GPIO_Value_Type state_button2_current;

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

    if (b_is_all_ok)
    {
        // Check for a button2 press
        if (GPIO_GetValue(g_fd_gpio_button2, &state_button2_current) != 0)
        {
            Log_Debug("ERROR: Could not read button GPIO: %s (%d).\n",
                strerror(errno), errno);
            gb_is_termination_requested = true;
            b_is_all_ok = false;
        }
        else if (state_button2_current != g_state_button2)
        {
            if (state_button2_current == GPIO_Value_Low)
            {
                handle_button2_press();
            }
            g_state_button2 = state_button2_current;
        }
    }

    return;
}

static void
show_standby_state(void)
{
    u8g2_ClearDisplay(&g_u8g2);
    u8g2_ClearBuffer(&g_u8g2);

    u8g2_SetFont(&g_u8g2, u8g2_font_t0_22b_tr);
    lib_u8g2_DrawCenteredStr(&g_u8g2, 42, "Ready");

    u8g2_SendBuffer(&g_u8g2);
}

static void
setup_item_sender(void)
{
    // Set time delay to forget
    if (clock_gettime(CLOCK_REALTIME, &g_time) == -1) {
        Log_Debug("ERROR: clock_gettime failed with error code: %s (%d).\n", 
            strerror(errno), errno);
        gb_is_termination_requested = true;
        return;
    }

    g_time_to_forget = g_time.tv_sec + PERIOD_TO_FORGET_SEC;

    // Setup display
    u8g2_ClearDisplay(&g_u8g2);

    u8g2_ClearBuffer(&g_u8g2);

    // Display name
    u8g2_SetFont(&g_u8g2, u8g2_font_crox4hb_tr);

    // If name overflows display width, "truncate" it with triple dot
    u8g2_uint_t w_display = u8g2_GetDisplayWidth(&g_u8g2);
    u8g2_uint_t w_string = u8g2_GetStrWidth(&g_u8g2, g_item_data.name);
    if (w_string <= w_display)
    {
        lib_u8g2_DrawCenteredStr(&g_u8g2, 26, g_item_data.name);
    }
    else
    {
        u8g2_DrawStr(&g_u8g2, 0, 26, g_item_data.name);
        u8g2_uint_t w_3dot = u8g2_GetStrWidth(&g_u8g2, STRING_3DOT);
        u8g2_SetDrawColor(&g_u8g2, 0);
        u8g2_DrawBox(&g_u8g2, (u8g2_uint_t)(w_display - w_3dot), 10, w_3dot, 20);
        u8g2_SetDrawColor(&g_u8g2, 1);
        u8g2_DrawStr(&g_u8g2, (u8g2_uint_t)(w_display - w_3dot), 26, STRING_3DOT);
    }

    // Display button description
    u8g2_SetFont(&g_u8g2, u8g2_font_ImpactBits_tr);
    
    if (g_item_data.send_uname_tab_pass)
    {
        lib_u8g2_DrawCenteredStr(&g_u8g2, 50, STRING_BUTTON1 STRING_UNAMETABPASS);
    }
    else
    {
        if (strlen(g_item_data.username) > 0)
        {
            lib_u8g2_DrawCenteredStr(&g_u8g2, 50, STRING_BUTTON1 STRING_USERNAME);
        }
        lib_u8g2_DrawCenteredStr(&g_u8g2, 64, STRING_BUTTON2 STRING_PASSWORD);
    }

    u8g2_SendBuffer(&g_u8g2);

    // If requested, send item data immediately to USB
    if (g_item_data.send_immediately)
    {
        if (strlen(g_item_data.username) > 0)
        {
            send_string_to_usb_keyboard(g_item_data.username);
            if (g_item_data.send_username_enter)
            {
                send_string_to_usb_keyboard(STRING_CR);
            }
            else if (g_item_data.send_uname_tab_pass)
            {
                send_string_to_usb_keyboard(STRING_TAB);
            }
        }

        if (strlen(g_item_data.password) > 0)
        {
            send_string_to_usb_keyboard(g_item_data.password);
            if (g_item_data.send_password_enter)
            {
                send_string_to_usb_keyboard(STRING_CR);
            }
        }
    }

    return;
}

static void 
*setup_heap_message(const char *p_message_fmt, size_t msg_length_max, ...)
{
    va_list args;
    va_start(args, msg_length_max);
    char *message = malloc(msg_length_max + 1); // +1 for the null terminator
    if (message != NULL) 
    {
        vsnprintf(message, msg_length_max, p_message_fmt, args);
    }
    va_end(args);
    return message;
}

static int 
cb_direct_method_call(const char *p_method_name, 
    const char *p_payload, size_t payload_size, 
    char **pp_response_payload, size_t *p_response_payload_size)
{
    Log_Debug("\nDirect Method called %s\n", p_method_name);

    int result = 404; // HTTP status code.

    if (payload_size < DIRECT_METHOD_CALL_PAYLOAD_MAX) 
    {
        // Declare a char buffer on the stack where we'll operate 
        // on a copy of the payload.
        char payload_string[payload_size + 1];

        if (payload_string == NULL)
        {
            // Not enough memory for local payload buffer
            Log_Debug("ERROR: Could not allocate buffer for direct method request payload.\n");
            abort();
        }

        // Prepare the payload for the response. This is a heap allocated null 
        // terminated string. The Azure IoT Hub SDK is responsible of freeing it.
        *pp_response_payload = NULL;  // Reponse payload content.
        *p_response_payload_size = 0; // Response payload content size.

        if (strcmp(p_method_name, "set_item_data") == 0) 
        {
            result = 200;

            // Copy the payload into our local buffer then null terminate it.
            memcpy(payload_string, p_payload, payload_size);
            payload_string[payload_size] = 0; // Null terminated string.

            JSON_Value *payload_json_value = json_parse_string(payload_string);

            // Verify we have a valid JSON string from the payload
            if (payload_json_value == NULL) 
            {
                goto payloadError;
            }

            // Verify that the payload_json_value contains a valid JSON object
            JSON_Object *payload_json_object = json_value_get_object(
                payload_json_value);
            if (payload_json_object == NULL) 
            {
                goto payloadError;
            }

            // Get item data from JSON object
            const char* p_value_string;
            int value_int;

            strcpy(g_item_data.name, "\0");
            p_value_string = json_object_get_string(payload_json_object, 
                JSON_NAME_NAME);
            if (p_value_string != NULL)
            {
                strncpy(g_item_data.name, p_value_string, JSON_NAME_LENGTH + 1);
            }

            strcpy(g_item_data.username, "\0");
            p_value_string = json_object_get_string(payload_json_object, 
                JSON_USERNAME_NAME);
            if (p_value_string != NULL)
            {
                strncpy(g_item_data.username, p_value_string, 
                    JSON_USERNAME_LENGTH + 1);
            }

            strcpy(g_item_data.password, "\0");
            p_value_string = json_object_get_string(payload_json_object, 
                JSON_PASSWORD_NAME);
            if (p_value_string != NULL)
            {
                strncpy(g_item_data.password, p_value_string, 
                    JSON_PASSWORD_LENGTH + 1);
            }

            g_item_data.send_username_enter = false;
            value_int = json_object_get_boolean(payload_json_object, JSON_USERNAMEENTER_NAME);
            if (value_int != -1)
            {
                g_item_data.send_username_enter = (bool)value_int;
            }

            g_item_data.send_password_enter = false;
            value_int = json_object_get_boolean(payload_json_object, JSON_PASSWORDENTER_NAME);
            if (value_int != -1)
            {
                g_item_data.send_password_enter = (bool)value_int;
            }

            g_item_data.send_uname_tab_pass = false;
            value_int = json_object_get_boolean(payload_json_object, JSON_TABJOIN_NAME);
            if (value_int != -1)
            {
                g_item_data.send_uname_tab_pass = (bool)value_int;
            }

            g_item_data.send_immediately = false;
            value_int = json_object_get_boolean(payload_json_object, JSON_LOADSEND_NAME);
            if (value_int != -1)
            {
                g_item_data.send_immediately = (bool)value_int;
            }

            if ((strlen(g_item_data.name) == 0) ||
                (strlen(g_item_data.password) == 0))
            {
                goto payloadError;
            }

            // Prepare item data to be sent to USB
            setup_item_sender();

            // Construct the response message.  This will be displayed in the cloud when calling the direct method
            static const char newPollTimeResponse[] =
                "{ \"success\" : true, \"message\" : \"'%s' loaded\" }";
            size_t responseMaxLength = sizeof(newPollTimeResponse) + strlen(g_item_data.name);
            *pp_response_payload = setup_heap_message(newPollTimeResponse, responseMaxLength, g_item_data.name);
            if (*pp_response_payload == NULL)
            {
                Log_Debug("ERROR: Could not allocate buffer for direct method response payload.\n");
                abort();
            }
            *p_response_payload_size = strlen(*pp_response_payload);

            return result;
        }
        else 
        {
            result = 404;
            Log_Debug("INFO: Direct Method called \"%s\" not found.\n", p_method_name);

            static const char noMethodFound[] = "\"method not found '%s'\"";
            size_t responseMaxLength = sizeof(noMethodFound) + strlen(p_method_name);
            *pp_response_payload = setup_heap_message(noMethodFound, responseMaxLength, p_method_name);
            if (*pp_response_payload == NULL) 
            {
                Log_Debug("ERROR: Could not allocate buffer for direct method response payload.\n");
                abort();
            }
            *p_response_payload_size = strlen(*pp_response_payload);
            return result;
        }

    }
    else 
    {
        Log_Debug("Payload size over limit, aborting Direct Method execution\n");
        goto payloadError;
    }

    // If there was a payload error, construct the 
    // response message and send it back to the IoT Hub for the user to see
payloadError:

    result = 400; // Bad request.
    Log_Debug("INFO: Unrecognised direct method payload format.\n");

    static const char noPayloadResponse[] =
        "{ \"success\" : false, \"message\" : \"Request does not contain an "
        "identifiable payload\" }";

    size_t responseMaxLength = sizeof(noPayloadResponse) + strlen(p_payload);
    responseMaxLength = sizeof(noPayloadResponse);
    *pp_response_payload = setup_heap_message(noPayloadResponse, responseMaxLength);
    if (*pp_response_payload == NULL) {
        Log_Debug("ERROR: Could not allocate buffer for direct method response payload.\n");
        abort();
    }
    *p_response_payload_size = strlen(*pp_response_payload);

    return result;
}

/* [] END OF FILE */
