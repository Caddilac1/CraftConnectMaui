namespace CraftConnect_Mobile_App.Models
{
    /// <summary>
    /// Unified item that represents either a group chat or a private DM
    /// in the combined chat list.
    /// </summary>
    public class ChatListItem
    {
        // ── Identity ───────────────────────────────────────────────
        public string Id { get; set; } = string.Empty;

        /// <summary>True = group chat, False = private DM.</summary>
        public bool IsGroup { get; set; }
        public bool IsDm => !IsGroup;

        // ── Display ────────────────────────────────────────────────
        public string DisplayName { get; set; } = string.Empty;
        public string Initial { get; set; } = string.Empty;
        public string LastMessage { get; set; } = string.Empty;
        public DateTime LastMessageTime { get; set; }
        public string DisplayTime { get; set; } = string.Empty;

        // ── Unread ─────────────────────────────────────────────────
        public bool HasUnreadMessages => UnreadCount > 0;
        public int UnreadCount { get; set; }

        // ── Avatar gradient ────────────────────────────────────────
        /// <summary>Rounded square (15) for groups, circle (27) for DMs.</summary>
        public int AvatarCornerRadius => IsGroup ? 15 : 27;
        public string AvatarGradientStart => IsGroup ? "#60A5FA" : "#93C5FD";
        public string AvatarGradientEnd   => IsGroup ? "#2563EB" : "#1D4ED8";

        // ── DM-only extras (ignored for groups) ───────────────────
        public string OtherUserId   { get; set; } = string.Empty;
        public string OtherUserName { get; set; } = string.Empty;

        // ── Factory helpers ────────────────────────────────────────

        public static ChatListItem FromGroup(GroupChatItem g) => new()
        {
            Id              = g.Id,
            IsGroup         = true,
            DisplayName     = g.Name,
            Initial         = g.Initial,
            LastMessage     = g.LastMessage,
            LastMessageTime = g.LastMessageTime,
            DisplayTime     = g.DisplayTime,
            UnreadCount     = g.UnreadCount
        };

        public static ChatListItem FromConversation(PrivateConversationItem c) => new()
        {
            Id              = c.Id,
            IsGroup         = false,
            DisplayName     = c.OtherUserName,
            Initial         = c.Initial,
            LastMessage     = c.LastMessage,
            LastMessageTime = c.LastMessageTime,
            DisplayTime     = c.DisplayTime,
            UnreadCount     = c.UnreadCount,
            OtherUserId     = c.OtherUserId,
            OtherUserName   = c.OtherUserName
        };
    }
}
