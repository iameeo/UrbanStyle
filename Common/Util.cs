using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace urban_style_auto_regist.Common
{
    public class Util
    {
        private const string BaseDirectory = @"D:\UrbanStyle";

        /// <summary>
        /// 한 웹드라이버에서 다른 웹드라이버로 쿠키를 복사합니다.
        /// </summary>
        public static void CopyCookies(IWebDriver source, IWebDriver target)
        {
            if (source == null || target == null)
            {
                throw new ArgumentNullException(nameof(source), "Source 또는 Target 웹드라이버가 null입니다.");
            }

            string sourceUrl = source.Url;
            if (string.IsNullOrEmpty(sourceUrl))
            {
                Debug.WriteLine("Source URL이 비어 있어 쿠키 복사를 진행할 수 없습니다.");
                return;
            }

            target.Navigate().GoToUrl(sourceUrl); // 같은 URL로 맞추기

            foreach (var cookie in source.Manage().Cookies.AllCookies)
            {
                try
                {
                    target.Manage().Cookies.AddCookie(cookie);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"쿠키 복사 오류: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 이미지를 다운로드하여 지정된 디렉터리에 저장합니다.
        /// </summary>
        public static async Task<bool> ImgDownloadAsync(string shopName, string type, string imgUrl, string fileName)
        {
            if (string.IsNullOrWhiteSpace(shopName) || string.IsNullOrWhiteSpace(type) ||
                string.IsNullOrWhiteSpace(imgUrl) || string.IsNullOrWhiteSpace(fileName))
            {
                Debug.WriteLine("잘못된 인자 값이 전달되었습니다.");
                return false;
            }

            try
            {
                string folderPath = Path.Combine(BaseDirectory, shopName, type);
                string destFilePath = Path.Combine(folderPath, fileName);

                // 폴더가 없으면 생성
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                using HttpClient httpClient = new();
                await using var responseStream = await httpClient.GetStreamAsync(imgUrl);
                await using var fileStream = File.Create(destFilePath);
                await responseStream.CopyToAsync(fileStream);

                return true; // 성공
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"이미지 다운로드 실패: {ex.Message}");
                return false; // 실패
            }
        }
    }
}
