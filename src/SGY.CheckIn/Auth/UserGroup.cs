using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using SGY.CheckIn.Models;

namespace SGY.CheckIn.Auth;

/// <summary>
/// The group someone signed in to, carried in their sign-in cookie. Every page and query
/// works on this one group.
/// </summary>
public static class UserGroup
{
    public const string Claim = "sg_group";

    /// <summary>
    /// Cookies from before groups existed have no group claim; those were all Youth sign-ins,
    /// so that's the fallback, and nobody is signed out by the upgrade.
    /// </summary>
    public static Group GetGroup(this ClaimsPrincipal user) =>
        GroupExtensions.FromKey(user.FindFirstValue(Claim)) ?? Group.Youth;

    public static async Task<Group> GetGroupAsync(this Task<AuthenticationState>? authState) =>
        authState is null ? Group.Youth : (await authState).User.GetGroup();
}
