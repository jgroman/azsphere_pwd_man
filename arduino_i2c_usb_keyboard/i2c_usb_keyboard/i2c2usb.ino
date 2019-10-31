// I2C to USB bridge

// This sketch will make Arduino Pro Micro act as USB keyboard and I2C slave device.
// Any bytes received from I2C will be transferred to USB keyboard as emulated keypresses

// I2C Pins on Leonardo board: SDA - 2, SCL - 3
// Notice: Maximum I2C transfer size is limited by the Wire library 32 byte buffer.

#include "Keyboard.h"
#include "Wire.h"

#define I2C_SLAVE_ADDRESS 0x08
#define PIN_RXLED 17

// Uncomment following line to enable serial debugging
#define DEBUG_ENABLED

void 
setup() 
{
  // Initialize USB keyboard
  Keyboard.begin();

  // Initialize I2C slave
  Wire.begin(I2C_SLAVE_ADDRESS);
  Wire.onReceive(evnt_receive);

  // Initialize Rx LED pin as output
  pinMode(PIN_RXLED, OUTPUT);
  
# ifdef DEBUG_ENABLED
  // Initialize serial
  Serial.begin(9600);
# endif
}

void 
loop() 
{
  delay(100);
}

void 
evnt_receive(int bytes_received)
{
  char received_byte;
  while (0 < Wire.available())
  {
    // Switch on Rx LED
    digitalWrite(PIN_RXLED, HIGH);
    
    // Read byte from I2C
    received_byte = Wire.read();

    // Send byte as keypress
    Keyboard.write(received_byte);
    
#   ifdef DEBUG_ENABLED
    // Print received byte on serial
    Serial.print(received_byte);
#   endif
  }

  delay(100);
  // Switch off Rx LED
  digitalWrite(PIN_RXLED, LOW);
}
