using System.Collections.Generic;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace mc_serverman.Models {
	public class MC_Container {
		public string ID { get; set; }
		public string Status { get; set; }
		public string Name { get; set; }
		public ushort Port { get; set; }
		public RconStream RconStatusStream { get; set; }
		public RconStream RconStream { get; set; }
		public string StdOut { get; set; }
		public int TotalPlayers { get; set; }
		public int MaxPlayers { get; set; }
		public List<string> Players { get; set; }

		public async Task<RconStream> ConnectToRcon(DockerClient dockerClient) {
			var response = await dockerClient.Containers.ExecCreateContainerAsync(ID, new ContainerExecCreateParameters {
				Cmd = new List<string> {
					"rcon-cli"
				},
				AttachStderr = true,
				AttachStdin = true,
				AttachStdout = true,
				Detach = false,
				Tty = true,
				Privileged = true
			});
			var stream = new RconStream(await dockerClient.Containers.StartAndAttachContainerExecAsync(response.ID, true));
			var res = await stream.Read();
			StdOut += res;
			if (res == "> ") {
				return stream;
			}
			return null;
		}
	}
}