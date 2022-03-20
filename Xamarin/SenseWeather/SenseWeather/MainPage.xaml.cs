using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
                await DisplayAlert("ERROR", "Problem connecting to the device. Are you in range?", "Drat.");
            }
            else
            {
                Debug.WriteLine(result);
            }
            _device = BleDevice.WeatherStationDevice;

            if (_device == null)
            {
                await DisplayAlert("No Device", "Weather device not found.", "Drat.");
                return;
            }

            await SetupWeatherService();
            await AskForData();
            await AskForHistory(true);
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
                                    HandleHistory(args.Characteristic.StringValue.Substring(1));
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
                await DisplayAlert("EX", ex.ToString(), "huh");
                //throw;
            }
            finally
            {
                btnRefresh.IsEnabled = true;
            }
        }

        private async Task HandleHistoryTest()
        {
            /*sample data
            "X360+T15.24",
            "X360+H38.92",
            "X360+P102479.67",*/

            var testInput = new List<string>();
            Random r = new Random();

            for (int x = 0; x < 144; x++)
            {
                var rtemp = (r.NextDouble() * 80) - 30;
                var rhumidity = r.Next(0, 100);
                var rPressure = (r.NextDouble() * 7500) + 96500; //102479
                var count = x * 10000;
                testInput.Add($"X{count}+T{rtemp}");
                testInput.Add($"X{count}+H{rhumidity}");
                testInput.Add($"X{count}+P{rPressure}");
            }
            foreach (var ti in testInput)
            {
                HandleHistory(ti);
            }
        }

        internal void UpdateChart(double key)
        {
            var wm = WeatherDictionary[key];
            for (int x = 0; x < WeatherViewModel.Data.Count; x++)
            {
                if (WeatherViewModel.Data[x].RelativeTimeStamp == wm.RelativeTimeStamp)
                {
                    return;
                }
            }
            WeatherViewModel.AddToData(wm);
            Device.BeginInvokeOnMainThread(() =>
             {
                 NumericalStripLine stripLine = new NumericalStripLine()
                 {
                     Start = key,
                     Width = 20,
                     StrokeColor = Color.FromHex("#565656")
                 };
                 primaryNumericalAxis.StripLines.Add(stripLine);
             });
            primaryNumericalAxis.Minimum = WeatherViewModel.Data.Min(v => v.RelativeTimeStamp);
        }

        internal bool EntryIsComplete(double key)
        {
            return (
                WeatherDictionary[key].TempValue != 0 &&
                WeatherDictionary[key].PressureValue != 0 &&
                WeatherDictionary[key].HumidityValue != 0);
        }

        private void HandleHistory(string value)
        {
            value = value.Substring(1);
            var parts = value.Split('+');
            var key = Int64.Parse(parts[0]);
            WeatherModel wm;

            if (!WeatherDictionary.TryGetValue(key, out wm))
            {
                // this key (timestamp) does not yet exist 
                WeatherDictionary.Add(key, new WeatherModel() { RelativeTimeStamp = key });
            }

            // this key exists...does it have all 3 data points?
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
            var batteryMax = 4.1;
            var batteryMin = 3.3;

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
            //await HandleHistoryTest();
            if (initialLoad || DateTimeOffset.Now >= lastHistoryCheck.AddMinutes(30))
            {
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
            if (charSend != null)
            {
                btnRefresh.IsEnabled = false;
                try
                {
                    byte[] senddata = Encoding.UTF8.GetBytes("A");
                    await charSend.WriteAsync(senddata);
                }
                catch (Exception ex)
                {
                    if (ex.GetType() == typeof(CharacteristicReadException) && OkToRetry)
                    {
                        //await GetStarted();
                        //await AskForData(false);
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
            }
            currentDateTime.Text = $"{DateTime.Now.ToShortTimeString()}{System.Environment.NewLine}{DateTime.Now.ToShortDateString()}";
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
            //TODO: set  to true for testing updates

            await AskForHistory(false);
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

        void TempTapGestureRecognizer_Tapped(System.Object sender, System.EventArgs e)
        {
            tempSplineSeries.Opacity = 1;
            pressureSplineSeries.Opacity = 0.2;
            humiditySplineSeries.Opacity = 0.2;
        }
        void PressureTapGestureRecognizer_Tapped(System.Object sender, System.EventArgs e)
        {
            pressureSplineSeries.Opacity = 1;
            tempSplineSeries.Opacity = 0.2;
            humiditySplineSeries.Opacity = 0.2;
        }
        void HumidityTapGestureRecognizer_Tapped(System.Object sender, System.EventArgs e)
        {
            humiditySplineSeries.Opacity = 1;
            pressureSplineSeries.Opacity = 0.2;
            tempSplineSeries.Opacity = 0.2;
        }
    }
}
