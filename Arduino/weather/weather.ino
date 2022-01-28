#include <bluefruit.h>
#include <Adafruit_LittleFS.h>
#include <InternalFileSystem.h>
#include <Adafruit_APDS9960.h>
#include <Adafruit_BMP280.h>
#include <Adafruit_LIS3MDL.h>
#include <Adafruit_LSM6DS33.h>
#include <Adafruit_SHT31.h>
#include <Adafruit_Sensor.h>
#include <PDM.h>
#include <stdlib.h>
#include <SPI.h>
#include <SdFat.h>
#include <Adafruit_SPIFlash.h>

// BLE Service
BLEDfu bledfu;   // OTA DFU service
BLEDis bledis;   // device information
BLEUart bleuart; // uart over ble
BLEBas blebas;   // battery

Adafruit_APDS9960 apds9960; // proximity, light, color, gesture
Adafruit_BMP280 bmp280;     // temperautre, barometric pressure
Adafruit_LIS3MDL lis3mdl;   // magnetometer
Adafruit_LSM6DS33 lsm6ds33; // accelerometer, gyroscope
Adafruit_SHT31 sht30;       // humidity
#define VBATPIN A6          //battery pin == A6

uint8_t proximity;
uint16_t r, g, b, c;
float temperature, pressure, altitude;
float magnetic_x, magnetic_y, magnetic_z;
float accel_x, accel_y, accel_z;
float gyro_x, gyro_y, gyro_z;
float humidity;
int32_t mic;

float sensors[] = {temperature, pressure, humidity, (float)c};
unsigned long lastMillis = -1;

/* HISTORY */
int logCount;
int logPointer;
#define maxLogCount 72 // every hour for 3 full days = 72
String weatherLog[maxLogCount];

// *** START SETUP***********************************************************
void setup()
{
  // initialize the sensors
  apds9960.begin();
  apds9960.enableProximity(true);
  apds9960.enableColor(true);
  bmp280.begin();
  lis3mdl.begin_I2C();
  lsm6ds33.begin_I2C();
  sht30.begin();
  Serial.begin(115200);

  Serial.println("Bluefruit52 BLEUART Example");
  Serial.println("---------------------------\n");

  Bluefruit.autoConnLed(true);

  // Config the peripheral connection with maximum bandwidth
  // more SRAM required by SoftDevice
  // Note: All config***() function must be called before begin()
  Bluefruit.configPrphBandwidth(BANDWIDTH_MAX);

  Bluefruit.begin();
  Bluefruit.setTxPower(4); // Check bluefruit.h for supported values
  //Bluefruit.setName(getMcuUniqueID()); // useful testing fconwith multiple central connections
  Bluefruit.Periph.setConnectCallback(connect_callback);
  Bluefruit.Periph.setDisconnectCallback(disconnect_callback);

  // To be consistent OTA DFU should be added first if it exists
  bledfu.begin();

  // Configure and Start Device Information Service
  bledis.setManufacturer("Adafruit Industries");
  bledis.setModel("Bluefruit Feather52");
  bledis.begin();

  // Configure and Start BLE Uart Service
  bleuart.begin();

  // Start BLE Battery Service
  blebas.begin();
  blebas.write(100);

  logCount = 0;
  logPointer = 0;
  getLatestData();
  WriteToBuffer();

  // Set up and start advertising
  startAdv();
}
// *** END SETUP***********************************************************

void startAdv(void)
{
  // Advertising packet
  Bluefruit.Advertising.addFlags(BLE_GAP_ADV_FLAGS_LE_ONLY_GENERAL_DISC_MODE);
  Bluefruit.Advertising.addTxPower();

  // Include bleuart 128-bit uuid
  Bluefruit.Advertising.addService(bleuart);

  // Secondary Scan Response packet (optional)
  // Since there is no room for 'Name' in Advertising packet
  Bluefruit.ScanResponse.addName();

  /* Start Advertising
   * - Enable auto advertising if disconnected
   * - Interval:  fast mode = 20 ms, slow mode = 152.5 ms
   * - Timeout for fast mode is 30 seconds
   * - Start(timeout) with timeout = 0 will advertise forever (until connected)
   * 
   * For recommended advertising interval
   * https://developer.apple.com/library/content/qa/qa1931/_index.html   
   */
  Bluefruit.Advertising.restartOnDisconnect(true);
  Bluefruit.Advertising.setInterval(32, 244); // in unit of 0.625 ms
  Bluefruit.Advertising.setFastTimeout(30);   // number of seconds in fast mode
  Bluefruit.Advertising.start(0);             // 0 = Don't stop advertising after n seconds
}

// *** START LOOP ***********************************************************
void loop()
{
  if (millis() - lastMillis >= 60 * 60 * 1000)
  {
    lastMillis = millis();
    getLatestData();
    WriteToBuffer();
  }

  while (bleuart.available())
  {
    uint8_t ch;
    ch = (uint8_t)bleuart.read();
    // **************************** SEND ALL RECENT DATA *****************************    
    if (ch == 65 || ch == 97) //"a" or "A"
    {
      getLatestData();
      sendTemperature();
      sendPressure();
      sendHumidity();
      sendBattery();
    }
    // ************************ SEND HISTORY **********************************
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
    // ************************ SEND TEMPERATURE **********************************
    else if (ch == 116 || ch == 84)
    { //"t" or "T"
      getLatestData();
      sendTemperature();
    }
    // ************************ SEND PRESSURE **********************************
    else if (ch == 112 || ch == 80)
    { //"p" or "P"
      getLatestData();
      sendPressure();
    }
    // ************************ SEND HUMIDITY **********************************
    else if (ch == 104 || ch == 72)
    { //"h" or "H"
      getLatestData();
      sendHumidity();
    }
    // ************************ SEND BATTERY **********************************
    else if (ch == 66 || ch == 98)
    { //"b" or "B"
      sendBattery();
    }
    // ************************ GET LUX **********************************
    else if (ch == 76 || ch == 108) //"l" or "L"
    {
      apds9960.getColorData(&r, &g, &b, &c);
      char lBuf[9];
      dtostrf(c, 6, 0, lBuf);
      char charBuf[15];
      strcpy(charBuf, "L");
      strcpy(charBuf + 1, lBuf);
      bleuart.write(charBuf);
      Serial.println(charBuf);
    }
    else
    {
      Serial.write(ch);
    }
  }
}
// *** END LOOP ***********************************************************

void sendTemperature(){
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
void WriteToBuffer()
{
  unsigned long mil = millis();
  String bloop = String(mil) + "+T" + String(temperature);
  bloop += "X" + String(mil) + "+H" + String(humidity);
  bloop += "X" + String(mil) + "+P" + String(pressure);
  weatherLog[logCount % maxLogCount] = bloop;
  logCount++;
  logPointer = logCount % maxLogCount;
}

void getLatestData()
{
  Serial.println("Getting latest sensor data");
  temperature = bmp280.readTemperature();
  pressure = bmp280.readPressure();
  humidity = sht30.readHumidity();

  while (!apds9960.colorDataReady())
  {
    delay(5);
  }
  apds9960.getColorData(&r, &g, &b, &c);
}

// callback invoked when central connects
void connect_callback(uint16_t conn_handle)
{
  // Get the reference to current connection
  BLEConnection *connection = Bluefruit.Connection(conn_handle);

  char central_name[32] = {0};
  connection->getPeerName(central_name, sizeof(central_name));

  Serial.print("Connected to ");
  Serial.println(central_name);
}

/**
 * Callback invoked when a connection is dropped
 * @param conn_handle connection where this event happens
 * @param reason is a BLE_HCI_STATUS_CODE which can be found in ble_hci.h
 */
void disconnect_callback(uint16_t conn_handle, uint8_t reason)
{
  (void)conn_handle;
  (void)reason;

  Serial.println();
  Serial.print("Disconnected, reason = 0x");
  Serial.println(reason, HEX);
}

char *dtostrf(double val, signed char width, unsigned char prec, char *sout)
{
  char fmt[20];
  sprintf(fmt, "%%%d.%df", width, prec);
  sprintf(sout, fmt, val);
  return sout;
}
