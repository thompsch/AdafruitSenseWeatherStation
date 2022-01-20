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
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("NTQyMzQ1QDMxMzkyZTMzMmUzMElaMktjSFE0RlNraTRua2UwdGJqbytvTXJzOFNoc1NKVHZ3ZFJLSWxlbEE9");

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
