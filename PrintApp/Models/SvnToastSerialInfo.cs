using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

[Table("SVN_Toast_Serial_Info")]
public class SVNToastSerialInfo
{
    [Key]
    [Column("serial_number")]
    [Required]
    [StringLength(100)]
    public string SerialNumber { get; set; } = "";

    [Column("work_order")]
    [StringLength(100)]
    public string? WorkOrder { get; set; }

    [Column("FCT_status")]
    [StringLength(100)]
    public string? FCTStatus { get; set; }

    [Column("FCT_status_datetime")]
    public DateTime? FCTStatusDatetime { get; set; }

    [Column("FQC_status")]
    [StringLength(100)]
    public string? FQCStatus { get; set; }

    [Column("FQC_status_datetime")]
    public DateTime? FQCStatusDatetime { get; set; }

    [Column("update_by_svncode")]
    public string? updateBySVNCode { get; set; }
}

// ── Request models ─────────────────────────────────────────────────────────────

public class FctSubmitReq
{
    public string Serial { get; set; } = "";
    public string Status { get; set; } = "OK"; // "OK" | "NG"
}

public class FqcUpdateRequest
{
    public string serialNumber { get; set; } = "";
    public string status { get; set; } = "";
}