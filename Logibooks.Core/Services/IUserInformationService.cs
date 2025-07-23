namespace Logibooks.Core.Services;

using Microsoft.AspNetCore.Mvc;
using Logibooks.Core.RestModels;

public interface IUserInformationService
{
    Task<bool> CheckAdmin(int cuid);
    Task<bool> CheckLogist(int cuid);
    Task<bool> CheckAdminOrSameUser(int id, int cuid);
    bool CheckSameUser(int id, int cuid);
    bool Exists(int id);
    bool Exists(string email);
    Task<UserViewItem?> UserViewItem(int id);
    Task<List<UserViewItem>> UserViewItems();
}
