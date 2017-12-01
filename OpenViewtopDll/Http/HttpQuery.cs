using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gosub.Http
{
    /// <summary>
    /// Quickie class to make it easier to use the query string.
    /// Keys are typically stored lower case
    /// </summary>
    public class HttpQuery : Dictionary<string, string>
    {
        /// <summary>
        /// Return the key or "" if not found
        /// </summary>
        public string Get(string key)
        {
            if (!TryGetValue(key, out string value))
                value = "";
            return value;
        }

    }
}
