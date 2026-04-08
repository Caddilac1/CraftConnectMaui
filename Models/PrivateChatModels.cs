namespace CraftConnect_Mobile_App.Models
{
    // ── Conversation list item (shown in the DMs tab) ─────────────────────────

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
                return diff switch
                {
                    { TotalMinutes: < 1 } => "Just now",
                    { TotalHours: < 1 } => $"{(int)diff.TotalMinutes}m",
                    { TotalDays: < 1 } => LastMessageTime.ToString("h:mm tt"),
                    { TotalDays: < 2 } => "Yesterday",
                    { TotalDays: < 7 } => LastMessageTime.ToString("ddd"),
                    _ => LastMessageTime.ToString("MMM d"),
                };
            }
        }
    }

    // ── Single message in a private conversation ──────────────────────────────

    public class PrivateMessageItem
    {
        // ── Core identity ─────────────────────────────────────────────────────

        public Guid Id { get; set; }
        public string ConversationId { get; set; } = string.Empty;
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime SentAt { get; set; }

        // ── Attachment ────────────────────────────────────────────────────────

        public bool HasAttachment { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public string? AttachmentSize { get; set; }
        public string? AttachmentType { get; set; }
        public string MediaType { get; set; } = "none";

        // ── Delivery status ───────────────────────────────────────────────────

        public bool IsPending { get; set; }
        public bool IsSent { get; set; }
        public bool IsDelivered { get; set; }

        // ── DM reply (reply to another message within this conversation) ──────

        public Guid? ReplyToMessageId { get; set; }
        public string? ReplyToSenderName { get; set; }
        public string? ReplyToText { get; set; }

        // ── Cross-chat group quote ("Reply Privately" from a group chat) ──────

        /// <summary>Display name of the group-chat sender being quoted.</summary>
        public string? QuotedGroupSender { get; set; }

        /// <summary>Text body of the quoted group-chat message.</summary>
        public string? QuotedGroupMessage { get; set; }

        /// <summary>
        /// ID of the specific group-chat message that was quoted.
        /// Used to scroll back to that message when the user taps the quote banner.
        /// </summary>
        public Guid? QuotedGroupMessageId { get; set; }

        /// <summary>
        /// ID of the source group chat.
        /// Persisted so navigation back works after query params are cleared.
        /// </summary>
        public string? SourceGroupId { get; set; }
    }
}