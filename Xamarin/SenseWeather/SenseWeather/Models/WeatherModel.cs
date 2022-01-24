using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
    }

    public class WeatherViewModel
    {
        public List<WeatherModel> Data { get; set; }
        private const int MaxPressureValue = 108400;
        private const int MaxTemperatureValue = 120;

        public WeatherViewModel()
        {
            Data = new List<WeatherModel>()
            {
                ConvertValues(new WeatherModel { TempValue = 6.3, PressureValue = 2701, HumidityValue = .70, RelativeTimeStamp = 0 }),
                ConvertValues(new WeatherModel { TempValue = 6.7, PressureValue = 2699, HumidityValue = .82, RelativeTimeStamp = 1 }),
                ConvertValues(new WeatherModel { TempValue = 6.8, PressureValue = 2678, HumidityValue = .93, RelativeTimeStamp = 2 }),
                ConvertValues(new WeatherModel { TempValue = 7.1, PressureValue = 2678, HumidityValue = .90, RelativeTimeStamp = 3 })
            };
        }

        public static WeatherModel ConvertValues(WeatherModel input)
        {
            //TODO: standardize everything to %
            return new WeatherModel()
            {
                TempValue = input.TempValue / MaxTemperatureValue * 100,
                PressureValue = input.PressureValue / MaxPressureValue * 100,
                HumidityValue = input.HumidityValue,
                RelativeTimeStamp = input.RelativeTimeStamp
            };
        }
    }
}
