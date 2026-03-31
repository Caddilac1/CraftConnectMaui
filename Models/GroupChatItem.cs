using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CraftConnect_Mobile_App.Models
{
    public class GroupChatItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public List<GroupMember> Members { get; set; } = new();

        /// <summary>
        /// Uses the real Members list when populated, otherwise falls back to
        /// the count value received from the API (set via the setter).
        /// </summary>
        private int _memberCount;
        public int MemberCount
        {
            get => Members?.Count > 0 ? Members.Count : _memberCount;
            set => _memberCount = value;
        }

        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        // ── Last message properties ───────────────────────────────────
        public string LastMessage { get; set; }
        public string LastMessageSender { get; set; }
        public DateTime LastMessageTime { get; set; }
        public bool LastMessageIsRead { get; set; }
        public bool LastMessageIsDelivered { get; set; }

        // ── Chat type ─────────────────────────────────────────────────
        /// <summary>
        /// True for group chats, false for personal / DM chats.
        /// Set this when mapping your API response to GroupChatItem.
        /// </summary>
        public bool IsGroup { get; set; }

        // ── Chat status properties ────────────────────────────────────
        // UnreadCount uses INotifyPropertyChanged so the UI badge updates
        // when RefreshUnreadCountsAsync assigns a new value without full reload.
        private int _unreadCount;
        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                if (_unreadCount == value) return;
                _unreadCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUnreadMessages));
            }
        }

        public bool IsMuted { get; set; }
        public bool IsPinned { get; set; }
        public bool IsArchived { get; set; }
        public bool IsRead => LastMessageIsRead;
        public bool IsDelivered => LastMessageIsDelivered;
        public bool HasUnreadMessages => _unreadCount > 0;

        // ── Display helpers ───────────────────────────────────────────
        public string LastMessagePreview => GetLastMessagePreview();
        public string DisplayTime => GetDisplayTime();
        public string Initial => GetInitial();

        private string GetLastMessagePreview()
        {
            if (string.IsNullOrEmpty(LastMessage))
                return "No messages yet";

            return LastMessage.Length > 40
                ? LastMessage.Substring(0, 40) + "..."
                : LastMessage;
        }

        private string GetDisplayTime()
        {
            var now = DateTime.Now;

            if (LastMessageTime.Date == now.Date)
                return LastMessageTime.ToString("HH:mm");

            if (LastMessageTime.Date == now.Date.AddDays(-1))
                return "Yesterday";

            if (LastMessageTime.Year == now.Year)
                return LastMessageTime.ToString("dd MMM");

            return LastMessageTime.ToString("dd/MM/yy");
        }

        private string GetInitial()
        {
            if (string.IsNullOrEmpty(Name))
                return "??";

            var namePart = Name.Split('-')[0].Trim();
            var words = namePart.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length >= 2)
                return $"{words[0][0]}{words[1][0]}".ToUpper();

            if (namePart.Length >= 2)
                return namePart.Substring(0, 2).ToUpper();

            if (namePart.Length == 1)
                return namePart.ToUpper();

            return "??";
        }
    }

    public class GroupMember
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; } // "Admin", "Member"
        public DateTime JoinedAt { get; set; }
        public bool IsOnline { get; set; }
        public DateTime LastSeen { get; set; }
        public string ProfileImage { get; set; }
    }

    public class ChatItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ProfileImage { get; set; }
        public string LastMessage { get; set; }
        public string LastMessageSender { get; set; }
        public DateTime LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
        public bool IsOnline { get; set; }
        public bool IsGroupChat { get; set; }
        public bool IsMuted { get; set; }
        public bool IsPinned { get; set; }
        public bool IsArchived { get; set; }
        public bool IsRead { get; set; }
        public bool IsDelivered { get; set; }

        public string LastMessagePreview => GetLastMessagePreview();
        public string DisplayTime => GetDisplayTime();
        public bool HasUnreadMessages => UnreadCount > 0;

        private string GetLastMessagePreview()
        {
            if (string.IsNullOrEmpty(LastMessage))
                return IsGroupChat ? "No messages yet" : "Start a conversation";

            return LastMessage.Length > 40
                ? LastMessage.Substring(0, 40) + "..."
                : LastMessage;
        }

        private string GetDisplayTime()
        {
            var now = DateTime.Now;

            if (LastMessageTime.Date == now.Date)
                return LastMessageTime.ToString("HH:mm");

            if (LastMessageTime.Date == now.Date.AddDays(-1))
                return "Yesterday";

            if (LastMessageTime.Year == now.Year)
                return LastMessageTime.ToString("dd MMM");

            return LastMessageTime.ToString("dd/MM/yy");
        }
    }

    public class PersonalChatItem
    {
        public string Id { get; set; }
        public string ContactId { get; set; }
        public string ContactName { get; set; }
        public string ProfileImage { get; set; }
        public ChatMessage LastMessage { get; set; }
        public int UnreadCount { get; set; }
        public bool IsOnline { get; set; }
        public bool IsGroupChat { get; set; } = false;
        public bool IsMuted { get; set; }
        public bool IsPinned { get; set; }
        public bool IsArchived { get; set; }
    }

    public class ChatMessage
    {
        public string Id { get; set; }
        public string ChatId { get; set; }
        public bool IsGroupChat { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string Content { get; set; }
        public MessageType Type { get; set; } = MessageType.Text;
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
        public bool IsDelivered { get; set; }
        public List<string> ReadBy { get; set; } = new();
        public List<string> DeliveredTo { get; set; } = new();

        // For media messages
        public string MediaUrl { get; set; }
        public string MediaThumbnail { get; set; }
        public long? MediaSize { get; set; }
        public string MediaDuration { get; set; }
        public string MediaCaption { get; set; }
    }

    public enum MessageType
    {
        Text,
        Image,
        Video,
        Audio,
        Document,
        Location,
        Contact,
        System
    }
}