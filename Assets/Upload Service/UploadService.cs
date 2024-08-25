using HttpMultipartParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using static zframework.net.UploadHelper;

namespace zframework.net
{
    /// <summary>
    ///  资源类型，类型不同决定了资源的存储路径
    ///  Scene: Custom Assets/StandaloneWindows64/{具体场景名}
    ///  Video: Custom Assets/Videos/{具体视频名}
    /// </summary>
    public enum ResourceType
    {
        None,
        Scene,
        Video
    }

    /// <summary>
    /// 文件上传微服务，仅接受一次 Post ，5 秒仍未授权通过，自动关闭服务
    /// IP 为本机局域网 IP，端口号为动态获取的端口号
    ///  主要为局域网上传 Addressable 场景和视频资源设计
    /// </summary>
    public class UploadService
    {
        /// <summary>
        /// 服务的URL
        /// </summary>
        public string url;
        /// <summary>
        /// 文件存储的根路径
        /// </summary>
        public string root;
        /// <summary>
        /// 用于校验是否有权限上传文件的令牌
        /// </summary>
        public string token;
        private HttpListener listener;
        private string target;
        private ResourceType type;
        readonly static Dictionary<string, UploadService> services = new();

        /// <summary>
        /// 文件上传服务的构造函数
        /// </summary>
        /// <param name="target">上传目标，文件夹或者视频</param>
        /// <param name="type">资源类型</param>
        public UploadService(string target, ResourceType type)
        {

            // 检查文件OR文件夹是否存在，存在则 throw exception
            if (type == ResourceType.Scene)
            {
                var path = Path.Combine(Application.streamingAssetsPath, "Custom Assets", "StandaloneWindows64", target);
                if (Directory.Exists(path))
                {
                    throw new System.Exception("AA 场景已经存在");
                }
            }
            else if (type == ResourceType.Video)
            {
                var path = Path.Combine(Application.streamingAssetsPath, "Custom Assets", "Videos", target);
                if (File.Exists(path))
                {
                    throw new System.Exception("视频已经存在");
                }
            }

            this.target = target;
            this.type = type;
            token = GenerateUniqueToken($"{type}{target}");
            if (services.ContainsKey(token))
            {
                throw new System.Exception("服务已经存在");
            }

            var ip = GetLocalIpAddress();
            var port = GetAvailablePort();
            url = $"http://{ip}:{port}/upload/";
            root = Path.Combine(Application.streamingAssetsPath, "Custom Assets");
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            services.Add(token, this);
        }

        /// <summary>
        /// 启动文件上传服务
        /// </summary>
        public async Task Start()
        {
            try
            {
                listener.Start();
                Debug.Log($"[Server] Upload Service started at {url}");
                HttpListenerContext context;
                HttpListenerRequest request;
                // 加入超时自动关闭服务的逻辑，如果超过 5 秒没有收到请求，关闭服务
                var cts = new System.Threading.CancellationTokenSource();
                cts.CancelAfter(60000);
                cts.Token.Register(() =>
                {
                    listener.Stop();
                    services.Remove(this.token);
                    Debug.Log($"[Server] Upload Service stopped due to timeout");
                });
                var token = string.Empty;
                do
                {
                    context = await listener.GetContextAsync();
                    request = context.Request;
                    if (request.HttpMethod == "POST")
                    {
                        token = request.Headers["Authorization"];
                    }
                } while (token != this.token);
                cts.Dispose();
                //log
                Debug.Log($"[Server] 授权码验证通过，开始处理文件保存");

                // 授权码验证通过,处理文件保存
                var parser = await MultipartFormDataParser.ParseAsync(request.InputStream);

                // log
                Debug.Log($"[Server] 解析文件成功，文件个数：{parser.Files.Count}");
                if (parser.Files.Count >= 1)
                {
                    var subfolder = type switch
                    {
                        ResourceType.Scene => Path.Combine("StandaloneWindows64", target),
                        ResourceType.Video => "Videos",
                        _ => throw new System.Exception("资源类型错误")
                    };
                    // 创建保存文件的任务列表
                    var saveTasks = new List<Task>();
                    foreach (var file in parser.Files)
                    {
                        saveTasks.Add(SaveFileAsync(file, subfolder));
                    }
                    // 等待所有文件保存任务完成
                    await Task.WhenAll(saveTasks);
                    // log
                    Debug.Log($"[Server] 文件保存成功");
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
            finally
            {
                // PostProcess : 关闭服务，回收资源
                listener.Stop();
                services.Remove(this.token);
            }
        }

        /// <summary>
        /// 异步保存文件
        /// </summary>
        /// <param name="file">要保存的文件</param>
        /// <param name="subfolder">子文件夹</param>
        /// <returns></returns>
        async Task SaveFileAsync(FilePart file, string subfolder)
        {
            var filename = Uri.UnescapeDataString(file.FileName);
            var path = Path.Combine(root, subfolder, filename);
            var folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            using var fileStream = new FileStream(path, FileMode.Create);
            await file.Data.CopyToAsync(fileStream);
        }
    }
}