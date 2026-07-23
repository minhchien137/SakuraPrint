using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintApp.Models;

// Bảng có sẵn trong DB (svn_pentaho), dùng chung với dự án DefectManagement —
// lưu cookie phiên đăng nhập Odoo để gọi API mrp.production.
[Table("SVN_Defect_Cookie")]
public class SvnDefectCookie
{
    [Key]
    public int id { get; set; }

    [Required]
    public string cookie { get; set; } = "";
}
