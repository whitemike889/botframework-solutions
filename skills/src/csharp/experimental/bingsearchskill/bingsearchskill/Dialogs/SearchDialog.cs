using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BingSearchSkill.Models;
using BingSearchSkill.Responses.Main;
using BingSearchSkill.Responses.Search;
using BingSearchSkill.Services;
using HtmlAgilityPack;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Solutions.Responses;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;

namespace BingSearchSkill.Dialogs
{
    public class SearchDialog : SkillDialogBase
    {
        private BotServices _services;
        private IStatePropertyAccessor<SkillState> _stateAccessor;

        public SearchDialog(
            BotSettings settings,
            BotServices services,
            ResponseManager responseManager,
            ConversationState conversationState,
            IBotTelemetryClient telemetryClient)
            : base(nameof(SearchDialog), settings, services, responseManager, conversationState, telemetryClient)
        {
            _stateAccessor = conversationState.CreateProperty<SkillState>(nameof(SkillState));
            _services = services;
            Settings = settings;

            var sample = new WaterfallStep[]
            {
                PromptForQuestion,
                ShowResult,
                End,
            };

            AddDialog(new WaterfallDialog(nameof(SearchDialog), sample));

            InitialDialogId = nameof(SearchDialog);
        }

        private async Task<DialogTurnResult> PromptForQuestion(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            GetEntityFromLuis(stepContext);

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> ShowResult(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var state = await _stateAccessor.GetAsync(stepContext.Context);
            var intent = state.LuisResult.TopIntent().intent;

            // Default
            Activity responseActivity = ResponseManager.GetResponse(SearchResponses.NoResultPrompt);

            var userInput = state.LuisResult.Text;

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-Uqu-RefererType", "1");
            httpClient.DefaultRequestHeaders.Add("X-Uqu-ResponseFormat", "0");
            httpClient.DefaultRequestHeaders.Add("opal-sessionid", "F96b98c24e25b152cf3790");
            httpClient.DefaultRequestHeaders.Add("X-Search-ClientId", "darrenj");
            httpClient.DefaultRequestHeaders.Add("muid", "1234");

            try
            {
                string bingUrl;
                if (Settings.Properties.TryGetValue("OpalUri", out bingUrl))
                {
                    // Retrieve Bing Response
                    var httpResponse = await httpClient.GetAsync(string.Format(bingUrl, userInput));
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        // Response is an HTML document. We need to parse and find the root level script node (only one) and then parse the contents to find the JSON payload.
                        // Excessive debugging to help triage issues in initial testing only!

                        HtmlDocument doc = new HtmlDocument();
                        doc.Load(await httpResponse.Content.ReadAsStreamAsync());

                        HtmlNodeCollection links = doc.DocumentNode.SelectNodes("/script");
                        if (links != null && links.Count == 1)
                        {
                            // The JSON payload is sandwiched between two single quotes within the identified script element.
                            var parts = links[0].InnerText.Split('\'');
                            if (parts.Length == 3)
                            {
                                // Middle part has the JSON payload. We need to HTML decode and Unescape
                                dynamic jsonPayload = JsonConvert.DeserializeObject(Regex.Unescape(WebUtility.HtmlDecode(parts[1])));

                                if (jsonPayload != null)
                                {
                                    if (jsonPayload.messageType == "spokenResponse")
                                    {
                                        responseActivity.Text = responseActivity.Speak = jsonPayload.fallbackSpokenText;
                                    }
                                    else
                                    {
                                        TelemetryClient.TrackTrace($"Question:{userInput} didn't get a spokenResponse from Bing", Severity.Error, null);
                                    }
                                }
                                else
                                {
                                    TelemetryClient.TrackTrace($"Question:{userInput} didn't result in a JSON response from Bing (JSON deserialization error)", Severity.Error, null);
                                }
                            }
                            else
                            {
                                TelemetryClient.TrackTrace($"Question:{userInput} didn't result in a JSON response from Bing (no JSON tag found)", Severity.Error, null);
                            }
                        }
                        else
                        {
                            TelemetryClient.TrackTrace($"Question:{userInput} didn't result in a JSON response from Bing (No root script tag found)", Severity.Error, null);
                        }
                    }
                    else
                    {
                        TelemetryClient.TrackTrace($"Question:{userInput} generated an HTTP failure when talking to Bing: {httpResponse.StatusCode}", Severity.Error, null);
                    }
                }
                else
                {
                    throw new Exception("OpalUri not provided in configuration.");
                }
            }
            catch (Exception e)
            {
                TelemetryClient.TrackException(e);
                responseActivity = ResponseManager.GetResponse(MainResponses.ErrorMessage);
            }

            await stepContext.Context.SendActivityAsync(responseActivity);
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> End(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var state = await _stateAccessor.GetAsync(stepContext.Context);
            state.Clear();

            return await stepContext.EndDialogAsync();
        }

        private async void GetEntityFromLuis(WaterfallStepContext stepContext)
        {
            var state = await _stateAccessor.GetAsync(stepContext.Context);

            if (state.LuisResult.Entities.MovieTitle != null)
            {
                state.SearchEntityName = state.LuisResult.Entities.MovieTitle[0];
                state.SearchEntityType = SearchResultModel.EntityType.Movie;
            }
            else if (state.LuisResult.Entities.MovieTitlePatten != null)
            {
                state.SearchEntityName = state.LuisResult.Entities.MovieTitlePatten[0];
                state.SearchEntityType = SearchResultModel.EntityType.Movie;
            }
            else if (state.LuisResult.Entities.CelebrityName != null)
            {
                state.SearchEntityName = state.LuisResult.Entities.CelebrityName[0];
                state.SearchEntityType = SearchResultModel.EntityType.Person;
            }
            else if (state.LuisResult.Entities.CelebrityNamePatten != null)
            {
                state.SearchEntityName = state.LuisResult.Entities.CelebrityNamePatten[0];
                state.SearchEntityType = SearchResultModel.EntityType.Person;
            }
        }

        private class DialogIds
        {
        }
    }
}
