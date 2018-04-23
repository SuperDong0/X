﻿using System;
using System.Collections.Generic;
using System.Linq;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using NewLife.Messaging;
using NewLife.Reflection;

namespace NewLife.Remoting
{
    /// <summary>Api主机</summary>
    public abstract class ApiHost : DisposeBase, IApiHost
    {
        #region 属性
        /// <summary>名称</summary>
        public String Name { get; set; }

        /// <summary>编码器</summary>
        public IEncoder Encoder { get; set; }

        /// <summary>处理器</summary>
        public IApiHandler Handler { get; set; }

        /// <summary>用户会话数据</summary>
        public IDictionary<String, Object> Items { get; set; } = new NullableDictionary<String, Object>();

        /// <summary>获取/设置 用户会话数据</summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual Object this[String key] { get { return Items[key]; } set { Items[key] = value; } }
        #endregion

        #region 控制器管理
        /// <summary>接口动作管理器</summary>
        public IApiManager Manager { get; } = new ApiManager();

        /// <summary>注册服务提供类。该类的所有公开方法将直接暴露</summary>
        /// <typeparam name="TService"></typeparam>
        public void Register<TService>() where TService : class, new() => Manager.Register<TService>();

        /// <summary>注册服务</summary>
        /// <param name="controller">控制器对象</param>
        /// <param name="method">动作名称。为空时遍历控制器所有公有成员方法</param>
        public void Register(Object controller, String method) => Manager.Register(controller, method);

        /// <summary>注册服务</summary>
        /// <param name="type">控制器类型</param>
        /// <param name="method">动作名称。为空时遍历控制器所有公有成员方法</param>
        public void Register(Type type, String method) => Manager.Register(type, method);

        /// <summary>显示可用服务</summary>
        protected void ShowService()
        {
            var ms = Manager.Services;
            if (ms.Count > 0)
            {
                Log.Info("可用服务{0}个：", ms.Count);
                var max = ms.Max(e => e.Key.Length);
                foreach (var item in ms)
                {
                    Log.Info("\t{0,-" + (max + 1) + "}{1}\t{2}", item.Key, item.Value, item.Value.Type.FullName);
                }
            }
        }
        #endregion

        #region 请求处理
        /// <summary>处理消息</summary>
        /// <param name="session"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        IMessage IApiHost.Process(IApiSession session, IMessage msg)
        {
            if (msg.Reply) return null;

            var pk = msg.Payload;
            // 如果外部事件未处理，再交给处理器
            pk = ProcessHandler(session, pk);

            // 封装响应消息
            var rs = msg.CreateReply();
            rs.Payload = pk;

            return rs;
        }

        private Packet ProcessHandler(IApiSession session, Packet pk)
        {
            var enc = Encoder;

            var action = "";
            Object result = null;
            var code = 0;
            //var seq = -1;
            try
            {
                // 这里会导致二次解码，因为解码以后才知道是不是请求
                var dic = enc.Decode(pk);

                // 请求响应，由code决定
                if (dic.ContainsKey("code")) return null;

                //if (!enc.TryGet(dic, out action, out args)) return null;
                if (!dic.TryGetValue("action", out var obj)) return null;

                // 参数可能不存在
                dic.TryGetValue("args", out var args);

                action = obj + "";

                //// 针对Http前端Json，可能带有序列号
                //if (dic.TryGetValue("seq", out obj)) seq = obj.ToInt();

                result = Handler.Execute(session, action, args as IDictionary<String, Object>);
            }
            catch (Exception ex)
            {
                ex = ex.GetTrue();

                // 支持自定义错误
                if (ex is ApiException aex)
                {
                    code = aex.Code;
                    if (ex.Data != null && ex.Data.Count > 0)
                        result = ex.Data.ToDictionary();
                    else
                        result = ex?.Message;
                }
                else
                {
                    code = 500;
                    result = ex?.Message;
                }
            }

            // 编码响应数据包
            //return enc.Encode(code, result);

            //if (seq >= 0) return enc.Encode(new { action, code, result, seq });

            //return enc.Encode(new { action, code, result });
            return enc.Encode(action, code, result);
        }
        #endregion

        #region 日志
        /// <summary>日志</summary>
        public ILog Log { get; set; } = Logger.Null;

        /// <summary>编码器日志</summary>
        public ILog EncoderLog { get; set; } = Logger.Null;

        /// <summary>写日志</summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void WriteLog(String format, params Object[] args) => Log?.Info(Name + " " + format, args);

        /// <summary>已重载。返回具有本类特征的字符串</summary>
        /// <returns>String</returns>
        public override String ToString() => Name;
        #endregion
    }
}