using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;

namespace mc_serverman.Models {
	public class RconStream {
		public MultiplexedStream Stream { get; private set; }
		public RconStream(MultiplexedStream stream) =>
			(Stream) = (stream);

		public async Task<string> Read() {
			var recvBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
			var read = await Stream.ReadOutputAsync(recvBuffer, 0, recvBuffer.Length, CancellationToken.None);
			string res = Encoding.UTF8.GetString(recvBuffer, 0, read.Count);
			System.Buffers.ArrayPool<byte>.Shared.Return(recvBuffer);
			return res;
		}
		public async Task<string> Send(string command) {
			var sendBuffer = Encoding.UTF8.GetBytes(command + "\n");
			await Stream.WriteAsync(sendBuffer, 0, sendBuffer.Length, CancellationToken.None);
			await Task.Delay(100);
			return await Read();
		}
	}
}
