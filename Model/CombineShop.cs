using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace urban_style_auto_regist.Model
{
    [Table("combine_shop")]  // 테이블 이름과 일치하도록 설정
    public class CombineShop
    {
        [Key]
        [Column("seq")]  // 기본 키 설정
        public int Seq { get; set; }

        [Column("shop_name")]
        [StringLength(50)]
        public string ShopName { get; set; } = string.Empty;

        [Column("shop_url")]
        [StringLength(500)]
        public string ShopUrl { get; set; } = string.Empty;

        [Column("shop_id")]
        [StringLength(50)]
        public string ShopId { get; set; } = string.Empty;

        [Column("shop_pw")]
        [StringLength(50)]
        public string ShopPw { get; set; } = string.Empty;

        [Column("shop_open")]
        [StringLength(1)]
        public string ShopOpen { get; set; } = string.Empty;
    }
}
