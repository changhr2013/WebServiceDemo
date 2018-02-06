using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebServiceDemo
{
    public class JFmpeg
    {
        public int InPort { get; set; }
        public int OutPort { get; set; }
        public string StreamUrl { get; set; }
        public int Jsmpegpid { get; set; }
        public int Ffmpegpid { get; set; }
        public string Password { get; set; }
        public string RtspUsername { get; set; }
        public string RtspPassword { get; set; }

        public JFmpeg(int inPort, int outPort, string streamUrl, string password, string rtspUsername, string rtspPassword)
        {
            this.InPort = inPort;
            this.OutPort = outPort;
            this.StreamUrl = streamUrl;
            this.Password = password;
            this.RtspUsername = rtspUsername;
            this.RtspPassword = rtspPassword;
        }
        public JFmpeg() { }
    }
}