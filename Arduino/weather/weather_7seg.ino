

#include <Wire.h> // Enable this line if using Arduino Uno, Mega, etc.
#include <Adafruit_GFX.h>
#include "Adafruit_LEDBackpack.h"
#include <bluefruit.h>
#include <Adafruit_BMP280.h>
#include <Adafruit_SHT31.h>

Adafruit_7segment matrix = Adafruit_7segment();
// BLE Service
BLEDfu bledfu;   // OTA DFU service
BLEDis bledis;   // device information
BLEUart bleuart; // uart over ble
BLEBas blebas;   // battery
Adafruit_BMP280 bmp280; 
Adafruit_SHT31 sht30;       // humidity
#define VBATPIN A6   

uint8_t proximity;
uint16_t r, g, b, c;
float temperature, pressure, altitude;
float humidity;
float sensors[] = {temperature, pressure, humidity, (float)c};
unsigned long lastMillis = -1;
unsigned long tMillis = -1;
unsigned long pMillis = -1;
unsigned long hMillis = -1;

/* HISTORY */
int logCount;
int logPointer;
#define maxLogCount 144 // every 1/2hour for 3 full days = 144
String weatherLog[maxLogCount];


void setup() {
#ifndef __AVR_ATtiny85__
  Serial.begin(9600);
  Serial.println("7 Segment Backpack Test");
#endif
  matrix.begin(0x70);
  matrix.setBrightness(3);  // BRIGHTNESS 0 -15

  matrix.writeDigitRaw(0, 64);
  matrix.writeDigitRaw(1, 118);
  matrix.writeDigitRaw(3, 48);
  matrix.writeDigitRaw(4, 64);
  matrix.writeDisplay(); 
  bmp280.begin();
  sht30.begin();
  Bluefruit.autoConnLed(true);
  Serial.begin(115200);

  Serial.println("Bluefruit52 BLEUART Example");
  Serial.println("---------------------------\n");

  Bluefruit.autoConnLed(true);
  Bluefruit.configPrphBandwidth(BANDWIDTH_MAX);

  Bluefruit.begin();
  Bluefruit.setTxPower(4); 
  bledfu.begin();
  bledis.setManufacturer("Adafruit Industries");
  bledis.setModel("Bluefruit Feather52");
  bledis.begin();
  bleuart.begin();
  blebas.begin();
  blebas.write(100);

  logCount = 0;
  logPointer = 0;
  
  // Set up and start advertising
  startAdv();
  getLatestData();
  

}

void startAdv(void)
{
  Bluefruit.Advertising.addFlags(BLE_GAP_ADV_FLAGS_LE_ONLY_GENERAL_DISC_MODE);
  Bluefruit.Advertising.addTxPower();
  Bluefruit.Advertising.addService(bleuart);
  Bluefruit.ScanResponse.addName();
  Bluefruit.Advertising.restartOnDisconnect(true);
  Bluefruit.Advertising.setInterval(32, 244); // in unit of 0.625 ms
  Bluefruit.Advertising.setFastTimeout(30);   // number of seconds in fast mode
  Bluefruit.Advertising.start(0);             // 0 = Don't stop advertising after n seconds
}


void loop() {

  // print a hex number
  //matrix.print(0xCAEB, HEX);
  //matrix.writeDisplay();
  while (bleuart.available())
  {
    uint8_t ch;
    ch = (uint8_t)bleuart.read();
    if (ch == 84 || ch == 116)
    { //"t" or "T"
      sendTemperature();
    }
    else if (ch == 80 || ch == 112)
    { //"p" or "P"
      getLatestData();
      sendPressure();
    }
    // ************************ SEND HUMIDITY **********************************
    else if (ch == 72 || ch == 104)
    { //"h" or "H"
      getLatestData();
      sendHumidity();
    }

  }

  HandleDisplay();
}
void getLatestData()
{
  Serial.println("Getting latest sensor data");
  temperature = bmp280.readTemperature();
  pressure = bmp280.readPressure();
  humidity = sht30.readHumidity();
}

void sendTemperature() {
  temperature = bmp280.readTemperature();
  char tBuf[9];
  dtostrf(temperature, 4, 2, tBuf);
  char charBuf[15];
  strcpy(charBuf, "T");
  strcpy(charBuf + 1, tBuf);
  bleuart.write(charBuf);
  Serial.println(charBuf);
}
void sendPressure(){
  char pBuf[9];
  dtostrf(pressure, 4, 2, pBuf);
  char charBuf[15];
  strcpy(charBuf, "P");
  strcpy(charBuf + 1, pBuf);
  bleuart.write(charBuf);
  Serial.println(charBuf);
}

void sendHumidity(){
  char hBuf[9];
  dtostrf(humidity, 4, 2, hBuf);
  char charBuf[15];
  strcpy(charBuf, "H");
  strcpy(charBuf + 1, hBuf);
  bleuart.write(charBuf);
  Serial.println(charBuf);
}

void HandleDisplay(){
  
  if (millis() - tMillis >= 8*1000) 
  {
    tMillis = millis();
    pMillis = tMillis + 4000;
    hMillis = pMillis + 6000;
    /*Serial.print(tMillis);
    Serial.print(" - ");
    Serial.print(pMillis);
    Serial.print(" - ");
    Serial.println(hMillis);
    Serial.print(" :: ");*/
    Serial.println(millis());
    
    bmp280.readTemperature();
    float fTemp = (temperature*9/5)+32;
    matrix.print(fTemp,2);
    matrix.writeDigitRaw(4, 113);
    matrix.writeDisplay(); 
  }
  if (millis() >= pMillis){
    matrix.print(pressure/1000,1);
    matrix.writeDisplay();
  }
  if (millis() >= hMillis) {
    matrix.print((int)humidity);
    matrix.writeDigitRaw(0, 116);
    matrix.writeDisplay();
  }
}

char *dtostrf (double val, signed char width, unsigned char prec, char *sout) {
  char fmt[20];
  sprintf(fmt, "%%%d.%df", width, prec);
  sprintf(sout, fmt, val);
  return sout;
}
