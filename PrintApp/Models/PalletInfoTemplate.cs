using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Template gộp sẵn PO Number/Inbound Reference/Warehouse Reference/Delivery Address cho vùng
// "Print Pallet" (CartonSN.cshtml) — chọn 1 template là điền đủ 4 trường, khỏi gõ tay mỗi lần in.
[Table("SM_Sakura_PalletInfoTemplate")]
public class PalletInfoTemplate
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string TemplateName { get; set; } = "";

    [StringLength(100)]
    public string PoNumber { get; set; } = "";

    [StringLength(100)]
    public string InboundReference { get; set; } = "";

    [StringLength(100)]
    public string WarehouseReference { get; set; } = "";

    [StringLength(500)]
    public string DeliveryAddress { get; set; } = "";

    public DateTime UpdatedAt { get; set; }
}

public class PalletInfoTemplateUpsertRequest
{
    public string TemplateName { get; set; } = "";
    public string PoNumber { get; set; } = "";
    public string InboundReference { get; set; } = "";
    public string WarehouseReference { get; set; } = "";
    public string DeliveryAddress { get; set; } = "";
}
