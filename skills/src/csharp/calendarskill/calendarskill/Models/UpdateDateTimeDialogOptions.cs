// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace CalendarSkill.Models
{
    public class ChooseMeetingToUpdateDialogOptions
    {
        public ChooseMeetingToUpdateDialogOptions()
        {
            Reason = UpdateReason.NotFound;
        }

        public ChooseMeetingToUpdateDialogOptions(UpdateReason reason)
        {
            Reason = reason;
        }

        public enum UpdateReason
        {
            /// <summary>
            /// NotADateTime.
            /// </summary>
            NotADateTime,

            /// <summary>
            /// NotFound.
            /// </summary>
            NotFound,

            /// <summary>
            /// NoEvent.
            /// </summary>
            NoEvent,
        }

        public UpdateReason Reason { get; set; }
    }
}