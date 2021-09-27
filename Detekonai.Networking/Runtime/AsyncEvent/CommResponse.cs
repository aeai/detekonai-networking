using Detekonai.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Detekonai.Networking.Runtime.AsyncEvent.ICommResponse;

namespace Detekonai.Networking.Runtime.AsyncEvent
{
	public class CommResponse : ICommResponse
	{
        private readonly ushort index;
        private readonly IAsyncEventHandlingTactics tactics;

		public BinaryBlob Blob { get; set; } = null;
		public Action Continuation { get; set; } = null;
		public AwaitResponseStatus Status { get; set; } = AwaitResponseStatus.Pending;

		public CommResponse(ushort index, IAsyncEventHandlingTactics tactics)
		{
            this.index = index;
            this.tactics = tactics;
        }

		public void Dispose()
		{
			Blob?.Release();
			tactics.ReleaseResponse(index);
		}
	}
}
