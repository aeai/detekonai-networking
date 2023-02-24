using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Tcp
{
    public class SimpleConnectionData : IConnectionData
    {
        public Socket Sock { get; private set; }
        private Dictionary<string, object> data = new Dictionary<string, object>();
        public SimpleConnectionData(Socket sock) 
        {
            Sock = sock;
        }

        public void AddCustomData<T>(string key, T value)
        {
            data[key] = value;
        }

        public T GetCustomData<T>(string key) 
        {
            data.TryGetValue(key, out object res);
            if(res is T tres)
            {
                return tres;
            }
            return default;
        }
    }
}
