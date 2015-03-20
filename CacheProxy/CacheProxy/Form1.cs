using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CacheProxy.Base;
using Newtonsoft.Json;

namespace CacheProxy
{
    public partial class Form1 : Form
    {
        FiddlerHelper _proxy = null;
        public Form1()
        {
            InitializeComponent();

            this.FormClosing += Form1_FormClosing;
        }

        void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(_proxy != null)
                _proxy.Dispose();

            SaveHosts();
        }

        private CachedHost CreateAndAddHost(string text)
        {
            var host = new CachedHost() { Pattern = text, ID = Guid.NewGuid().ToString(), Responses = new List<AppResponse>(), Selected = true };

            var hostDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, host.ID.ToString());
            if (!Directory.Exists(hostDir))
                Directory.CreateDirectory(hostDir);

            _filterHosts.ForEach(i => i.Selected = false);
            _filterHosts.Add(host);


            return host;
        }

        void SaveHosts()
        {
            var settFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cacheproxy.json");
            File.WriteAllText(settFile, JsonConvert.SerializeObject(_filterHosts));
        }

        public List<CachedHost> _filterHosts = new List<CachedHost>();
        private void Form1_Load(object sender, EventArgs e)
        {
            var initList = new List<AppResponse>();
            var settFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cacheproxy.json");
            CachedHost host = null;
            if (File.Exists(settFile))
            {
                _filterHosts = JsonConvert.DeserializeObject<List<CachedHost>>(File.ReadAllText(settFile));
                host = _filterHosts.FirstOrDefault(h => h.Selected);
                if (host == null)
                    host = _filterHosts.FirstOrDefault();
            }

            if(host == null) {
                host = CreateAndAddHost("localhost.*?\\.php");
            }

            
            cboFilters.DataSource = _filterHosts.Select(h => h.Pattern).ToList();
            cboFilters.Text = host.Pattern;

            //grdCache.ColumnCount = 4;
            grdCache.AutoGenerateColumns = false;
            grdCache.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);

            grdCache.Columns.Add(new DataGridViewTextBoxColumn() { DataPropertyName = "Created", Name = "Added", });
            grdCache.Columns.Add(new DataGridViewTextBoxColumn() { DataPropertyName = "LastHit", Name = "Last hit" });
            grdCache.Columns.Add(new DataGridViewTextBoxColumn() { DataPropertyName = "CacheHit", Name = "Hit count" });
            grdCache.Columns.Add(new DataGridViewTextBoxColumn() { DataPropertyName = "URI", Name = "URI" });
       
            grdCache.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grdCache.MultiSelect = false;
            grdCache.CellFormatting += grdCache_CellFormatting;

            InitProxy(host);
        }

        CachedHost _host = null;
        private void InitProxy(CachedHost host)
        {
            _host = host;
            txtLog.Clear();
            grdCache.DataSource = _host.Responses;
            //if(grdCache.Rows != null)
            //    grdCache.Rows.Clear();

            _proxy = new FiddlerHelper(host);
            _proxy.LogMessage += logMessageHandler;
            _proxy.ResponseFileAdded += responseFileAdded;
            _proxy.Refresh += _proxy_Refresh;
        }

        void _proxy_Refresh(object sender, string e)
        {
            grdCache.BeginInvoke(new InvokeDelegate(() =>
            {
            grdCache.Refresh();
            }));
        }

        void RefreshSource()
        {
            var selIndex = -1;
            if(grdCache.SelectedRows != null && grdCache.SelectedRows.Count > 0)
                selIndex = grdCache.SelectedRows[0].Index;
            grdCache.DataSource = _host.Responses.ToList();
            if(grdCache.Rows.Count > selIndex && selIndex > -1)
                grdCache.Rows[selIndex].Selected = true;
        }

        void responseFileAdded(object sender, AppResponse resp)
        {
            
            grdCache.BeginInvoke(new InvokeDelegate(() =>
            {
                //grdCache.DataSource = null;
                //Monitor.Enter(_respList);
                //_respList.Add(resp);
                //Monitor.Exit(_respList);
                RefreshSource();
                //grdCache.Refresh();

                //grdCache.Rows.Add(resp);
                //new string[] { resp.Created.ToShortTimeString(), resp.LastHit.ToShortTimeString(), resp.CacheHit.ToString(), resp.URI});
            }));
        }

        public delegate void InvokeDelegate();
        void logMessageHandler(object sender, string msg)
        {
            txtLog.BeginInvoke(new InvokeDelegate(() =>
            {
                txtLog.AppendText(msg + Environment.NewLine);
            }));
        }

        private void btnChange_Click(object sender, EventArgs e)
        {
            _proxy.Dispose();

            var text = cboFilters.Text;
            var host = _filterHosts.SingleOrDefault(h => h.Pattern == text);

            _filterHosts.ForEach(f => f.Selected = false);
            if (host == null)
                host = CreateAndAddHost(text);
            host.Selected = true;

            SaveHosts();
           
            InitProxy(host);
        }

        private void grdCache_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (grdCache.Rows[e.RowIndex].DataBoundItem != null)
            {
                AppResponse data = grdCache.Rows[e.RowIndex].DataBoundItem as AppResponse;

                if (e.ColumnIndex == 0) e.Value = data.Created.ToString("HH:mm:ss");
                else if (e.ColumnIndex == 1) e.Value = data.LastHit.ToString("HH:mm:ss");
                else if (e.ColumnIndex == 3)
                {


                    var cellVal = BindProperty(data,
                        grdCache.Columns[e.ColumnIndex].DataPropertyName);
                    e.Value = new Uri(cellVal).PathAndQuery;

                    DataGridViewCell cell = this.grdCache.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    var reqContent = File.ReadAllText(data.RequestFilePath);
                    if(string.IsNullOrEmpty(reqContent)) cell.ToolTipText = "No POST data";
                    else cell.ToolTipText = "Request: \n\n" + reqContent;
                }
            }
        }

        private string BindProperty(object property, string propertyName)
        {
            string retValue = "";

                Type propertyType;
                PropertyInfo propertyInfo;

                propertyType = property.GetType();
                propertyInfo = propertyType.GetProperty(propertyName);
                retValue = propertyInfo.GetValue(property, null).ToString();

            return retValue;
        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            txtLog.Clear();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            _host.Responses = new List<AppResponse>();
            foreach(var file in Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _host.ID)))
                File.Delete(file);

            RefreshSource();
            _proxy.ClearCacheList();
        }

        private void btnAddResp_Click(object sender, EventArgs e)
        {
            OpenEditMode(null);
        }

        void OpenEditMode(AppResponse resp)
        {
            EditCached edit = new EditCached();
            edit.AppResp = resp;

            edit.ShowDialog();

            if (resp == null && edit.AppResp != null)
            {
                _host.Responses.Add(edit.AppResp);
            }
            RefreshSource();
        }

        private void btnEditSel_Click(object sender, EventArgs e)
        {
            var selRows =grdCache.SelectedRows;
            if(selRows.Count > 0)
                OpenEditMode(grdCache.Rows[selRows[0].Index].DataBoundItem as AppResponse);
        }

        private void btnRemoveSel_Click(object sender, EventArgs e)
        {
            if (grdCache.SelectedRows != null && grdCache.SelectedRows.Count > 0)
            {
                _host.Responses.Remove(grdCache.SelectedRows[0].DataBoundItem as AppResponse);
                RefreshSource();
            }
        }
    }
}
