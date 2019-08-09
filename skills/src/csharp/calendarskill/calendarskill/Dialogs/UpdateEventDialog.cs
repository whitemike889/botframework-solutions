using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalendarSkill.Models;
using CalendarSkill.Models.DialogOptions;
using CalendarSkill.Prompts.Options;
using CalendarSkill.Responses.Shared;
using CalendarSkill.Responses.UpdateEvent;
using CalendarSkill.Services;
using CalendarSkill.Utilities;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.LanguageGeneration;
using Microsoft.Bot.Builder.Skills;
using Microsoft.Bot.Builder.Solutions.Responses;
using Microsoft.Bot.Builder.Solutions.Util;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;

namespace CalendarSkill.Dialogs
{
    public class UpdateEventDialog : CalendarSkillDialogBase
    {
        private ResourceMultiLanguageGenerator _lgMultiLangEngine;

        public UpdateEventDialog(
            BotSettings settings,
            BotServices services,
            ResponseManager responseManager,
            ConversationState conversationState,
            IServiceManager serviceManager,
            IBotTelemetryClient telemetryClient,
            MicrosoftAppCredentials appCredentials)
            : base(nameof(UpdateEventDialog), settings, services, responseManager, conversationState, serviceManager, telemetryClient, appCredentials)
        {
            _lgMultiLangEngine = new ResourceMultiLanguageGenerator("UpdateEventDialog.lg");

            TelemetryClient = telemetryClient;
            var updateEvent = new WaterfallStep[]
            {
                GetAuthToken,
                AfterGetAuthToken,
                GetMeetingsToUpdate,
                ChooseMeetingFromListPrompt,
                AfterChooseMeetingFromListPrompt,
                GetNewMeetingDetail,
                ConfirmBeforeUpdate,
                UpdateEventTime,
            };

            var chooseMeetingToUpdate = new WaterfallStep[]
            {
            };

            var updateNewStartTime = new WaterfallStep[]
            {
                GetNewEventTime,
                AfterGetNewEventTime,
            };

            // Define the conversation flow using a waterfall model.
            AddDialog(new WaterfallDialog(Actions.UpdateEventTime, updateEvent) { TelemetryClient = telemetryClient });
            AddDialog(new WaterfallDialog(Actions.ChooseMeetingToUpdate, chooseMeetingToUpdate) { TelemetryClient = telemetryClient });
            AddDialog(new WaterfallDialog(Actions.UpdateNewStartTime, updateNewStartTime) { TelemetryClient = telemetryClient });

            // Set starting dialog for component
            InitialDialogId = Actions.UpdateEventTime;
        }

        public async Task<DialogTurnResult> GetMeetingsToUpdate(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var state = await Accessor.GetAsync(sc.Context);

                if (state.ShowMeetingInfor.FocusedEvents.Count > 0)
                {
                    return await sc.NextAsync();
                }

                var calendarService = ServiceManager.InitCalendarService(state.APIToken, state.EventSource);

                if (state.UpdateMeetingInfor.OriginalStartDate.Any() || state.UpdateMeetingInfor.OriginalStartDate.Any())
                {
                    state.ShowMeetingInfor.FocusedEvents = await GetEventsByTime(state.UpdateMeetingInfor.OriginalStartDate, state.UpdateMeetingInfor.OriginalStartDate, state.UpdateMeetingInfor.OriginalEndDate, state.UpdateMeetingInfor.OriginalEndTime, state.GetUserTimeZone(), calendarService);
                    state.UpdateMeetingInfor.OriginalStartDate = new List<DateTime>();
                    state.UpdateMeetingInfor.OriginalStartDate = new List<DateTime>();
                    state.UpdateMeetingInfor.OriginalEndDate = new List<DateTime>();
                    state.UpdateMeetingInfor.OriginalEndTime = new List<DateTime>();
                    if (state.ShowMeetingInfor.FocusedEvents.Count > 0)
                    {
                        return await sc.NextAsync();
                    }
                }

                if (state.MeetingInfor.Title != null)
                {
                    state.ShowMeetingInfor.FocusedEvents = await calendarService.GetEventsByTitle(state.MeetingInfor.Title);
                    state.MeetingInfor.Title = null;
                    if (state.ShowMeetingInfor.FocusedEvents.Count > 0)
                    {
                        return await sc.NextAsync();
                    }
                }

                return await sc.PromptAsync(Actions.GetEventPrompt, new GetEventOptions(calendarService, state.GetUserTimeZone())
                {
                    Prompt = (Activity)await LGHelper.GenerateMessageAsync(_lgMultiLangEngine, sc.Context, "[NoUpdateStartTime]", null),
                    RetryPrompt = (Activity)await LGHelper.GenerateMessageAsync(_lgMultiLangEngine, sc.Context, "[EventWithStartTimeNotFound]", null)
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                await HandleDialogExceptions(sc, ex);
                return new DialogTurnResult(DialogTurnStatus.Cancelled, CommonUtil.DialogTurnResultCancelAllDialogs);
            }
        }

        public async Task<DialogTurnResult> ChooseMeetingFromListPrompt(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var state = await Accessor.GetAsync(sc.Context);

                if (sc.Result != null)
                {
                    state.ShowMeetingInfor.FocusedEvents = sc.Result as List<EventModel>;
                }

                if (state.ShowMeetingInfor.FocusedEvents.Count == 0)
                {
                    // should not doto this part. add log here for safe
                    await HandleDialogExceptions(sc, new Exception("Unexpect zero events count"));
                    return new DialogTurnResult(DialogTurnStatus.Cancelled, CommonUtil.DialogTurnResultCancelAllDialogs);
                }
                else
                if (state.ShowMeetingInfor.FocusedEvents.Count > 1)
                {
                    var options = new PromptOptions()
                    {
                        Choices = new List<Choice>(),
                    };

                    for (var i = 0; i < state.ShowMeetingInfor.FocusedEvents.Count; i++)
                    {
                        var item = state.ShowMeetingInfor.FocusedEvents[i];
                        var choice = new Choice()
                        {
                            Value = string.Empty,
                            Synonyms = new List<string> { (i + 1).ToString(), item.Title },
                        };
                        options.Choices.Add(choice);
                    }

                    var prompt = await GetGeneralMeetingListResponseAsync(sc, _lgMultiLangEngine, CalendarCommonStrings.MeetingsToChoose, state.ShowMeetingInfor.FocusedEvents, "MultipleEventsStartAtSameTime");

                    options.Prompt = prompt;

                    return await sc.PromptAsync(Actions.EventChoice, options);
                }
                else
                {
                    return await sc.EndDialogAsync(true);
                }
            }
            catch (SkillException ex)
            {
                await HandleDialogExceptions(sc, ex);
                return new DialogTurnResult(DialogTurnStatus.Cancelled, CommonUtil.DialogTurnResultCancelAllDialogs);
            }
            catch (Exception ex)
            {
                await HandleDialogExceptions(sc, ex);
                return new DialogTurnResult(DialogTurnStatus.Cancelled, CommonUtil.DialogTurnResultCancelAllDialogs);
            }
        }

        public async Task<DialogTurnResult> AfterChooseMeetingFromListPrompt(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            var state = await Accessor.GetAsync(sc.Context);

            if (sc.Result != null && state.ShowMeetingInfor.FocusedEvents.Count > 1)
            {
                var events = state.ShowMeetingInfor.FocusedEvents;
                state.ShowMeetingInfor.FocusedEvents = new List<EventModel>
                {
                    events[(sc.Result as FoundChoice).Index],
                };
            }

            return await sc.NextAsync();
        }

        public async Task<DialogTurnResult> GetNewMeetingDetail(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Get new meeting start time
            try
            {
                var state = await Accessor.GetAsync(sc.Context);
                if (sc.Result != null && sc.Result is FoundChoice && state.ShowMeetingInfor.FocusedEvents.Count > 1)
                {
                    var events = state.ShowMeetingInfor.FocusedEvents;
                    state.ShowMeetingInfor.FocusedEvents = new List<EventModel>
                {
                    events[(sc.Result as FoundChoice).Index],
                };
                }

                var origin = state.ShowMeetingInfor.FocusedEvents[0];
                if (!origin.IsOrganizer)
                {
                    await sc.Context.SendActivityAsync(await LGHelper.GenerateMessageAsync(_lgMultiLangEngine, sc.Context, "[NotEventOrganizer]", null));
                    state.Clear();
                    return await sc.EndDialogAsync(true);
                }
                else if (state.UpdateMeetingInfor.NewStartDateTime == null)
                {
                    return await sc.BeginDialogAsync(Actions.UpdateNewStartTime, new UpdateDateTimeDialogOptions(UpdateDateTimeDialogOptions.UpdateReason.NotFound));
                }
                else
                {
                    return await sc.NextAsync();
                }
            }
            catch (Exception ex)
            {
                await HandleDialogExceptions(sc, ex);
                return new DialogTurnResult(DialogTurnStatus.Cancelled, CommonUtil.DialogTurnResultCancelAllDialogs);
            }
        }

        public async Task<DialogTurnResult> GetNewEventTime(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var state = await Accessor.GetAsync(sc.Context);
                if (state.UpdateMeetingInfor.NewStartDate.Any() || state.UpdateMeetingInfor.NewStartTime.Any() || state.UpdateMeetingInfor.MoveTimeSpan != 0)
                {
                    return await sc.ContinueDialogAsync();
                }

                return await sc.PromptAsync(Actions.TimePrompt, new PromptOptions
                {
                    Prompt = (Activity)await LGHelper.GenerateMessageAsync(_lgMultiLangEngine, sc.Context, "[NoNewTime]", null),
                    RetryPrompt = (Activity)await LGHelper.GenerateMessageAsync(_lgMultiLangEngine, sc.Context, "[NoNewTimeRetry]", null)
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                await HandleDialogExceptions(sc, ex);
                return new DialogTurnResult(DialogTurnStatus.Cancelled, CommonUtil.DialogTurnResultCancelAllDialogs);
            }
        }

        public async Task<DialogTurnResult> AfterGetNewEventTime(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var state = await Accessor.GetAsync(sc.Context);
                if (state.UpdateMeetingInfor.NewStartDate.Any() || state.UpdateMeetingInfor.NewStartTime.Any() || state.UpdateMeetingInfor.MoveTimeSpan != 0)
                {
                    var originalEvent = state.ShowMeetingInfor.FocusedEvents[0];
                    var originalStartDateTime = TimeConverter.ConvertUtcToUserTime(originalEvent.StartTime, state.GetUserTimeZone());
                    var userNow = TimeConverter.ConvertUtcToUserTime(DateTime.UtcNow, state.GetUserTimeZone());

                    if (state.UpdateMeetingInfor.NewStartDate.Any() || state.UpdateMeetingInfor.NewStartTime.Any())
                    {
                        var newStartDate = state.UpdateMeetingInfor.NewStartDate.Any() ?
                            state.UpdateMeetingInfor.NewStartDate.Last() :
                            originalStartDateTime;

                        var newStartTime = new List<DateTime>();
                        if (state.UpdateMeetingInfor.NewStartTime.Any())
                        {
                            foreach (var time in state.UpdateMeetingInfor.NewStartTime)
                            {
                                var newStartDateTime = new DateTime(
                                    newStartDate.Year,
                                    newStartDate.Month,
                                    newStartDate.Day,
                                    time.Hour,
                                    time.Minute,
                                    time.Second);

                                if (state.UpdateMeetingInfor.NewStartDateTime == null)
                                {
                                    state.UpdateMeetingInfor.NewStartDateTime = newStartDateTime;
                                }

                                if (newStartDateTime >= userNow)
                                {
                                    state.UpdateMeetingInfor.NewStartDateTime = newStartDateTime;
                                    break;
                                }
                            }
                        }
                    }
                    else if (state.UpdateMeetingInfor.MoveTimeSpan != 0)
                    {
                        state.UpdateMeetingInfor.NewStartDateTime = originalStartDateTime.AddSeconds(state.UpdateMeetingInfor.MoveTimeSpan);
                    }
                    else
                    {
                        return await sc.BeginDialogAsync(Actions.UpdateNewStartTime, new UpdateDateTimeDialogOptions(UpdateDateTimeDialogOptions.UpdateReason.NotFound));
                    }

                    state.UpdateMeetingInfor.NewStartDateTime = TimeZoneInfo.ConvertTimeToUtc(state.UpdateMeetingInfor.NewStartDateTime.Value, state.GetUserTimeZone());

                    return await sc.ContinueDialogAsync();
                }
                else if (sc.Result != null)
                {
                    IList<DateTimeResolution> dateTimeResolutions = sc.Result as List<DateTimeResolution>;

                    DateTime? newStartTime = null;

                    foreach (var resolution in dateTimeResolutions)
                    {
                        var utcNow = DateTime.UtcNow;
                        var dateTimeConvertTypeString = resolution.Timex;
                        var dateTimeConvertType = new TimexProperty(dateTimeConvertTypeString);
                        var dateTimeValue = DateTime.Parse(resolution.Value);
                        if (dateTimeValue == null)
                        {
                            continue;
                        }

                        var isRelativeTime = IsRelativeTime(sc.Context.Activity.Text, resolution.Value, dateTimeConvertTypeString);
                        if (isRelativeTime)
                        {
                            dateTimeValue = DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Local);
                        }

                        dateTimeValue = isRelativeTime ? TimeZoneInfo.ConvertTime(dateTimeValue, TimeZoneInfo.Local, state.GetUserTimeZone()) : dateTimeValue;
                        var originalStartDateTime = TimeConverter.ConvertUtcToUserTime(state.ShowMeetingInfor.FocusedEvents[0].StartTime, state.GetUserTimeZone());
                        if (dateTimeConvertType.Types.Contains(Constants.TimexTypes.Date) && !dateTimeConvertType.Types.Contains(Constants.TimexTypes.DateTime))
                        {
                            dateTimeValue = new DateTime(
                                dateTimeValue.Year,
                                dateTimeValue.Month,
                                dateTimeValue.Day,
                                originalStartDateTime.Hour,
                                originalStartDateTime.Minute,
                                originalStartDateTime.Second);
                        }
                        else if (dateTimeConvertType.Types.Contains(Constants.TimexTypes.Time) && !dateTimeConvertType.Types.Contains(Constants.TimexTypes.DateTime))
                        {
                            dateTimeValue = new DateTime(
                                originalStartDateTime.Year,
                                originalStartDateTime.Month,
                                originalStartDateTime.Day,
                                dateTimeValue.Hour,
                                dateTimeValue.Minute,
                                dateTimeValue.Second);
                        }

                        dateTimeValue = TimeZoneInfo.ConvertTimeToUtc(dateTimeValue, state.GetUserTimeZone());

                        if (newStartTime == null)
                        {
                            newStartTime = dateTimeValue;
                        }

                        if (dateTimeValue >= utcNow)
                        {
                            newStartTime = dateTimeValue;
                            break;
                        }
                    }

                    if (newStartTime != null)
                    {
                        state.UpdateMeetingInfor.NewStartDateTime = newStartTime;

                        return await sc.ContinueDialogAsync();
                    }
                    else
                    {
                        return await sc.BeginDialogAsync(Actions.UpdateNewStartTime, new UpdateDateTimeDialogOptions(UpdateDateTimeDialogOptions.UpdateReason.NotADateTime));
                    }
                }
                else
                {
                    return await sc.BeginDialogAsync(Actions.UpdateNewStartTime, new UpdateDateTimeDialogOptions(UpdateDateTimeDialogOptions.UpdateReason.NotADateTime));
                }
            }
            catch (Exception ex)
            {
                await HandleDialogExceptions(sc, ex);
                return new DialogTurnResult(DialogTurnStatus.Cancelled, CommonUtil.DialogTurnResultCancelAllDialogs);
            }
        }

        public async Task<DialogTurnResult> ConfirmBeforeUpdate(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var state = await Accessor.GetAsync(sc.Context);
                var newStartTime = (DateTime)state.UpdateMeetingInfor.NewStartDateTime;
                var origin = state.ShowMeetingInfor.FocusedEvents[0];
                var last = origin.EndTime - origin.StartTime;
                origin.StartTime = newStartTime;
                origin.EndTime = (newStartTime + last).AddSeconds(1);

                var replyMessage = await GetDetailMeetingResponseAsync(sc, _lgMultiLangEngine, origin, "ConfirmUpdate");

                return await sc.PromptAsync(Actions.TakeFurtherAction, new PromptOptions
                {
                    Prompt = replyMessage,
                    RetryPrompt = (Activity)await LGHelper.GenerateMessageAsync(_lgMultiLangEngine, sc.Context, "[ConfirmUpdateFailed]", null),
                });
            }
            catch (Exception ex)
            {
                await HandleDialogExceptions(sc, ex);
                return new DialogTurnResult(DialogTurnStatus.Cancelled, CommonUtil.DialogTurnResultCancelAllDialogs);
            }
        }

        public async Task<DialogTurnResult> UpdateEventTime(WaterfallStepContext sc, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var state = await Accessor.GetAsync(sc.Context);
                var options = (CalendarSkillDialogOptions)sc.Options;

                var confirmResult = (bool)sc.Result;
                if (confirmResult)
                {
                    var newStartTime = (DateTime)state.UpdateMeetingInfor.NewStartDateTime;
                    var origin = state.ShowMeetingInfor.FocusedEvents[0];
                    var updateEvent = new EventModel(origin.Source);
                    var last = origin.EndTime - origin.StartTime;
                    updateEvent.StartTime = newStartTime;
                    updateEvent.EndTime = (newStartTime + last).AddSeconds(1);
                    updateEvent.TimeZone = TimeZoneInfo.Utc;
                    updateEvent.Id = origin.Id;

                    if (!string.IsNullOrEmpty(state.UpdateMeetingInfor.RecurrencePattern) && !string.IsNullOrEmpty(origin.RecurringId))
                    {
                        updateEvent.Id = origin.RecurringId;
                    }

                    var calendarService = ServiceManager.InitCalendarService(state.APIToken, state.EventSource);
                    var newEvent = await calendarService.UpdateEventById(updateEvent);

                    var replyMessage = await GetDetailMeetingResponseAsync(sc, _lgMultiLangEngine, newEvent, "EventUpdated");

                    await sc.Context.SendActivityAsync(replyMessage);
                }

                if (options.SubFlowMode)
                {
                    state.UpdateMeetingInfor.Clear();
                }
                else
                {
                    state.Clear();
                }

                return await sc.EndDialogAsync(true);
            }
            catch (SkillException ex)
            {
                await HandleDialogExceptions(sc, ex);
                return new DialogTurnResult(DialogTurnStatus.Cancelled, CommonUtil.DialogTurnResultCancelAllDialogs);
            }
            catch (Exception ex)
            {
                await HandleDialogExceptions(sc, ex);
                return new DialogTurnResult(DialogTurnStatus.Cancelled, CommonUtil.DialogTurnResultCancelAllDialogs);
            }
        }
    }
}