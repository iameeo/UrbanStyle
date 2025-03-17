using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Diagnostics;
using System.Text.RegularExpressions;
using urban_style_auto_regist.Common;
using urban_style_auto_regist.Model;

namespace urban_style_auto_regist
{
    public partial class Form1 : Form
    {
        private readonly AppDbContext _context;

        public Form1(AppDbContext context)
        {
            InitializeComponent();
            _context = context;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var shops = _context.CombineShops.Where(x => x.ShopOpen == "Y").ToList();
            ShopList.Items.AddRange(shops.Select(shop => shop.ShopName).ToArray());
            if (ShopList.Items.Count > 0) ShopList.SelectedIndex = 0;
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            var shopInfo = _context.CombineShops.FirstOrDefault(x => x.ShopName == ShopList.Text);
            if (shopInfo == null)
            {
                MessageBox.Show("Shop information not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (shopInfo.ShopName.Equals("ddmshu", StringComparison.OrdinalIgnoreCase))
            {
                PerformLogin(shopInfo.ShopUrl, shopInfo.ShopId, shopInfo.ShopPw, shopInfo.ShopName);
            }
            else
            {
                MessageBox.Show("Unknown shop.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void PerformLogin(string url, string id, string pw, string shopName)
        {
            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");

            using var loginDriver = new ChromeDriver(options);
            using var parseDriver = new ChromeDriver(options);

            try
            {
                loginDriver.Navigate().GoToUrl(url + "/member/login.html");

                var loginWait = new WebDriverWait(loginDriver, TimeSpan.FromSeconds(10));

                // 아이디 및 비밀번호 입력
                loginWait.Until(ExpectedConditions.ElementIsVisible(By.Name("member_id"))).SendKeys(id);
                loginWait.Until(ExpectedConditions.ElementIsVisible(By.Name("member_passwd"))).SendKeys(pw);

                // 로그인 버튼 클릭
                loginWait.Until(ExpectedConditions.ElementToBeClickable(By.ClassName("btnSubmit"))).Click();

                Console.WriteLine("로그인 성공!");

                Util.CopyCookies(loginDriver, parseDriver);
            }
            catch (Exception ex)
            {
                loginDriver.Quit();
                parseDriver.Quit();
                Console.WriteLine($"오류 발생: {ex.Message}");
            }finally {
                loginDriver.Navigate().GoToUrl(url);
                var loginWait = new WebDriverWait(loginDriver, TimeSpan.FromSeconds(10));
                var productLists = loginWait.Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.XPath("//*[@id='contents']/section[2]/div/div[1]/ul/li")));

                foreach (var product in productLists)
                {
                    string productUrl = product.FindElement(By.TagName("a")).GetAttribute("href")+ "#detail";
                    parseDriver.Navigate().GoToUrl(productUrl);
                    var parseWait = new WebDriverWait(parseDriver, TimeSpan.FromSeconds(10));

                    DdmShuInsert(parseWait, shopName, productUrl);
                }
            }
        }

        private void DdmShuInsert(WebDriverWait parseWait, string shopName, string productUrl)
        {
            try
            {
                IWebElement element = parseWait.Until(driver => driver.FindElement(By.XPath("//*[@id='contents']")));

                string ProductCode = element.FindElement(By.XPath(".//div[3]/div/div[2]/div[1]/h1")).Text;
                string ProductPrice = Regex.Replace(element.FindElement(By.XPath("//*[@id='span_product_price_text']")).Text.ToString(), @"\D", "");  // 숫자 아닌 문자를 제거
                string ProductThumbImg = element.FindElement(By.XPath(".//div[3]/div/div[1]/div[1]/div[1]/div/a/img")).GetAttribute("src");
                
                string ProductSize = string.Empty;
                string ProductColor = string.Empty;

                var ProductSizes = element.FindElements(By.XPath(".//div[3]/div/div[2]/table/tbody[1]/tr/td/ul/li"));
                var sizes = ProductSizes.Select(li => li.Text).ToList();

                var ProductColors = element.FindElements(By.XPath(".//div[3]/div/div[2]/table/tbody[2]/tr/td/ul/li"));
                var colos = ProductColors.Select(li => li.Text).ToList();

                var ProductDetailImage = element.FindElements(By.XPath(".//div[@class=\"productDetail\"]/div/img"));
                var imgs = ProductDetailImage.Select(img => img.GetAttribute("src")).ToList();

                ProductSize = string.Join(",", sizes);
                ProductColor = string.Join(",", colos);

                CombineProduct combineProduct = new()
                {
                    ProductTitle = ProductCode,
                    ProductCode = ProductCode,
                    ProductSize = ProductSize,
                    ProductColor = ProductColor,
                    ProductPrice = ProductPrice,
                    ProductThumbImg = ProductThumbImg,
                    ProductShop = shopName,
                    ProductUrl = productUrl,
                    ProductRegdate = DateTime.Now,
                };

                _context.CombineProducts.Add(combineProduct);
                _context.SaveChanges();

                int seq = combineProduct.Seq;

                Task<bool> titleImgDown = Util.ImgDownloadAsync(shopName, "title", ProductThumbImg, seq + ".jpg");

                for(var i=0; i<imgs.Count; i++)
                {
                    Task<bool> descImgDown = Util.ImgDownloadAsync(shopName, "desc", imgs[i], seq + "_" + i + ".jpg");

                    CombineProductImg combineProductImg = new()
                    {
                        ProductRegdate = DateTime.Now,
                        ProductShop = shopName,
                        ProductSeq = seq,
                        ProductImgSort = i,
                        ProductImgUrl = imgs[i],
                    };

                    _context.CombineProductImgs.Add(combineProductImg);
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
