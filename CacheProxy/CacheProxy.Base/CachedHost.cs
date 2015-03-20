using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CacheProxy.Base;

namespace CacheProxy.Base
{
    public class CachedHost
    {
        public string ID { get; set; }
        public string Pattern { get; set; }
        public List<AppResponse> Responses { get; set; }
        public bool Selected { get; set; }
    }
}
