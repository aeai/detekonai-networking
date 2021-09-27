using Detekonai.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.AsyncEvent
{
    public interface ICommResponse : IDisposable
    {
		public AwaitResponseStatus Status { get; }
		public BinaryBlob Blob { get; }
	}
}
