using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    public interface IPrivateChatService
    {
        /// <summary>Returns all DM conversations for the current user.</summary>
        Task<List<PrivateConversationItem>> GetMyConversationsAsync();

        /// <summary>
        /// Gets (or creates) the conversation with another user.
        /// Returns the conversation ID and other user details.
        /// </summary>
        Task<(string conversationId, string otherUserName)> OpenConversationAsync(string otherUserId);

        /// <summary>Returns all messages in a conversation (also marks as read).</summary>
        Task<List<PrivateMessageItem>> GetMessagesAsync(string conversationId);

        /// <summary>Persists a message to the server DB.</summary>
        Task<(bool success, string? messageId)> SendMessageAsync(
            string conversationId,
            string? message,
            string? attachmentUrl = null,
            string? replyToMessageId = null,
            string? quotedGroupSender = null,
            string? quotedGroupMessage = null);

        Task<bool> MarkAsReadAsync(string conversationId);
        Task<bool> DeleteForMeAsync(string messageId);
        Task<bool> DeleteForEveryoneAsync(string messageId);
    }
}
