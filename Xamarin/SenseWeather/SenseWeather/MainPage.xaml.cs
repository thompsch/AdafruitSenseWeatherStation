using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Exceptions;
using SenseWeather.Models;
using Syncfusion.SfChart.XForms;
using Xamarin.Forms;

namespace SenseWeather
{
    public partial class MainPage : ContentPage
    {
        private IDevice _device;
        private IService weatherService;
        private ICharacteristic charReceive;
        private ICharacteristic charSend;
        public Dictionary<double, WeatherModel> WeatherDictionary = new Dictionary<double, WeatherModel>();
        private DateTimeOffset lastHistoryCheck;

        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            try
            {
                lastHistoryCheck = DateTimeOffset.Now;
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
            if (!BleDevice.IsBluetoothEnabled())
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
            await AskForHistory(true);
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
                    charSend = await weatherService.GetCharacteristicAsync(GattConstants.UartRxCharacteristic);
                    charReceive = await weatherService.GetCharacteristicAsync(GattConstants.UartTxCharacteristic);
                    await charReceive.StartUpdatesAsync();
                    charReceive.ValueUpdated += async (o, args) =>
                    {
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
                                    HandleHumidity(args);
                                    break;
                                }
                            case 66: //Battery
                                {
                                    HandleBattery(args);
                                    break;
                                }
                            case 76: //LUX
                                {
                                    //HandleLux(args);
                                    break;
                                }
                            case 88: //History
                                {
                                    HandleHistory(args);
                                    break;
                                }
                            default:
                                {
                                    //unknown data. 
                                    break;
                                }
                        }
                    };
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

        private async Task HandleHistoryTest()
        {
            //X367+T25.24X367+H38.92X367+P102479.67
            var testInput = new List<string>()
            {
                "X360+T15.24",
                "X360+H38.92",
                "X360+P102479.67",
                "X3600360+T18.24",
                "X3600360+H48.92",
                "X3600360+P102600",
                "X7200360+T25.24",
                "X7200360+H58.92",
                "X7200360+P102650",
                "X10800360+T22.24",
                "X10800360+H55.92",
                "X10800360+P101150",
                "X14400360+T5.24",
                "X14400360+H30",
                "X14400360+P103000",
            };


            foreach (var ti in testInput)
            {
                var value = ti.Substring(1);
                var parts = value.Split('+');
                var key = Double.Parse(parts[0]);
                WeatherModel wm;
                if (!WeatherDictionary.TryGetValue(key, out wm))
                {
                    // this key (timestasmp) does not yet exist 
                    WeatherDictionary.Add(key, new WeatherModel() { RelativeTimeStamp = key });
                }
                // this key exists.

                if (EntryIsComplete(key))
                {
                    UpdateChart(key);
                }
                else
                {
                    AddToDictionary(key, parts[1]);
                    if (EntryIsComplete(key))
                    {
                        UpdateChart(key);
                    }
                }
            }
        }

        internal void UpdateChart(double key)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                WeatherViewModel.AddToData(WeatherDictionary[key]);
                NumericalStripLine stripLine = new NumericalStripLine()
                {
                    Start = key,
                    Width = 2,
                    StrokeColor = Color.FromHex("#232323")
                };
                primaryNumericalAxis.StripLines.Add(stripLine);

            });
        }

        internal bool EntryIsComplete(double key)
        {
            return (
                WeatherDictionary[key].TempValue != 0 &&
                WeatherDictionary[key].PressureValue != 0 &&
                WeatherDictionary[key].HumidityValue != 0);
        }

        private void HandleHistory(CharacteristicUpdatedEventArgs args)
        {
            //X367+T25.24X367+H38.92X367+P102479.67
            var value = args.Characteristic.StringValue.Substring(1);

            var parts = value.Split('+');
            var key = Double.Parse(parts[0]);
            WeatherModel wm;
            if (!WeatherDictionary.TryGetValue(key, out wm))
            {
                // this key (timestasmp) does not yet exist 
                WeatherDictionary.Add(key, new WeatherModel() { RelativeTimeStamp = key });
            }
            // this key exists.

            if (EntryIsComplete(key))
            {
                UpdateChart(key);
            }
            else
            {
                AddToDictionary(key, parts[1]);
                if (EntryIsComplete(key))
                {
                    UpdateChart(key);
                }
            }
        }

        private void HandleBattery(CharacteristicUpdatedEventArgs args)
        {
            var batteryAsString = args.Characteristic.StringValue.Substring(1);
            var battVolts = float.Parse(batteryAsString);
            var batteryMax = 4.2;
            var batteryMin = 3.3;

            // (input-min)/0.9 * 100
            var voltsAsPercent = (battVolts - batteryMin) / (batteryMax - batteryMin) * 100;
            Device.BeginInvokeOnMainThread(() =>
            {
                lblBatteryPercent.Text = $"{battVolts}v ({voltsAsPercent:0}%)";
                battery.Value = voltsAsPercent;
                if (voltsAsPercent <= 10) battery.Color = Color.FromHex("#c78787");
                else if (voltsAsPercent <= 20) battery.Color = Color.FromHex("#c9a76b");
                else if (voltsAsPercent <= 30) battery.Color = Color.FromHex("#e6e691");
                else battery.Color = Color.FromHex("#8dc98d");
            });
        }

        private void HandleHumidity(CharacteristicUpdatedEventArgs args)
        {
            var humidityAsString = args.Characteristic.StringValue.Substring(1);
            var humidity = float.Parse(humidityAsString);
            Device.BeginInvokeOnMainThread(() =>
            {
                humidityHeader.Text = $"{humidity:0.0}%";
                humidityNeedle.Value = humidity;
            });
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

        private async Task AskForHistory(bool initialLoad)
        {
            // await HandleHistoryTest();
            //only update history if it's been more than 15 minutes since
            //last check. station updates every hour, so this is some
            //arbitrary limit to reduce BLE traffic and power usage....theoretically
            if (initialLoad || DateTimeOffset.Now >= lastHistoryCheck.AddMinutes(15))
            {
                await checkDeviceSettings();

                if (charSend != null)
                {
                    btnRefresh.IsEnabled = false;
                    try
                    {
                        byte[] senddata = Encoding.UTF8.GetBytes("X");
                        await charSend.WriteAsync(senddata);
                    }
                    catch
                    {

                    }
                    finally
                    {
                        btnRefresh.IsEnabled = true;
                    }
                }
            }
        }

        private async Task AskForData(bool OkToRetry = true)
        {
            await checkDeviceSettings();

            if (charSend != null)
            {
                btnRefresh.IsEnabled = false;
                try
                {
                    byte[] senddata = Encoding.UTF8.GetBytes("A");
                    await charSend.WriteAsync(senddata);
                    /*if (x)
                    {
                        senddata = Encoding.UTF8.GetBytes("T");
                        var t = await charSend.WriteAsync(senddata);
                        if (t)
                        {
                            senddata = Encoding.UTF8.GetBytes("P");
                            var p = await charSend.WriteAsync(senddata);
                            if (p)
                            {
                                senddata = Encoding.UTF8.GetBytes("H");
                                var h = await charSend.WriteAsync(senddata);
                                if (h)
                                {
                                    senddata = Encoding.UTF8.GetBytes("B");
                                    await charSend.WriteAsync(senddata);
                                }
                            }
                        }
                    }*/

                }
                catch (Exception ex)
                {
                    if (ex.GetType() == typeof(CharacteristicReadException) && OkToRetry)
                    {
                        await GetStarted();
                        await AskForData(false);
                    }
                    else
                    {
                        await DisplayAlert("Error", ex.ToString(), "Wuh? That's a bummer.");
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
                //await AskForData(); DANGERLOOP
            }
            currentDateTime.Text = $"{DateTime.Now.ToShortTimeString()}{System.Environment.NewLine}{DateTime.Now.ToShortDateString()}";
            // currentDate.Text = $"{DateTime.Now.ToShortDateString()}";
            btnRefresh.IsEnabled = true;
        }

        private async Task checkDeviceSettings()
        {
            if (_device == null)
            {
                if (BleDevice.WeatherStationDevice == null)
                {
                    await BleDevice.GetWeatherStationDevice();
                }

                _device = BleDevice.WeatherStationDevice;


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
        }

        private async void RefreshButton_Clicked(object sender, EventArgs e)
        {
            await AskForData();
            //TODO: set back to false
            await AskForHistory(true);
        }


        internal void AddToDictionary(double key, string input)
        {
            try
            {
                var item = WeatherDictionary[key];
                switch (input[0])
                {
                    case 'T':
                        if (item.TempValue == 0) item.TempValue = Double.Parse(input.Substring(1));
                        break;
                    case 'P':

                        if (item.PressureValue == 0) item.PressureValue = Double.Parse(input.Substring(1));
                        break;
                    case 'H':
                        if (item.HumidityValue == 0) item.HumidityValue = Double.Parse(input.Substring(1));
                        break;
                }
            }
            catch (Exception feck)
            {
                DisplayAlert("error", $"Error handling the historic data.\n{feck}", "Drat.");
            }
        }

    }
}
