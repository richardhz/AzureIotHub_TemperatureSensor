using System;
using System.Collections.Generic;
using Sensors.Dht;
using Windows.Devices.Gpio;
using Windows.UI.Xaml;
using Microsoft.Devices.Tpm;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System.Text;
using Windows.UI.Xaml.Controls;

namespace Sensors.OneWire
{
    public sealed partial class MainPage : Page
    {
        private DispatcherTimer _timer = new DispatcherTimer();
        int totalAttempts = 0;
        int totalSuccess = 0;

        GpioPin _pin = null;
        private IDht _dht = null;
        private List<int> _retryCount = new List<int>();
        private DateTimeOffset _startedAt = DateTimeOffset.MinValue;

        DeviceClient deviceClient;
        string deviceName = "---";

        public MainPage()
        {
            this.InitializeComponent();
            var iotDevice = new TpmDevice(0);
            string hubUri = iotDevice.GetHostName();
            string deviceId = iotDevice.GetDeviceId();
            string sasToken = iotDevice.GetSASToken();

            deviceName = deviceId;

            deviceClient = DeviceClient.Create(hubUri,
                AuthenticationMethodFactory.CreateAuthenticationWithToken(deviceId, sasToken), TransportType.Mqtt);
           

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += _timer_Tick;

            GpioController controller = GpioController.GetDefault();

            if (controller != null)
            {
                _pin = GpioController.GetDefault().OpenPin(17, GpioSharingMode.Exclusive);
                _dht = new Dht11(_pin, GpioPinDriveMode.Input);
                _timer.Start();
                _startedAt = DateTimeOffset.Now;
            }
        }



        private async void _timer_Tick(object sender, object e)
        {
            float temperature;
            DhtReading reading = new DhtReading();
            totalAttempts++;

            reading = await _dht.GetReadingAsync().AsTask();

            _retryCount.Add(reading.RetryCount);
            

			if (reading.IsValid)
			{
				totalSuccess++;
                temperature = Convert.ToSingle(reading.Temperature);


                var telemetryDataPoint = new
                {
                    messageId = totalSuccess,
                    deviceId = deviceName,
                    temperature = temperature,
                    humidity = Convert.ToSingle(reading.Humidity),
                    lastUpdated = DateTimeOffset.Now,
                    successRate = this.SuccessRate,
                    percentSuccess = this.PercentSuccess
                };

                var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
                var message = new Message(Encoding.ASCII.GetBytes(messageString));
                message.Properties.Add("temperatureAlert", (temperature > 24) ? "true" : "false");

                await deviceClient.SendEventAsync(message);

            }
        }

        public string PercentSuccess
        {
            get
            {
                string returnValue = string.Empty;

                int attempts = totalAttempts;

                if (attempts > 0)
                {
                    returnValue = string.Format("{0:0.0}%", 100f * (float)totalSuccess / (float)attempts);
                }
                else
                {
                    returnValue = "0.0%";
                }

                return returnValue;
            }
        }

        

        public string SuccessRate
        {
            get
            {
                string returnValue = string.Empty;

                double totalSeconds = DateTimeOffset.Now.Subtract(_startedAt).TotalSeconds;
                double rate = totalSuccess / totalSeconds;

                if (rate < 1)
                {
                    returnValue = string.Format("{0:0.00} seconds/reading", 1d / rate);
                }
                else
                {
                    returnValue = string.Format("{0:0.00} readings/sec", rate);
                }

                return returnValue;
            }
        }
    }
}
