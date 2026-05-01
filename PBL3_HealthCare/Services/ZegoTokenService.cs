using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Generic; // Thêm thư viện này để dùng Dictionary
using Microsoft.Extensions.Configuration;

namespace PBL3_HealthCare.Services
{
    public class ZegoTokenService
    {
        private readonly IConfiguration _configuration;

        public ZegoTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(string userId, string roomId)
        {
            // 1. Đọc AppId và ServerSecret từ file appsettings.json
            uint appId = _configuration.GetValue<uint>("ZegoCloud:AppId");
            string serverSecret = _configuration.GetValue<string>("ZegoCloud:ServerSecret");

            if (string.IsNullOrEmpty(serverSecret) || serverSecret.Length != 32)
            {
                throw new Exception("ServerSecret của ZegoCloud bắt buộc phải là 32 ký tự. Hãy check lại appsettings.json!");
            }

            int effectiveTimeInSeconds = 3600; // Token sống 1 tiếng
            long createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long expireTime = createTime + effectiveTimeInSeconds;
            long nonce = new Random().Next();

            // 2. TẠO PAYLOAD CẤP QUYỀN VÀO ĐÚNG PHÒNG (ROOM PRIVILEGE)
            var privilegeDict = new Dictionary<string, int>
            {
                { "1", 1 }, // Quyền số 1: Cho phép đăng nhập vào phòng (1 là Cho phép, 0 là Cấm)
                { "2", 1 }  // Quyền số 2: Cho phép phát luồng âm thanh/hình ảnh
            };

            var payloadData = new
            {
                room_id = roomId, // Ép dính Token này với đúng roomId được truyền vào
                privilege = privilegeDict,
                stream_id_list = Array.Empty<string>() // Rỗng = cho phép mọi stream
            };

            // Ép cục payload này thành chuỗi JSON
            string payloadJsonStr = JsonSerializer.Serialize(payloadData);

            // 3. Tạo gói dữ liệu JSON tổng theo chuẩn ZegoCloud
            var tokenInfo = new
            {
                app_id = appId,
                user_id = userId,
                nonce = nonce,
                ctime = createTime,
                expire = expireTime,
                payload = payloadJsonStr // Gắn chuỗi quyền vào đây!
            };

            string jsonBody = JsonSerializer.Serialize(tokenInfo);
            byte[] plaintext = Encoding.UTF8.GetBytes(jsonBody);

            // 4. Tạo 16 bytes ngẫu nhiên làm Vector Khởi tạo (IV)
            string ivStr = Guid.NewGuid().ToString("N").Substring(0, 16);
            byte[] iv = Encoding.UTF8.GetBytes(ivStr);
            byte[] key = Encoding.UTF8.GetBytes(serverSecret);
            byte[] ciphertext;

            // 5. Mã hóa AES-256-CBC
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(plaintext, 0, plaintext.Length);
                        cs.FlushFinalBlock();
                        ciphertext = ms.ToArray();
                    }
                }
            }

            // 6. Đóng gói dữ liệu thành Byte Array theo chuẩn Big-Endian
            using (var ms = new MemoryStream())
            {
                byte[] expireBytes = BitConverter.GetBytes(expireTime);
                if (BitConverter.IsLittleEndian) Array.Reverse(expireBytes);
                ms.Write(expireBytes, 0, expireBytes.Length);

                byte[] ivLenBytes = BitConverter.GetBytes((ushort)iv.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(ivLenBytes);
                ms.Write(ivLenBytes, 0, ivLenBytes.Length);

                ms.Write(iv, 0, iv.Length);

                byte[] cipherLenBytes = BitConverter.GetBytes((ushort)ciphertext.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(cipherLenBytes);
                ms.Write(cipherLenBytes, 0, cipherLenBytes.Length);

                ms.Write(ciphertext, 0, ciphertext.Length);

                byte[] assembledBytes = ms.ToArray();

                // 7. Trả về Token hoàn chỉnh
                return "04" + Convert.ToBase64String(assembledBytes);
            }
        }
    }
}