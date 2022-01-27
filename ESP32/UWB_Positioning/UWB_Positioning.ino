/**
 * Ultra WideBand Real-Time Positioning System (UWBRTLS)
 * 
 * Note: There are a lot of useful application notes provided by Decawave available at
 *  https://www.decawave.com/application-notes/
 * e.g. APS017 Maximising Range: https://www.decawave.com/wp-content/uploads/2018/10/APS017_Max-Range-in-DW1000-Systems_v1.1.pdf
 */

// DEFINES
// Is this device acting as a (static) anchor, or a (mobile) tag?
#define IS_TAG
// #define IS_ANCHOR

// INCLUDES
// SPI interface required for DW1000 communication
#include <SPI.h>
// DW-1000 specific library. See https://github.com/playfultechnology/arduino-dw1000
#include <DW1000Ranging.h>
// For displaying output on OLED display. See https://github.com/lexus2k/lcdgfx
#include "lcdgfx.h"
// The tag will connect to Wi-Fi to stream its distance readings to server as JSON using UDP protocol
#ifdef IS_TAG
  #include <WiFi.h>
  #include <WiFiUdp.h>
  #include "link.h"
  #include <ArduinoJson.h>
#endif

// CONSTANTS
// Every UWB device must have a unique EUI
// I'm using x2:xx:xx to define a locally-administered address suitable for testing.
// See: https://en.wikipedia.org/wiki/MAC_address#Universal_vs._local
#define DEVICE_ADDRESS "02:00:00:00:00:00:00:02"

// CONSTANTS
#ifdef IS_TAG
  // The tag will update a server with its location information
  // allowing it to be remotely tracked
  // Wi-Fi credentials 
  const char *ssid = "INSERT SSID HERE";
  const char *password = "INSERT WIFI PASSWORD HERE";
  // IP address of server to send location information to
  const char *host = "192.168.0.155";
  uint16_t portNum = 50000;
#endif

// GLOBALS
// If using an OLED display, use the constructor below
// The (-1) constructor uses default I2C pins, suitable for most platforms by default
DisplaySSD1306_128x64_I2C display(-1);
#ifdef IS_TAG
  // Reference to the WiFiUDP interface
  WiFiUDP udp;
  // Linked list of known anchors
  struct MyLink *uwb_data;
  // Timestamp at which updated data was last broadcast
  unsigned long lastUpdateTime = 0;
  // Time interval (in ms) between updates
  unsigned int updateInterval = 200;
#endif
// We'll use a "short address" to make it easier to reference devices
char shortAddress[6];

void setup() {

  // Initialise serial connection for debugging  
  Serial.begin(115200);
  Serial.println(__FILE__ __DATE__);

  // Initialise SPI interface on specified SCK, MISO, MOSI pins
  SPI.begin(18, 19, 23);
  // Start up DW1000 chip on specified RESET, CS, and IRQ pins
  DW1000Ranging.initCommunication(27, 4, 34);
  // Assign callback handlers...
  // ...when distance to a known tag changes
  DW1000Ranging.attachNewRange(newRange);
  // ...when new device found on network
  DW1000Ranging.attachNewDevice(newDevice);
  // ...when previously known device has been declared inactive and removed from network
  DW1000Ranging.attachInactiveDevice(inactiveDevice);

  // Initialise the OLED display
  display.setFixedFont(ssd1306xled_font6x8);
  display.begin();
  display.clear();

  #ifdef IS_ANCHOR
    // Start the DW-1000 as an anchor specifying pre-configured mode of operation
    // to prioritise accuracy/range/low power usage etc. Modes available are:
    // - MODE_LONGDATA_RANGE_LOWPOWER (110kb/s data rate, 16 MHz PRF and long preambles)
    // - MODE_SHORTDATA_FAST_LOWPOWER (6.8Mb/s data rate, 16 MHz PRF and short preambles)
    // - MODE_LONGDATA_FAST_LOWPOWER (6.8Mb/s data rate, 16 MHz PRF and long preambles)
    // - MODE_SHORTDATA_FAST_ACCURACY (6.8Mb/s data rate, 64 MHz PRF and short preambles)
    // - MODE_LONGDATA_FAST_ACCURACY (6.8Mb/s data rate, 64 MHz PRF and long preambles)
    // - MODE_LONGDATA_RANGE_ACCURACY (110kb/s data rate, 64 MHz PRF and long preambles)
    DW1000Ranging.startAsAnchor(DEVICE_ADDRESS, DW1000.MODE_LONGDATA_RANGE_ACCURACY, false);
    // Update the display
    display.printFixed(0, 0, "ANCHOR", STYLE_NORMAL); 
    
  #else if defined(IS_TAG)
    // Start the DW-1000 as a tag (using the same mode as the anchors)
    DW1000Ranging.startAsTag(DEVICE_ADDRESS, DW1000.MODE_LONGDATA_RANGE_ACCURACY, false);
    // Update the display
    display.printFixed(0, 0, "TAG", STYLE_NORMAL);
    // Initialise the array to keep track of links to all anchors 
    uwb_data = init_link();
    // Start a Wi-Fi connection to update host with tag's location
    WiFi.mode(WIFI_STA);
    WiFi.setSleep(false);
    WiFi.begin(ssid, password);
    while (WiFi.status() != WL_CONNECTED) {
      delay(500);
      Serial.print(".");
    }
    Serial.println(F("Connected"));
    Serial.print(F("IP Address:"));
    Serial.println(WiFi.localIP());
    // Short pause before starting main loop
    delay(500);
    // Start the UDP interface
    udp.begin(50000);
  #endif

  // For debugging, let's print the address of this device
  Serial.println(DEVICE_ADDRESS);
  display.printFixed(0, 8, DEVICE_ADDRESS, STYLE_NORMAL);

  // Let's calculate a "short address" from the last 2 bytes of the device address
  byte* currentShortAddress = DW1000Ranging.getCurrentShortAddress();
  sprintf(shortAddress, "%02X%02X", currentShortAddress[1], currentShortAddress[0]);
  Serial.print(F("Short Address: "));
  Serial.println(shortAddress);
  display.printFixed(0, 16, shortAddress, STYLE_NORMAL);

  Serial.println("Setup complete");
}

void loop() {
  // This needs to be called on every iteration of the main program loop
  DW1000Ranging.loop();

  #ifdef IS_TAG
    if((millis() - lastUpdateTime) > updateInterval){
      // Create the JSON document describing the array of links
      send_json(uwb_data);
      // Update the timestamp
      lastUpdateTime = millis();
    }
  #endif
}

#ifdef IS_TAG
void send_json(struct MyLink *p) {

  // Allocate a temporary JsonDocument
  // Use https://arduinojson.org/v6/assistant to compute the capacity.
  StaticJsonDocument<500> doc;

  // Use the devices's short address as the root JSON element
  doc["id"] = shortAddress;
  
  // Create the array of links
  JsonArray links = doc.createNestedArray("links");
  struct MyLink *temp = p;
  while (temp->next != NULL) {
    temp = temp->next;
    JsonObject obj1 = links.createNestedObject();
    obj1["a"] = temp->anchor_addr;
    char range[5];
    sprintf(range, "%.2f", temp->range[0]);
    obj1["r"] = range;
  }
  // Send JSON to serial connection
  serializeJson(doc, Serial);
  Serial.println("");

  // Send JSON over UDP
  udp.beginPacket(host, portNum);
  serializeJson(doc, udp);
  udp.println();
  udp.endPacket();
}
#endif

// CALLBACK HANDLERS
void newRange() {
  /*
  // Display on serial monitor
  Serial.print(F("From:"));
  Serial.print(DW1000Ranging.getDistantDevice()->getShortAddress(), HEX);
  Serial.print(F(", Range:"));
  Serial.print(DW1000Ranging.getDistantDevice()->getRange());
  Serial.print(F("m"));
  */
  // Display on OLED
  char buffer[21];
  //display.clear();
  snprintf(buffer, sizeof buffer, "%04x", DW1000Ranging.getDistantDevice()->getShortAddress());
  display.printFixed(0, 16+(int)(DW1000Ranging.getDistantDevice()->getShortAddress())*8, buffer, STYLE_NORMAL);
  int ret = snprintf(buffer, sizeof buffer, "%.2f", DW1000Ranging.getDistantDevice()->getRange());
  display.printFixed(32, 16+(int)(DW1000Ranging.getDistantDevice()->getShortAddress())*8, buffer, STYLE_NORMAL);

  // Update links
  #ifdef IS_TAG
    update_link(uwb_data, DW1000Ranging.getDistantDevice()->getShortAddress(), DW1000Ranging.getDistantDevice()->getRange(), DW1000Ranging.getDistantDevice()->getRXPower());
  #endif
}

void newDevice(DW1000Device *device) {
  // Serial.print(F("New device detected! "));
  // Serial.println(device->getShortAddress(), HEX);
  #ifdef IS_TAG 
    add_link(uwb_data, device->getShortAddress());
  #endif
}

void inactiveDevice(DW1000Device *device) {
  // Serial.print(F("Device removed: "));
  // Serial.println(device->getShortAddress(), HEX);
  #ifdef IS_TAG 
    delete_link(uwb_data, device->getShortAddress());
  #endif
}
