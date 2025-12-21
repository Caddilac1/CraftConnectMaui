using System;
using System.Collections.Generic;

namespace CraftConnect_Mobile_App.Models
{
    public class GroupChatItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public List<GroupMember> Members { get; set; } = new();
        public int MemberCount => Members?.Count ?? 0;
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        // Last message properties
        public string LastMessage { get; set; }
        public string LastMessageSender { get; set; }
        public DateTime LastMessageTime { get; set; }
        public bool LastMessageIsRead { get; set; }
        public bool LastMessageIsDelivered { get; set; }

        // Chat status properties
        public int UnreadCount { get; set; }
        public bool IsMuted { get; set; }
        public bool IsPinned { get; set; }
        public bool IsArchived { get; set; }
        public bool IsRead => LastMessageIsRead;
        public bool IsDelivered => LastMessageIsDelivered;
        public bool HasUnreadMessages => UnreadCount > 0;

        // For display in chat list
        public string LastMessagePreview => GetLastMessagePreview();
        public string DisplayTime => GetDisplayTime();
        public string Initial => GetInitial();

        // Helper methods
        private string GetLastMessagePreview()
        {
            if (string.IsNullOrEmpty(LastMessage))
                return "No messages yet";

            if (LastMessage.Length > 40)
                return LastMessage.Substring(0, 40) + "...";

            return LastMessage;
        }

        private string GetDisplayTime()
        {
            var now = DateTime.Now;

            if (LastMessageTime.Date == now.Date)
            {
                // Today: show time only
                return LastMessageTime.ToString("HH:mm");
            }
            else if (LastMessageTime.Date == now.Date.AddDays(-1))
            {
                // Yesterday
                return "Yesterday";
            }
            else if (LastMessageTime.Year == now.Year)
            {
                // Same year: show day and month
                return LastMessageTime.ToString("dd MMM");
            }
            else
            {
                // Different year: show full date
                return LastMessageTime.ToString("dd/MM/yy");
            }
        }

        private string GetInitial()
        {
            if (string.IsNullOrEmpty(Name))
                return "??";

            var namePart = Name.Split('-')[0].Trim();
            var words = namePart.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length >= 2)
            {
                // Two or more words: take first letter of first two words
                return $"{words[0][0]}{words[1][0]}".ToUpper();
            }
            else if (namePart.Length >= 2)
            {
                // Single word: take first two letters
                return namePart.Substring(0, 2).ToUpper();
            }
            else if (namePart.Length == 1)
            {
                // Single character: just return it
                return namePart.ToUpper();
            }

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

        // Computed properties
        public string LastMessagePreview => GetLastMessagePreview();
        public string DisplayTime => GetDisplayTime();
        public bool HasUnreadMessages => UnreadCount > 0;

        private string GetLastMessagePreview()
        {
            if (string.IsNullOrEmpty(LastMessage))
                return IsGroupChat ? "No messages yet" : "Start a conversation";

            if (LastMessage.Length > 40)
                return LastMessage.Substring(0, 40) + "...";

            return LastMessage;
        }

        private string GetDisplayTime()
        {
            var now = DateTime.Now;

            if (LastMessageTime.Date == now.Date)
            {
                return LastMessageTime.ToString("HH:mm");
            }
            else if (LastMessageTime.Date == now.Date.AddDays(-1))
            {
                return "Yesterday";
            }
            else if (LastMessageTime.Year == now.Year)
            {
                return LastMessageTime.ToString("dd MMM");
            }
            else
            {
                return LastMessageTime.ToString("dd/MM/yy");
            }
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
        public string MediaDuration { get; set; } // For audio/video
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