using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace SenseWeather.Models
{
    public class WeatherModel
    {
        public double TempValue { get; set; }
        public double HumidityValue { get; set; }
        public double PressureValue { get; set; }
        public double RelativeTimeStamp { get; set; }

        public WeatherModel()
        {
        }
        public override bool Equals(object obj)
        {
            try
            {
                var wd = (WeatherModel)obj;
                return (this.RelativeTimeStamp == wd.RelativeTimeStamp &&
                    this.TempValue == wd.TempValue &&
                    this.PressureValue == wd.PressureValue &&
                    this.HumidityValue == wd.HumidityValue);
            }
            catch
            {
                return false;
            }
        }
    }

    public class WeatherViewModel
    {

        public static event PropertyChangedEventHandler StaticPropertyChanged;
        public static ObservableCollection<WeatherModel> Data { get; set; }

        static WeatherViewModel()
        {
            Data = new ObservableCollection<WeatherModel>();
        }

        public static void AddToData(WeatherModel input)
        {
            var normalizedData = NormalizeToPercent(input);
            if (!Data.Contains<WeatherModel>(normalizedData))
            {
                Data.Add(normalizedData);
            }
        }

        internal static WeatherModel NormalizeToPercent(WeatherModel input)
        {
            int minPressureValue = 72000;
            int maxPressureValue = 108400;

            int minTempValue = -20;
            int maxTempValue = 120;

            // (input - min) / (max-min) * 100;
            return new WeatherModel()
            {
                TempValue = (input.TempValue - minTempValue) / (maxTempValue - minTempValue) * 100,
                PressureValue = (input.PressureValue - minPressureValue) / (maxPressureValue - minPressureValue) * 100,
                HumidityValue = input.HumidityValue, //already a % value
                RelativeTimeStamp = input.RelativeTimeStamp
            };
        }
    }
}
