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
        public double NormalizedTempValue { get; set; }
        public double NormalizedHumidityValue { get; set; }
        public double NormalizedPressureValue { get; set; }
        public long RelativeTimeStamp { get; set; }


        public WeatherModel()
        {
        }

        public override bool Equals(object obj)
        {
            try
            {
                var wm = (WeatherModel)obj;
                return (this.RelativeTimeStamp == wm.RelativeTimeStamp &&
                    this.TempValue == wm.TempValue &&
                    this.PressureValue == wm.PressureValue &&
                    this.HumidityValue == wm.HumidityValue);
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
        public static LimitedSizeObservableCollection<WeatherModel> Data
        {
            get
            {
                return data;
            }
        }
        private static LimitedSizeObservableCollection<WeatherModel> data { get; set; }
        private static double currentRangeMaxTemp;
        private static double currentRangeMinTemp;
        private static double currentRangeMaxPressure;
        private static double currentRangeMinPressure;

        static WeatherViewModel()
        {
            data = new LimitedSizeObservableCollection<WeatherModel>(72);
        }

        public static void AddToData(WeatherModel input)
        {
            var normalizedData = NormalizeToPercent(input);
            if (!data.Contains(normalizedData))
            {
                var index =
                    data.IndexOf(data.Where(x => x.RelativeTimeStamp < input.RelativeTimeStamp).LastOrDefault());
                index = index < 0 ? 0 : index + 1;
                data.Insert(index, normalizedData); //.Add(normalizedData);//

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

            int maxPressureValue = 104000;
            int minPressureValue = 96500;

            int minTempValue = -20;
            int maxTempValue = 120;

            if (Data != null && Data.Count > 0)
            {
                currentRangeMaxTemp = Math.Max(Data.Max(t => t.TempValue) + 5, input.TempValue);
                currentRangeMinTemp = Math.Min(Data.Min(t => t.TempValue) - 5, input.TempValue);
                currentRangeMaxPressure = Math.Max(Data.Max(t => t.PressureValue) + 5, input.PressureValue);
                currentRangeMinPressure = Math.Min(Data.Min(t => t.PressureValue) - 5, input.PressureValue);
            }
            else
            {
                currentRangeMaxTemp = maxTempValue;
                currentRangeMinTemp = minTempValue;
                currentRangeMinPressure = minPressureValue;
                currentRangeMaxPressure = maxPressureValue;
            }

            return new WeatherModel()
            {
                TempValue = input.TempValue,
                PressureValue = input.PressureValue,
                HumidityValue = input.HumidityValue,
                NormalizedTempValue = (input.TempValue - currentRangeMinTemp) / (currentRangeMaxTemp - currentRangeMinTemp) * 100,
                NormalizedPressureValue = (input.PressureValue - currentRangeMinPressure) / (currentRangeMaxPressure - currentRangeMinPressure) * 100,
                NormalizedHumidityValue = input.HumidityValue, //already a % value
                RelativeTimeStamp = input.RelativeTimeStamp
            };
        }
    }
}
