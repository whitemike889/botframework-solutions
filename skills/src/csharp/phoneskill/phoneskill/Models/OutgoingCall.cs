using System;
using System.Collections.Generic;

namespace PhoneSkill.Models
{
    /// <summary>
    /// An outgoing call to be placed.
    /// </summary>
    public class OutgoingCall : IEquatable<OutgoingCall>
    {
        /// <summary>
        /// Gets or sets the phone number to call.
        /// </summary>
        /// <value>
        /// The phone number to call.
        /// </value>
        public string Number { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the contact to call.
        /// </summary>
        /// <value>
        /// The contact to call.
        /// This may be null if, for example, the user asked to call a phone number that is not in their contact list.
        /// Note that the list of phone numbers of this contact may have been shortened to only the number to be called.
        /// </value>
        public ContactCandidate Contact { get; set; }

        /// <summary>
        /// Gets or sets the URI for the phone number to call (see RFC 3966).
        /// </summary>
        /// <value>
        /// The URI for the phone number to call (see RFC 3966).
        /// This may be empty if it was not possible to generate an RFC 3966 compliant URI for the phone number,
        /// for example because it is missing the country code.
        /// </value>
        public string Uri { get; set; } = string.Empty;

        public override bool Equals(object obj)
        {
            return Equals(obj as OutgoingCall);
        }

        public bool Equals(OutgoingCall other)
        {
            return other != null &&
                   Number == other.Number &&
                   (Contact == null ? other.Contact == null : EqualityComparer<ContactCandidate>.Default.Equals(Contact, other.Contact)) &&
                   Uri == other.Uri;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Number, Contact, Uri);
        }

        public override string ToString()
        {
            return $"OutgoingCall{{Number={Number}, Contact={Contact}, Uri={Uri}}}";
        }
    }
}
