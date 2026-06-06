using Microsoft.AspNetCore.Identity;
using Medo;

namespace NxRebuild.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string GroupCode { get; set; } = string.Empty;

    public ApplicationUser()
    {
        // IdentityUser.Id は string 型なので UUIDv7 をそのまま入れる
        Id = Uuid7.NewUuid7().ToString();

        // グループコードも UUIDv7
        GroupCode = Uuid7.NewUuid7().ToString();
    }
}
