using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using urban_style_auto_regist.Common;
using urban_style_auto_regist.Model;
using Timer = System.Windows.Forms.Timer;

namespace urban_style_auto_regist
{
    public partial class Form1 : Form
    {
        private readonly AppDbContext _context;
        private Timer timer;

        public Form1(AppDbContext context)
        {
            InitializeComponent();
            InitializeTimer();

            _context = context;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var shops = _context.CombineShops.Where(x => x.ShopOpen == "Y").ToList();
            ShopList.Items.AddRange(shops.Select(shop => shop.ShopName).ToArray());
            if (ShopList.Items.Count > 0) ShopList.SelectedIndex = 0;
        }

        private void InitializeTimer()
        {
            timer = new Timer();
            timer.Interval = 30 * 60 * 1000; // 30�� (�и��� ����)
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // ��ư Ŭ�� �̺�Ʈ Ʈ����
            BtnAll.PerformClick();
        }

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            string selectedShop = ShopList.Text;
            if (string.IsNullOrWhiteSpace(selectedShop))
            {
                MessageBox.Show("Please select a shop.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var shopInfo = await _context.CombineShops.FirstOrDefaultAsync(x => x.ShopName == selectedShop);
            if (shopInfo == null)
            {
                MessageBox.Show("Shop information not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            await ShopProcess(shopInfo);
        }

        private async Task ShulineLoginAsync(string url, string id, string pw, string shopName)
        {
            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");
            options.AddArgument("--headless"); // ������ â �����

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
                var productLists = loginWait.Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.XPath("//*[@id=\"contents\"]/div[5]/ul/li")));

                foreach (var product in productLists)
                {
                    string productUrl = product.FindElement(By.TagName("a")).GetAttribute("href") + "#detail";
                    parseDriver.Navigate().GoToUrl(productUrl);
                    var parseWait = new WebDriverWait(parseDriver, TimeSpan.FromSeconds(10));

                    await ShulineInsert(parseWait, shopName, productUrl);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"���� �߻�: {ex.Message}");
            }
            finally
            {
                loginDriver.Quit();
                parseDriver.Quit();
            }
        }

        private async Task ShulineInsert(WebDriverWait parseWait, string shopName, string productUrl)
        {
            try
            {
                var element = parseWait.Until(driver => driver.FindElement(By.XPath("//*[@id='contents']")));

                string productCode = element.FindElement(By.XPath("//*[@id=\"df-product-detail\"]/div/div[2]/div/div/div[1]/div[1]/div[1]/h2")).Text;

                if (!_context.CombineProducts.Where(x => x.ProductCode == productCode).Any())
                {
                    string productPrice = Regex.Replace(element.FindElement(By.XPath("//*[@id='span_product_price_text']")).Text, @"\D", "");
                    string productThumbImg = element.FindElement(By.XPath("//*[@id=\"df-product-detail\"]/div/div[1]/div/div/div[1]/span/img")).GetAttribute("src");
                    string productNewTitle = element.FindElement(By.CssSelector("tr.prd_model_css.xans-record- > td > span:nth-child(1)")).Text;
                    // JavaScript ���� ��������
                    string script = "return option_stock_data;";  // JavaScript ���� ȣ��
                    string jsonData = (string)((IJavaScriptExecutor)parseWait.Until(driver => driver)).ExecuteScript(script);

                    // ��������ǥ -> ū����ǥ ��ȯ
                    jsonData = jsonData.Replace("'", "\"");

                    // �̽������� ���� �ذ�
                    jsonData = Regex.Unescape(jsonData);

                    // JSON �Ľ�
                    var sizes = new HashSet<string>();  // �ߺ��� ������ HashSet ���
                    var colors = new HashSet<string>();  // �ߺ��� ������ HashSet ���

                    try
                    {
                        var jsonDoc = JsonDocument.Parse(jsonData);
                        Debug.WriteLine("JSON �Ľ� ����!");

                        foreach (var json in jsonDoc.RootElement.EnumerateObject())
                        {
                            var jsonParser = json.Value;

                            // "option_value" �Ӽ� �Ľ�
                            if (jsonParser.TryGetProperty("option_value", out var optionValue))
                            {
                                string optionData = optionValue.GetString();
                                if (!string.IsNullOrEmpty(optionData))
                                {
                                    // -�� �����Ͽ� ������� ���� ���� ����
                                    var optionParts = optionData.Split('-');
                                    if (optionParts.Length > 0)
                                    {
                                        sizes.Add(optionParts[1]);  // ������ �߰� (�ߺ� �ڵ� ����)
                                    }
                                    if (optionParts.Length > 1)
                                    {
                                        colors.Add(optionParts[0]);  // ���� �߰� (�ߺ� �ڵ� ����)
                                    }
                                }
                            }
                        }

                        // HashSet�� ������ ���ڿ��� ��ȯ�Ͽ� ���
                        Debug.WriteLine($"Sizes: {string.Join(",", sizes)}");
                        Debug.WriteLine($"Colors: {string.Join(",", colors)}");
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"JSON �Ľ� �� ���� �߻�: {jsonEx.Message}");
                    }

                    var imgs = element.FindElements(By.XPath("//*[@id=\"prdDetail\"]/*/*/p/img"))
                                      .Select(img => img.GetAttribute("src"))
                                      .ToList();

                    var combineProduct = new CombineProduct
                    {
                        ProductTitle = productCode,
                        ProductNewTitle = productNewTitle,
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
            }
        }

        private async Task ShubasicLoginAsync(string url, string id, string pw, string shopName)
        {
            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");
            options.AddArgument("--headless"); // ������ â �����

            using var loginDriver = new ChromeDriver(options);
            using var parseDriver = new ChromeDriver(options);

            try
            {
                loginDriver.Navigate().GoToUrl(url + "/member/login.html");
                var loginWait = new WebDriverWait(loginDriver, TimeSpan.FromSeconds(10));

                // �α���
                loginWait.Until(ExpectedConditions.ElementIsVisible(By.Name("member_id"))).SendKeys(id);
                loginWait.Until(ExpectedConditions.ElementIsVisible(By.Name("member_passwd"))).SendKeys(pw);
                loginWait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//form/div/div/fieldset/a"))).Click();

                Debug.WriteLine("�α��� ����!");

                Util.CopyCookies(loginDriver, parseDriver);

                loginDriver.Navigate().GoToUrl(url + "/product/list.html?cate_no=39");
                var productLists = loginWait.Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.XPath("//*[@id=\"contents\"]/div[1]/div[2]/ul/li")));

                foreach (var product in productLists)
                {
                    string productUrl = product.FindElement(By.TagName("a")).GetAttribute("href");
                    parseDriver.Navigate().GoToUrl(productUrl);
                    var parseWait = new WebDriverWait(parseDriver, TimeSpan.FromSeconds(10));

                    await ShubasicInsert(parseWait, shopName, productUrl);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"���� �߻�: {ex.Message}");
            }
            finally
            {
                loginDriver.Quit();
                parseDriver.Quit();
            }
        }

        private async Task ShubasicInsert(WebDriverWait parseWait, string shopName, string productUrl)
        {
            try
            {
                var element = parseWait.Until(driver => driver.FindElement(By.XPath("//*[@id='contents']")));
                var metaTag = element.FindElement(By.XPath("//meta[@property='og:title']"));
                var content = metaTag.GetAttribute("content").Split("_");
                string productCode = content[0];
                if (productCode.Contains('('))
                {
                    productCode = productCode.Split("(")[0].Trim();
                }

                if (!_context.CombineProducts.Where(x => x.ProductCode == productCode).Any())
                {
                    string productPrice = Regex.Replace(element.FindElement(By.XPath("//*[@id=\"span_product_price_text\"]")).Text, @"\D", "");
                    string productThumbImg = element.FindElement(By.XPath("//*[@id=\"contents\"]/div[1]/div[2]/div[1]/div[1]/div/a/img")).GetAttribute("src");

                    // JavaScript ���� ��������
                    string script = "return option_stock_data;";  // JavaScript ���� ȣ��
                    string jsonData = (string)((IJavaScriptExecutor)parseWait.Until(driver => driver)).ExecuteScript(script);

                    // ��������ǥ -> ū����ǥ ��ȯ
                    jsonData = jsonData.Replace("'", "\"");

                    // �̽������� ���� �ذ�
                    jsonData = Regex.Unescape(jsonData);

                    // JSON �Ľ�
                    var sizes = new HashSet<string>();  // �ߺ��� ������ HashSet ���
                    var colors = new HashSet<string>();  // �ߺ��� ������ HashSet ���

                    try
                    {
                        var jsonDoc = JsonDocument.Parse(jsonData);
                        Debug.WriteLine("JSON �Ľ� ����!");

                        foreach (var json in jsonDoc.RootElement.EnumerateObject())
                        {
                            var jsonParser = json.Value;

                            // "option_value" �Ӽ� �Ľ�
                            if (jsonParser.TryGetProperty("option_value", out var optionValue))
                            {
                                string optionData = optionValue.GetString();
                                if (!string.IsNullOrEmpty(optionData))
                                {
                                    // -�� �����Ͽ� ������� ���� ���� ����
                                    var optionParts = optionData.Split('-');
                                    if (optionParts.Length > 0)
                                    {
                                        sizes.Add(optionParts[1]);  // ������ �߰� (�ߺ� �ڵ� ����)
                                    }
                                    if (optionParts.Length > 1)
                                    {
                                        colors.Add(optionParts[0]);  // ���� �߰� (�ߺ� �ڵ� ����)
                                    }
                                }
                            }
                        }

                        // HashSet�� ������ ���ڿ��� ��ȯ�Ͽ� ���
                        Debug.WriteLine($"Sizes: {string.Join(",", sizes)}");
                        Debug.WriteLine($"Colors: {string.Join(",", colors)}");
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"JSON �Ľ� �� ���� �߻�: {jsonEx.Message}");
                    }

                    var imgs = element.FindElements(By.XPath("//*[@id=\"prdDetail\"]//img"))
                                      .Select(img => img.GetAttribute("ec-data-src") ?? img.GetAttribute("src")) // �켱������ ec-data-src, ������ src
                                      .Where(src => !string.IsNullOrEmpty(src)) // null �Ǵ� �� ���� ����
                                      .Distinct() // �ߺ� ����
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
                        string imgUrl = !imgs[i].Contains("http") ? "https:" + imgs[i] : imgs[i];

                        imageDownloadTasks.Add(Util.ImgDownloadAsync(shopName, "desc", imgUrl, $"{seq}_{i}.jpg"));

                        _context.CombineProductImgs.Add(new CombineProductImg
                        {
                            ProductRegdate = DateTime.Now,
                            ProductShop = shopName,
                            ProductSeq = seq,
                            ProductImgSort = i,
                            ProductImgUrl = imgUrl,
                        });
                    }

                    await _context.SaveChangesAsync();  // �񵿱� ����

                    // ��� �̹��� �ٿ�ε带 ���ķ� ����
                    await Task.WhenAll(imageDownloadTasks);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
            }
        }

        private async Task GirlsgoobLoginAsync(string url, string id, string pw, string shopName)
        {
            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");
            options.AddArgument("--headless"); // ������ â �����

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

                loginDriver.Navigate().GoToUrl(url + "/product/list.html?cate_no=80");
                var productLists = loginWait.Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.XPath("/html/body/div[1]/div[2]/div/div[2]/div[2]/ul/li/div/div[1]")));

                foreach (var product in productLists)
                {
                    string productUrl = product.FindElement(By.TagName("a")).GetAttribute("href");
                    parseDriver.Navigate().GoToUrl(productUrl);
                    var parseWait = new WebDriverWait(parseDriver, TimeSpan.FromSeconds(10));

                    await GirlsgoobInsert(parseWait, shopName, productUrl);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"���� �߻�: {ex.Message}");
            }
            finally
            {
                loginDriver.Quit();
                parseDriver.Quit();
            }
        }

        private async Task GirlsgoobInsert(WebDriverWait parseWait, string shopName, string productUrl)
        {
            try
            {
                var element = parseWait.Until(driver => driver.FindElement(By.XPath("//*[@id='contents']")));

                string productCode = element.FindElement(By.XPath("//*[@id=\"df-product-detail\"]/div/div[2]/div/div/div[1]/div[1]/div[1]/h2")).Text;

                if (!_context.CombineProducts.Where(x => x.ProductCode == productCode).Any())
                {
                    string productPrice = Regex.Replace(element.FindElement(By.XPath("//*[@id=\"span_product_price_text\"]")).Text, @"\D", "");
                    string productThumbImg = element.FindElement(By.XPath("//*[@id=\"df-product-detail\"]/div/div[1]/div/div/div[1]/span/img")).GetAttribute("src");


                    // JavaScript ���� ��������
                    string script = "return option_stock_data;";  // JavaScript ���� ȣ��
                    string jsonData = (string)((IJavaScriptExecutor)parseWait.Until(driver => driver)).ExecuteScript(script);

                    // ��������ǥ -> ū����ǥ ��ȯ
                    jsonData = jsonData.Replace("'", "\"");

                    // �̽������� ���� �ذ�
                    jsonData = Regex.Unescape(jsonData);

                    // JSON �Ľ�
                    var sizes = new HashSet<string>();  // �ߺ��� ������ HashSet ���
                    var colors = new HashSet<string>();  // �ߺ��� ������ HashSet ���

                    try
                    {
                        var jsonDoc = JsonDocument.Parse(jsonData);
                        Debug.WriteLine("JSON �Ľ� ����!");

                        foreach (var json in jsonDoc.RootElement.EnumerateObject())
                        {
                            var jsonParser = json.Value;

                            // "option_value" �Ӽ� �Ľ�
                            if (jsonParser.TryGetProperty("option_value", out var optionValue))
                            {
                                string optionData = optionValue.GetString();
                                if (!string.IsNullOrEmpty(optionData))
                                {
                                    // -�� �����Ͽ� ������� ���� ���� ����
                                    var optionParts = optionData.Split('-');
                                    if (optionData.Contains("-"))
                                    {
                                        if (optionParts.Length > 0)
                                        {
                                            sizes.Add(optionParts[1]);  // ������ �߰� (�ߺ� �ڵ� ����)
                                        }
                                        if (optionParts.Length > 1)
                                        {
                                            colors.Add(optionParts[0]);  // ���� �߰� (�ߺ� �ڵ� ����)
                                        }
                                    }
                                    else
                                    {
                                        colors.Add(optionParts[0]);  // ���� �߰� (�ߺ� �ڵ� ����)
                                    }                                    
                                }
                            }
                        }

                        // HashSet�� ������ ���ڿ��� ��ȯ�Ͽ� ���
                        Debug.WriteLine($"Sizes: {string.Join(",", sizes)}");
                        Debug.WriteLine($"Colors: {string.Join(",", colors)}");
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"JSON �Ľ� �� ���� �߻�: {jsonEx.Message}");
                    }

                    var imgs = element.FindElements(By.XPath("//*[@id=\"prdDetail\"]/div[3]/div[2]/*/img"))
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
            }
        }

        private async Task DdmshuLoginAsync(string url, string id, string pw, string shopName)
        {
            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");
            options.AddArgument("--headless"); // ������ â �����

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
                loginWait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector("#contents > section:nth-child(4) > div > div.xans-element-.xans-product.xans-product-listmore-1.xans-product-listmore.xans-product-1.more > a"))).Click();
                loginDriver.Navigate().GoToUrl(url);
                loginWait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector("#contents > section:nth-child(4) > div > div.xans-element-.xans-product.xans-product-listmore-1.xans-product-listmore.xans-product-1.more > a"))).Click();

                var productLists = loginWait.Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.XPath("//*[@id='contents']/section[2]/div/div[1]/ul/li")));

                foreach (var product in productLists)
                {
                    string productUrl = product.FindElement(By.TagName("a")).GetAttribute("href");
                    parseDriver.Navigate().GoToUrl(productUrl);
                    var parseWait = new WebDriverWait(parseDriver, TimeSpan.FromSeconds(10));

                    await DdmShuInsert(parseWait, shopName, productUrl);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"���� �߻�: {ex.Message}");
            }
            finally
            {
                loginDriver.Quit();
                parseDriver.Quit();
            }
        }

        private async Task DdmShuInsert(WebDriverWait parseWait, string shopName, string productUrl)
        {
            try
            {
                var element = parseWait.Until(driver => driver.FindElement(By.XPath("//*[@id='contents']")));

                string productCode = element.FindElement(By.XPath(".//div[3]/div/div[2]/div[1]/h1")).Text;

                if (!_context.CombineProducts.Where(x => x.ProductCode == productCode).Any())
                {
                    string productPrice = Regex.Replace(element.FindElement(By.XPath("//*[@id='span_product_price_text']")).Text, @"\D", "");
                    string productThumbImg = element.FindElement(By.XPath(".//div[3]/div/div[1]/div[1]/div[1]/div/a/img")).GetAttribute("src");

                    var sizes = element.FindElements(By.XPath(".//div[3]/div/div[2]/table/tbody[1]/tr/td/ul/li"))
                                       .Select(li => li.Text)
                                       .ToList();

                    var colors = element.FindElements(By.XPath(".//div[3]/div/div[2]/table/tbody[2]/tr/td/ul/li"))
                                        .Select(li => li.Text)
                                        .ToList();

                    var imgs = element.FindElements(By.XPath(".//div[@class=\"productDetail\"]//img"))
                                      .Select(img => img.GetAttribute("ec-data-src") ?? img.GetAttribute("src")) // �켱������ ec-data-src, ������ src
                                      .Where(src => !string.IsNullOrEmpty(src)) // null �Ǵ� �� ���� ����
                                      .Distinct() // �ߺ� ����
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
                        string imgUrl = GetDomainWithProtocol(parseWait, imgs[i]);

                        imageDownloadTasks.Add(Util.ImgDownloadAsync(shopName, "desc", imgUrl, $"{seq}_{i}.jpg"));

                        _context.CombineProductImgs.Add(new CombineProductImg
                        {
                            ProductRegdate = DateTime.Now,
                            ProductShop = shopName,
                            ProductSeq = seq,
                            ProductImgSort = i,
                            ProductImgUrl = imgUrl,
                        });
                    }

                    await _context.SaveChangesAsync();  // �񵿱� ����

                    // ��� �̹��� �ٿ�ε带 ���ķ� ����
                    await Task.WhenAll(imageDownloadTasks);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
            }
        }

        private string GetDomainWithProtocol(WebDriverWait parseWait, string v)
        {
            // ���� URL ��������
            string currentUrl = parseWait.Until(driver => driver).Url;

            // �������� + ������ ����
            Uri uri = new Uri(currentUrl);
            string domainWithProtocol = uri.Scheme + "://" + uri.Host + v;

            return domainWithProtocol;
        }

        private async void BtnAll_Click(object sender, EventArgs e)
        {
            foreach (var item in ShopList.Items)
            {
                string shopName = item.ToString();
                var shopInfo = _context.CombineShops.FirstOrDefault(x => x.ShopName == shopName);

                if (shopInfo == null)
                {
                    MessageBox.Show($"Shop information for '{shopName}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }

                await ShopProcess(shopInfo); // ���������� ����
            }
        }

        private async Task ShopProcess(CombineShop shopInfo)
        {
            try
            {
                var shopLoginMethods = new Dictionary<string, Func<string, string, string, string, Task>>
                {
                    { "ddmshu", DdmshuLoginAsync },
                    { "girlsgoob", GirlsgoobLoginAsync },
                    { "shubasic", ShubasicLoginAsync },
                    { "shuline", ShulineLoginAsync }
                };

                if (shopLoginMethods.TryGetValue(shopInfo.ShopName.ToLower(), out var loginMethod))
                {
                    await loginMethod(shopInfo.ShopUrl, shopInfo.ShopId, shopInfo.ShopPw, shopInfo.ShopName);
                }
                else
                {
                    MessageBox.Show($"Unknown shop: {shopInfo.ShopName}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error logging into {shopInfo.ShopName}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
