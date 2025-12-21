using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    public interface IChatService
    {
        Task<List<GroupChatItem>> GetMyGroupsAsync();

        Task<List<GroupMessageItem>> GetGroupMessagesAsync(Guid groupId);

        Task<bool> SendMessageAsync(Guid groupId, string message);

        Task<bool> TestChatApiAsync();
        Task<bool> TestAuthAsync();
    }
}
