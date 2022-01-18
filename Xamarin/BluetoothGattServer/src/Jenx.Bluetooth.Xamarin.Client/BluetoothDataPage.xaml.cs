using Plugin.BLE.Abstractions.Contracts;
using System;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace Jenx.Bluetooth.Xamarin.Client
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class BluetoothDataPage : ContentPage
    {
        private readonly IDevice _connectedDevice;
        private IService weatherService;
        private ICharacteristic charReceive;
        private ICharacteristic charSend;

        public BluetoothDataPage(IDevice connectedDevice)
        {
            InitializeComponent();
            _connectedDevice = connectedDevice;
            AskForData();
        }
        private bool isStarted = false;



        protected override void OnDisappearing()
        {
            if (_connectedDevice.State == Plugin.BLE.Abstractions.DeviceState.Connected)
            {
                Plugin.BLE.CrossBluetoothLE.Current.Adapter.DisconnectDeviceAsync(_connectedDevice);

            }
            base.OnDisappearing();
        }

        private async Task SetUpServiceAndCharacteristics()
        {
            try
            {
                if (weatherService == null) weatherService = await _connectedDevice.GetServiceAsync(GattCharacteristicIdentifiers.UartServiceId);
                if (weatherService != null)
                {
                    charReceive = await weatherService.GetCharacteristicAsync(GattCharacteristicIdentifiers.UartTxCharacteristic);
                    charReceive.ValueUpdated += (o, args) =>
                    {
                        switch (args.Characteristic.Value[0])
                        {
                            case 84: //temp F
                                {
                                    var tempCAsString = args.Characteristic.StringValue.Substring(1);
                                    var tempC = float.Parse(tempCAsString);
                                    var tempF = tempC * 9 / 5 + 32;
                                    Device.BeginInvokeOnMainThread(() =>
                                    {
                                        lblTemperature.Text = $"{tempF:0.0}° F"; //= $"{tempC}° C / {tempF}° F";
                                    });
                                    break;
                                }
                            case 80: //Pressure (mmHg)
                                {
                                    var pressureAsString = args.Characteristic.StringValue.Substring(1);
                                    // this is in hPa == 100 pa. A bar is 100,000 pa
                                    var pressureInhPa = float.Parse(pressureAsString);
                                    var pressureInmmHg = pressureInhPa / 133.322387415;

                                    Device.BeginInvokeOnMainThread(() =>
                                    {
                                        lblPressure.Text = $"{pressureInmmHg:0.0} mmHg";
                                    });
                                    break;
                                }
                            case 72: //Humidity
                                {
                                    var humidityAsString = args.Characteristic.StringValue.Substring(1);

                                    Device.BeginInvokeOnMainThread(() =>
                                    {
                                        lblHumidity.Text = $"{humidityAsString}%";
                                    });
                                    break;
                                }
                            case 66: //Battery
                                {
                                    var batteryAsString = args.Characteristic.StringValue.Substring(1);
                                    var battVolts = float.Parse(batteryAsString);

                                    if (battVolts <= 3.0) lblBatt.TextColor = Color.Red;
                                    else if (battVolts <= 3.2) lblBatt.TextColor = Color.Orange;
                                    else if (battVolts <= 3.5) lblBatt.TextColor = Color.Yellow;
                                    else lblBatt.TextColor = Color.Green;

                                    Device.BeginInvokeOnMainThread(() =>
                                    {
                                        lblBatt.Text = $"{battVolts:0.0}V";
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

                    charSend = await weatherService.GetCharacteristicAsync(GattCharacteristicIdentifiers.UartRxCharacteristic);
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
        }

        private async Task AskForData()
        {
            if (charSend != null)
            {
                try
                {
                    byte[] senddata = Encoding.UTF8.GetBytes("t");
                    await charSend.WriteAsync(senddata);
                    senddata = Encoding.UTF8.GetBytes("p");
                    await charSend.WriteAsync(senddata);
                    senddata = Encoding.UTF8.GetBytes("h");
                    await charSend.WriteAsync(senddata);
                    senddata = Encoding.UTF8.GetBytes("b");
                    await charSend.WriteAsync(senddata);
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", ex.ToString(), "Wuh? That's a pisser.");
                }
            }
            else
            {
                await SetUpServiceAndCharacteristics();
            }
        }

        private async void SendMessageButton_Clicked(object sender, EventArgs e)
        {
            await SetUpServiceAndCharacteristics();
            await AskForData();
        }
    }
}