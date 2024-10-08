﻿/*
*代码由 TinyRPC 自动生成，请勿修改
*don't modify manually as it generated by TinyRPC
*/
using System;
using zFramework.TinyRPC.Messages;
namespace zFramework.TinyRPC.Generated
{
    /// <summary>
    ///  向服务器请求上传文件
    /// </summary>
    [Serializable]
    [ResponseType(typeof(FileUploadResponse))]
    public partial class FileUploadRequest : Request
    {
        /// <summary>
        ///  文件或者文件夹名
        /// </summary>
        public string name;
        /// <summary>
        ///  资产类型
        /// </summary>
        public int type;
        public override void OnRecycle()
        {
            base.OnRecycle();
            name = "";
            type = 0;
        }
    }
}
