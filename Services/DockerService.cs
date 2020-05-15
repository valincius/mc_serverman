using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using mc_serverman.Models;

namespace mc_serverman.Services {
	public class DockerService {
		private const string MC_DOCKER_IMAGE_NAME = "itzg/minecraft-server";
		private DockerClient DockerClient;

		public List<MCContainer> Containers { get; private set; } = new List<MCContainer>();

		public DockerService() {
			DockerClient = new DockerClientConfiguration(LocalDockerUri()).CreateClient();
		}
		public Uri LocalDockerUri() => new Uri(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "npipe://./pipe/docker_engine" : "unix:/var/run/docker.sock");

		public async Task<string> GetContainerStatus(string ID) {
			return (await DockerClient.Containers.InspectContainerAsync(ID)).State.Health.Status;
		}
		public async Task RefreshContainers() {
			var dockerContainers = (await DockerClient.Containers.ListContainersAsync(new ContainersListParameters()))
				.Where(c => c.Image == MC_DOCKER_IMAGE_NAME);
			
			await Task.WhenAll(dockerContainers.Select(async dockerContainer => {
				var containerID = dockerContainer.ID;
				var container = Containers.Where(c => c.ID == containerID).SingleOrDefault() ?? new MCContainer();

				container.ID = containerID;
				container.Status = await GetContainerStatus(containerID);
				container.Name = dockerContainer.Names.Where(n => !string.IsNullOrEmpty(n)).Single().Substring(1);
				container.Port = dockerContainer.Ports.Where(p => p.PublicPort > 0).Select(p => p.PublicPort).SingleOrDefault();
				container.StdOut ??= string.Empty;
				container.RconStatusStream ??= await container.ConnectToRcon(DockerClient);
				container.RconStream ??= await container.ConnectToRcon(DockerClient);
				string listResponse = await container.RconStatusStream.Send("list");
				var listResponseParsed = new Regex(@"There are (?<total>\d+) of a max (?<max>\d+) players online: (?<users>.*)\r\n").Match(listResponse);
				if (listResponseParsed.Success) {
					container.TotalPlayers = int.Parse(listResponseParsed.Groups["total"].ToString());
					container.MaxPlayers = int.Parse(listResponseParsed.Groups["max"].ToString());
					container.Players = listResponseParsed.Groups["users"].ToString().Split(", ").Where(s => s != string.Empty).ToList();
				} else {
					container.Players = new List<string>();
				}

				if (!Containers.Contains(container)) {
					container.StdOut += await container.RconStream.Read();
					Containers.Add(container);
				}
			}));
		}
		private int GetNextAvailablePort(int startingPort) {
			var portArray = new List<int>();
			var properties = IPGlobalProperties.GetIPGlobalProperties();

			var connections = properties.GetActiveTcpConnections();
			portArray.AddRange(connections.Where(c => c.LocalEndPoint.Port >= startingPort).Select(c => c.LocalEndPoint.Port));

			var endPoints = properties.GetActiveTcpListeners();
			portArray.AddRange(endPoints.Where(e => e.Port >= startingPort).Select(e => e.Port));

			portArray.Sort();

			for (var i = startingPort; i < ushort.MaxValue; i++)
				if (!portArray.Contains(i))
					return i;

			return 0;
		}
		public async Task CreateNewServer() {
			var container = await DockerClient.Containers.CreateContainerAsync(new CreateContainerParameters {
				Image = MC_DOCKER_IMAGE_NAME,
				Env = new List<string> {
					"EULA=TRUE"
				},
				ExposedPorts = new Dictionary<string, EmptyStruct> {
					{ "25565", default }
				},
				HostConfig = new HostConfig {
					PortBindings = new Dictionary<string, IList<PortBinding>> {
						{ "25565", new List<PortBinding> { new PortBinding { HostPort = GetNextAvailablePort(25565).ToString() } } }
					},
				}
			});

			await DockerClient.Containers.StartContainerAsync(container.ID, null);
		}
	}
}