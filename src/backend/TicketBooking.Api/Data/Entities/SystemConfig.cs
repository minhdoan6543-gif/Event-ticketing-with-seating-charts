using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TicketBooking.Api.Data.Entities;

/// <summary>
/// Bảng cấu hình kỹ thuật tối thiểu cho User Story S-01.
/// Dùng để kiểm chứng kết nối cơ sở dữ liệu và xác nhận cơ chế Migration hoạt động tiến/lùi.
/// Không chứa nghiệp vụ bán vé/ghế của các story sau.
/// </summary>
[Table("system_configs")]
public class SystemConfig
{
    [Key]
    [MaxLength(100)]
    [Column("key")]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    [Column("value")]
    public string Value { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
