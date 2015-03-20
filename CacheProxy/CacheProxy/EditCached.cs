using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CacheProxy.Base;

namespace CacheProxy
{
    public partial class EditCached : Form
    {
        public EditCached()
        {
            InitializeComponent();
        }

        public AppResponse AppResp { get; set; }
        public CachedHost Host { get; set; }

        private void EditCached_Load(object sender, EventArgs e)
        {
            if (AppResp == null)
            {
                tbResponse.AppendText(@"HTTP/1.1 200 OK
Date: Sat, 07 Mar 2015 14:37:54 GMT
Server: Apache/2.4.10 (Win32) OpenSSL/1.0.1i PHP/5.6.3
X-Powered-By: PHP/5.6.3
Content-Length: 1991
Keep-Alive: timeout=5, max=94
Connection: Keep-Alive
Content-Type: application/json");
            }
            else
            {
                tbRequest.Text = File.ReadAllText(AppResp.RequestFilePath);
                tbResponse.Text = File.ReadAllText(AppResp.ResponseFilePath);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (AppResp == null)
            {
                AppResp = new AppResponse() { ID = Guid.NewGuid().ToString(), Created = DateTime.Now, URI = "" };
                AppResp.RequestFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Host.ID, "req-" + AppResp.ID + ".txt");
                AppResp.ResponseFilePath = AppResp.RequestFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Host.ID, "res-" + AppResp.ID + ".txt");
            }

            File.WriteAllText(AppResp.RequestFilePath, tbRequest.Text);

            //see content length + update it
            var resp = tbResponse.Text;
            //var len = resp.
            File.WriteAllText(AppResp.ResponseFilePath, resp);


            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo("cmd.exe", "/c \""+AppResp.ResponseFilePath+"\""));
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo("cmd.exe", "/c \""+AppResp.RequestFilePath+"\""));
        }
    }
}
