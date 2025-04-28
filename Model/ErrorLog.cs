using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace urban_style_auto_regist.Model
{
    [Table("error_logs")]  // MySQL 테이블명
    public class ErrorLog
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("error_date")]
        public DateTime ErrorDate { get; set; } = DateTime.Now;

        [Column("url")]
        [StringLength(1000)]
        public string? Url { get; set; }

        [Column("message")]
        public string Message { get; set; } = string.Empty;

        [Column("stack_trace")]
        public string? StackTrace { get; set; }
    }
}
