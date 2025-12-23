using APIGigaChatImage_True.Models;
using APIGigaChatImage_True.Models.Response;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace APIGigaChatImage_True
{
    public class Program
    {
        static string ClientId = "019b493f-6c86-7084-8ea6-3ca097ab1023";
        static string AuthorizationKey = "MDE5YjQ5M2YtNmM4Ni03MDg0LThlYTYtM2NhMDk3YWIxMDIzOjA4NTYyZDJiLTI5NjYtNDk4Mi1iYWNmLWY4N2I1NWYxMTlkOQ==";
        static async Task Main(string[] args)
        {
            string Token = await GetToken(ClientId, AuthorizationKey);

            if (Token == null)
            {
                Console.WriteLine("Не удалось получить токен");
                return;
            }
            while (true)
            {
                Console.Write("Сообщение: ");
                string Message = Console.ReadLine();
                string imagePath = await GenerateImage(Token, Message);

                WallpaperSetter.SetWallpaper(imagePath);
            }
        }

        public static async Task<string> GetToken(string rqUID, string bearer)
        {
            string ReturnToken = null;
            string Url = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";

            using (HttpClientHandler Handler = new HttpClientHandler())
            {
                Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, ss1PolicyErrors) => true;

                using (HttpClient Clien = new HttpClient(Handler))
                {
                    HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Url);

                    Request.Headers.Add("Accept", "application/json");
                    Request.Headers.Add("RqUID", rqUID);
                    Request.Headers.Add("Authorization", $"Bearer {bearer}");

                    var Data = new List<KeyValuePair<string, string>> {
                        new KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
                    };

                    Request.Content = new FormUrlEncodedContent(Data);

                    HttpResponseMessage Response = await Clien.SendAsync(Request);

                    if (Response.IsSuccessStatusCode)
                    {
                        string ResponseContent = await Response.Content.ReadAsStringAsync();
                        ResponseToken Token = JsonConvert.DeserializeObject<ResponseToken>(ResponseContent);
                        ReturnToken = Token.access_token;
                    }
                }
            }
            return ReturnToken;
        }
        public static async Task<string> GenerateImage(string token, string prompt)
        {
            string apiUrl = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";

            using (HttpClientHandler handler = new HttpClientHandler())
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

                using (HttpClient client = new HttpClient(handler))
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                    client.DefaultRequestHeaders.Add("X-Client-ID", ClientId);
                    client.Timeout = TimeSpan.FromSeconds(120);

                    var requestData = new
                    {
                        model = "GigaChat",
                        messages = new[]
                        {
                                new { role = "user", content = prompt }
                            },
                        function_call = "auto"
                    };

                    string json = JsonConvert.SerializeObject(requestData);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    Console.WriteLine($"Отправка запроса в GigaChat...");
                    HttpResponseMessage response = await client.PostAsync(apiUrl, content);

                    Console.WriteLine($"Статус ответа: {response.StatusCode}");

                    if (response.IsSuccessStatusCode)
                    {
                        string responseJson = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"✓ Получен ответ от API");

                        var data = JObject.Parse(responseJson);
                        string htmlContent = data["choices"]?[0]?["message"]?["content"]?.ToString();

                        if (string.IsNullOrEmpty(htmlContent))
                        {
                            Console.WriteLine("✗ Пустой ответ от нейросети");
                            return null;
                        }

                        Console.WriteLine($"HTML ответ (первые 200 символов): {htmlContent.Substring(0, Math.Min(200, htmlContent.Length))}");

                        var match = Regex.Match(htmlContent, @"src=""([^""]+)""");

                        if (!match.Success)
                        {
                            match = Regex.Match(htmlContent, @"<img[^>]+src=['""]([^'""]+)['""]");

                            if (!match.Success)
                            {
                                Console.WriteLine("✗ Не найдено изображение в ответе");
                                return null;
                            }
                        }

                        string imageId = match.Groups[1].Value;
                        Console.WriteLine($"✓ ID изображения получен: {imageId}");

                        string fileUrl = $"https://gigachat.devices.sberbank.ru/api/v1/files/{imageId}/content";
                        Console.WriteLine($"URL для скачивания: {fileUrl}");

                        Console.WriteLine($"Скачивание изображения...");
                        var fileResponse = await client.GetAsync(fileUrl);

                        if (fileResponse.IsSuccessStatusCode)
                        {
                            byte[] imageData = await fileResponse.Content.ReadAsByteArrayAsync();
                            Console.WriteLine($"✓ Изображение скачано: {imageData.Length} байт");

                            string fileName = $"generated_{DateTime.Now:yyyyMMddHHmmss}.jpg";
                            string filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

                            File.WriteAllBytes(filePath, imageData);
                            Console.WriteLine($"✓ Изображение сохранено: {filePath}");

                            return filePath;
                        }
                        else
                        {
                            string error = await fileResponse.Content.ReadAsStringAsync();
                            Console.WriteLine($"✗ Ошибка скачивания изображения: {fileResponse.StatusCode}");
                            Console.WriteLine($"Детали: {error}");
                            return null;
                        }
                    }
                    else
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"✗ Ошибка API: {response.StatusCode}");
                        Console.WriteLine($"Детали: {error}");
                        return null;
                    }
                }
            }
        }
        public class WallpaperSetter
        {
            private const int SPI_SETDESKWALLPAPER = 0x0014;
            private const int SPIF_UPDATEINIFILE = 0x01;
            private const int SPIF_SENDWININICHANGE = 0x02;

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            private static extern int SystemParametersInfo(
                int uAction,
                int uParam,
                string lpvParam,
                int fuWinIni
            );

            public static void SetWallpaper(string imagePath)
            {
                try
                {
                    SystemParametersInfo(
                        SPI_SETDESKWALLPAPER,
                        0,
                        imagePath,
                        SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE
                    );
                    Console.WriteLine($"Обои установлены: {imagePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка: {ex.Message}");
                }
            }
        }
    }
}
