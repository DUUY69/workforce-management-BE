using Microsoft.EntityFrameworkCore;
using WorkforceManagement.Api.Data;

namespace WorkforceManagement.Api.Services;

public static class AttendanceStatuses
{
    public const string Worked = "Worked";
    public const string Absent = "Absent";

    public static string Label(string? status) => status switch
    {
        Worked => "Đi làm",
        Absent => "Không đi làm",
        _ => status ?? ""
    };

    public static bool IsValid(string? status)
        => status == Worked || status == Absent;
}

public static class AttendanceRules
{
    /// <summary>Giờ chuẩn: ca đăng ký đã duyệt → giờ ca; không thì cấu hình cửa hàng trong DB.</summary>
    public static async Task<decimal> GetStandardHoursAsync(
        AppDbContext db, int employeeId, int storeId, DateOnly workDate, CancellationToken ct = default)
    {
        var shiftReg = await db.ShiftRegistrations
            .Include(r => r.Shift)
            .Where(r => r.EmployeeId == employeeId
                && r.StoreId == storeId
                && r.WorkDate == workDate
                && r.Status == "Approved")
            .FirstOrDefaultAsync(ct);

        if (shiftReg != null)
        {
            var span = shiftReg.EndTime.ToTimeSpan() - shiftReg.StartTime.ToTimeSpan();
            return Math.Round((decimal)span.TotalHours, 2);
        }

        var store = await db.Stores.AsNoTracking()
            .Where(s => s.Id == storeId)
            .Select(s => new { s.StandardWorkHoursPerDay })
            .FirstOrDefaultAsync(ct);

        if (store == null)
            throw new InvalidOperationException($"Không tìm thấy cửa hàng Id={storeId} trong DB.");

        if (store.StandardWorkHoursPerDay <= 0)
            throw new InvalidOperationException(
                $"Cửa hàng Id={storeId} chưa cấu hình StandardWorkHoursPerDay hợp lệ trong DB.");

        return store.StandardWorkHoursPerDay;
    }

    public static async Task<(decimal StandardHours, decimal OtMultiplier, string? ShiftName)> GetWorkContextAsync(
        AppDbContext db, int employeeId, int storeId, DateOnly workDate, CancellationToken ct = default)
    {
        var shiftReg = await db.ShiftRegistrations
            .Include(r => r.Shift)
            .Where(r => r.EmployeeId == employeeId
                && r.StoreId == storeId
                && r.WorkDate == workDate
                && r.Status == "Approved")
            .FirstOrDefaultAsync(ct);

        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storeId, ct)
            ?? throw new InvalidOperationException($"Không tìm thấy cửa hàng Id={storeId} trong DB.");

        if (shiftReg != null)
        {
            var span = shiftReg.EndTime.ToTimeSpan() - shiftReg.StartTime.ToTimeSpan();
            var label = shiftReg.Shift?.Name ?? $"{shiftReg.StartTime:HH\\:mm}–{shiftReg.EndTime:HH\\:mm}";
            return (Math.Round((decimal)span.TotalHours, 2), store.OvertimeRateMultiplier, label);
        }

        return (store.StandardWorkHoursPerDay, store.OvertimeRateMultiplier, null);
    }

    public static decimal CalcWorkedHours(TimeOnly checkIn, TimeOnly checkOut)
    {
        if (checkOut <= checkIn) return 0;
        return Math.Round((decimal)(checkOut.ToTimeSpan() - checkIn.ToTimeSpan()).TotalHours, 2);
    }

    public static decimal CalcOvertimeHours(decimal workedHours, decimal standardHours)
        => Math.Max(0, Math.Round(workedHours - standardHours, 2));

    public static void ApplyWorkedTimes(
        Attendance record, TimeOnly checkIn, TimeOnly checkOut, decimal standardHours)
    {
        record.CheckIn = checkIn;
        record.CheckOut = checkOut;
        record.Status = AttendanceStatuses.Worked;
        var worked = CalcWorkedHours(checkIn, checkOut);
        record.OvertimeHours = CalcOvertimeHours(worked, standardHours);
    }
}
