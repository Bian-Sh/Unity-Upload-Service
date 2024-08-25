using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace zframework.net
{
    public static class UploadHelper
    {
        public static string GetLocalIpAddress()
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                               nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211));

            foreach (var networkInterface in networkInterfaces)
            {
                var ipProperties = networkInterface.GetIPProperties();
                var ipAddressInfo = ipProperties.UnicastAddresses
                    .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork);

                if (ipAddressInfo != null)
                {
                    return ipAddressInfo.Address.ToString();
                }
            }

            return "127.0.0.1"; // 如果没有找到合适的 IP 地址，返回回环地址
        }

        internal static object GetAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public static string GenerateUniqueToken(string input)
        {
            // 使用 SHA256 生成哈希
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                // 将哈希结果转换为 Base64 字符串
                var base64 = Convert.ToBase64String(hashBytes);
                var token = base64.Replace("/", "").Replace("+", "").Replace("=", "");
                return $"Bearer {token}";
            }
        }
    }
}