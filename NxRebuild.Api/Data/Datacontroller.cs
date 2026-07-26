//public class LockStatus {
//    public bool IsLocked { get; set; }
//    public string LockedByUserId { get; set; }
//    public string LockedByUserName { get; set; }
//}

// Services/ZmstLockChecker.cs
public class ZmstLockChecker : ILockChecker {
    private readonly IDbConnection _db;
    private readonly IUserService _userService; // ユーザー名取得用サービス

    public ZmstLockChecker(IDbConnection db, IUserService userService) {
        _db = db;
        _userService = userService;
    }

    public async Task<LockStatus> GetLockStatusAsync(string groupCode, string localCode) {
        // SQLでlocked_atとlocked_byの両方を取得
        var sql = @"SELECT locked_at, locked_by as UserId 
                    FROM Zmst 
                    WHERE group_code = @groupCode AND local_code = @localCode";
        
        var result = await _db.QueryFirstOrDefaultAsync<dynamic>(sql, new { groupCode, localCode });

        if (result == null || result.locked_at == null) 
            return new LockStatus { IsLocked = false };

        bool isLocked = (DateTime.UtcNow - (DateTime)result.locked_at).TotalMinutes < 10;
        
        if (!isLocked) return new LockStatus { IsLocked = false };

        // ロックされていたらユーザー名を取得
        string userName = await _userService.GetUserNameAsync(result.UserId);

        return new LockStatus {
            IsLocked = true,
            LockedByUserId = result.UserId,
            LockedByUserName = userName
        };
    }
}

[HttpGet("islocked")]
public async Task<ActionResult<LockStatus>> GetLockStatus(string groupCode, string localCode) {
    var status = await _lockChecker.GetLockStatusAsync(groupCode, localCode);
    return Ok(status);
}