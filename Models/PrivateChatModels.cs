namespace CraftConnect_Mobile_App.Models
{
    // ── Conversation list item (shown in the DMs tab) ─────────────────
    public class PrivateConversationItem
    {
        public string Id { get; set; } = string.Empty;
        public string OtherUserId { get; set; } = string.Empty;
        public string OtherUserName { get; set; } = string.Empty;
        public string? LastMessage { get; set; }
        public DateTime LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
        public bool HasUnreadMessages => UnreadCount > 0;

        public string Initial =>
            string.IsNullOrWhiteSpace(OtherUserName) ? "?"
            : OtherUserName[0].ToString().ToUpper();

        public string DisplayTime
        {
            get
            {
                if (LastMessageTime == default) return string.Empty;
                var diff = DateTime.Now - LastMessageTime;
                if (diff.TotalMinutes < 1) return "Just now";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m";
                if (diff.TotalDays < 1) return LastMessageTime.ToString("h:mm tt");
                if (diff.TotalDays < 2) return "Yesterday";
                if (diff.TotalDays < 7) return LastMessageTime.ToString("ddd");
                return LastMessageTime.ToString("MMM d");
            }
        }
    }

    // ── Single message in a private conversation ───────────────────────
    public class PrivateMessageItem
    {
        public Guid Id { get; set; }
        public string ConversationId { get; set; } = string.Empty;
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime SentAt { get; set; }

        public bool HasAttachment { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public string? AttachmentSize { get; set; }
        public string? AttachmentType { get; set; }
        public string MediaType { get; set; } = "none";

        public bool IsPending { get; set; }
        public bool IsSent { get; set; }
        public bool IsDelivered { get; set; }

        // Reply to another DM message
        public Guid? ReplyToMessageId { get; set; }

        // Quoted from a GROUP chat (Reply Privately feature)
        public string? QuotedGroupSender { get; set; }
        public string? QuotedGroupMessage { get; set; }

        /// <summary>
        /// Id of the specific group message that was quoted.
        /// Used to scroll back to that message when the user taps the quote banner.
        /// </summary>
        public Guid? QuotedGroupMessageId { get; set; }

        /// <summary>
        /// GroupId of the source group chat.
        /// Persisted so navigation back works after query params are cleared.
        /// </summary>
        public string? SourceGroupId { get; set; }
    }
}