namespace CraftConnect_Mobile_App.Services
{
    // ── Event arg types ───────────────────────────────────────────────────

    public class MessageReceivedEventArgs : EventArgs
    {
        public string Id { get; set; } = string.Empty;
        public string GroupChatId { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderFullName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool HasAttachment { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public string? AttachmentSize { get; set; }
        public string? AttachmentType { get; set; }
        public string? MediaType { get; set; }
    }

    public class PrivateMessageReceivedEventArgs : EventArgs
    {
        public string Id { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool HasAttachment { get; set; }
        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public string? AttachmentType { get; set; }
        public string? MediaType { get; set; }
        public string? QuotedGroupSender { get; set; }
        public string? QuotedGroupMessage { get; set; }
        public string? ReplyToMessageId { get; set; }
    }

    public class PrivateMessageNotificationEventArgs : EventArgs
    {
        public string ConversationId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class TypingEventArgs : EventArgs
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    // ── Interface ─────────────────────────────────────────────────────────

    public interface IChatSignalRService
    {
        // Connection state
        event EventHandler<bool> ConnectionStateChanged;

        // Group chat events
        event EventHandler<MessageReceivedEventArgs> MessageReceived;
        event EventHandler<TypingEventArgs> UserTyping;
        event EventHandler<string> UserStoppedTyping;
        event EventHandler<string> Reconnected;
        event EventHandler<string> MessageDeleted;

        // Private chat events
        event EventHandler<PrivateMessageReceivedEventArgs> PrivateMessageReceived;
        event EventHandler<string> PrivateMessageDeleted;
        event EventHandler<PrivateMessageNotificationEventArgs> PrivateMessageNotification;

        bool IsConnected { get; }
        bool IsConnecting { get; }

        Task ConnectAsync(CancellationToken cancellationToken = default);
        Task DisconnectAsync();

        // Group chat
        Task JoinGroupAsync(string groupId);
        Task LeaveGroupAsync(string groupId);
        Task SendMessageAsync(string groupId, string message, string senderName, string senderFullName);
        Task SendMessageWithAttachmentAsync(string groupId, string message, string senderName,
            string senderFullName, string attachmentUrl, string attachmentName,
            string attachmentSize, string attachmentType);
        Task NotifyTypingAsync(string groupId, string userName);
        Task NotifyStoppedTypingAsync(string groupId);
        Task DeleteMessageAsync(string groupId, string messageId);

        // Private chat
        Task JoinPrivateConversationAsync(string conversationId);
        Task LeavePrivateConversationAsync(string conversationId);
        Task SendPrivateMessageAsync(string conversationId, string messageId, string message,
            string? attachmentUrl = null, string? quotedGroupSender = null,
            string? quotedGroupMessage = null, string? replyToMessageId = null);
        Task DeletePrivateMessageAsync(string conversationId, string messageId);
        Task PrivateTypingAsync(string conversationId, string userName);
        Task PrivateStoppedTypingAsync(string conversationId);

        void UpdateHubUrl(string newBaseUrl);
    }
}