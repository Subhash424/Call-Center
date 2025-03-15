using Microsoft.AspNetCore.Mvc;
using System.Dynamic;
using Twilio;
using Twilio.AspNet.Core;
using Twilio.Jwt.AccessToken;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Rest.Intelligence.V2;
using Twilio.Rest.Intelligence.V2.Transcript;
using Twilio.TwiML;
using Twilio.TwiML.Voice;
using static Twilio.TwiML.Voice.Dial;
using Mirabel.CallCenter.Models.Twilio;
using Microsoft.AspNetCore.SignalR.Client;
using Twilio.Types;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using Mirabel.CallCenter.Common;
using System.Runtime.CompilerServices;
using Conversation = Mirabel.CallCenter.Models.Twilio.Conversation;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualBasic;

namespace TwilioApp.Controllers
{
    //api/Twilio/MakeCallPhoneToBrowser
    [Route("api/[controller]")]
    [ApiController]
    public class TwilioController : ControllerBase
    {

        private string AccountSid;
        private string AuthToken;
        private string ApiSid;
        private string AuthSid;
        private string TwiMlID;
        private bool EnableTranscription;
        private string UserPhoneNumber;
        private string FromPhoneMumber;
        private string WebHookUrl;
        private string VerifySid;
        public static List<CallsHistory> CallsHistories = new();
        public static List<Conversation> TranscriptionList = new();

        public static object _object = new();
        public TwilioController(IConfiguration configuration)
        {
            AccountSid = configuration["TwilioSettings:AccountSid"].ToString();
            AuthToken = configuration["TwilioSettings:AuthToken"].ToString();
            ApiSid = configuration["TwilioSettings:ApiSid"].ToString();
            AuthSid = configuration["TwilioSettings:AuthSid"].ToString();
            TwiMlID = configuration["TwilioSettings:TwiMlID"].ToString();
            EnableTranscription = Convert.ToBoolean(configuration["TwilioSettings:EnableTranscription"].ToString());
            UserPhoneNumber = configuration["TwilioSettings:UserPhoneNumber"].ToString();
            FromPhoneMumber = configuration["TwilioSettings:FromPhoneNumber"].ToString();
            WebHookUrl = configuration["TwilioSettings:WebHookUrl"].ToString();
            VerifySid = configuration["TwilioSettings:VerifySid"].ToString();
            TwilioClient.Init(AccountSid, AuthToken);
        }


        //Token method
        [HttpPost("token")]
        public async Task<IActionResult> GenerateToken(ExpandoObject obj)
        {
            try
            {
                var reqDict = new Dictionary<string, object>(obj);
                var identity = reqDict["UserName"].ToString();
                var grants = new HashSet<IGrant>
            {
                 new VoiceGrant
                 {
                   OutgoingApplicationSid=TwiMlID,
                   IncomingAllow = true// This grants the client permission to receive incoming calls
               
                 }
            };
                var token = new Token(AccountSid, ApiSid, AuthSid, identity, grants: grants);
                return Ok(new { token = token.ToJwt() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error in GenerateToken", message = ex.Message, stackTrace = ex.StackTrace });
            }
        }


        //Getting register numbers in twilio
        [HttpGet("GetVerifiedNumbers")]
        public IActionResult GetVerifiedNumbers()
        {
            try
            {
                List<string> numbers = new List<string>();
                var PhoneNumbers = OutgoingCallerIdResource.Read();
                if (PhoneNumbers != null)
                {
                    numbers = PhoneNumbers.Select(x => x.PhoneNumber.ToString()).ToList();
                }
                return Ok(numbers);

            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message.ToString());
            }
        }


        #region Incoming Calls

        //Need to check this how it works 
        [HttpPost("MakeCallFromPhoneToBrowser")]
        public IActionResult MakeCallFromPhoneToBrowser([FromForm] IFormCollection request)
        {
            //Need to take clientID from the request
            var ClientID = CallsHistories.Last(x => x.To == request["From"].ToString())?.Identity;
            var response = new VoiceResponse();
            response.Say("Thank you for calling to Mirabel technologies");
            response.Pause(1);
            response.Say("Please be on the line");
            var dial = new Dial();
            dial.Record = RecordEnum.RecordFromAnswer;
            var ConferenceID = Guid.NewGuid().ToString();
            dial.Client(identity: ClientID, statusCallback: new Uri($"{WebHookUrl}/api/Twilio/singleCallUpdate/{ConferenceID}"),
            statusCallbackEvent: new List<Twilio.TwiML.Voice.Client.EventEnum> { Twilio.TwiML.Voice.Client.EventEnum.Ringing, Twilio.TwiML.Voice.Client.EventEnum.Answered },
            statusCallbackMethod: Twilio.Http.HttpMethod.Post);
            dial.Action = new Uri($"{WebHookUrl}/api/Twilio/singleCallUpdate/{ConferenceID}");
            dial.Method = Twilio.Http.HttpMethod.Post;
            response.Append(dial);
            response.Record(transcribe: EnableTranscription);
            var newRecord = new CallsHistory()
            {
                ConferenceID = ConferenceID,
                CallType = "Incoming",
                From = request["From"],
                To = request["To"],
                Status = "Not connected",
                CreatedDate = DateTime.Now
            };
            CallsHistories.Add(newRecord);
            return Results.Extensions.TwiML(response);
        }


        #endregion

        [HttpPost("MakeCallFromBrowserToPhone")]
        public async Task<IActionResult> MakeCallFromBrowserToPhone([FromForm] IFormCollection request)
        {
            var identity = request["Caller"][0].Split(":")[1];
            var voice = new VoiceResponse();
            var dial = new Dial();
            var conferenceID = Guid.NewGuid().ToString();
            dial.CallerId = FromPhoneMumber;
            dial.Record = RecordEnum.RecordFromAnswerDual;
            if (Convert.ToBoolean(request["IsConference"]))
            {
                // Conference Call
                var participants = request["Participants"];
                dial.Conference(conferenceID,
                    startConferenceOnEnter: false,
                    endConferenceOnExit: true,
                    waitUrl: new Uri($"{WebHookUrl}/api/Twilio/empty-wait"),
                    statusCallbackEvent: new List<Conference.EventEnum> { Conference.EventEnum.Join, Conference.EventEnum.Leave },
                    statusCallback: new Uri($"{WebHookUrl}/api/Twilio/conference-status"),
                    statusCallbackMethod: Twilio.Http.HttpMethod.Post
                );
                //AutoCancelled(conferenceID);
                ConferenceDial(conferenceID, participants, identity);
            }
            else
            {

                // Single Call
                dial.Number(request["ToPhoneNumber"].ToString(), statusCallback: new Uri($"{WebHookUrl}/api/Twilio/singleCallUpdate/{conferenceID}"),
                  statusCallbackEvent: new List<Number.EventEnum> { Number.EventEnum.Ringing, Number.EventEnum.Answered },
                  statusCallbackMethod: Twilio.Http.HttpMethod.Post);
                dial.Action = new Uri($"{WebHookUrl}/api/Twilio/singleCallUpdate/{conferenceID}");
                dial.Method = Twilio.Http.HttpMethod.Post;
            }

            // Save call history
            var newRecord = new CallsHistory()
            {
                ConferenceID = conferenceID,
                CallType = "Outgoing",
                Identity = identity,
                To = request["ToPhoneNumber"],
                From = FromPhoneMumber,
                Status = "Not Connected",
                CreatedDate = DateTime.Now
            };
            CallsHistories.Add(newRecord);
            voice.Append(dial);
            voice.Record(transcribe: EnableTranscription);
            return Results.Extensions.TwiML(voice);
        }


    
        //If it is a conference call then we need to call this method
        public async Task<IActionResult> ConferenceDial(string ConferenceID, string toPhoneNumbers, string Identity)
        {
            await System.Threading.Tasks.Task.Delay(1000);
            var UserNumbers = toPhoneNumbers.Split(",");
            foreach (var number in UserNumbers)
            {
                var callinfo = CallResource.Create(
                   to: new PhoneNumber(number),
                   from: new PhoneNumber(FromPhoneMumber),
                   url: new Uri($"{WebHookUrl}/api/Twilio/join-conference/{ConferenceID}/{number}"),
                   method: Twilio.Http.HttpMethod.Get,
                   statusCallback: new Uri($"{WebHookUrl}/api/Twilio/IndividualCallStatus"), // Webhook to receive status updates
                   statusCallbackMethod: Twilio.Http.HttpMethod.Post,
                   statusCallbackEvent: new List<string> { "ringing", "completed", "busy", "failed", "no-answer", "canceled" }
               );
                var newRecord = new CallsHistory()
                {
                    To = number,
                    From = FromPhoneMumber,
                    CallsID = callinfo.Sid,
                    ConferenceID = ConferenceID,
                    CallType = callinfo.Direction.ToString(),
                    Status = callinfo.Status.ToString(),
                    PhoneNumberSID = callinfo.PhoneNumberSid,
                    Identity = Identity,
                    CreatedDate = DateTime.Now
                };
                CallsHistories.Add(newRecord);
            }
            return Ok(new { message = "Conference participants are being added" });
        }




        [HttpPost("SingleCallUpdate/{conferenceID}")]
        public void singleCallUpdate(string conferenceID, [FromForm] IFormCollection data)
        {
            var callRecord = CallsHistories.FirstOrDefault(x => x.ConferenceID == conferenceID);
            string key = data.ContainsKey("ParentCallsID") == false ? "action-" + data["CallStatus"] : data["CallStatus"];
            string CallStatus = Resource.CallStatus[key];
            callRecord.CallsID = data["CallsID"];
            if (CallStatus == "No answer" || CallStatus == "Completed")
            {
                CallStatus = callRecord.Status == "Answered" ? "Completed" : (CallStatus == "Completed" ? "Cancelled" : CallStatus);
            }
            callRecord.Status = CallStatus;
            Configuration.hubConnection.InvokeAsync("UpdateActiveCallStatus", callRecord).GetAwaiter().GetResult();
            if (CallStatus is "Completed" or "No answer" or "Cancelled")
            {
                CallResource.Update(pathSid: callRecord.CallsID, status: CallResource.UpdateStatusEnum.Completed);
            }
        }


        //Only for conference Calls
        [HttpPost("IndividualCallStatus")]
        public void IndividualCallStatus([FromForm] IFormCollection data)
        {
            lock(_object)
            {
                string CallStatus = Resource.CallStatus[data["CallStatus"]];
                var callRecord = CallsHistories.FirstOrDefault(x => x.CallsID == data["CallsID"]);
                if (callRecord != null && callRecord.Status != "Left")
                {
                    if (CallStatus == "Ringing")
                    {
                        callRecord.ParentCallsID = CallsHistories.FirstOrDefault(x => x.ConferenceID == callRecord.ConferenceID && x.CallType == "Outgoing")?.CallsID;
                    }
                    callRecord.Status = CallStatus;
                    StatusActions(callRecord, CallStatus).GetAwaiter().GetResult();
                }

            }
        }

        private async Task StatusActions(CallsHistory callRecord, string Status = "Completed")
        {
            bool IsEndCall = CallsHistories.Where(x => x.ConferenceID == callRecord.ConferenceID && (x.Status == "Answered" || x.Status == "Ringing")).Count() == 0;
            if (IsEndCall || Status == "Auto cancel")
            {
                if (CallsHistories.Any(x => x.ConferenceID == callRecord.ConferenceID && (x.Status == "Left")))
                {
                    Status = "Completed";
                }
                else
                {
                    callRecord.Annoncement = "No participants joined. The call has been aborted.";
                }
                var parentRecord = CallsHistories.FirstOrDefault(x => x.CallsID == callRecord.ParentCallsID);
                if (parentRecord != null)
                {
                    parentRecord.Status = Status;
                    await CallResource.UpdateAsync(pathSid: callRecord.ParentCallsID, status: CallResource.UpdateStatusEnum.Completed);
                }
            }
            await Configuration.hubConnection.InvokeAsync("UpdateActiveCallStatus", callRecord);
            callRecord.Annoncement = string.Empty;
        }

        private async Task AutoCancelled(string conferenceID)
        {
            await Task.Delay(30000);
            if (!CallsHistories.Any(x => x.Status == "Answered" && x.ConferenceID == conferenceID))
            {
                var callrecord = CallsHistories.FirstOrDefault(x => x.ConferenceID == conferenceID && !string.IsNullOrEmpty(x.ParentCallsID));
                if (callrecord != null)
                {
                    await StatusActions(callrecord, "Auto cancel");
                }
            }
        }


        [HttpGet("join-conference/{ConferenceID}/{phoneNumber}")]
        public IActionResult JoinConference(string ConferenceID, string phoneNumber)
        {
            var response = new VoiceResponse();
            var dial = new Dial();
            dial.Conference(ConferenceID,
                startConferenceOnEnter: true,
                endConferenceOnExit: false,
                participantLabel: phoneNumber
             );
            response.Append(dial);
            return Results.Extensions.TwiML(response);
        }


        [HttpPost("conference-status")]
        public async Task<IActionResult> ConferenceStatus([FromForm] Dictionary<string, string> formData)
        {
            string callSid = formData["CallSid"];
            string statusCallbackEvent = formData["StatusCallbackEvent"];
            string friendlyName = formData["FriendlyName"];
            string ParentStatus = Resource.CallStatus[statusCallbackEvent];
            //End All calls manually
            var parentRecord = CallsHistories.FirstOrDefault(x => x.CallsID == callSid && x.CallType == "Outgoing" &&(x.Status != "No answer"&&x.Status!="Failed"));
            bool IsCallEndCheck = (parentRecord != null && ParentStatus == "Left");
            if (IsCallEndCheck)
            {
                bool isCallCancelled = CallsHistories.Where(x => x.ConferenceID == parentRecord.ConferenceID && (x.Status == "Answered" || x.Status == "Left")).Count() == 0;
                if (isCallCancelled)
                {
                    var childRecords = CallsHistories.Where(x => x.ConferenceID == friendlyName && x.CallsID != callSid).ToList();
                    foreach (var call in childRecords)
                    {
                        call.Status = "Cancelled";
                        CallResource.Update(pathSid: call.CallsID, status: CallResource.UpdateStatusEnum.Completed);
                    }
                    parentRecord.Status = "Cancelled";
                    await Configuration.hubConnection.InvokeAsync("UpdateActiveCallStatus", parentRecord);
                    return null;
                }
            }
            // Update status if CallsID exists in CallsHistories
            var callRecord = CallsHistories.FirstOrDefault(x => x.CallsID == callSid && x.CallType != "Outgoing");
            if (callRecord != null)
            {
                callRecord.Status = Resource.CallStatus[statusCallbackEvent];
                await StatusActions(callRecord);
            }
            var conferenceRecord = CallsHistories.FirstOrDefault(x => x.ConferenceID == friendlyName && string.IsNullOrEmpty(x.CallsID));
            if (conferenceRecord != null)
            {
                conferenceRecord.CallsID = callSid;
            }
            return Ok();
        }


        [HttpGet("GetCallsHistory")]
        public IActionResult GetCallsHistory()
        {
            var recentCalls = new List<CallsHistory>();
            var records = CallsHistories.Where(x =>x.CallType=="Outgoing"||x.CallType=="Incoming").TakeLast(10).OrderByDescending(x => x.CreatedDate);
            foreach (var record in records)
            {
                var childCalls = CallsHistories.Where(x => x.ConferenceID == record.ConferenceID && x.CallsID!=record.CallsID).ToList();
                var from = childCalls.FirstOrDefault()?.From;
                var toArray = childCalls.Select(x => x.To)?.ToList<string>();
                var identity = childCalls.FirstOrDefault()?.Identity;

                var recentCall = new CallsHistory
                {
                    CallsID = record.CallsID, // Copy necessary properties
                    ConferenceID = record.ConferenceID,
                    ParentCallsID = record.ParentCallsID,
                    From = from ?? record.From,
                    To = toArray.Count > 0 ? string.Join(",", toArray) : record.To,
                    Status = record.Status,
                    CallType = record.CallType,
                    Identity = identity,
                };
                recentCalls.Add(recentCall);
            }
            return Ok(recentCalls);
        }


        [HttpGet("GetTranscriptionByCallsID/{callsID}")]
        public IActionResult GetTranscriptionByCallsID(string callsID)
        {
            var result = new Mirabel.CallCenter.Models.Twilio.Conversation { Transcription = new List<FinalTranscript>() };
            var recordingSid = RecordingResource.Read(callSid: callsID, limit: 1).FirstOrDefault()?.Sid;
            result = TranscriptionList.FirstOrDefault(x => x.RecordingID == recordingSid);
            return Ok(result);
        }

        [HttpGet("GetAllTranscriptions")]
        public IActionResult Transcriptions()
        {
            var result = new List<Mirabel.CallCenter.Models.Twilio.Conversation>();
            var data = TranscriptResource.Read(limit: 10);
            if (data.Count() > 0)
            {
                foreach (var item in data)
                {
                    var conversation = GetConversation(item.Sid);
                    result.Add(conversation);
                }
            }
            return Ok(result);
        }


        [HttpPost("empty-wait")]
        public IActionResult EmptyWait()
        {
            var response = new VoiceResponse();
            response.Say("Hi please be on the line", voice: "alice"); // Empty speech ensures silence
            return Results.Extensions.TwiML(response);
        }
        public Mirabel.CallCenter.Models.Twilio.Conversation GetConversation(string transcriptSid)
        {
            var result = new Mirabel.CallCenter.Models.Twilio.Conversation() { Transcription = new List<FinalTranscript>() };
            dynamic TransactionData = GetTranscriptionSummary(transcriptSid).GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(TransactionData?.Value))
            {
                result.ConversationSummary = TransactionData?.Value;
            }
            var transcriptData = TranscriptResource.Fetch(pathSid: transcriptSid);
            if (transcriptData != null)
            {
                JObject jsonObject = JObject.Parse(transcriptData.Channel?.ToString());
                string sourceSid = jsonObject["media_properties"]?["source_sid"]?.ToString();
                if (!string.IsNullOrEmpty(sourceSid))
                {
                    result.RecordingID = sourceSid;
                }
                var ParticipantData = Newtonsoft.Json.JsonConvert.DeserializeObject<Channel>(transcriptData.Channel?.ToString());
                if (ParticipantData != null)
                {
                    var sentenseResponse = SentenceResource.ReadAsync(
                           pathTranscriptSid: transcriptSid).GetAwaiter().GetResult();
                    if (sentenseResponse != null)
                    {
                        var convertedData = new List<SentenceResource>(sentenseResponse);
                        if (convertedData.Count > 0)
                        {
                            foreach (var sentense in convertedData)
                            {
                                var userInfo = ParticipantData.Participants.Where(x => x.UserID == sentense.MediaChannel).Select(x => x).First();
                                var transcript = new FinalTranscript()
                                {
                                    UserID = userInfo.UserID,
                                    Role = userInfo.Role,
                                    TranscriptText = sentense.Transcript
                                };
                                result.Transcription.Add(transcript);
                            }

                        }
                    }
                }
            }
            return result;
        }

        [HttpPost("transcriptions")]
        public void TranscriptionData(ExpandoObject data)
        {
            lock(_object)
            {
                //Need to store this with callsid
                var result = new Mirabel.CallCenter.Models.Twilio.Conversation() { Transcription = new List<FinalTranscript>() };
                var _data = new Dictionary<string, object>(data);
                if (_data != null && _data.ContainsKey("transcript_sid"))
                {
                    var transcriptSid = _data["transcript_sid"].ToString();
                    result = GetConversation(transcriptSid);
                }
                if (result.Transcription.Count > 0)
                {
                    TranscriptionList.Add(result);
                }
            }
           
        }

        public async Task<IActionResult> GetTranscriptionSummary(string TransitionId)
        {
            string url = $"https://intelligence.twilio.com/v2/Transcripts/{TransitionId}/OperatorResults";
            using (System.Net.Http.HttpClient client = new System.Net.Http.HttpClient())
            {
                try
                {
                    var byteArray = Encoding.ASCII.GetBytes($"{AccountSid}:{AuthToken}");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                    HttpResponseMessage response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        string result = await response.Content.ReadAsStringAsync();
                        var jobject = JObject.Parse(result);
                        string summary = jobject["operator_results"]?[0]["text_generation_results"]?["result"]?.ToString();
                        return Ok(summary);
                    }
                    else
                    {
                        return StatusCode((int)response.StatusCode, response.ReasonPhrase);
                    }
                }
                catch (Exception ex)
                {
                    return StatusCode(500, ex.Message.ToString());
                }
            }
        }
    }
}
