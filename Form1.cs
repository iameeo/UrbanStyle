using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Diagnostics;
using System.Text.Json;
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
                Task task = DdmshuLoginAsync(shopInfo.ShopUrl, shopInfo.ShopId, shopInfo.ShopPw, shopInfo.ShopName);
            }
            else if (shopInfo.ShopName.Equals("girlsgoob", StringComparison.OrdinalIgnoreCase))
            {
                Task task = GirlsgoobLoginAsync(shopInfo.ShopUrl, shopInfo.ShopId, shopInfo.ShopPw, shopInfo.ShopName);
            }
            else if (shopInfo.ShopName.Equals("shubasic", StringComparison.OrdinalIgnoreCase))
            {
                Task task = ShubasicLoginAsync(shopInfo.ShopUrl, shopInfo.ShopId, shopInfo.ShopPw, shopInfo.ShopName);
            }
            else if (shopInfo.ShopName.Equals("shuline", StringComparison.OrdinalIgnoreCase))
            {
                Task task = ShulineLoginAsync(shopInfo.ShopUrl, shopInfo.ShopId, shopInfo.ShopPw, shopInfo.ShopName);
            }
            else
            {
                MessageBox.Show("Unknown shop.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task ShulineLoginAsync(string url, string id, string pw, string shopName)
        {
            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");

            using var loginDriver = new ChromeDriver(options);
            using var parseDriver = new ChromeDriver(options);

            try
            {
                loginDriver.Navigate().GoToUrl(url + "/member/login.html");
                var loginWait = new WebDriverWait(loginDriver, TimeSpan.FromSeconds(10));

                // 로그인
                loginWait.Until(ExpectedConditions.ElementIsVisible(By.Name("member_id"))).SendKeys(id);
                loginWait.Until(ExpectedConditions.ElementIsVisible(By.Name("member_passwd"))).SendKeys(pw);
                loginWait.Until(ExpectedConditions.ElementToBeClickable(By.ClassName("btnSubmit"))).Click();

                Debug.WriteLine("로그인 성공!");

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
                Debug.WriteLine($"오류 발생: {ex.Message}");
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

                    // JavaScript 변수 가져오기
                    string script = "return option_stock_data;";  // JavaScript 변수 호출
                    string jsonData = (string)((IJavaScriptExecutor)parseWait.Until(driver => driver)).ExecuteScript(script);

                    // 작은따옴표 -> 큰따옴표 변환
                    jsonData = jsonData.Replace("'", "\"");

                    // 이스케이프 문제 해결
                    jsonData = Regex.Unescape(jsonData);

                    // JSON 파싱
                    var sizes = new HashSet<string>();  // 중복을 방지할 HashSet 사용
                    var colors = new HashSet<string>();  // 중복을 방지할 HashSet 사용

                    try
                    {
                        var jsonDoc = JsonDocument.Parse(jsonData);
                        Debug.WriteLine("JSON 파싱 성공!");

                        foreach (var json in jsonDoc.RootElement.EnumerateObject())
                        {
                            var jsonParser = json.Value;

                            // "option_value" 속성 파싱
                            if (jsonParser.TryGetProperty("option_value", out var optionValue))
                            {
                                string optionData = optionValue.GetString();
                                if (!string.IsNullOrEmpty(optionData))
                                {
                                    // -로 구분하여 사이즈와 색상 정보 추출
                                    var optionParts = optionData.Split('-');
                                    if (optionParts.Length > 0)
                                    {
                                        sizes.Add(optionParts[1]);  // 사이즈 추가 (중복 자동 제거)
                                    }
                                    if (optionParts.Length > 1)
                                    {
                                        colors.Add(optionParts[0]);  // 색상 추가 (중복 자동 제거)
                                    }
                                }
                            }
                        }

                        // HashSet의 값들을 문자열로 변환하여 출력
                        Debug.WriteLine($"Sizes: {string.Join(",", sizes)}");
                        Debug.WriteLine($"Colors: {string.Join(",", colors)}");
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"JSON 파싱 중 오류 발생: {jsonEx.Message}");
                    }

                    var imgs = element.FindElements(By.XPath("//*[@id=\"prdDetail\"]/*/*/p/img"))
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
                    await _context.SaveChangesAsync();  // 비동기 저장

                    int seq = combineProduct.Seq;

                    // 비동기 이미지 다운로드
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

                    await _context.SaveChangesAsync();  // 비동기 저장

                    // 모든 이미지 다운로드를 병렬로 실행
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

            using var loginDriver = new ChromeDriver(options);
            using var parseDriver = new ChromeDriver(options);

            try
            {
                loginDriver.Navigate().GoToUrl(url + "/member/login.html");
                var loginWait = new WebDriverWait(loginDriver, TimeSpan.FromSeconds(10));

                // 로그인
                loginWait.Until(ExpectedConditions.ElementIsVisible(By.Name("member_id"))).SendKeys(id);
                loginWait.Until(ExpectedConditions.ElementIsVisible(By.Name("member_passwd"))).SendKeys(pw);
                loginWait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//form/div/div/fieldset/a"))).Click();

                Debug.WriteLine("로그인 성공!");

                Util.CopyCookies(loginDriver, parseDriver);

                loginDriver.Navigate().GoToUrl(url + "/product/list.html?cate_no=39");
                var productLists = loginWait.Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.XPath("//*[@id=\"contents\"]/div[1]/div[2]/ul/li")));

                foreach (var product in productLists)
                {
                    string productUrl = product.FindElement(By.TagName("a")).GetAttribute("href") + "#detail";
                    parseDriver.Navigate().GoToUrl(productUrl);
                    var parseWait = new WebDriverWait(parseDriver, TimeSpan.FromSeconds(10));

                    await ShubasicInsert(parseWait, shopName, productUrl);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"오류 발생: {ex.Message}");
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

                if (!_context.CombineProducts.Where(x => x.ProductCode == productCode).Any())
                {
                    string productPrice = Regex.Replace(element.FindElement(By.XPath("//*[@id=\"span_product_price_text\"]")).Text, @"\D", "");
                    string productThumbImg = element.FindElement(By.XPath("//*[@id=\"contents\"]/div[1]/div[2]/div[1]/div[1]/div/a/img")).GetAttribute("src");

                    // JavaScript 변수 가져오기
                    string script = "return option_stock_data;";  // JavaScript 변수 호출
                    string jsonData = (string)((IJavaScriptExecutor)parseWait.Until(driver => driver)).ExecuteScript(script);

                    // 작은따옴표 -> 큰따옴표 변환
                    jsonData = jsonData.Replace("'", "\"");

                    // 이스케이프 문제 해결
                    jsonData = Regex.Unescape(jsonData);

                    // JSON 파싱
                    var sizes = new HashSet<string>();  // 중복을 방지할 HashSet 사용
                    var colors = new HashSet<string>();  // 중복을 방지할 HashSet 사용

                    try
                    {
                        var jsonDoc = JsonDocument.Parse(jsonData);
                        Debug.WriteLine("JSON 파싱 성공!");

                        foreach (var json in jsonDoc.RootElement.EnumerateObject())
                        {
                            var jsonParser = json.Value;

                            // "option_value" 속성 파싱
                            if (jsonParser.TryGetProperty("option_value", out var optionValue))
                            {
                                string optionData = optionValue.GetString();
                                if (!string.IsNullOrEmpty(optionData))
                                {
                                    // -로 구분하여 사이즈와 색상 정보 추출
                                    var optionParts = optionData.Split('-');
                                    if (optionParts.Length > 0)
                                    {
                                        sizes.Add(optionParts[1]);  // 사이즈 추가 (중복 자동 제거)
                                    }
                                    if (optionParts.Length > 1)
                                    {
                                        colors.Add(optionParts[0]);  // 색상 추가 (중복 자동 제거)
                                    }
                                }
                            }
                        }

                        // HashSet의 값들을 문자열로 변환하여 출력
                        Debug.WriteLine($"Sizes: {string.Join(",", sizes)}");
                        Debug.WriteLine($"Colors: {string.Join(",", colors)}");
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"JSON 파싱 중 오류 발생: {jsonEx.Message}");
                    }

                    var imgs = element.FindElements(By.XPath("//*[@id=\"prdDetail\"]/div/*/img"))
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
                    await _context.SaveChangesAsync();  // 비동기 저장

                    int seq = combineProduct.Seq;

                    // 비동기 이미지 다운로드
                    var imageDownloadTasks = new List<Task<bool>>
                {
                    Util.ImgDownloadAsync(shopName, "title", productThumbImg, $"{seq}.jpg")
                };

                    for (int i = 0; i < imgs.Count; i++)
                    {
                        imageDownloadTasks.Add(Util.ImgDownloadAsync(shopName, "desc", imgs[i].Contains("http") ? "" : "https:" + imgs[i], $"{seq}_{i}.jpg"));

                        _context.CombineProductImgs.Add(new CombineProductImg
                        {
                            ProductRegdate = DateTime.Now,
                            ProductShop = shopName,
                            ProductSeq = seq,
                            ProductImgSort = i,
                            ProductImgUrl = imgs[i].Contains("http") ? "" : "https:" + imgs[i],
                        });
                    }

                    await _context.SaveChangesAsync();  // 비동기 저장

                    // 모든 이미지 다운로드를 병렬로 실행
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

            using var loginDriver = new ChromeDriver(options);
            using var parseDriver = new ChromeDriver(options);

            try
            {
                loginDriver.Navigate().GoToUrl(url + "/member/login.html");
                var loginWait = new WebDriverWait(loginDriver, TimeSpan.FromSeconds(10));

                // 로그인
                loginWait.Until(ExpectedConditions.ElementIsVisible(By.Name("member_id"))).SendKeys(id);
                loginWait.Until(ExpectedConditions.ElementIsVisible(By.Name("member_passwd"))).SendKeys(pw);
                loginWait.Until(ExpectedConditions.ElementToBeClickable(By.ClassName("btnSubmit"))).Click();

                Debug.WriteLine("로그인 성공!");

                Util.CopyCookies(loginDriver, parseDriver);

                loginDriver.Navigate().GoToUrl(url + "/product/list.html?cate_no=80");
                var productLists = loginWait.Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.XPath("/html/body/div[1]/div[2]/div/div[2]/div[2]/ul/li/div/div[1]")));

                foreach (var product in productLists)
                {
                    string productUrl = product.FindElement(By.TagName("a")).GetAttribute("href") + "#df-prd-tab-detail";
                    parseDriver.Navigate().GoToUrl(productUrl);
                    var parseWait = new WebDriverWait(parseDriver, TimeSpan.FromSeconds(10));

                    await GirlsgoobInsert(parseWait, shopName, productUrl);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"오류 발생: {ex.Message}");
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


                    // JavaScript 변수 가져오기
                    string script = "return option_stock_data;";  // JavaScript 변수 호출
                    string jsonData = (string)((IJavaScriptExecutor)parseWait.Until(driver => driver)).ExecuteScript(script);

                    // 작은따옴표 -> 큰따옴표 변환
                    jsonData = jsonData.Replace("'", "\"");

                    // 이스케이프 문제 해결
                    jsonData = Regex.Unescape(jsonData);

                    // JSON 파싱
                    var sizes = new HashSet<string>();  // 중복을 방지할 HashSet 사용
                    var colors = new HashSet<string>();  // 중복을 방지할 HashSet 사용

                    try
                    {
                        var jsonDoc = JsonDocument.Parse(jsonData);
                        Debug.WriteLine("JSON 파싱 성공!");

                        foreach (var json in jsonDoc.RootElement.EnumerateObject())
                        {
                            var jsonParser = json.Value;

                            // "option_value" 속성 파싱
                            if (jsonParser.TryGetProperty("option_value", out var optionValue))
                            {
                                string optionData = optionValue.GetString();
                                if (!string.IsNullOrEmpty(optionData))
                                {
                                    // -로 구분하여 사이즈와 색상 정보 추출
                                    var optionParts = optionData.Split('-');
                                    if (optionParts.Length > 0)
                                    {
                                        sizes.Add(optionParts[1]);  // 사이즈 추가 (중복 자동 제거)
                                    }
                                    if (optionParts.Length > 1)
                                    {
                                        colors.Add(optionParts[0]);  // 색상 추가 (중복 자동 제거)
                                    }
                                }
                            }
                        }

                        // HashSet의 값들을 문자열로 변환하여 출력
                        Debug.WriteLine($"Sizes: {string.Join(",", sizes)}");
                        Debug.WriteLine($"Colors: {string.Join(",", colors)}");
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"JSON 파싱 중 오류 발생: {jsonEx.Message}");
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
                    await _context.SaveChangesAsync();  // 비동기 저장

                    int seq = combineProduct.Seq;

                    // 비동기 이미지 다운로드
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

                    await _context.SaveChangesAsync();  // 비동기 저장

                    // 모든 이미지 다운로드를 병렬로 실행
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

            using var loginDriver = new ChromeDriver(options);
            using var parseDriver = new ChromeDriver(options);

            try
            {
                loginDriver.Navigate().GoToUrl(url + "/member/login.html");
                var loginWait = new WebDriverWait(loginDriver, TimeSpan.FromSeconds(10));

                // 로그인
                loginWait.Until(ExpectedConditions.ElementIsVisible(By.Name("member_id"))).SendKeys(id);
                loginWait.Until(ExpectedConditions.ElementIsVisible(By.Name("member_passwd"))).SendKeys(pw);
                loginWait.Until(ExpectedConditions.ElementToBeClickable(By.ClassName("btnSubmit"))).Click();

                Debug.WriteLine("로그인 성공!");

                Util.CopyCookies(loginDriver, parseDriver);

                loginDriver.Navigate().GoToUrl(url);
                var productLists = loginWait.Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.XPath("//*[@id='contents']/section[2]/div/div[1]/ul/li")));

                foreach (var product in productLists)
                {
                    string productUrl = product.FindElement(By.TagName("a")).GetAttribute("href") + "#detail";
                    parseDriver.Navigate().GoToUrl(productUrl);
                    var parseWait = new WebDriverWait(parseDriver, TimeSpan.FromSeconds(10));

                    await DdmShuInsert(parseWait, shopName, productUrl);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"오류 발생: {ex.Message}");
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
                    await _context.SaveChangesAsync();  // 비동기 저장

                    int seq = combineProduct.Seq;

                    // 비동기 이미지 다운로드
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

                    await _context.SaveChangesAsync();  // 비동기 저장

                    // 모든 이미지 다운로드를 병렬로 실행
                    await Task.WhenAll(imageDownloadTasks);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
            }
        }

        private void BtnAll_Click(object sender, EventArgs e)
        {
            foreach (var item in ShopList.Items)
            {
                // ComboBox 항목을 선택
                ShopList.SelectedItem = item;
                BtnStart_Click(sender, e);
            }
        }
    }
}
