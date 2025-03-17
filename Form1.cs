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

                // �α���
                loginWait.Until(ExpectedConditions.ElementIsVisible(By.Name("member_id"))).SendKeys(id);
                loginWait.Until(ExpectedConditions.ElementIsVisible(By.Name("member_passwd"))).SendKeys(pw);
                loginWait.Until(ExpectedConditions.ElementToBeClickable(By.ClassName("btnSubmit"))).Click();

                Debug.WriteLine("�α��� ����!");

                Util.CopyCookies(loginDriver, parseDriver);

                loginDriver.Navigate().GoToUrl(url);
                var productLists = loginWait.Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.XPath("//*[@id='contents']/section[2]/div/div[1]/ul/li")));

                foreach (var product in productLists)
                {
                    string productUrl = product.FindElement(By.TagName("a")).GetAttribute("href") + "#detail";
                    parseDriver.Navigate().GoToUrl(productUrl);
                    var parseWait = new WebDriverWait(parseDriver, TimeSpan.FromSeconds(10));

                    Task task = DdmShuInsert(parseWait, shopName, productUrl);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"���� �߻�: {ex.Message}");
            }

            // ���⿡�� WebDriver ����
            loginDriver.Quit();
            parseDriver.Quit();
        }

        private async Task DdmShuInsert(WebDriverWait parseWait, string shopName, string productUrl)
        {
            try
            {
                var element = parseWait.Until(driver => driver.FindElement(By.XPath("//*[@id='contents']")));

                string productCode = element.FindElement(By.XPath(".//div[3]/div/div[2]/div[1]/h1")).Text;
                string productPrice = Regex.Replace(element.FindElement(By.XPath("//*[@id='span_product_price_text']")).Text, @"\D", "");
                string productThumbImg = element.FindElement(By.XPath(".//div[3]/div/div[1]/div[1]/div[1]/div/a/img")).GetAttribute("src");

                var sizes = element.FindElements(By.XPath(".//div[3]/div/div[2]/table/tbody[1]/tr/td/ul/li"))
                                   .Select(li => li.Text)
                                   .ToList();

                var colors = element.FindElements(By.XPath(".//div[3]/div/div[2]/table/tbody[2]/tr/td/ul/li"))
                                    .Select(li => li.Text)
                                    .ToList();

                var imgs = element.FindElements(By.XPath(".//div[@class=\"productDetail\"]/div/img"))
                                  .Select(img => img.GetAttribute("src"))
                                  .ToList();

                var combineProduct = new CombineProduct
                {
                    ProductTitle = productCode,
                    ProductCode = productCode,
                    ProductSize = string.Join(",", sizes),
                    ProductColor = string.Join(",", colors),
                    ProductPrice = productPrice,
                    ProductThumbImg = productThumbImg,
                    ProductShop = shopName,
                    ProductUrl = productUrl,
                    ProductRegdate = DateTime.Now,
                };

                _context.CombineProducts.Add(combineProduct);
                await _context.SaveChangesAsync();  // �񵿱� ����

                int seq = combineProduct.Seq;

                // �񵿱� �̹��� �ٿ�ε�
                var imageDownloadTasks = new List<Task<bool>>
                {
                    Util.ImgDownloadAsync(shopName, "title", productThumbImg, $"{seq}.jpg")
                };

                for (int i = 0; i < imgs.Count; i++)
                {
                    imageDownloadTasks.Add(Util.ImgDownloadAsync(shopName, "desc", imgs[i], $"{seq}_{i}.jpg"));

                    _context.CombineProductImgs.Add(new CombineProductImg
                    {
                        ProductRegdate = DateTime.Now,
                        ProductShop = shopName,
                        ProductSeq = seq,
                        ProductImgSort = i,
                        ProductImgUrl = imgs[i],
                    });
                }

                await _context.SaveChangesAsync();  // �񵿱� ����

                // ��� �̹��� �ٿ�ε带 ���ķ� ����
                await Task.WhenAll(imageDownloadTasks);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
