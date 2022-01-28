using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions;

namespace SenseWeather
{
    public class BleDevice
    {
        private static IAdapter _bluetoothAdapter;
        public static IDevice WeatherStationDevice;
        private static Guid weatherStationGuid = Guid.Parse("00000000-0000-0000-0000-de02576ef3b7");
        internal static ConnectParameters _connectParameters = new ConnectParameters(autoConnect: true, forceBleTransport: true);

        static BleDevice()
        {
            //_bluetoothAdapter.DeviceConnectionLost += _bluetoothAdapter_DeviceConnectionLost;
        }

        private static void _bluetoothAdapter_DeviceConnectionLost(object sender, Plugin.BLE.Abstractions.EventArgs.DeviceErrorEventArgs e)
        {
            //TODO: reconnect
            throw new NotImplementedException();
        }

        public static bool IsBluetoothConnected()
        {
            var ble = CrossBluetoothLE.Current;
            if (ble.State == BluetoothState.Off)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static async Task<string> GetWeatherStationDevice()
        {
            if (!IsBluetoothConnected())
            {
                return null;
            }
            if (_bluetoothAdapter == null)
            {
                _bluetoothAdapter = CrossBluetoothLE.Current.Adapter;
            }

            if (_bluetoothAdapter != null && WeatherStationDevice != null)
            {
                return "We already have a weatherstation.";
            }
            /*if (!await CheckPermissions())
            {
                return null;
            }*/

            var device = await _bluetoothAdapter.ConnectToKnownDeviceAsync(weatherStationGuid, _connectParameters);
            if (device != null && device.State == DeviceState.Connected)
            {
                WeatherStationDevice = device;
                return "success";
            }
            else
            {
                try
                {
                    await _bluetoothAdapter.ConnectToKnownDeviceAsync(weatherStationGuid, _connectParameters);
                    if (device.State != DeviceState.Connected)
                    {
                        //TODO error!!
                        return "ERROR: Device is no longer connected!";
                    }
                    else
                    {
                        WeatherStationDevice = device;
                        return "reconnect worked!";
                    }
                }
                catch
                {
                    WeatherStationDevice = null;
                    return "ERROR: ConnectToKnownDevice has failed.";
                }
            }
        }


        /*private static async Task<bool> CheckPermissions()
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
        }*/
    }
}
