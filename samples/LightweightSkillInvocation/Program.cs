using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Skills;
using Microsoft.Bot.Builder.Skills.Auth;
using Microsoft.Bot.Builder.Skills.Models.Manifest;
using Microsoft.Bot.Schema;
using Microsoft.Bot.StreamingExtensions;
using Microsoft.Bot.StreamingExtensions.Transport.WebSockets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LightweightDialog
{
    public class Program
    {
        static void Main(string[] args)
        {
            Task t = MainAsync(args);
            t.Wait();
        }

        static async Task MainAsync(string[] args)
        {
            // To be removed after refactoring.
            IStorage storage = new Microsoft.Bot.Builder.MemoryStorage();
            var userState = new UserState(storage);

            // Retrieve a SkillManifest instance from the manifest endpoint of the Skill
            var skillManifest =  GetManifestInstance(new Uri("https://djskillwithactions-atb6vky.azurewebsites.net/api/skill/manifest"));

            // Credentials for skill-level authentication
            var credentials = new MicrosoftAppCredentialsEx("YOUR_APP_ID_HERE", "YOUR_SECRET_HERE", skillManifest.MSAappId);
            
            // Initialise WebSocket transport
            var wsClient = new WebSocketClient(EnsureWebSocketUrl(skillManifest.Endpoint.ToString()), new SkillRequestHandler());
            var skillTransport = new SkillWebSocketTransport(new NullBotTelemetryClient(), wsClient);

            // Prepare the SkillDialog (UserState will be removed as a requirement)
            var skill = new SkillDialog(skillManifest, credentials, new NullBotTelemetryClient(), userState, null, skillTransport);

            // SkillDialog can't be invoked directly through BeginDialog with DialogHost so we currently have to use an Activity
            // rather than just pass a SemanticAction through DialogOptions.
            var activity = new Activity();
            activity.From = new ChannelAccount(id: Guid.NewGuid().ToString());
            activity.Conversation = new ConversationAccount(id: Guid.NewGuid().ToString());
            activity.Recipient = new ChannelAccount(id: Guid.NewGuid().ToString());
            activity.ChannelId = "testchannel"; // can set to emulator if you want to receive trace activities for debug

            // Temporarily required to avoid RouterDialog filtering out of empty messages
            activity.Type = "message";
            activity.Text = "dummy";
            activity.Locale = "en-us";

            // Initially defined slot per item
            var slots = new Dictionary<string, Entity>();
            slots.Add("title", new Entity { 
                Properties = JObject.FromObject(new KeyValuePair<string, string>("Text", "Planning Meeting"))});
            slots.Add("content", new Entity { 
                Properties = JObject.FromObject(new KeyValuePair<string, string>("Text", "Booking some time for our planning"))});

            // Testing structured objects approach
            var dummyObject = new DummyObject();
            dummyObject.Name = "name";
            dummyObject.Number = 2;
            slots.Add("complexObject", new Entity { 
                Properties = JObject.FromObject(dummyObject)});

            // Invoke the 'action1' action and pass the slots
            activity.SemanticAction = new SemanticAction("action1", slots);

            System.Diagnostics.Trace.WriteLine(JsonConvert.SerializeObject(activity));
           
            // State key
            string key = $"{activity.ChannelId}/conversations/{activity.Conversation?.Id}";

            IStore store = new MemoryStore();

            // Retrieve state and run the dialog
            var (oldState, etag) = await store.LoadAsync(key);
            var (activities, newState) = await DialogHost.RunAsync(skill, activity, oldState, default(CancellationToken));
            
            // Save the updated state associated with this key.
            bool success = await store.SaveAsync(key, newState, etag);

            // Following a successful save, send any outbound Activities
            if (success)
            {
                if (activities.Any())
                {
                    foreach(var responseActivity in activities)
                    {
                        {
                            // In this sample these will be trace activities if enabled
                            Console.WriteLine(responseActivity.Text);
                        }
                    }
                }
            }

            Console.WriteLine("Press enter key to exit");
            Console.ReadLine();
        }

        // Invoked for each message received from the Skill        
        public class SkillRequestHandler : RequestHandler
        {
            public async override Task<StreamingResponse> ProcessRequestAsync(ReceiveRequest request, ILogger<RequestHandler> logger, object context = null, CancellationToken cancellationToken = default)
            {
                var response = new StreamingResponse();

                var body = request.ReadBodyAsString();
                if (string.IsNullOrEmpty(body) || request.Streams?.Count == 0)
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;

                    return response;
                }

                if (request.Streams.Where(x => x.ContentType != "application/json; charset=utf-8").Any())
                {
                    response.StatusCode = (int)HttpStatusCode.NotAcceptable;

                    return response;
                }

                var activity = JsonConvert.DeserializeObject<Activity>(body);

                if (activity.Type == ActivityTypes.Message && activity.SemanticAction != null)
                {
                    Console.WriteLine($"Received SemanticAction: {activity.SemanticAction.State}");
                    foreach(var e in activity.SemanticAction.Entities)
                    {
                        String values = string.Join(",",e.Value.Properties);
                        Console.WriteLine($"{e.Key}:{values}");
                    }
                }
                else if (activity.Type == ActivityTypes.Trace)
                {
                    if (!string.IsNullOrEmpty(activity.Text))
                    {
                        Console.WriteLine($"Trace: {activity.Text}");
                    }
                }

                response.StatusCode = (int)HttpStatusCode.OK;

                return response;
            }
        }

        static SkillManifest GetManifestInstance(Uri uri)
        {
            var json = new WebClient().DownloadString(uri);
            return JsonConvert.DeserializeObject<SkillManifest>(json);
        }

        private static string EnsureWebSocketUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentNullException(nameof(url), "url is empty!");
            }

            var httpPrefix = "http://";
            var httpsPrefix = "https://";
            var wsPrefix = "ws://";
            var wssPrefix = "wss://";

            if (url.StartsWith(httpPrefix))
            {
                return url.Replace(httpPrefix, wsPrefix);
            }
            else if (url.StartsWith(httpsPrefix))
            {
                return url.Replace(httpsPrefix, wssPrefix);
            }

            return url;
        }
    }

    public class DummyObject
    {
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }
        public int Number
        {
            get { return _number; }
            set { _number = value; }
        }
        private string _name;
        private int _number;
    }
}
