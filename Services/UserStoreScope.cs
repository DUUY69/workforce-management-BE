using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WorkforceManagement.Api.Data;

namespace WorkforceManagement.Api.Services;

/// <summary>Phạm vi cửa hàng theo tài khoản: Admin = tất cả; Manager/Employee = CH được gán.</summary>
public class UserStoreScope
{
    private readonly AppDbContext _db;
    private readonly ClaimsPrincipal _user;
    private List<int>? _managedStoreIds;
    private bool _loaded;

    public UserStoreScope(AppDbContext db, ClaimsPrincipal user)
    {
        _db = db;
        _user = user;
    }

    public string Role => _user.FindFirstValue(ClaimTypes.Role) ?? "";
    public bool IsAdmin => Role == "Admin";
    public bool IsManager => Role == "Manager";

    public int? EmployeeId
    {
        get
        {
            var raw = _user.FindFirstValue("employeeId");
            return int.TryParse(raw, out var id) && id > 0 ? id : null;
        }
    }

    /// <summary>null = mọi cửa hàng (Admin); rỗng = không có quyền CH nào.</summary>
    public async Task<List<int>?> GetManagedStoreIdsAsync()
    {
        if (_loaded) return _managedStoreIds;
        _loaded = true;

        if (IsAdmin)
        {
            _managedStoreIds = null;
            return null;
        }

        if (EmployeeId == null)
        {
            _managedStoreIds = new List<int>();
            return _managedStoreIds;
        }

        _managedStoreIds = await _db.EmployeeStores
            .Where(es => es.EmployeeId == EmployeeId.Value)
            .Select(es => es.StoreId)
            .Distinct()
            .ToListAsync();
        return _managedStoreIds;
    }

    public async Task<bool> CanAccessStoreAsync(int storeId)
    {
        var ids = await GetManagedStoreIdsAsync();
        if (ids == null) return true;
        return ids.Contains(storeId);
    }

    public async Task<bool> CanAccessEmployeeAsync(int employeeId)
    {
        var ids = await GetManagedStoreIdsAsync();
        if (ids == null) return true;
        if (ids.Count == 0) return false;
        return await _db.EmployeeStores.AnyAsync(es =>
            es.EmployeeId == employeeId && ids.Contains(es.StoreId));
    }

    /// <summary>Kiểm tra storeId lọc từ query; trả về false nếu manager chọn CH ngoài phạm vi.</summary>
    public async Task<bool> IsStoreFilterAllowedAsync(int? storeId)
    {
        if (!storeId.HasValue) return true;
        return await CanAccessStoreAsync(storeId.Value);
    }
}
