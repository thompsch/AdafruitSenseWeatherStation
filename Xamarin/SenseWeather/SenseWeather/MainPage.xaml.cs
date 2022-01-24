using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.Exceptions;
using Xamarin.Forms;

namespace SenseWeather
{
    public partial class MainPage : ContentPage
    {
        private IDevice _device;
        private IService weatherService;
        private ICharacteristic charReceive;
        private ICharacteristic charSend;

        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            try
            {
                await GetStarted();
                base.OnAppearing();
            }
            catch (Exception e)
            {
                DisplayAlert("ERROR", e.ToString(), "ugh");
            }
        }


        private async Task GetStarted()
        {
            if (!BleDevice.IsBluetoothConnected())
            {
                await DisplayAlert("Bluetooth Disabled", "Dude, turn on your Bluetooth!", "Oops.");
                return;
            }

            var result = (await BleDevice.GetWeatherStationDevice());
            if (result.StartsWith("ERROR"))
            {
                await DisplayAlert("ERROR", "Problem connecting to the device.", result);
            }
            else
            {
                Debug.WriteLine(result);
            }
            _device = BleDevice.WeatherStationDevice;

            if (_device == null)
            {
                await DisplayAlert("No Device", "Weather device not found.", "Drat");
                return;
            }

            await SetupWeatherService();
            await AskForData();

        }

        protected override void OnDisappearing()
        {
            if (BleDevice.WeatherStationDevice.State == Plugin.BLE.Abstractions.DeviceState.Connected)
            {
                Plugin.BLE.CrossBluetoothLE.Current.Adapter.DisconnectDeviceAsync(BleDevice.WeatherStationDevice);
            }
            base.OnDisappearing();
        }

        private async Task SetupWeatherService()
        {
            try
            {
                if (weatherService == null) weatherService =
                        await BleDevice.WeatherStationDevice.GetServiceAsync(GattConstants.UartServiceId);
                if (weatherService != null)
                {
                    charReceive = await weatherService.GetCharacteristicAsync(GattConstants.UartTxCharacteristic);

                    charReceive.ValueUpdated += async (o, args) =>
                    {
                        if (charReceive.StringValue.StartsWith("X"))
                        {
                            var borp = charReceive.StringValue;
                            var burp = await charReceive.ReadAsync();
                        }
                        var boo = charReceive.StringValue;
                        switch (args.Characteristic.Value[0])
                        {
                            case 84: //temp F
                                {
                                    HandleTemperature(args.Characteristic.StringValue.Substring(1), false);
                                    break;
                                }
                            case 80: //Pressure (mmHg)
                                {
                                    HandlePressure(args.Characteristic.StringValue.Substring(1));
                                    break;
                                }
                            case 72: //Humidity
                                {
                                    var humidityAsString = args.Characteristic.StringValue.Substring(1);
                                    var humidity = float.Parse(humidityAsString);
                                    Device.BeginInvokeOnMainThread(() =>
                                    {
                                        humidityHeader.Text = $"{humidity:0.0}%";
                                        humidityNeedle.Value = humidity;
                                    });
                                    break;
                                }
                            case 66: //Battery
                                {
                                    var batteryAsString = args.Characteristic.StringValue.Substring(1);
                                    var battVolts = float.Parse(batteryAsString);
                                    var batteryMax = 4.2;
                                    var batteryMin = 3.3;

                                    var voltsAsPercent = (1 - (batteryMax - battVolts)) * 100;


                                    // (battVolts - 3.6) / 4.14 * 100;
                                    Device.BeginInvokeOnMainThread(() =>
                                    {
                                        lblBatteryPercent.Text = $"{battVolts}v ({voltsAsPercent:0}%)";
                                        battery.Value = voltsAsPercent;// $"{battVolts:0.0} volts";
                                        if (voltsAsPercent <= 10) battery.Color = Color.FromHex("#ff0000");
                                        else if (voltsAsPercent <= 20) battery.Color = Color.FromHex("#ffa500");
                                        else if (voltsAsPercent <= 30) battery.Color = Color.FromHex("#dddd00");
                                        else battery.Color = Color.FromHex("#008000");
                                    });
                                    break;
                                }
                            case 76: //LUX
                                {
                                    /* var luxValueAsString = args.Characteristic.StringValue.Substring(1);
                                     var luxValue = Int16.Parse(luxValueAsString);
                                     //value maxes at 4097, but realistic daylight is...~200
                                     var luxPercent = (float)(luxValue / 2);
                                     Device.BeginInvokeOnMainThread(() =>
                                     {
                                         lux.Text = $"{luxPercent}%";
                                         if (luxPercent > 85) lux.BackgroundColor = Color.FromHex("#fff100");
                                         if (luxPercent > 60) lux.BackgroundColor = Color.FromHex("#c2ac11");
                                         if (luxPercent > 45) lux.BackgroundColor = Color.FromHex("#fcdf03");
                                         if (luxPercent > 25) lux.BackgroundColor = Color.FromHex("#736b32");
                                         else lux.BackgroundColor = Color.FromHex("#45422d");
                                     });*/
                                    break;
                                }
                            case 88: //History
                                {
                                    //M=77 (millis)
                                    var floop = args.Characteristic.StringValue.Substring(1);
                                    break;
                                }
                            default:
                                {
                                    /* Device.BeginInvokeOnMainThread(() =>
                                     {
                                         DisplayAlert("Weird",
                                             $"Unknown data was sent to me: {args.Characteristic.Value[0]}",
                                             "That's odd...");
                                     });*/
                                    break;
                                }
                        }
                    };
                    await charReceive.StartUpdatesAsync();

                    charSend = await weatherService.GetCharacteristicAsync(GattConstants.UartRxCharacteristic);
                }
                else
                {
                    await DisplayAlert("No Weather Data", "The Weather Service is not available. :(", "Suxor");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                btnRefresh.IsEnabled = true;
            }
        }

        private void HandleTemperature(string tempCAsString, bool changeUnits)
        {
            var tempC = float.Parse(tempCAsString);
            var tempF = tempC * 9 / 5 + 32;
            Device.BeginInvokeOnMainThread(() =>
            {
                tempHeader.Text = $"{tempF:0.0}° F";
                tempHeader2.Text = $"{tempC:0.0}° C";
                tempFNeedle.Value = tempF;
            });
        }

        private void HandlePressure(string pressureAsString)
        {
            var pressureInhPa = float.Parse(pressureAsString) / 100;
            var pressureInmmHg = pressureInhPa * 100 / 133.322387415;

            Device.BeginInvokeOnMainThread(() =>
            {
                pressureHeadermmHg.Text = $"{pressureInmmHg:0.0}";
                pressureHeaderhPa.Text = $"{pressureInhPa:0.0}";
                pressureNeedle.Value = pressureInmmHg;
            });

        }

        private async Task AskForData(bool OkToRetry = true)
        {
            btnRefresh.IsEnabled = false;
            if (_device == null)
            {
                if (BleDevice.WeatherStationDevice == null)
                {
                    await BleDevice.GetWeatherStationDevice();
                }

                _device = BleDevice.WeatherStationDevice;
                charSend = await weatherService.GetCharacteristicAsync(GattConstants.UartRxCharacteristic);

                if (_device == null)
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        DisplayAlert("Can't Connect",
                            "Unable to connect to the weather station. Be sure you're " +
                            "within range and have bluetooth enabled!",
                            "Harumph");
                    });
                    btnRefresh.IsEnabled = true;
                }
                return;
            }

            if (charSend != null)
            {
                try
                {
                    await AskForTemp();
                    await AskForPressure();
                    byte[] senddata = Encoding.UTF8.GetBytes("H");
                    await charSend.WriteAsync(senddata);
                    senddata = Encoding.UTF8.GetBytes("B");
                    await charSend.WriteAsync(senddata);
                    senddata = Encoding.UTF8.GetBytes("L");
                    await charSend.WriteAsync(senddata);
                    senddata = Encoding.UTF8.GetBytes("X");
                    await charSend.WriteAsync(senddata);
                }
                catch (Exception ex)
                {
                    if (ex.GetType() == typeof(CharacteristicReadException) && OkToRetry)
                    {
                        await AskForData(false);
                    }
                    else
                    {
                        await DisplayAlert("Error", ex.ToString(), "Wuh? That's a pisser.");
                    }
                }
                finally
                {
                    btnRefresh.IsEnabled = true;
                }
            }
            else
            {
                await SetupWeatherService();
            }
            currentTime.Text = $"{DateTime.Now.ToShortTimeString()}";
            currentDate.Text = $"{DateTime.Now.ToShortDateString()}";
            btnRefresh.IsEnabled = true;
        }

        private async Task AskForTemp()
        {
            charSend = await weatherService.GetCharacteristicAsync(GattConstants.UartRxCharacteristic);
            byte[] senddata = Encoding.UTF8.GetBytes("t");
            await charSend.WriteAsync(senddata);
        }
        private async Task AskForPressure()
        {
            charSend = await weatherService.GetCharacteristicAsync(GattConstants.UartRxCharacteristic);
            byte[] senddata = Encoding.UTF8.GetBytes("p");
            await charSend.WriteAsync(senddata);
        }

        private async void RefreshButton_Clicked(object sender, EventArgs e)
        {
            await AskForData();
        }
    }
}
