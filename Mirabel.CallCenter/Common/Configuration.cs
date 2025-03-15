using Microsoft.AspNetCore.SignalR.Client;

namespace Mirabel.CallCenter.Common
{
    public static class Configuration
    {
        private static IConfiguration _configuration;
        public static HubConnection hubConnection { get; private set; }

        public static void SetConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
            RegisterHub();
        }

        public static IConfiguration GetConfiguration()
        {
            return _configuration;
        }

        public static async Task RegisterHub()
        {
            if(hubConnection==null)
            {
               hubConnection = new HubConnectionBuilder().WithUrl(_configuration["TwilioSettings:WebHookUrl"] + "/Notifier").WithAutomaticReconnect(new TimeSpan[] { TimeSpan.FromSeconds(5) }).Build();
               await Task.Delay(1000);
               await hubConnection.StartAsync();
            }
        }
    }
}
