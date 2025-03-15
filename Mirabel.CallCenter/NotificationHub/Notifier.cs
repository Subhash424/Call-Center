using Microsoft.AspNetCore.SignalR;
using Mirabel.CallCenter.Models.Twilio;
using MongoDB.Driver.Core.Connections;
using System.Dynamic;
using Twilio.Http;
using Twilio.Types;

namespace Mirabel.CallCenter.NotificationHub
{
    public class Users
    {
        public string ConnectionID { get; set; }
        public string Identity { get; set; } //email

    }

    public class Notifier:Hub
    {
        
        public static List<Users> ConnectedUsers = new List<Users>();

        public Task UserConnected(string Identity)
        {
            var index = ConnectedUsers.FindIndex(x => x.Identity == Identity);
            if (index==-1)
            {
                var userInfo = new Users()
                {
                    Identity = Identity,
                    ConnectionID = Context.ConnectionId
                };
                ConnectedUsers.Add(userInfo);
            }
            else
            {
                ConnectedUsers[index].ConnectionID = Context.ConnectionId;
            }
            return base.OnConnectedAsync();
        }

        public Task UserDisconnected(string Identity)
        {
            var userInfo = ConnectedUsers.Find(x => x.Identity == Identity);
            if (userInfo != null)
            {
                ConnectedUsers.Remove(userInfo);
            }
            return base.OnDisconnectedAsync(null);
        }

        public  void CallLogNotifier(Conversation result,string Identity)
        {
            var connectionID = ConnectedUsers.Find(x => x.Identity == Identity)?.ConnectionID;
            Clients.Client(connectionID).SendAsync("callLogNotifier", result);

        }


        public void EndConference(string Identity)
        {
            var connectionID = ConnectedUsers.Find(x => x.Identity == Identity)?.ConnectionID;
            Clients.Client(connectionID).SendAsync("endConference");

        }

        public void UpdateActiveCallStatus(CallsHistory callRecord)
        {
            var connectionID = ConnectedUsers.Find(x => x.Identity == callRecord.Identity)?.ConnectionID;
            if(!string.IsNullOrEmpty(connectionID))
            {
                Clients.Client(connectionID).SendAsync("updateCallStatus", callRecord);
            }
        }



    }
}
