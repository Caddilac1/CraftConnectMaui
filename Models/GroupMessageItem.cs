namespace CraftConnect_Mobile_App.Models
{
    public class GroupMessageItem
    {
        public Guid Id { get; set; }
        public string Message { get; set; }
        public DateTime SentAt { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; }
        public string SenderFullName { get; set; }
    }
}
