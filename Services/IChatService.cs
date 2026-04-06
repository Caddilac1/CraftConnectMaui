using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    public interface IChatService
    {
        // ── Groups ────────────────────────────────────────────────────
        Task<List<GroupChatItem>> GetMyGroupsAsync();

        // ── Messages ──────────────────────────────────────────────────
        Task<List<GroupMessageItem>> GetGroupMessagesAsync(Guid groupId);
        Task<bool> SendMessageAsync(Guid groupId, string message);

        // ── Unread tracking ───────────────────────────────────────────
        Task<bool> MarkGroupAsReadAsync(Guid groupId);
        Task<int> GetUnreadCountAsync(Guid groupId);
        Task<int> GetTotalUnreadCountAsync();

        // ── Tests ─────────────────────────────────────────────────────
        Task<bool> TestChatApiAsync();
        Task<bool> TestAuthAsync();
    }
}