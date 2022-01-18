// DEFINES
#define IS_TAG

// INCLUDES
#include <SPI.h>
#include <DW1000Ranging.h>
#include "lcdgfx.h"
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
#define TAG_ADDR "02:00:00:00:00:00:56:78"

// CONSTANTS
#ifdef IS_TAG
  // The tag will update a server with its location information
  // allowing it to be remotely tracked
  // Wi-Fi credentials 
  const char *ssid = "Hyrule";
  const char *password = "molly1869";
  // IP address of server to send location information to
  const char *host = "192.168.0.121";
  uint16_t portNum = 50000;
#endif

// GLOBALS
#ifdef IS_TAG
  WiFiUDP udp;
#endif
DisplaySSD1306_128x64_I2C display(-1); // This line is suitable for most platforms by default
#ifdef IS_TAG
  struct MyLink *uwb_data;
  // Timestamp at which updated data was last broadcast
  unsigned long lastUpdateTime = 0;
  // Time interval (in ms) between updates
  unsigned int updateInterval = 1000;
  String all_json = "";
#endif

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
  // ...new device found on network
  DW1000Ranging.attachNewDevice(newDevice);
  // ...previously known device has been declared inactive and removed from network
  DW1000Ranging.attachInactiveDevice(inactiveDevice);
  Serial.println("DW started");
  delay(1000);

  // Initialise the OLED display
  display.setFixedFont( ssd1306xled_font6x8 );
  display.begin();
  display.clear();
  Serial.println("Display started");
  delay(1000);

  #ifdef IS_ANCHOR
    // Start the DW-1000 as an anchor specifying pre-configured mode of operation
    // to prioritise accuracy/range/low power usage etc. Modes available are:
    // - MODE_LONGDATA_RANGE_LOWPOWER (110kb/s data rate, 16 MHz PRF and long preambles)
    // - MODE_SHORTDATA_FAST_LOWPOWER (6.8Mb/s data rate, 16 MHz PRF and short preambles)
    // - MODE_LONGDATA_FAST_LOWPOWER (6.8Mb/s data rate, 16 MHz PRF and long preambles)
    // - MODE_SHORTDATA_FAST_ACCURACY (6.8Mb/s data rate, 64 MHz PRF and short preambles)
    // - MODE_LONGDATA_FAST_ACCURACY (6.8Mb/s data rate, 64 MHz PRF and long preambles)
    // - MODE_LONGDATA_RANGE_ACCURACY (110kb/s data rate, 64 MHz PRF and long preambles)
    DW1000Ranging.startAsAnchor(TAG_ADDR, DW1000.MODE_LONGDATA_RANGE_ACCURACY, false);
    // Update the display
    display.printFixed(0, 0, "ANCHOR", STYLE_NORMAL); 
    
  #else if defined(IS_TAG)
    // Start the DW-1000 as a tag (using the same mode as the anchors)
    DW1000Ranging.startAsTag(TAG_ADDR, DW1000.MODE_LONGDATA_RANGE_ACCURACY, false);
    // Update the display
    display.printFixed(0, 0, "TAG", STYLE_NORMAL);
    // Initialise the array to keeps track of links to all anchors 
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
    udp.begin(50000);
  #endif

  Serial.print(TAG_ADDR);
  display.printFixed(0, 8, TAG_ADDR, STYLE_NORMAL);

  byte* currentShortAddress = DW1000Ranging.getCurrentShortAddress();
  char string[6];
  sprintf(string, "%02X%02X", currentShortAddress[1], currentShortAddress[0]);
  Serial.print(F("Short Address: "));
  Serial.println(string);
  display.printFixed(0, 16, string, STYLE_NORMAL);

/*
  char shortAddress[4];
  // We fill it with the char array under the form of "AA:FF:1C:...."
  for(uint16_t i = LEN_EUI-4; i < LEN_EUI; i++) {
    shortAddress[i] = (nibbleFromChar(string[i*3]) << 4)+nibbleFromChar(string[i*3+1]);
  }
*/

  Serial.println("Setup complete");
}

void loop() {
  // This needs to be called on every iteration of the main program loop
  DW1000Ranging.loop();

  #ifdef IS_TAG
    if((millis() - lastUpdateTime) > updateInterval){
      // Create the JSON string describing all links
        make_link_json(uwb_data, &all_json);
      uint8_t buffer[50];
      int count = all_json.length();
      all_json.getBytes(buffer, count+1);
      // Initialise UDP and transfer buffer
      udp.beginPacket(host, portNum);
      udp.write(buffer, count+1);
      udp.endPacket();

        
       prepare_json(uwb_data);
      
      lastUpdateTime = millis();
    }
  #endif
}

#ifdef IS_TAG
void prepare_json(struct MyLink *p) {

  // Allocate a temporary JsonDocument
  // Use https://arduinojson.org/v6/assistant to compute the capacity.
  StaticJsonDocument<500> doc;
  
  byte* currentShortAddress = DW1000Ranging.getCurrentShortAddress();
  char string[6];
  sprintf(string, "%02X%02X", currentShortAddress[1], currentShortAddress[0]);
  doc["id"] = string;
  // Create the "analog" array
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
  serializeJson(doc, Serial);
  Serial.println("");

  //char  ReplyBuffer[] = "acknowledged";

  udp.beginPacket(host, portNum);
  serializeJson(doc, udp);
  udp.println();
  // uint8_t buffer[500];
  // serializeJson(doc, buffer);
  // udp.write(buffer, 500);
  udp.endPacket();
}
#endif

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
  display.printFixed(0, 0, buffer, STYLE_NORMAL);
  int ret = snprintf(buffer, sizeof buffer, "%.2f", DW1000Ranging.getDistantDevice()->getRange());
  display.printFixed(32, 0, buffer, STYLE_NORMAL);

  // Update links
  #ifdef IS_TAG
    update_link(uwb_data, DW1000Ranging.getDistantDevice()->getShortAddress(), DW1000Ranging.getDistantDevice()->getRange(), DW1000Ranging.getDistantDevice()->getRXPower());
  #endif
}

void newDevice(DW1000Device *device) {
  // Serial.print(F("New device found! "));
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
