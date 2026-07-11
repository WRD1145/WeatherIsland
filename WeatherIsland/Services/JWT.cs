using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using ScottBrady.IdentityModel.Crypto;
using ScottBrady.IdentityModel.Tokens;

namespace WeatherIsland.Services
{
    public static class JwtGenerator
    {
        public static string GenerateJwt(string privateKeyPem, string projectId, string keyId)
        {
            // 1. 从 PEM 提取 Base64 数据
            var base64 = privateKeyPem
                .Replace("-----BEGIN PRIVATE KEY-----", "")
                .Replace("-----END PRIVATE KEY-----", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace(" ", "")
                .Trim();

            // 2. 解码并提取最后 32 字节
            var fullBytes = Base64UrlEncoder.DecodeBytes(base64);
            if (fullBytes.Length < 32)
                throw new ArgumentException($"私钥数据不足 32 字节，当前为 {fullBytes.Length} 字节");

            var privateKey = new byte[32];
            Array.Copy(fullBytes, fullBytes.Length - 32, privateKey, 0, 32);

            // 3. 创建 EdDsa
            var dsa = EdDsa.Create(new EdDsaParameters(ExtendedSecurityAlgorithms.Curves.Ed25519)
            {
                D = privateKey
            });

            // 4. 构建 Header 和 Payload
            var now = DateTimeOffset.UtcNow;
            var iat = now.ToUnixTimeSeconds() - 30;
            var exp = iat + 7200;

            var header = new { alg = "EdDSA", kid = keyId };
            var payload = new { sub = projectId, iat, exp };

            string jsonheader = JsonSerializer.Serialize(header);
            string jsonpayload = JsonSerializer.Serialize(payload);

            // ✅ 关键修正：对字节数组进行 Base64URL 编码
            string urlheader = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(jsonheader));
            string urlpayload = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(jsonpayload));
            string signatureToSign = urlheader + "." + urlpayload;

            // 5. 签名
            byte[] dataBytes = Encoding.UTF8.GetBytes(signatureToSign);
            byte[] signature = dsa.Sign(dataBytes);
            string urlsignature = Base64UrlEncoder.Encode(signature);
            // 在生成后打印各部分
            Console.WriteLine($"Header JSON: {jsonheader}");
            Console.WriteLine($"Header Encoded: {urlheader}");
            Console.WriteLine($"Payload JSON: {jsonpayload}");
            Console.WriteLine($"Payload Encoded: {urlpayload}");
            string token = urlheader + "." + urlpayload + "." + urlsignature;
            Console.WriteLine($"Full Token: {token}");
            return token;
        }
        /// <summary>
        /// 校验私钥是否有效
        /// </summary>
        /// <param name="privateKeyPem">PEM 格式的私钥字符串</param>
        /// <returns>如果私钥有效返回 true，否则返回 false</returns>
        public static bool IsPrivateKeyValid(string privateKeyPem)
        {
            try
            {
                // 1. 检查是否为空
                if (string.IsNullOrWhiteSpace(privateKeyPem))
                    return false;

                // 2. 检查是否包含 PEM 标记
                if (!privateKeyPem.Contains("BEGIN PRIVATE KEY") || !privateKeyPem.Contains("END PRIVATE KEY"))
                    return false;

                // 3. 提取 Base64 数据
                var base64 = privateKeyPem
                    .Replace("-----BEGIN PRIVATE KEY-----", "")
                    .Replace("-----END PRIVATE KEY-----", "")
                    .Replace("\r", "")
                    .Replace("\n", "")
                    .Replace(" ", "")
                    .Trim();

                if (string.IsNullOrEmpty(base64))
                    return false;

                // 4. 解码并提取私钥
                var fullBytes = Base64UrlEncoder.DecodeBytes(base64);
                if (fullBytes.Length < 32)
                    return false;

                var privateKey = new byte[32];
                Array.Copy(fullBytes, fullBytes.Length - 32, privateKey, 0, 32);

                // 5. 创建 EdDsa 实例并测试签名
                var dsa = EdDsa.Create(new EdDsaParameters(ExtendedSecurityAlgorithms.Curves.Ed25519)
                {
                    D = privateKey
                });

                // 6. 执行签名和自验证
                string testData = "QWeatherPlugin_Key_Test";
                byte[] dataBytes = Encoding.UTF8.GetBytes(testData);
                byte[] signature = dsa.Sign(dataBytes);

                return dsa.Verify(dataBytes, signature);
            }
            catch
            {
                return false;
            }
        }
    }
}