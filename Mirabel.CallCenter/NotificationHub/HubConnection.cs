using Microsoft.AspNetCore.SignalR.Client;

namespace Mirabel.CallCenter.NotificationHub
{
    public class HubClientConnection
    {

        public static HubConnection _hubConnection;
        public static void RegisterHub()
        {
            if(_hubConnection==null)
            {
                //var hubconnection = new HubConnectionBuilder().WithUrl("http://localhost:1234/Notifier").Build();
                //hubconnection.StartAsync();
                //_hubConnection = hubconnection;
            } 
        }
    }
}
