using System;
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
            await GetStarted();
            base.OnAppearing();
        }

        private async Task GetStarted()
        {
            if (!BleDevice.IsBluetoothConnected())
            {
                await DisplayAlert("Bluetooth Disabled", "Dude, turn on your Bluetooth!", "Oops.");
                return;
            }

            _device = await BleDevice.GetWeatherStationDevice();
            if (_device == null)
            {
                await DisplayAlert("No Device", "Weather device found.", "Drat");
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
                if (weatherService == null) weatherService = await BleDevice.WeatherStationDevice.GetServiceAsync(GattConstants.UartServiceId);
                if (weatherService != null)
                {
                    charReceive = await weatherService.GetCharacteristicAsync(GattConstants.UartTxCharacteristic);
                    charReceive.ValueUpdated += (o, args) =>
                    {
                        switch (args.Characteristic.Value[0])
                        {
                            case 84: //temp F
                                {
                                    HandleTemperature(args.Characteristic.StringValue.Substring(1));
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
                                        humidityHeader.Text = $"{humidityAsString:0.0}%";
                                        humidityNeedle.Value = humidity;
                                    });
                                    break;
                                }
                            case 66: //Battery
                                {
                                    var batteryAsString = args.Characteristic.StringValue.Substring(1);
                                    var battVolts = float.Parse(batteryAsString);
                                    var voltsAsPercent = (battVolts / 4.4) * 100;
                                    Device.BeginInvokeOnMainThread(() =>
                                    {
                                        battery.Value = voltsAsPercent;// $"{battVolts:0.0} volts";
                                        if (voltsAsPercent <= 10) battery.Color = Color.Red;
                                        else if (voltsAsPercent <= 20) battery.Color = Color.Orange;
                                        else if (voltsAsPercent <= 30) battery.Color = Color.Yellow;
                                        else battery.Color = Color.Green;
                                    });
                                    break;
                                }
                            case 76: //LUX
                                {

                                    var luxValueAsString = args.Characteristic.StringValue.Substring(1);
                                    var luxValue = Int16.Parse(luxValueAsString);
                                    //value maxes at 4097, but realistic daylight is...~200
                                    var luxPercent = (luxValue / 200) * 100;
                                    Device.BeginInvokeOnMainThread(() =>
                                    {
                                        lux.Value = luxPercent;

                                        if (luxPercent > 85) lux.Color = ConvertCmykToRgb(0, 0, 1, 0);
                                        if (luxPercent > 60) lux.Color = ConvertCmykToRgb(0, 0, .77f, .33f);
                                        if (luxPercent > 45) lux.Color = ConvertCmykToRgb(0, 0, .54f, .66f);
                                        if (luxPercent > 25) lux.Color = ConvertCmykToRgb(0, 0, .34f, .70f);
                                        else lux.Color = ConvertCmykToRgb(0, 0, .26f, .73f);
                                    });
                                    break;
                                }
                            default:
                                {
                                    Device.BeginInvokeOnMainThread(() =>
                                    {
                                        DisplayAlert("Weird",
                                            $"Unknown data was sent to me: {args.Characteristic.Value[0]}",
                                            "That's odd...");
                                    });
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

        private void HandleTemperature(string tempCAsString)
        {
            var tempC = float.Parse(tempCAsString);
            var tempF = tempC * 9 / 5 + 32;

            //Show to F
            if (String.IsNullOrEmpty(tempHeader.Text) || tempHeader.Text.EndsWith("C"))
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    tempScale.Ranges.Clear();
                    tempScale.StartValue = -20;
                    tempScale.EndValue = 120;

                    tempScale.Ranges.Add(new Syncfusion.SfGauge.XForms.Range()
                    {
                        StartValue = -20,
                        EndValue = 0,
                        Color = Color.FromHex("#94b6d4")
                    });
                    tempScale.Ranges.Add(new Syncfusion.SfGauge.XForms.Range()
                    {
                        StartValue = 0,
                        EndValue = 32,
                        Color = Color.SteelBlue
                    }); ;
                    tempScale.Ranges.Add(new Syncfusion.SfGauge.XForms.Range()
                    {
                        StartValue = 32,
                        EndValue = 80,
                        Color = Color.FromHex("#90cc72")
                    });
                    tempScale.Ranges.Add(new Syncfusion.SfGauge.XForms.Range()
                    {
                        StartValue = 80,
                        EndValue = 100,
                        Color = Color.Orange
                    });
                    tempScale.Ranges.Add(new Syncfusion.SfGauge.XForms.Range()
                    {
                        StartValue = 100,
                        EndValue = 120,
                        Color = Color.Red
                    });


                    tempHeader.Text = $"{tempF:0.0}° F";
                    tempHeader2.Text = $"{tempC:0.0}° C";
                    tempNeedle.Value = tempF;
                });
            }
            else //show C
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    tempScale.Ranges.Clear();

                    tempScale.StartValue = -30;
                    tempScale.EndValue = 50;

                    tempScale.Ranges.Add(new Syncfusion.SfGauge.XForms.Range()
                    {
                        StartValue = -30,
                        EndValue = -18,
                        Color = Color.FromHex("#94b6d4")
                    });
                    tempScale.Ranges.Add(new Syncfusion.SfGauge.XForms.Range()
                    {
                        StartValue = -18,
                        EndValue = 0,
                        Color = Color.SteelBlue
                    }); ;
                    tempScale.Ranges.Add(new Syncfusion.SfGauge.XForms.Range()
                    {
                        StartValue = 0,
                        EndValue = 27,
                        Color = Color.FromHex("#90cc72")
                    });
                    tempScale.Ranges.Add(new Syncfusion.SfGauge.XForms.Range()
                    {
                        StartValue = 27,
                        EndValue = 38,
                        Color = Color.Orange
                    });
                    tempScale.Ranges.Add(new Syncfusion.SfGauge.XForms.Range()
                    {
                        StartValue = 38,
                        EndValue = 50,
                        Color = Color.Red
                    });

                    tempHeader.Text = $"{tempC:0.0}° C";
                    tempHeader2.Text = $"{tempF:0.0}° F";
                    tempNeedle.Value = tempC;
                });
            }
        }

        private void HandlePressure(string pressureAsString)
        {
            var pressureInhPa = float.Parse(pressureAsString) / 1000;

            if (string.IsNullOrEmpty(pressureHeader2.Text) || pressureHeader2.Text == "hPa") //show in mmHg
            {
                var pressureInmmHg = pressureInhPa * 100 / 133.322387415;

                Device.BeginInvokeOnMainThread(() =>
                {

                    pressureRange.StartValue = 650;
                    pressureRange.EndValue = 820;
                    pressureHeader2.Text = "mmHg";
                    pressureHeader.Text = $"{pressureInmmHg:0.0}";
                    pressureNeedle.Value = pressureInmmHg;
                });
            }
            else
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    pressureRange.StartValue = 850;
                    pressureRange.EndValue = 1094;
                    pressureHeader2.Text = "hPa";

                    pressureHeader.Text = $"{pressureInhPa:0.0}";
                    pressureNeedle.Value = pressureInhPa;
                });
            }
        }

        private async Task AskForData(bool OkToRetry = true)
        {
            btnRefresh.IsEnabled = false;
            if (_device == null)
            {
                _device = await BleDevice.GetWeatherStationDevice();
                if (BleDevice.WeatherStationDevice == null)
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
                    byte[] senddata = Encoding.UTF8.GetBytes("h");
                    await charSend.WriteAsync(senddata);
                    senddata = Encoding.UTF8.GetBytes("b");
                    await charSend.WriteAsync(senddata);
                    senddata = Encoding.UTF8.GetBytes("l");
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

            btnRefresh.IsEnabled = true;
        }

        private async Task AskForTemp()
        {
            byte[] senddata = Encoding.UTF8.GetBytes("t");
            await charSend.WriteAsync(senddata);
        }
        private async Task AskForPressure()
        {
            byte[] senddata = Encoding.UTF8.GetBytes("p");
            await charSend.WriteAsync(senddata);
        }

        private async void RefreshButton_Clicked(object sender, EventArgs e)
        {
            await AskForData();
        }

        public static Color ConvertCmykToRgb(float c, float m, float y, float k)
        {
            int r;
            int g;
            int b;

            r = Convert.ToInt32(255 * (1 - c) * (1 - k));
            g = Convert.ToInt32(255 * (1 - m) * (1 - k));
            b = Convert.ToInt32(255 * (1 - y) * (1 - k));

            return Color.FromRgba(r, g, b, 1);
        }

        async void Temperature_Tapped(System.Object sender, System.EventArgs e)
        {
            try
            {
                await AskForTemp();
            }
            catch (Exception ex)
            {
                if (ex.GetType() == typeof(CharacteristicReadException))
                {
                    await AskForTemp();
                }
                else
                {
                    await DisplayAlert("Error", ex.ToString(), "Wuh?");
                }
            }
        }
        async void Pressure_Tapped(System.Object sender, System.EventArgs e)
        {
            await AskForPressure();
        }
    }
}
