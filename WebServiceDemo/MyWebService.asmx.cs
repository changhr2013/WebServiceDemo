using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Web.Services;
using System.Xml;

namespace WebServiceDemo
{
    /// <summary>
    /// MyWebService 的摘要说明
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // 若要允许使用 ASP.NET AJAX 从脚本中调用此 Web 服务，请取消注释以下行。 
    [System.Web.Script.Services.ScriptService]
    public class MyWebService : System.Web.Services.WebService
    {
        //用来存储JFmpeg的视频流参数对象
        private static List<JFmpeg> JFmpegList = new List<JFmpeg>();

        private static Dictionary<string,JFmpeg> jfmpegMap=new Dictionary<string,JFmpeg>();  
        //本地jsmpeg的根目录
        private const string MENU = @"cd C:\Users\Tony\Desktop\视频转换测试报告\jsmpeg&&";
        //只是进程pid的默认值
        private int ffmpegpid = 0, jsmpegpid = 0;
        //缓存JFmpeg测试流的参数集合
        List<JFmpeg> cacheList = new List<JFmpeg>();

        /// <summary>
        /// 在服务端执行命令行
        /// </summary>
        /// <param name="command">cmd指令内容</param>
        /// <param name="seconds">设置等待指令结束的时间</param>
        /// <returns>cmd程序的pid</returns>
        [WebMethod]
        public int RunExecute(string command, int seconds)
        {
            return Tools.Execute(command, seconds);
        }

        /// <summary>
        /// 请求服务器的ip地址
        /// </summary>
        /// <returns>服务器的ip地址</returns>
        [WebMethod]
        public string GetServerIpAddress()
        {
            return Tools.GetLocalIP();
        }

        /// <summary>
        /// 开启所有配置文件中配置的JFmpeg视频流
        /// </summary>
        /// <returns>读取配置完成的提示信息</returns>
        [WebMethod]
        public List<JFmpeg> RunAllConfigJFmpeg()
        {
            OpenConfStream();
            return JFmpegList;
        }

        /// <summary>
        /// 获取最新的JFmpegList列表
        /// </summary>
        /// <returns>JFmpegList 列表</returns>
        [WebMethod]
        public List<JFmpeg> GetCurrentJFmpegList()
        {
            RefreshJfmpegList();
            return JFmpegList;
        }

        /// <summary>
        /// 通过参数启动一个JFmpeg视频流
        /// </summary>
        /// <param name="password">jsmpeg密码</param>
        /// <param name="rtspStreamUrl">rtsp流地址</param>
        /// <param name="rtspUsername">rtsp用户名</param>
        /// <param name="rtspPsd">rtsp密码</param>
        /// <returns>JFmpegList 列表</returns>
        [WebMethod]
        public List<JFmpeg> OpenSingleJFmpeg(string password, string rtspStreamUrl, string rtspUsername, string rtspPsd)
        {
            OpenSelectedJFmpeg(password, rtspStreamUrl, rtspUsername, rtspPsd);
            //RefreshJFmpegList();
            return JFmpegList;
        }

        /// <summary>
        /// 通过视频出入端口号关闭一个JFmpeg视频流
        /// </summary>
        /// <param name="rtspStreamUrl">rtsp流地址</param>
        /// <returns>JFmpegList 列表</returns>
        [WebMethod]
        public List<JFmpeg> CloseSingleJFmpeg(string rtspStreamUrl)
        {
            CloseSelectedJFmpeg(rtspStreamUrl);
            RefreshJfmpegList();
            return JFmpegList;
        }

        /// <summary>
        /// 清除当前运行的所有流
        /// </summary>
        [WebMethod]
        public string KillAllJFmpegStream()
        {
            foreach (KeyValuePair<string, JFmpeg> jfmpegPair in jfmpegMap)
            {
                Tools.KillProcessAndChildren(jfmpegPair.Value.Ffmpegpid);
                jfmpegPair.Value.Ffmpegpid = 0;
                Tools.KillProcessAndChildren(jfmpegPair.Value.Jsmpegpid);
                jfmpegPair.Value.Jsmpegpid = 0;
            }
            RefreshJfmpegList();
            return "The Config JFmpeg Stream Already Exited.";
        }

        /// <summary>
        /// 清除所有相关进程，重置服务器流转换环境
        /// </summary>
        [WebMethod]
        public string ResetJFmpeg()
        {
            foreach (string killcmd in GetKillCmdArr())
            {
                Tools.Execute(killcmd, 3);
            }

            foreach (KeyValuePair<string, JFmpeg> jfmpegPair in jfmpegMap)
            {
                Tools.KillProcessAndChildren(jfmpegPair.Value.Ffmpegpid);
                jfmpegPair.Value.Ffmpegpid = 0;
                Tools.KillProcessAndChildren(jfmpegPair.Value.Jsmpegpid);
                jfmpegPair.Value.Jsmpegpid = 0;
            }
            RefreshJfmpegList();
            return "JFmpeg's Environment has been Reset.";
        }

        /// <summary>
        /// 通过比对正在使用的端口列表来随机获取两个未使用的端口号
        /// </summary>
        /// <returns>unUsedInOutPortList</returns>
        [WebMethod]
        public List<int> GetUnusedInOutPort()
        {
            List<int> unUsedInOutPortList=new List<int>();
            //获取正在使用的端口列表
            IList isUsedPorts=Tools.PortIsUsed();

            Random rd = new Random();

            //获取两个随机未使用的端口
            int randomUnusedInPort=rd.Next(1024, 65535);
            int randomUnusedOutPort = rd.Next(1024, 65535);

            while (isUsedPorts.Contains(randomUnusedInPort)&&isUsedPorts.Contains(randomUnusedOutPort))
            {
                randomUnusedInPort = rd.Next(1024, 65535);
                randomUnusedOutPort = rd.Next(1024, 65535);
            }

            unUsedInOutPortList.Add(randomUnusedInPort);
            unUsedInOutPortList.Add(randomUnusedOutPort);

            return unUsedInOutPortList;
        }



        #region 私有方法 GetKillCmdArr | OpenFFmpeg | OpenJsmpeg | OpenConfStream | OpenSelectedJFmpeg |CloseSelectedJFmpeg |
        /// <summary>
        /// 初始化重置服务器流相关的命令数组
        /// </summary>
        /// <returns>ArrayList killcmdArr</returns>
        private static ArrayList GetKillCmdArr()
        {
            ArrayList killcmdArr = new ArrayList();
            killcmdArr.Add(@"taskkill /f /im ffmpeg.exe");
            killcmdArr.Add(@"taskkill /f /im node.exe");
            killcmdArr.Add(@"taskkill /f /im conhost.exe");
            killcmdArr.Add(@"taskkill /f /im cmd.exe");
            return killcmdArr;
        }

        /// <summary>
        /// 开启ffmpeg流转换的方法
        /// </summary>
        /// <param name="rtspStreamUrl">rtsp流地址</param>
        /// <param name="rtspUsername">rtsp用户名</param>
        /// <param name="rtspPsd">rtsp密码</param>
        /// <param name="inPortNum">jsmpeg入端口</param>
        /// <param name="password">jsmpeg密码</param>
        /// <param name="isTest">是否为测试流</param>
        private void OpenFFmpeg(string rtspStreamUrl, string rtspUsername, string rtspPsd, string inPortNum, string password, bool isTest = false)
        {
            //通过参数组装ffmpeg的命令行
            string ffmpegCmd = MENU + @"ffmpeg -r 30 -rtsp_transport tcp -i rtsp://" + rtspStreamUrl +
                   " -f mpegts -codec:v mpeg1video -codec:a mp2 -b:v 3500k http://127.0.0.1:" + inPortNum + "/" + password;

            if (!String.IsNullOrEmpty(rtspUsername))
            {
                ffmpegCmd = MENU + @"ffmpeg -r 30 -rtsp_transport tcp -i rtsp://" + rtspUsername + ":" + rtspPsd + "@" + rtspStreamUrl +
                   " -f mpegts -codec:v mpeg1video -codec:a mp2 -b:v 3500k http://127.0.0.1:" + inPortNum + "/" + password;
            }

            //通过cmd执行ffmpeg的流转换进程
            ffmpegpid = Tools.Execute(ffmpegCmd, 5);
            //dic.Add(inPortNum, ffmpegpid);

            jfmpegMap[rtspStreamUrl].Ffmpegpid = ffmpegpid;

            //如果为测试流，缓存测试流的pid
            if (isTest)
            {
                cacheList[0].Ffmpegpid = ffmpegpid;
            }
        }

        /// <summary>
        /// 开启jsmpeg通道
        /// </summary>
        /// <param name="rtspStreamUrl">rtsp流地址</param>
        /// <param name="inPortNum">入端口</param>
        /// <param name="outPortNum">出端口</param>
        /// <param name="password">jsmpeg密码</param>
        /// <param name="isTest">是否为测试流</param>
        private void OpenJsmpeg(string rtspStreamUrl,string inPortNum, string outPortNum, string password, bool isTest = false)
        {
            //通过参数组装jsmpeg的node命令行
            string nodeCmd = MENU + @"node websocket-relay.js " + password + " " + inPortNum + " " + outPortNum;

            //执行node命令
            jsmpegpid = Tools.Execute(nodeCmd, 5);
            //dic.Add(outPortNum, jsmpegpid);

            jfmpegMap[rtspStreamUrl].Jsmpegpid = jsmpegpid;

            //如果为测试流，缓存测试流的pid
            if (isTest)
            {
                cacheList[0].Jsmpegpid = jsmpegpid;
            }
        }

        /// <summary>
        /// 通过读取配置文件打开配置的所有视频流
        /// </summary>
        private void OpenConfStream()
        {
            XmlDocument xmlDoc = new XmlDocument();

            xmlDoc.Load(AppDomain.CurrentDomain.BaseDirectory + "configlist.xml");

            XmlNodeList monitors = xmlDoc.SelectNodes("Monitors/monitor");

            if (monitors != null)
            {
                foreach (XmlElement monitor in monitors)
                {
                    List<int> inOutPort = GetUnusedInOutPort();

                    string inPortNum = inOutPort[0].ToString();
                    string outPortNum = inOutPort[1].ToString();
                    //string inPortNum = monitor.FirstChild.ChildNodes[0].InnerText;
                    //string outPortNum = monitor.FirstChild.ChildNodes[1].InnerText;
                    string password = monitor.FirstChild.ChildNodes[2].InnerText;

                    string rtspStreamUrl = monitor.LastChild.ChildNodes[0].InnerText;
                    string rtspUsername = monitor.LastChild.ChildNodes[1].InnerText;
                    string rtspPsd = monitor.LastChild.ChildNodes[2].InnerText;

                    if (jfmpegMap.ContainsKey(rtspStreamUrl))
                    {
                        if (jfmpegMap[rtspStreamUrl].Ffmpegpid != 0 || jfmpegMap[rtspStreamUrl].Jsmpegpid != 0)
                        {
                            return;
                        }
                        else
                        {
                            jfmpegMap[rtspStreamUrl].InPort = int.Parse(inPortNum);
                            jfmpegMap[rtspStreamUrl].OutPort = int.Parse(outPortNum);
                            jfmpegMap[rtspStreamUrl].Password = password;
                            jfmpegMap[rtspStreamUrl].RtspUsername = rtspUsername;
                            jfmpegMap[rtspStreamUrl].RtspPassword = rtspPsd;
                        }
                    }
                    else
                    {
                        JFmpeg jfmpegItem = new JFmpeg(int.Parse(inPortNum), int.Parse(outPortNum), rtspStreamUrl, password, rtspUsername, rtspPsd);
                        jfmpegMap.Add(rtspStreamUrl, jfmpegItem);
                    }

                    //按配置项开启视频流
                    ManualResetEvent jsmpegManual = new ManualResetEvent(false);
                    ThreadPool.QueueUserWorkItem(o =>
                    {
                        OpenJsmpeg(rtspStreamUrl,inPortNum, outPortNum, password);
                        jsmpegManual.Set();
                    });
                    jsmpegManual.WaitOne();

                    ManualResetEvent ffmpegManual = new ManualResetEvent(false);
                    ThreadPool.QueueUserWorkItem(o =>
                    {
                        OpenFFmpeg(rtspStreamUrl, rtspUsername, rtspPsd, inPortNum, password);
                        ffmpegManual.Set();
                    });
                    ffmpegManual.WaitOne();

                }
            }
            //刷新视频流参数列表
            RefreshJfmpegList();

        }

        /// <summary>
        /// 通过参数打开一个JFmpeg视频流
        /// </summary>
        /// <param name="password">jsmpeg密码</param>
        /// <param name="rtspStreamUrl">rtsp流地址</param>
        /// <param name="rtspUsername">rtsp用户名</param>
        /// <param name="rtspPsd">rtsp密码</param>
        private void OpenSelectedJFmpeg(string password, string rtspStreamUrl, string rtspUsername, string rtspPsd)
        {
            List<int> inOutPort = GetUnusedInOutPort();

            string inPortNum = inOutPort[0].ToString();
            string outPortNum = inOutPort[1].ToString();

            if (jfmpegMap.ContainsKey(rtspStreamUrl))
            {
                if (jfmpegMap[rtspStreamUrl].Ffmpegpid!=0||jfmpegMap[rtspStreamUrl].Jsmpegpid!=0)
                {
                    return;
                }
                jfmpegMap[rtspStreamUrl].InPort = int.Parse(inPortNum);
                jfmpegMap[rtspStreamUrl].OutPort = int.Parse(outPortNum);
                jfmpegMap[rtspStreamUrl].Password = password;
                jfmpegMap[rtspStreamUrl].RtspUsername = rtspUsername;
                jfmpegMap[rtspStreamUrl].RtspPassword = rtspPsd;
            }
            else
            {
                JFmpeg jfmpegItem = new JFmpeg(int.Parse(inPortNum), int.Parse(outPortNum), rtspStreamUrl, password, rtspUsername, rtspPsd);
                jfmpegMap.Add(rtspStreamUrl, jfmpegItem);
            }

            ManualResetEvent jsmpegManual = new ManualResetEvent(false);
            ThreadPool.QueueUserWorkItem(o => { OpenJsmpeg(rtspStreamUrl,inPortNum, outPortNum, password);
                                                  jsmpegManual.Set();
            });
            jsmpegManual.WaitOne();

            ManualResetEvent ffmpegManual = new ManualResetEvent(false);
            ThreadPool.QueueUserWorkItem(o =>
            {
                OpenFFmpeg(rtspStreamUrl, rtspUsername, rtspPsd, inPortNum, password);
                ffmpegManual.Set();
            });
            ffmpegManual.WaitOne();

            //刷新视频流参数列表
            RefreshJfmpegList();
        }

        /// <summary>
        /// 通过rtsp流地址来关闭相应的视频流
        /// </summary>
        /// <param name="rtspStreamUrl"></param>
        private void CloseSelectedJFmpeg(string rtspStreamUrl)
        {

            int jsmpegPid = jfmpegMap[rtspStreamUrl].Jsmpegpid;
            int ffmpegPid = jfmpegMap[rtspStreamUrl].Ffmpegpid;

            Tools.KillProcessAndChildren(jsmpegPid);
            Tools.KillProcessAndChildren(ffmpegPid);

            jfmpegMap[rtspStreamUrl].Jsmpegpid = 0;
            jfmpegMap[rtspStreamUrl].Ffmpegpid = 0;

        }

        /// <summary>
        /// 刷新视频列表
        /// </summary>
        private void RefreshJfmpegList()
        {
            JFmpegList.Clear();
            foreach (KeyValuePair<string, JFmpeg> jfmpegPair in jfmpegMap)
            {
                JFmpegList.Add(jfmpegPair.Value);
            }

        }

        //[Obsolete("方法已不推荐使用，使用新方法传入rtsp流地址来结束单个jfmpeg流")]
        ///// <summary>
        ///// 通过入端口和出端口关闭一个视频流
        ///// </summary>
        ///// <param name="inPortNum">入端口</param>
        ///// <param name="outPortNum">出端口</param>
        //private void CloseSelectedJFmpeg(string inPortNum, string outPortNum)
        //{
        //    if (dic.ContainsKey(inPortNum))
        //    {
        //        Tools.KillProcessAndChildren(dic[inPortNum]);
        //        dic.Remove(inPortNum);
        //    }
        //    if (dic.ContainsKey(outPortNum))
        //    {
        //        Tools.KillProcessAndChildren(dic[outPortNum]);
        //        dic.Remove(outPortNum);
        //    }
        //}


        ///// <summary>
        ///// 刷新视频流列表
        ///// </summary>
        //private void RefreshJFmpegList()
        //{
        //    JFmpegList.Clear();

        //    XmlDocument xmlDoc = new XmlDocument();

        //    xmlDoc.Load(AppDomain.CurrentDomain.BaseDirectory + "configlist.xml");

        //    XmlNodeList monitors = xmlDoc.SelectNodes("Monitors/monitor");

        //    if (monitors != null)
        //    {
        //        //遍历xml文件，将信息写入list列表
        //        foreach (XmlElement monitor in monitors)
        //        {
        //            JFmpeg myJFmpeg = new JFmpeg();
        //            myJFmpeg.InPort = int.Parse(monitor.FirstChild.ChildNodes[0].InnerText);
        //            myJFmpeg.OutPort = int.Parse(monitor.FirstChild.ChildNodes[1].InnerText);
        //            myJFmpeg.StreamUrl = monitor.LastChild.ChildNodes[0].InnerText;

        //            myJFmpeg.Password = monitor.FirstChild.ChildNodes[2].InnerText;
        //            myJFmpeg.RtspUsername = monitor.LastChild.ChildNodes[1].InnerText;
        //            myJFmpeg.RtspPassword = monitor.LastChild.ChildNodes[2].InnerText;

        //            if (dic.ContainsKey(myJFmpeg.InPort.ToString()))
        //            {
        //                myJFmpeg.Jsmpegpid = dic[myJFmpeg.InPort.ToString()];
        //            }
        //            if (dic.ContainsKey(myJFmpeg.OutPort.ToString()))
        //            {
        //                myJFmpeg.Ffmpegpid = dic[myJFmpeg.OutPort.ToString()];
        //            }
        //            JFmpegList.Add(myJFmpeg);
        //        }
        //    }
        //}

        #endregion
    }
}
