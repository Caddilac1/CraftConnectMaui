namespace CraftConnect_Mobile_App.Models
{
    public class GroupMessageItem
    {
        public Guid Id { get; set; }
        public Guid GroupChatId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; }
        public string SenderFullName { get; set; }
        public string Message { get; set; }
        public DateTime SentAt { get; set; }

        public bool IsPending { get; set; }
        public bool IsSent { get; set; }
        public bool IsDelivered { get; set; }
        public bool IsRead { get; set; }
        public string MediaType { get; set; } = "none";

        // Attachment properties
        public bool HasAttachment { get; set; }
        public string AttachmentUrl { get; set; }
        public string AttachmentName { get; set; }
        public string AttachmentSize { get; set; }
        public string AttachmentType { get; set; }
    }
}