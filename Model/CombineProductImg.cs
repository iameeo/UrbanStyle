using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace urban_style_auto_regist.Model
{
    [Table("combine_product_img")]
    public class CombineProductImg
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("seq")]
        public int Seq { get; set; }

        [Column("product_regdate")]
        public DateTime? ProductRegdate { get; set; }

        [Column("product_shop")]
        [StringLength(50)]
        public string? ProductShop { get; set; }

        [Column("product_new_img_url")]
        [StringLength(500)]
        public string? ProductNewImgUrl { get; set; }

        [Column("product_seq")]
        public int? ProductSeq { get; set; }

        [Column("product_img_sort")]
        public int? ProductImgSort { get; set; }

        [Column("product_img_url")]
        [StringLength(500)]
        public string? ProductImgUrl { get; set; }
    }
}
