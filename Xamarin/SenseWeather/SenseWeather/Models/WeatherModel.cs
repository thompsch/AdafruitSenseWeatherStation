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
        private static double currentRangMaxTemp;
        private static double currentRangMinTemp;
        private static double currentRangMaxPressure;
        private static double currentRangMinPressure;

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
            //avergae on floating scale is: [ (input - min) / (max-min) * 100 ]

            //29.5 -> 30.4 is normal. 28.5 is severe low pressure storm
            // 773-750 mm Hg; 724 extreme low
            // 1031  -> 1000 mBar; 965 extreme low


            // TODO: What happens if we have an extreme event and pressure or
            // temp goes beyond the assumed limits? Graph show no line.
            // Do we want to adjust the scale in these cases, or will we be
            // seeking shelter and not caring about it?

            int minPressureValue = 104000;
            int maxPressureValue = 96500;

            int minTempValue = -20;
            int maxTempValue = 120;

            if (Data != null && Data.Count > 0)
            {
                currentRangMaxTemp = Math.Max(Data.Max(t => t.TempValue) + 5, input.TempValue);
                currentRangMinTemp = Math.Min(Data.Min(t => t.TempValue) - 5, input.TempValue);
                currentRangMaxPressure = Math.Max(Data.Max(t => t.PressureValue) + 5, input.PressureValue);
                currentRangMinPressure = Math.Min(Data.Min(t => t.PressureValue) - 5, input.PressureValue);
            }
            else
            {
                currentRangMaxTemp = maxTempValue;
                currentRangMinTemp = minTempValue;
                currentRangMinPressure = minPressureValue;
                currentRangMaxPressure = maxPressureValue;
            }

            return new WeatherModel()
            {
                TempValue = (input.TempValue - currentRangMinTemp) / (currentRangMaxTemp - currentRangMinTemp) * 100,
                // full scale: (input.TempValue - minTempValue) / (maxTempValue - minTempValue) * 100,
                PressureValue = (input.PressureValue - currentRangMinPressure) / (currentRangMaxPressure - currentRangMinPressure) * 100,
                HumidityValue = input.HumidityValue, //already a % value
                RelativeTimeStamp = input.RelativeTimeStamp
            };
        }
    }
}
