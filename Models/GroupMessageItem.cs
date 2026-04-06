namespace CraftConnect_Mobile_App.Models
{
    /// <summary>
    /// Domain model for a single message in a group chat.
    /// Extended with voice-note duration and reply-thread fields.
    /// </summary>
    public class GroupMessageItem
    {
        // ── Identity ──────────────────────────────────────────────
        public Guid Id { get; set; }
        public Guid GroupChatId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderFullName { get; set; } = string.Empty;

        // ── Content ───────────────────────────────────────────────
        public string Message { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }

        // ── Delivery status ───────────────────────────────────────
        public bool IsPending { get; set; }
        public bool IsSent { get; set; }
        public bool IsDelivered { get; set; }
        public bool IsRead { get; set; }

        // ── Attachment ────────────────────────────────────────────
        public bool HasAttachment { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public string? AttachmentSize { get; set; }
        public string? AttachmentType { get; set; }

        /// <summary>
        /// Media type discriminator: "none" | "image" | "audio" | "video" | "document"
        /// </summary>
        public string MediaType { get; set; } = "none";

        // ── Voice note ────────────────────────────────────────────
        /// <summary>
        /// Human-readable duration of a voice note, e.g. "0:12" or "1:04".
        /// Populated when MediaType == "audio".
        /// </summary>
        public string? VoiceDuration { get; set; }

        // ── Reply thread ──────────────────────────────────────────
        /// <summary>Id of the message being replied to, if any.</summary>
        public Guid? ReplyToMessageId { get; set; }
        public string? ReplyToSender { get; set; }
        public string? ReplyToMessage { get; set; }
    }
}