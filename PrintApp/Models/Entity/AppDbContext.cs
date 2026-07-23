using Microsoft.EntityFrameworkCore;
using PrintApp.Models;

namespace PrintApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AstroLabelData> AstroLabelDatas { get; set; }
    public DbSet<PrinterInfo> PrinterInfos { get; set; }
    public DbSet<SVNToastSerialInfo> SVNToastSerialInfos { get; set; }
    public DbSet<SnLabelPrint> SnLabelPrints { get; set; }
    public DbSet<SnLabelScanLog> SnLabelScanLogs { get; set; }
    public DbSet<SakuraZplTemplate> SakuraZplTemplates { get; set; }
    public DbSet<SvnDefectCookie> SvnDefectCookies { get; set; }
    public DbSet<BackPanelLaserLog> BackPanelLaserLogs { get; set; }
    public DbSet<MiddleDimensionCheckResult> MiddleDimensionCheckResults { get; set; }
    public DbSet<ProductionInputLog> ProductionInputLogs { get; set; }
    public DbSet<MiddleLog> MiddleLogs { get; set; }
    public DbSet<CartonSnScanLog> CartonSnScanLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SVNToastSerialInfo>()
            .HasKey(x => x.SerialNumber);

        modelBuilder.Entity<SnLabelPrint>(e =>
        {
            e.HasIndex(x => x.SerialNumber).IsUnique();
            e.HasIndex(x => new { x.ProductionDate, x.ProductionLine, x.Variant, x.RunningNumberInt })
                .HasDatabaseName("IX_SM_SNLabelPrint_Counter");
        });

        modelBuilder.Entity<CartonSnScanLog>(e =>
        {
            // Serial giờ là chuỗi CSV nhiều serial/carton trong 1 dòng -> không unique theo cột
            // này nữa (kiểm tra trùng serial dùng LIKE, xem SakuraService.IsCartonSerialAlreadyUsedAsync).
            e.HasIndex(x => x.WorkOrder);
        });

        modelBuilder.Entity<SakuraZplTemplate>(e =>
        {
            e.HasIndex(x => x.TemplateKey).IsUnique();
        });
    }
}