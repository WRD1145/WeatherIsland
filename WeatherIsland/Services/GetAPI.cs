using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WeatherIsland.Services
{
    public class WeatherIslandGet
    {
        
        private readonly string _apiHost;

        public WeatherIslandGet(string apiHost)
        {
            _apiHost = apiHost ?? throw new ArgumentNullException(nameof(apiHost));
        }

        /// <summary>
        /// 通用请求方法
        /// </summary>
        public async Task<string> RequestAsync(
            string path,
            object queryParams = null,
            string jwtToken = null,
            string apiKey = null)
        {
            if (string.IsNullOrEmpty(jwtToken) && string.IsNullOrEmpty(apiKey))
                throw new ArgumentException("必须提供 jwtToken 或 apiKey 之一");

            // 构建查询字符串
            string queryString = BuildQueryString(queryParams);
            string url = $"https://{_apiHost}{path}{queryString}";

            // 生成缓存文件名（基于 URL 的哈希，确保唯一且安全）
            string cacheFileName = GetCacheFileName(url);
            string cacheFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cacheFileName);

            // 尝试从缓存读取（有效期为5分钟）
            if (File.Exists(cacheFilePath))
            {
                try
                {
                    string cacheJson = File.ReadAllText(cacheFilePath);
                    var cacheObj = JObject.Parse(cacheJson);
                    var expire = cacheObj["expire"]?.ToObject<DateTime>();
                    if (expire.HasValue && expire.Value > DateTime.UtcNow)
                    {
                        return cacheObj["data"]?.ToString();
                    }
                }
                catch
                {
                    // 缓存损坏则忽略，重新请求
                }
            }

            // 发起 HTTP 请求（使用 using 确保 HttpClient 释放，仿示例风格）
            using (var httpClient = new HttpClient())
            {
                // 设置认证头
                if (!string.IsNullOrEmpty(jwtToken))
                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
                else if (!string.IsNullOrEmpty(apiKey))
                    httpClient.DefaultRequestHeaders.Add("X-QW-Api-Key", apiKey);

                // 支持压缩
                httpClient.DefaultRequestHeaders.AcceptEncoding.Add(
                    new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
                httpClient.DefaultRequestHeaders.AcceptEncoding.Add(
                    new System.Net.Http.Headers.StringWithQualityHeaderValue("deflate"));

                Console.WriteLine($"[WeatherIslandGet] 请求 URL: {url}");
                var response = await httpClient.GetAsync(url);

                // 检查状态码（仿示例使用 EnsureSuccessStatusCode）
                response.EnsureSuccessStatusCode();

                // 解压响应（与示例完全一致）
                string responseBody;
                using (var responseStream = await response.Content.ReadAsStreamAsync())
                using (var decompressStream = new GZipStream(responseStream, CompressionMode.Decompress))
                using (var streamReader = new StreamReader(decompressStream))
                {
                    responseBody = await streamReader.ReadToEndAsync();
                }

                // 检查是否为有效 JSON（仿示例）
                if (!IsValidJson(responseBody))
                {
                    throw new Exception("API 返回的数据不是有效的 JSON");
                }

                // 保存缓存（有效期5分钟）
                var cacheData = new JObject
                {
                    ["expire"] = DateTime.UtcNow.AddMinutes(5),
                    ["data"] = responseBody
                };
                File.WriteAllText(cacheFilePath, cacheData.ToString());

                return responseBody;
            }
        }

        /// <summary>
        /// 获取当前天气（封装方法）
        /// </summary>
        public async Task<string> GetWeatherNowAsync(string location, string jwtToken = null, string apiKey = null)
        {
            return await RequestAsync(
                path: "/v7/weather/now",
                queryParams: new { location },
                jwtToken: jwtToken,
                apiKey: apiKey
            );
        }

        /// <summary>
        /// 构建查询字符串（与示例中不同的是，示例直接拼接，此处用反射）
        /// </summary>
        private string BuildQueryString(object queryParams)
        {
            if (queryParams == null) return "";

            var properties = queryParams.GetType().GetProperties();
            var pairs = new System.Collections.Generic.List<string>();
            foreach (var prop in properties)
            {
                var value = prop.GetValue(queryParams)?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    pairs.Add($"{Uri.EscapeDataString(prop.Name)}={Uri.EscapeDataString(value)}");
                }
            }
            pairs.Sort();
            return "?" + string.Join("&", pairs);
        }

        /// <summary>
        /// 根据 URL 生成安全的缓存文件名
        /// </summary>
        private string GetCacheFileName(string url)
        {
            // 使用 URL 的 SHA256 哈希作为文件名，避免特殊字符和长度问题
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(url));
                string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                return $"WeatherCache_{hash}.json";
            }
        }

        /// <summary>
        /// 验证 JSON 格式（与示例完全一致）
        /// </summary>
        private bool IsValidJson(string json)
        {
            try
            {
                JToken.Parse(json);
                return true;
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                return false;
            }
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearCache()
        {
            var files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "WeatherCache_*.json");
            foreach (var file in files)
            {
                try { File.Delete(file); } catch { }
            }
        }
    }
}