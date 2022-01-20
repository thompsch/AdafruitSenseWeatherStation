using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;

namespace SenseWeather
{
    public class BleDevice
    {
        private static IAdapter _bluetoothAdapter;
        public static IDevice WeatherStationDevice;


        static BleDevice()
        {

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

        public static async Task<IDevice> GetWeatherStationDevice()
        {
            if (!IsBluetoothConnected())
            {
                return null;
            }
            if (_bluetoothAdapter == null)
            {
                _bluetoothAdapter = CrossBluetoothLE.Current.Adapter;
            }

            if (WeatherStationDevice != null)
            {
                return WeatherStationDevice;
            }
            /*if (!await CheckPermissions())
            {
                return null;
            }*/
            var device = await _bluetoothAdapter.ConnectToKnownDeviceAsync(Guid.Parse("00000000-0000-0000-0000-de02576ef3b7"));
            if (device.State == Plugin.BLE.Abstractions.DeviceState.Connected)
            {
                WeatherStationDevice = device;
                return WeatherStationDevice;
            }
            else
            {
                try
                {
                    await _bluetoothAdapter.ConnectToDeviceAsync(device);
                    if (device.State != Plugin.BLE.Abstractions.DeviceState.Connected)
                    {
                        //TODO error!!
                        return null;
                    }
                    else
                    {
                        WeatherStationDevice = device;
                        return WeatherStationDevice;
                    }
                }
                catch
                {
                    WeatherStationDevice = null;
                    return WeatherStationDevice;
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
