using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using zFramework.TinyRPC;
using zFramework.TinyRPC.Generated;
using Debug = UnityEngine.Debug;

namespace zframework.net
{
    [MessageHandlerProvider]
    public class Foo : MonoBehaviour
    {
        TinyClient client;
        TinyServer server;
        public string ip = "127.0.0.1";
        public int port = 2333;
        public string target;
        public bool deleteBeforeUpload = true;
        private float uploadProgress = 0;


        private async void Start()
        {
            client = new TinyClient(ip, port);
            server = new TinyServer(port);
            server.Start();
            await client.ConnectAsync();
        }

        private void OnDestroy()
        {
            client?.Stop();
            server?.Stop();
        }

        private void OnGUI()
        {
            var screenRect = new Rect(0, 0, Screen.width, Screen.height);
            using (new GUILayout.AreaScope(screenRect, "项目说明", GUI.skin.box))
            {
                GUILayout.Space(24);
                GUILayout.Label("1. 本项目演示了一个简单的文件上传微服务，包含客户端和服务器两部分");
                GUILayout.Label("2. 服务器使用 HttpListener 监听客户端请求，客户端使用 UnityWebRequest 上传文件");
                GUILayout.Label("3. 客户端通过 TinyRPC 告知服务器开启上传服务并获得上传连接及令牌");
                GUILayout.Label("4. 上传服务器只接受一次正确的 Post 请求，超 60 秒未请求自动关闭上传服务");
                GUILayout.Label("5. 选择的文件类型为 .json 或 .mp4");
                GUILayout.Label("6. 选择 json 文件会将 json 文件所在的整个目录上传，比如上传 Addressable 构建的文件夹");
                GUILayout.Label("7. 选择 .mp4 文件，如果有提供跟视频同名的 .png 封面，也会一并上传");
                GUILayout.Label("8. 上传成功后，Unity 编辑器会自动打开文件 Or 文件夹");
                GUILayout.Space(24);
                GUILayout.Label($"IP: {ip}");
                GUILayout.Label($"Port: {port}");
                target = GUILayout.TextField(target);
                //Button
                if (GUILayout.Button("Upload"))
                {
                    Upload();
                }
                GUILayout.Label($"Progress: {uploadProgress:P}");
            }
        }

        // 客户端
        public async void Upload() // scene 上传目标，可以是文件夹或者视频
        {
            //使用编辑器文件选择器选择 Json 和 .mp4 文件
            var type = target switch
            {
                string t when t.EndsWith(".json") => ResourceType.Scene,
                string t when t.EndsWith(".mp4") => ResourceType.Video,
                _ => ResourceType.None
            };
            if (type == ResourceType.None)
            {
                Debug.LogError("请选择正确的文件");
                return;
            }

            var name = target switch
            {
                string t when t.EndsWith(".json") => Path.GetFileName(Path.GetDirectoryName(t)),
                string t when t.EndsWith(".mp4") => Path.GetFileName(t),
                _ => ""
            };

            // 是否删除 StreamingAssets 下同名文件 Or 文件夹
            if (deleteBeforeUpload)
            {
                var root = Path.Combine(Application.streamingAssetsPath, "Custom Assets");
                var path = type switch
                {
                    ResourceType.Scene => Path.Combine(root, "StandaloneWindows64", name),
                    ResourceType.Video => Path.Combine(root, "Videos", name),
                    _ => throw new System.Exception("资源类型错误")
                };
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    // META 文件
                    var meta = path + ".meta";
                    if (File.Exists(meta))
                    {
                        File.Delete(meta);
                    }
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                    // 删除视频封面
                    if (type == ResourceType.Video)
                    {
                        var thumbnail = path.Replace(".mp4", ".png");
                        if (File.Exists(thumbnail))
                        {
                            File.Delete(thumbnail);
                        }
                    }
                }
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
            }


            var request_prepare = new FileUploadRequest
            {
                name = name,
                type = (int)type
            };

            // log target and name
            Debug.Log($"[Client] 上传目标：{request_prepare.name} 类型：{type},路径：{target}");

            var response_prepare = await client.Call<FileUploadResponse>(request_prepare);
            if (string.IsNullOrEmpty(response_prepare.message))
            {
                Debug.Log($"[Client] 获取上传服务器及令牌成功\n服务地址：{response_prepare.url} \n服务授权码：{response_prepare.token}");

                // 获取文件列表
                Dictionary<string, string> files = new(); // key: 相对路径，value: 绝对文件路径
                if (type == ResourceType.Scene)
                {
                    var parent = Path.GetDirectoryName(target);
                    var contents = Directory.GetFiles(parent, "*.*", SearchOption.AllDirectories);
                    foreach (var file in contents)
                    {
                        var relativePath = file.Replace(parent, "").TrimStart('\\');
                        files.Add(relativePath, file);
                    }
                }
                else if (type == ResourceType.Video)
                {
                    // 视频文件只有一个
                    files.Add(Path.GetFileName(target), target);
                    // 获取视频文件的缩略图，保存为 视频文件名 + ".png"
                    var thumbnail = target.Replace(".mp4", ".png");
                    if (File.Exists(thumbnail))
                    {
                        files.Add(Path.GetFileName(thumbnail), thumbnail);
                    }
                }

                // 使用 UnityWebRequest 上传文件
                List<IMultipartFormSection> formData = new();
                foreach (var pair in files)
                {
                    var fileFullPath = pair.Value;
                    byte[] fileData = File.ReadAllBytes(fileFullPath);
                    var fileName = Uri.EscapeDataString(pair.Key); // 解决中文乱码问题
                    var fileContent = new MultipartFormFileSection("files[]", fileData, fileName, "application/octet-stream");
                    formData.Add(fileContent);
                }

                using (UnityWebRequest request = UnityWebRequest.Post(response_prepare.url, formData))
                {
                    request.SetRequestHeader("Authorization", response_prepare.token);
                    request.SendWebRequest();

                    while (!request.isDone)
                    {
                        uploadProgress = request.uploadProgress;
                        await Task.Delay(100);
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log("[Client] Upload complete!");
#if UNITY_EDITOR
                        UnityEditor.AssetDatabase.Refresh();
#endif

                        // 上传成功后，打开文件夹
                        var root = Path.Combine(Application.streamingAssetsPath, "Custom Assets");
                        var path = type switch
                        {
                            ResourceType.Scene => Path.Combine(root, "StandaloneWindows64", name),
                            ResourceType.Video => Path.Combine(root, "Videos", name),
                            _ => throw new System.Exception("资源类型错误")
                        };
                        // 选中文件 Or 文件夹
                        path = path.Replace(@"/", @"\");
                        System.Diagnostics.Process.Start("explorer.exe", "/select," + path);
                    }
                    else
                    {
                        Debug.Log($"[Client] Upload failed with error: {request.error}");
                    }
                }
            }
            else
            {
                Debug.LogError(response_prepare.message);
            }
        }

        // 服务器
        [MessageHandler(zFramework.TinyRPC.MessageType.RPC)]
        static async Task OnClientFileUploadRequired(Session session, FileUploadRequest request, FileUploadResponse response)
        {
            try
            {
                // 启动上传服务
                var service = new UploadService(request.name, (ResourceType)request.type);
                _ = Task.Run(service.Start);
                response.url = service.url;
                response.token = service.token;
                // log
                Debug.Log($"[Server] 客户端请求上传文件：{request.name} 类型：{(ResourceType)request.type}, 上传服务启动！");
            }
            catch (System.Exception e)
            {
                Debug.LogError(e.Message);
                response.message = e.Message;
            }
            await Task.CompletedTask;
        }
    }
}