using System;
using System.Diagnostics;
using Windows.Devices.Gpio;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// 
// This program was written by Mark P. Kleine - 9/10/2015
// This program will run on a Windows IoT device, and will toggle the GPIO bit 5 low at sunset, and high at sunrise.
// The lat/long must be provided.
// The computer must be set for the proper timezone, and daylight saving time status.
// Use at your own risk, your mileage may vary...
//

namespace SunTimer
{
    public sealed partial class MainPage : Page
    {
        private const int GPIO_CHANNEL = 5;
        private GpioPin channel;
        private GpioPinValue channelValue;

        private DispatcherTimer timerOn;
        private DispatcherTimer timerOff;

        private SolidColorBrush redDot = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush blackDot = new SolidColorBrush(Windows.UI.Colors.Black);

        private double latLocal = 35.1515;
        private double lonLocal = -97.2919;
        
        public MainPage()
        {
            InitializeComponent();

            DateTime date = DateTime.Today;
            DateTime sunrise = DateTime.Now;
            DateTime sunset = DateTime.Now;
            DateTime sunriseToday = DateTime.Now;
            DateTime sunsetToday = DateTime.Now;
            DateTime sunriseTomorrow = DateTime.Now;
            DateTime sunsetTomorrow = DateTime.Now;
            bool isSunrise;
            bool isSunset;

            bool initStatus = false;

            // Calculate Sunrise/Sunset Times for today
            sunCheck today = new sunCheck();
            today.checkDate = date;
            today.lat = latLocal;
            today.lon = lonLocal;
            bool todayok = today.checkTime();

            // Calculate Sunrise/Sunset Times for tomoorow
            sunCheck tomorrow = new sunCheck();
            tomorrow.checkDate = date.AddDays(1);
            tomorrow.lat = latLocal;
            tomorrow.lon = lonLocal;
            bool tomorrowok = tomorrow.checkTime();

            // If today's sunrise has already passed, use tomorrows
            if (today.sunrise < DateTime.Now)
            {
                sunrise = tomorrow.sunrise;
                isSunrise = tomorrow.isSunrise;
            }
            else
            {
                sunrise = today.sunrise;
                isSunrise = today.isSunrise;
            }

            // If today's sunset has already passed, use tomorrows
            if (today.sunset < DateTime.Now)
            {
                sunset = tomorrow.sunset;
                isSunset = tomorrow.isSunset;
            }
            else
            {
                sunset = today.sunset;
                isSunset = today.isSunset;
            }


            // Set the initial condition for the light
            if ((!isSunrise)|(!isSunset))
            { // Set the initial condition for 'no sunrise or sunset' (near the poles)
                if (today.lat > 0) { // Northern Hemisphere
                    if ((DateTime.Now.Month < 3) | (DateTime.Now.Month > 9))
                    { // Winter
                        initStatus = true;
                        LED.Fill = redDot;
                    }
                } else { // Southern Hemisphere
                    if ((DateTime.Now.Month >= 3) & (DateTime.Now.Month <= 9))
                    { // Winter
                        initStatus = true;
                        LED.Fill = redDot;
                    }
                }
            } else { // Set the initial condition for 'normal sunrise-sunset'
                if ((today.sunset < DateTime.Now) | (today.sunrise > DateTime.Now))
                { // Sun is currently down
                    initStatus = true;
                    LED.Fill = redDot;
                }
            }

            // initialize the GPIO, and set the output to the proper value
            InitGPIO(initStatus);

            // Display the times...
            DelayCalcOn.Text = "Next Sunset: " + sunset.ToString();
            DelayCalcOff.Text = "Next Sunrise: " + sunrise.ToString();
            IoTTime.Text = "Last Action Time: " + DateTime.Now.ToString();
            
            // Setup the on timer
            timerOn = new DispatcherTimer();
            DateTime dton1 = sunset;
            DateTime dton2 = DateTime.Now;
            TimeSpan tson = dton1.Subtract(dton2);
            if (isSunset)
            { // normal locations
                timerOn.Interval = TimeSpan.FromMilliseconds(tson.TotalMilliseconds);
            } else
            { // near the poles (no sunrise/sunset)
                timerOn.Interval = TimeSpan.FromDays(1);
            }
            timerOn.Tick += Timer_Tick_On;

            // Setup the off timer
            timerOff = new DispatcherTimer();
            DateTime dtoff1 = sunrise;
            DateTime dtoff2 = DateTime.Now;
            TimeSpan tsoff = dtoff1.Subtract(dtoff2);
            if (isSunrise)
            { // normal locations
                timerOff.Interval = TimeSpan.FromMilliseconds(tsoff.TotalMilliseconds);
            }
            else
            { // near the poles (no sunrise/sunset)
                timerOff.Interval = TimeSpan.FromDays(1);
            }
            timerOff.Tick += Timer_Tick_Off;

            // Let the timers begin!!!
            if (channel != null)
            {
                timerOn.Start();
                timerOff.Start();
            }
        }

        // Initialize the GPIO, and set the initial condition based on initStatus. 
        private void InitGPIO(bool initStatus)
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                channel = null;
                return;
            }

            channel = gpio.OpenPin(GPIO_CHANNEL);

            if (initStatus)
            {
                channelValue = GpioPinValue.Low; // Turn on the SSR (low is 'on')
            }
            else
            {
                channelValue = GpioPinValue.High; // Turn off the SSR (high is 'off')
            }
            channel.Write(channelValue);
            channel.SetDriveMode(GpioPinDriveMode.Output);
        }

        // Handle the 'turn-on' timer tick condition
        private void Timer_Tick_On(object sender, object e)
        {
 
            // Turn on the SSR
            channelValue = GpioPinValue.Low;
            channel.Write(channelValue);

            // Change the screen color to signify the SSR is on
            LED.Fill = redDot;

            // Calculate the Sunrise/Sunset times for tomorrow
            sunCheck tomorrow = new sunCheck();
            tomorrow.checkDate = DateTime.Today.AddDays(1);
            tomorrow.lat = this.latLocal;
            tomorrow.lon = this.lonLocal;
            bool tomorrowok = tomorrow.checkTime();

            // Set up the timer for tomorrow's turn-on timer
            DispatcherTimer timerOn = (DispatcherTimer)sender;
            DateTime dton1 = tomorrow.sunset;
            DateTime dton2 = DateTime.Now;
            TimeSpan tson = dton1.Subtract(dton2);
            if (tomorrow.isSunset)
            {
                timerOn.Interval = TimeSpan.FromMilliseconds(tson.TotalMilliseconds);
            }
            else
            {
                timerOn.Interval = TimeSpan.FromDays(1);
            }

            // Update the screen times
            IoTTime.Text = "Last Action Time: " + DateTime.Now.ToString();
            DelayCalcOn.Text = "Next Sunset: " + tomorrow.sunset.ToString();

        }

        // Handle the 'turn-off' timer tick condition
        private void Timer_Tick_Off(object sender, object e)
        {

            // Turn off the SSR
            channelValue = GpioPinValue.High;
            channel.Write(channelValue);

            // Change the screen color to signify the off condition
            LED.Fill = blackDot;

            // Calculate sunrise/sunset times for tomorrow
            sunCheck tomorrow = new sunCheck();
            tomorrow.checkDate = DateTime.Today.AddDays(1);
            tomorrow.lat = this.latLocal;
            tomorrow.lon = this.lonLocal;
            bool tomorrowok = tomorrow.checkTime();

            // Set up timer for tomorrows turn-off time
            DispatcherTimer timerOff = (DispatcherTimer)sender;
            DateTime dtoff1 = tomorrow.sunrise;
            DateTime dtoff2 = DateTime.Now;
            TimeSpan tsoff = dtoff1.Subtract(dtoff2);
            if (tomorrow.isSunrise)
            {
                timerOff.Interval = TimeSpan.FromMilliseconds(tsoff.TotalMilliseconds);
            }
            else
            {
                timerOff.Interval = TimeSpan.FromDays(1);
            }

            // Update the screen times
            IoTTime.Text = "Last Action Time: " + DateTime.Now.ToString();
            DelayCalcOff.Text = "Next Sunrise: " + tomorrow.sunrise.ToString();
        }
    }
}