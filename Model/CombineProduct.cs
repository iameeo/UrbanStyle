using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace urban_style_auto_regist.Model
{
    [Table("combine_product")]  // 테이블 이름과 일치하도록 설정
    public class CombineProduct
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]  // 자동 증가 설정
        [Column("seq")]  // 기본 키 설정
        public int Seq { get; set; }

        [Column("product_shop")]
        [StringLength(50)]
        public string? ProductShop { get; set; }

        [Column("product_regdate")]
        public DateTime? ProductRegdate { get; set; }

        [Column("product_code")]
        [StringLength(50)]
        public string? ProductCode { get; set; }

        [Column("product_url")]
        [StringLength(500)]
        public string? ProductUrl { get; set; }

        [Column("product_title")]
        [StringLength(200)]
        public string? ProductTitle { get; set; }

        [Column("product_text")]
        public string? ProductText { get; set; }  // MEDIUMTEXT 타입은 string으로 처리

        [Column("product_price")]
        [StringLength(50)]
        public string? ProductPrice { get; set; }

        [Column("product_thumbImg")]
        [StringLength(200)]
        public string? ProductThumbImg { get; set; }

        [Column("product_color")]
        [StringLength(200)]
        public string? ProductColor { get; set; }

        [Column("product_size")]
        [StringLength(200)]
        public string? ProductSize { get; set; }

        [Column("product_price2")]
        [StringLength(50)]
        public string? ProductPrice2 { get; set; }

        [Column("product_price3")]
        [StringLength(50)]
        public string? ProductPrice3 { get; set; }

        [Column("isGodo")]
        public int IsGodo { get; set; }

        [Column("product_new_title")]
        [StringLength(100)]
        public string? ProductNewTitle { get; set; }

        [Column("product_category")]
        [StringLength(10)]
        public string? ProductCategory { get; set; }
    }
}
