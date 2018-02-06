using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;

namespace WebServiceDemo
{
    public static class Tools
    {
        #region GetLocalIP Method 获取本机ip地址
        /// <summary>  
        /// 获取当前使用的IP  
        /// </summary>  
        /// <returns>string IP</returns>  
        public static string GetLocalIP()
        {
            string result = RunApp("route", "print", true);
            Match m = Regex.Match(result, @"0.0.0.0\s+0.0.0.0\s+(\d+.\d+.\d+.\d+)\s+(\d+.\d+.\d+.\d+)");
            if (m.Success)
            {
                return m.Groups[2].Value;
            }
            else
            {
                try
                {
                    System.Net.Sockets.TcpClient c = new System.Net.Sockets.TcpClient();
                    c.Connect("www.baidu.com", 80);
                    string ip = ((System.Net.IPEndPoint)c.Client.LocalEndPoint).Address.ToString();
                    c.Close();
                    return ip;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>  
        /// 获取本机主DNS  
        /// </summary>  
        /// <returns>string DNS</returns>  
        public static string GetPrimaryDNS()
        {
            string result = RunApp("nslookup", "", true);
            Match m = Regex.Match(result, @"\d+\.\d+\.\d+\.\d+");
            if (m.Success)
            {
                return m.Value;
            }
            else
            {
                return null;
            }
        }

        /// <summary>  
        /// 运行一个控制台程序并返回其输出参数。  
        /// </summary>  
        /// <param name="filename">程序名</param>  
        /// <param name="arguments">输入参数</param>  
        /// <returns>string RunApp</returns>  
        public static string RunApp(string filename, string arguments, bool recordLog)
        {
            try
            {
                if (recordLog)
                {
                    Trace.WriteLine(filename + " " + arguments);
                }
                Process proc = new Process();
                proc.StartInfo.FileName = filename;
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.Arguments = arguments;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.UseShellExecute = false;
                proc.Start();

                using (System.IO.StreamReader sr = new System.IO.StreamReader(proc.StandardOutput.BaseStream, Encoding.Default))
                {
                    //string txt = sr.ReadToEnd();  
                    //sr.Close();  
                    //if (recordLog)  
                    //{  
                    //    Trace.WriteLine(txt);  
                    //}  
                    //if (!proc.HasExited)  
                    //{  
                    //    proc.Kill();  
                    //}  
                    //上面标记的是原文，下面是我自己调试错误后自行修改的  
                    Thread.Sleep(100);
                    //貌似调用系统的nslookup还未返回数据或者数据未编码完成，程序就已经跳过直接执行  
                    //txt = sr.ReadToEnd()了，导致返回的数据为空，故睡眠令硬件反应  
                    //在无参数调用nslookup后，可以继续输入命令继续操作，如果进程未停止就直接执行
                    //txt = sr.ReadToEnd()程序就在等待输入，而且又无法输入，直接掐住无法继续运行
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                    string txt = sr.ReadToEnd();
                    sr.Close();
                    if (recordLog)
                        Trace.WriteLine(txt);
                    return txt;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return ex.Message;
            }
        }
        #endregion

        #region Execute Method 通过cmd执行命令
        /// <summary>
        /// 通过cmd执行命令行程序
        /// </summary>
        /// <param name="command">命令行</param>
        /// <param name="seconds">设置等待进程结束的时间</param>
        /// <returns>cmd.exe的pid</returns>
        public static int Execute(string command, int seconds)
        {
            string output = ""; //输出字符串 
            int pid = 0;
            if (command != null && !command.Equals(""))
            {
                //创建进程对象  
                Process process = new Process();
                ProcessStartInfo startInfo = new ProcessStartInfo();

                //设定需要执行的命令  
                startInfo.FileName = "cmd.exe";

                //“/C”表示执行完命令后马上退出  
                startInfo.Arguments = "/C " + command;

                //不使用系统外壳程序启动  
                startInfo.UseShellExecute = false;

                //不重定向输入  
                startInfo.RedirectStandardInput = false;

                //重定向输出  
                startInfo.RedirectStandardOutput = true;

                //startInfo.RedirectStandardError = true;

                //不创建窗口  
                startInfo.CreateNoWindow = true;
                process.StartInfo = startInfo;
                try
                {
                    //开始进程  
                    if (process.Start())
                    {
                        if (seconds == 0)
                        {   //这里无限等待进程结束  
                            process.WaitForExit();

                        }
                        else
                        {
                            //等待进程结束，等待时间为指定的毫秒 
                            process.WaitForExit(seconds);                              
                        }
                            //output = process.StandardOutput.ReadToEnd();//读取进程的输出  

                        pid = process.Id;
                    }
                }
                catch
                {
                }
                finally
                {
                    if (process != null)
                        process.Close();
                }
            }
            //return output;
            return pid;
        }
        #endregion

        #region KillProcessAndChildren Method 通过cmd父级pid结束关联子进程
        /// <summary>
        ///功能：根据父进程id，杀死与之相关的进程树 
        /// </summary>
        /// <param name="pid">父进程id</param>
        public static void KillProcessAndChildren(int pid)
        {
            if (pid == 0)
            {
                return;
            }
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));  
            }
            try
            {
                Process proc = Process.GetProcessById(pid);
                //Console.WriteLine(pid.ToString());
                proc.Kill();
            }
            catch (Exception)
            {
                return;
                /* process already exited */
            }
        }
        #endregion

        #region PortIsUsed Method 获取操作系统正在使用的端口号列表
        /// <summary>        
        /// 获取操作系统已用的端口号        
        /// </summary>        
        /// <returns></returns>        
        public static IList PortIsUsed()
        {
            //获取本地计算机的网络连接和通信统计数据的信息            
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();

            //返回本地计算机上的所有Tcp监听程序            
            IPEndPoint[] ipsTCP = ipGlobalProperties.GetActiveTcpListeners();

            //返回本地计算机上的所有UDP监听程序            
            IPEndPoint[] ipsUDP = ipGlobalProperties.GetActiveUdpListeners();

            //返回本地计算机上的Internet协议版本4(IPV4 传输控制协议(TCP)连接的信息。            
            TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

            IList allPorts = new ArrayList();

            foreach (IPEndPoint ep in ipsTCP)
            {
                allPorts.Add(ep.Port);
            }

            foreach (IPEndPoint ep in ipsUDP)
            {
                allPorts.Add(ep.Port);
            }

            foreach (TcpConnectionInformation conn in tcpConnInfoArray)
            {
                allPorts.Add(conn.LocalEndPoint.Port);
            }

            return allPorts;
        }
        #endregion
    }
}