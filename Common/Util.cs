using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace urban_style_auto_regist.Common
{
    public class Util
    {
        public static void CopyCookies(IWebDriver source, IWebDriver target)
        {
            target.Navigate().GoToUrl(source.Url); // 같은 URL로 맞추기
            foreach (var cookie in source.Manage().Cookies.AllCookies)
            {
                try
                {
                    target.Manage().Cookies.AddCookie(cookie);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"쿠키 복사 오류: {ex.Message}");
                }
            }
        }

        public static bool ImgDownload(string shopName, string type, string imgUrl, string fileName)
        {
            try
            {
                // 경로 조합
                string baseDir = @"D:\urban-style-auto-regist";
                string folderPath = Path.Combine(baseDir, shopName, type);
                string destFilePath = Path.Combine(folderPath, fileName);

                // 폴더가 없으면 생성
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                using (WebClient webClient = new WebClient())
                {
                    webClient.DownloadFile(imgUrl, destFilePath);
                }

                return true; // 성공
            }
            catch (Exception ex)
            {
                // 에러 로그 남기기 (선택)
                Console.WriteLine($"이미지 다운로드 실패: {ex.Message}");
                return false; // 실패
            }
        }
    }
}
