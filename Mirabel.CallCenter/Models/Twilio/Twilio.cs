using Newtonsoft.Json;

namespace Mirabel.CallCenter.Models.Twilio
{
    public class participant
    {
        [JsonProperty("channel_participant")]
        public int UserID { get; set; }
        
        [JsonProperty("role")]
        public string Role { get; set; }
    }

    public class Channel
    {
        public List<participant> Participants { get; set; }
    }

    public class Conversation
    {
        public string RecordingID { get; set; }
        public string ConversationSummary { get; set; }
        public List<FinalTranscript> Transcription { get; set; }
    }

    public class FinalTranscript:participant
    {
        public string TranscriptText { get; set; }
    }



    public class CallsHistory
    {

        public string CallsID { get;set; }
        public string ParentCallsID { get;set; }

        public string Status { get; set;}

        public string From { get; set; }
        public string To { get; set; }

        public string ConferenceID { get; set; }

        public string CallType { get; set; }

        public string PhoneNumberSID { get; set; }

        public string Identity { get; set; }

        public DateTime CreatedDate { get; set; }

        public TimeOnly Duration { get; set; }

        public string Annoncement { get; set; }


    }

    public record Resource {
        public static Dictionary<string, string> CallStatus = new()
        {
            {
                "participant-join","Answered"
            },
            {
                "in-progress","Answered"
            },
            {
                "participant-leave","Left"
            },
            {
                "ringing","Ringing"
            },
            {
                "no-answer","No answer"
            },
            {
                "action-completed","Completed"
            },
            {
                "completed","Completed"
            },
            {
                "action-in-progress","No answer"
            },
            {
                "busy","No answer"
            },
            {
                "failed","Failed"
            }
        };

    }
  

}
