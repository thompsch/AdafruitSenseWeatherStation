using System;
using System.Collections.Generic;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SenseWeather
{
    public partial class App : Application
    {

        public App()
        {
            InitializeComponent();
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
                "NTY4MzEwQDMxMzkyZTM0MmUzMGczeGE3TzU3ZFMyWEViZG10WVg3dDh0M2ZyVWMySVZwUnJmUThCQ1c4eWs9;NTY4MzExQDMxMzkyZTM0MmUzMEFHZTBFRlZTZzRENGMrZGtwU2tSTno5MG5xRHhxbllzcWpiUkpaY2h2U1E9;NTY4MzEyQDMxMzkyZTM0MmUzMEk1MWV5M1psdVkvZFhCcmRiUnpiRlVZQkRzZ0YyR0o4bGdZWHA5Z1gvckE9;NTY4MzEzQDMxMzkyZTM0MmUzMEFQelVUbWFRQitTdnVyazRFOHlid2MvUVdvZk4yTFV3MEN6VVE1dlhRNmM9");

            MainPage = new MainPage();
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}
