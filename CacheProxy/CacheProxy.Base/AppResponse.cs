using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheProxy.Base
{
    public class AppResponse
    {
        public string ID { get; set; }
        public string RequestFilePath { get; set; }
        public string ResponseFilePath { get; set; }
        public string URI { get; set; }
        public DateTime Created { get; set; }
        public int CacheHit { get; set; }
        public DateTime LastHit { get; set; }

        public bool IsMatchTo(string req)
        {
            var existingReq = File.ReadAllText(RequestFilePath);
            return req == existingReq;
        }
        //public override string ToString()
        //{
        //    return URI + "("+Uri.UnescapeDataString(File.ReadAllText(RequestFilePath))+")";
        //}
    }
}
