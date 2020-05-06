using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Linq;
using System.Net.NetworkInformation;
using mc_serverman.Models;
using System.Runtime.InteropServices;

namespace mc_serverman.Services {
    public class DockerService {
        private const string MC_DOCKER_IMAGE_NAME = "itzg/minecraft-server";
        private DockerClient DockerClient;

        private IEnumerable<Container> _containers;
        public IEnumerable<Container> Containers => _containers;

        public DockerService() {
            DockerClient = new DockerClientConfiguration(LocalDockerUri()).CreateClient();
        }
        public Uri LocalDockerUri() {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            return isWindows ? new Uri("npipe://./pipe/docker_engine") : new Uri("unix:/var/run/docker.sock");
        }
        public async Task<string> GetContainerStatus(string ID) {
            return (await DockerClient.Containers.InspectContainerAsync(ID)).State.Health.Status;
        }
        public async Task RefreshContainers() {
            var containers = await DockerClient.Containers.ListContainersAsync(new ContainersListParameters());
            var tasks = containers.Where(c => c.Image == MC_DOCKER_IMAGE_NAME).Select(async c =>
                new Container {
                    ID = c.ID,
                    Status = await GetContainerStatus(c.ID),
                    Name = c.Names.Where(n => !string.IsNullOrEmpty(n)).Single().Substring(1),
                    Port = c.Ports.Where(p => p.PublicPort > 0).Select(p => p.PublicPort).SingleOrDefault(),
                }
            );
            _containers = await Task.WhenAll(tasks);
        }
        private int GetNextAvailablePort(int startingPort) {
            var portArray = new List<int>();
            var properties = IPGlobalProperties.GetIPGlobalProperties();

            var connections = properties.GetActiveTcpConnections();
            portArray.AddRange(from n in connections where n.LocalEndPoint.Port >= startingPort select n.LocalEndPoint.Port);

            var endPoints = properties.GetActiveTcpListeners();
            portArray.AddRange(from n in endPoints where n.Port >= startingPort select n.Port);

            portArray.Sort();

            for (var i = startingPort; i < UInt16.MaxValue; i++)
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
        public async Task<MultiplexedStream> ConnectToRcon(string ID) {
            var response = await DockerClient.Containers.ExecCreateContainerAsync(ID, new ContainerExecCreateParameters {
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
            return await DockerClient.Containers.StartAndAttachContainerExecAsync(response.ID, true);
        }
    }
}