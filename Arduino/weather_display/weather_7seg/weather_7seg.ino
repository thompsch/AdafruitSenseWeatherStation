

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
unsigned long brightMillis = -1;
unsigned long lastMillis = -1;
unsigned long tMillis = -1;
uint8_t brightness = 2;

/* HISTORY */
int logCount;
int logPointer;
#define maxLogCount 72 // every hour for 3 full days = 72
String weatherLog[maxLogCount];


void setup() {
#ifndef __AVR_ATtiny85__
  Serial.begin(9600);
  Serial.println("7 Segment Backpack Test");
#endif
  matrix.begin(0x70);
  matrix.setBrightness(brightness);  // BRIGHTNESS 0 -15

  matrix.writeDigitRaw(0, 64);
  matrix.writeDigitRaw(1, 118);
  matrix.writeDigitRaw(3, 48);
  matrix.writeDigitRaw(4, 64);
  matrix.writeDisplay(); 
  bmp280.begin();
  sht30.begin();
  Bluefruit.autoConnLed(true);
  Serial.begin(115200);

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
  if (millis() - lastMillis >= 60*60*1000) //collect history every 60 minutes
  {
    lastMillis = millis();
    getLatestData();
    WriteHistoryToBuffer();
  }

  while (bleuart.available())
  {
    uint8_t ch;
    ch = (uint8_t)bleuart.read();
    if (ch == 76 || ch == 108) // l or L
    {
      Serial.print("bright: ");
      Serial.println(brightness);
      if (brightness  == 2) {
        brightness = 10;
        matrix.setBrightness(brightness);
        brightMillis = millis();
      }
      else {
        brightness = 2;
        matrix.setBrightness(brightness);
      } 
    }
    if (ch == 84 || ch == 116)
    { //"t" or "T"
      sendTemperature();
    }
    else if (ch == 80 || ch == 112)
    { //"p" or "P"
      getLatestData();
      sendPressure();
    }
    else if (ch == 72 || ch == 104)
    { //"h" or "H"
      getLatestData();
      sendHumidity();
    }
    else if (ch == 66 || ch == 98)
    { //"b" or "B"
      sendBattery();
    }
    if (ch == 65 || ch == 97) //"a" or "A"
    {
      getLatestData();
      sendTemperature();
      sendPressure();
      sendHumidity();
      sendBattery();
    }
    if (ch == 88 || ch == 120) //"x" or "X"
    {
      for (int x = logPointer; x < maxLogCount + logPointer; x++)
      {
        String strs[3];
        int StringCount = 0;
        String str = weatherLog[x % maxLogCount];

        while (str.length() > 0)
        {
          int index = str.indexOf('X');
          if (index == -1) // No X found (probably the last substring)
          {
            strs[StringCount++] = "X" + str;
            break;
          }
          else
          {
            strs[StringCount++] = "X" + str.substring(0, index);
            str = str.substring(index + 1);
          }
        }
        for (int i = 0; i < StringCount; i++)
        {
          bleuart.print(strs[i]);
          Serial.print(strs[i]);
        }
      }
    }
  }
  if (millis() - tMillis >= 13*1000) 
  {
  HandleDisplay();
  }
  if (millis() - brightMillis >= 10*1000 && brightness == 10) {
        brightMillis = millis();
        brightness = 2;
        matrix.setBrightness(brightness);
  }
}
void getLatestData()
{
  temperature = bmp280.readTemperature();
  pressure = bmp280.readPressure();
  humidity = sht30.readHumidity();
}

void sendTemperature() {
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

void sendBattery(){
  float measuredvbat = analogRead(VBATPIN);
  measuredvbat *= 2;
  measuredvbat *= 3.6;  // Multiply by 3.6V, our reference voltage
  measuredvbat /= 1024; // convert to voltage
  char bBuf[9];
  dtostrf(measuredvbat, 4, 2, bBuf);
  char charBuf[6];
  strcpy(charBuf, "B");
  strcpy(charBuf + 1, bBuf);
  bleuart.write(charBuf);
  Serial.println(charBuf);
}

void HandleDisplay(){
  getLatestData();
  tMillis = millis();
  float fTemp = (temperature*9/5)+32;
  matrix.print(fTemp,2);
  matrix.writeDigitRaw(4, 0);
  matrix.writeDisplay(); 
  delay(3000);
  matrix.clear();
}

void WriteHistoryToBuffer()
{
  unsigned long mil = millis();
  String bloop = String(mil) + "+T" + String(temperature);
  bloop += "X" + String(mil) + "+H" + String(humidity);
  bloop += "X" + String(mil) + "+P" + String(pressure);
  weatherLog[logCount % maxLogCount] = bloop;
  logCount++;
  logPointer = logCount % maxLogCount;
}

char *dtostrf (double val, signed char width, unsigned char prec, char *sout) {
  char fmt[20];
  sprintf(fmt, "%%%d.%df", width, prec);
  sprintf(sout, fmt, val);
  return sout;
}
