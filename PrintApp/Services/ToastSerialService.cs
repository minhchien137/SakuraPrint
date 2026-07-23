using Microsoft.EntityFrameworkCore;
using PrintApp.Data;
using PrintApp.Models;

namespace PrintApp.Services;

public class ToastSerialService
{
    private readonly AppDbContext _context;

    public ToastSerialService(AppDbContext context)
    {
        _context = context;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static DateTime VietnamNow()
    {
        var tzId = System.Runtime.InteropServices.RuntimeInformation
            .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
            ? "SE Asia Standard Time" : "Asia/Bangkok";
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById(tzId));
    }

    // ── FCT ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ghi FCT status lần đầu. Trả về (ok, message).
    /// </summary>
    public async Task<(bool ok, string message, string? serial, string? status)>
        SubmitFctAsync(string serial, string status)
    {
        serial = serial.Trim();
        status = status.Trim().ToUpperInvariant();

        if (serial.Length != 13)
            return (false, "Serial phải đúng 13 ký tự.", null, null);

        if (status != "OK" && status != "NG")
            return (false, "Trạng thái không hợp lệ (OK hoặc NG).", null, null);

        var exists = await _context.SVNToastSerialInfos
            .AsNoTracking()
            .AnyAsync(x => x.SerialNumber == serial);

        if (exists)
            return (false, $"Serial đã tồn tại: {serial}", null, null);

        var now = VietnamNow();
        var rec = new SVNToastSerialInfo
        {
            SerialNumber = serial,
            FCTStatus = status,
            FCTStatusDatetime = now
        };

        try
        {
            _context.SVNToastSerialInfos.Add(rec);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return (false, $"Serial đã tồn tại: {serial}", null, null);
        }

        return (true, $"Đã ghi Serial: {serial} | FCT: {status}", serial, status);
    }

    // ── FQC ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cập nhật FQC status. Trả về (ok, message).
    /// </summary>
    public async Task<(bool ok, string message)>
        UpdateFqcAsync(string serialNumber, string status)
    {
        serialNumber = serialNumber.Trim();
        status = status.Trim().ToUpperInvariant();

        var rec = await _context.SVNToastSerialInfos
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);

        if (rec == null)
            return (false, $"Serial {serialNumber} chưa qua FCT.");

        if (rec.FCTStatus?.ToUpper() == "NG")
            return (false, $"Serial {serialNumber} có trạng thái FCT là NG.");

        if (!string.IsNullOrWhiteSpace(rec.FQCStatus))
            return (false, $"Serial {serialNumber} đã có FQC status.");

        var now = VietnamNow();
        rec.FQCStatus = status;
        rec.FQCStatusDatetime = now;

        await _context.SaveChangesAsync();

        return (true, $"Cập nhật FQC thành công ({status}) cho serial: {serialNumber}.");
    }

    // ── Lookup ─────────────────────────────────────────────────────────────────

    public async Task<SVNToastSerialInfo?> GetBySerialAsync(string serial)
    {
        return await _context.SVNToastSerialInfos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SerialNumber == serial.Trim());
    }
}