using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace Jenx.Bluetooth.Xamarin.Client
{
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        private readonly IAdapter _bluetoothAdapter;
        private List<IDevice> _gattDevices = new List<IDevice>();

        public MainPage()
        {
            InitializeComponent();

            _bluetoothAdapter = CrossBluetoothLE.Current.Adapter;
            _bluetoothAdapter.DeviceDiscovered += (s, a) =>
            {
                _gattDevices.Add(a.Device);
            };

            SkipTheBS_Clicked(null, null);
        }

        private async void ScanButton_Clicked(object sender, EventArgs e)
        {


            ScanButton.IsEnabled = false;

            await CheckPermissions();

            _gattDevices.Clear();
            await _bluetoothAdapter.StartScanningForDevicesAsync();
            listView.ItemsSource = _gattDevices.ToArray();
            ScanButton.IsEnabled = true;
        }

        private async Task<bool> CheckPermissions()
        {
            var locationPermissionStatus = await CrossPermissions.Current.CheckPermissionStatusAsync(Permission.Location);

            if (locationPermissionStatus != PermissionStatus.Granted)
            {
                if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Permission.Location))
                {
                    await DisplayAlert("Permission required", "Application needs location permission", "OK");
                    return false;
                }

                var requestLocationPermissionStatus = await CrossPermissions.Current.RequestPermissionsAsync(new[] { Permission.Location });

                var locationPermissionPersent = requestLocationPermissionStatus.FirstOrDefault(x => x.Key == Permission.Location);
                if (locationPermissionPersent.Value == PermissionStatus.Granted)
                {
                    locationPermissionStatus = PermissionStatus.Granted;
                    return true;
                }
            }

            if (locationPermissionStatus != PermissionStatus.Granted)
            {
                await DisplayAlert("Permission required", "Application needs location permission", "OK");
                return false;
            }
            else
            {
                return true;
            }
        }

        private async void FoundBluetoothDevicesListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            IDevice selectedItem = e.SelectedItem as IDevice;

            if (selectedItem.State == Plugin.BLE.Abstractions.DeviceState.Connected)
                await Navigation.PushAsync(new BluetoothDataPage(selectedItem));
            else
            {
                try
                {
                    await _bluetoothAdapter.ConnectToDeviceAsync(selectedItem);
                    await Navigation.PushAsync(new BluetoothDataPage(selectedItem));
                }
                catch
                {
                    await DisplayAlert("Connection Failure", "Could not connect to the device.", "Drat");
                }
            }
        }

        async void SkipTheBS_Clicked(object sender, EventArgs e)
        {
            await CheckPermissions();

            var device = await _bluetoothAdapter.ConnectToKnownDeviceAsync(Guid.Parse("00000000-0000-0000-0000-de02576ef3b7"));
            if (device.State == Plugin.BLE.Abstractions.DeviceState.Connected)
                await Navigation.PushAsync(new BluetoothDataPage(device));

            else
            {
                try
                {
                    await _bluetoothAdapter.ConnectToDeviceAsync(device);
                    await Navigation.PushAsync(new BluetoothDataPage(device));
                }
                catch
                {
                    await DisplayAlert("No Device", "No device found", "Drat");
                }
            }
        }
    }
}