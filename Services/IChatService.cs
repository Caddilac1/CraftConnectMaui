using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        /// <summary>
        /// Marks all messages in a group as read for the current user.
        /// Updates LastReadAt on the server — unread count drops to 0.
        /// </summary>
        Task<bool> MarkGroupAsReadAsync(Guid groupId);

        /// <summary>
        /// Returns the unread message count for a single group.
        /// </summary>
        Task<int> GetUnreadCountAsync(Guid groupId);

        /// <summary>
        /// Returns the total unread count across ALL groups.
        /// Use this to drive the main nav badge.
        /// </summary>
        Task<int> GetTotalUnreadCountAsync();

        // ── Tests ─────────────────────────────────────────────────────
        Task<bool> TestChatApiAsync();
        Task<bool> TestAuthAsync();
    }
}