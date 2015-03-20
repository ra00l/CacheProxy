using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Fiddler;
using Newtonsoft.Json;

namespace CacheProxy.Base
{
    public class FiddlerHelper : IDisposable
    {
        public event EventHandler<string> Refresh;
        public event EventHandler<string> LogMessage;
        public event EventHandler<AppResponse> ResponseFileAdded;

        string _urlMatch = "*";
        List<AppResponse> _respList = null;

        public FiddlerHelper(CachedHost host)
        {
            _respList = host.Responses;
            var cacheFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.json");
            if (File.Exists(cacheFileName))
                _respList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<AppResponse>>(File.ReadAllText(cacheFileName));


            _urlMatch = host.Pattern ?? "*";
            Fiddler.FiddlerApplication.SetAppDisplayName("CacheProxy");

            Fiddler.FiddlerApplication.OnNotification += delegate(object sender, NotificationEventArgs oNEA) { Console.WriteLine("** NotifyUser: " + oNEA.NotifyString); };
            Fiddler.FiddlerApplication.Log.OnLogString += delegate(object sender, LogEventArgs oLEA) { Console.WriteLine("** LogString: " + oLEA.LogString); };

            bool caughtMsg = false;
            FiddlerApplication.BeforeResponse += delegate(Fiddler.Session session)
            {
                if (Regex.IsMatch(session.fullUrl, host.Pattern))
                {
                    if (session.oResponse.headers.HTTPResponseCode == 200) //only save 200
                    {
                        //var strResponse = session.GetResponseBodyAsString();
                        //session.SaveResponse("C:\\Users\\raul\\Desktop\\fidResp\\resp-" + DateTime.Now.Ticks + ".txt", false);

                        if (string.IsNullOrEmpty(session.oResponse.headers["cache-hit"]))
                        {
                            Monitor.Enter(_respList);
                            var existingResp = _respList.SingleOrDefault(r => r.URI == session.fullUrl && r.IsMatchTo(session.GetRequestBodyAsString()));
                            if (existingResp != null && (existingResp.ResponseFilePath == null || !File.Exists(existingResp.ResponseFilePath)))
                            {
                                SendMessage("Adding " + session.fullUrl + " to cache!");

                                existingResp.ResponseFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, host.ID, "res-" + existingResp.ID + ".txt");
                                session.SaveResponse(existingResp.ResponseFilePath, false);

                                SendCacheFile(existingResp);
                                //session.LoadResponseFromFile(@"C:\Users\raul\Desktop\fidResp\fidFile-635611958862841575.txt");
                            }
                            Monitor.Exit(_respList);
                        }
                    }
                }
            };
            Fiddler.FiddlerApplication.BeforeRequest += delegate(Fiddler.Session session)
            {
                if (!caughtMsg)
                {
                    SendMessage("First HTTP request intercepted");
                    caughtMsg = true;
                }

                session.bBufferResponse = true;
                //Monitor.Enter(oAllSessions);
                //oAllSessions.Add(oS);
                //Monitor.Exit(oAllSessions);
                session["X-AutoAuth"] = "(default)";
                
                if (Regex.IsMatch(session.fullUrl, host.Pattern))
                {
                    //check if I have cached response for this URL + headers.
                    //SendMessage("URL matched: " + session.fullUrl);

                    Monitor.Enter(_respList);
                    var existingResp = _respList.SingleOrDefault(r => r.URI == session.fullUrl && r.IsMatchTo(session.GetRequestBodyAsString()));

                    if (existingResp != null)
                    {
                        existingResp.CacheHit += 1;
                        existingResp.LastHit = DateTime.Now;
                        SendMessage("Cache HIT! " + existingResp.CacheHit + ", " + session.fullUrl);

                        session.utilCreateResponseAndBypassServer();
                        //session.utilSetResponseBody(existingResp.RawResponse);
                        session.LoadResponseFromFile(existingResp.ResponseFilePath);
                        session.oResponse.headers.Add("cache-hit", "1");
                        //session.oResponse.

                        SendRefresh();
                    }
                    else 
                    {
                        SendMessage("URL not in cache! " + session.fullUrl);
                        var reqID = Guid.NewGuid().ToString();
                        var exReqFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, host.ID.ToString(), "req-" + reqID + ".txt");
                        session.SaveRequestBody(exReqFile);
                        _respList.Add(new AppResponse() { ID = reqID, URI = session.fullUrl, Created = DateTime.Now, CacheHit = 0, LastHit = DateTime.Now, RequestFilePath = exReqFile });
                    }

                    Monitor.Exit(_respList);

                    //if (ResponseHit != null)
                    //    ResponseHit(this, session.fullUrl);

                    //session.SaveRequest("C:\\Users\\raul\\Desktop\\fidResp\\req-" + DateTime.Now.Ticks + ".txt", false);

                    //session.utilCreateResponseAndBypassServer();
                    //session.utilSetResponseBody("this is the response");
                    //session.LoadResponseFromFile(@"C:\Users\raul\Desktop\fidResp\fidFile-635611958862841575.txt");
                    
                    //session.utilSetResponseBody("string");
                    //using (var ms = new MemoryStream())
                    //{
                    //    var wr = new StreamWriter(ms);
                    //    wr.Write("hello from gdo!");
                    //    wr.Flush();
                    //    session.ResponseBody = ms.ToArray();
                    //}
                }
            };

            Fiddler.CONFIG.IgnoreServerCertErrors = true;
            FiddlerApplication.Prefs.SetBoolPref("fiddler.network.streaming.abortifclientaborts", true);
            FiddlerCoreStartupFlags oFCSF = FiddlerCoreStartupFlags.Default;
            int iPort = 8877;
            Fiddler.FiddlerApplication.Startup(iPort, true, false, false);
            //Fiddler.URLMonInterop.SetProxyInProcess("127.0.0.1:8877", String.Empty);
        }

        void SendMessage(string msg) 
        {
            if (LogMessage != null)
                LogMessage(this, msg);
        }
        void SendCacheFile(AppResponse resp)
        {
            if (ResponseFileAdded != null)
                ResponseFileAdded(this, resp);
        }
        void SendRefresh()
        {
            if (Refresh != null)
                Refresh(this, null);
        }

        Proxy oSecureEndpoint;
        string sSecureEndpointHostname = "localhost";
        int iSecureEndpointPort = 7777;

        public static void WriteCommandResponse(string s)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(s);
            Console.ForegroundColor = oldColor;
        }

        private static string Ellipsize(string s, int iLen)
        {
            if (s.Length <= iLen) return s;
            return s.Substring(0, iLen - 3) + "...";
        }

        private static void WriteSessionList(List<Fiddler.Session> oAllSessions)
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Session list contains...");
            try
            {
                Monitor.Enter(oAllSessions);
                foreach (Session oS in oAllSessions)
                {
                    Console.Write(String.Format("{0} {1} {2}\n{3} {4}\n\n", oS.id, oS.oRequest.headers.HTTPMethod, Ellipsize(oS.fullUrl, 60), oS.responseCode, oS.oResponse.MIMEType));
                }
            }
            finally
            {
                Monitor.Exit(oAllSessions);
            }
            Console.WriteLine();
            Console.ForegroundColor = oldColor;
        }

        public void Dispose()
        {
            if (null != oSecureEndpoint) oSecureEndpoint.Dispose();
            Fiddler.FiddlerApplication.Shutdown();
            Thread.Sleep(500);
        }

        public void ClearCacheList()
        {
            _respList.Clear();
        }
    }
}
