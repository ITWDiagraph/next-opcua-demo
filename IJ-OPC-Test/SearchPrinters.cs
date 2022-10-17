using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IJ_OPC_Test;

public class SearchPrinters
{
    public enum PrinterType
    {
        [Description("Unknown printer")] UNKNOWN,
        [Description("ALP")] ALP,
        [Description("IJTask1")] LCIJTask1,
        [Description("IJTask2")] LCIJTask2,
        [Description("LINXSCP")] LINXSCP,
        [Description("FOXJET")] FOXJET,
        [Description("TTO")] TTO,
        [Description("SIM")] SIM,
        [Description("LINXRCI")] LINXRCI
    }

    private const int LocateSendPort = 2201;
    private const int LocateReceivePort = 1706;
    private const int IJBootPPort = 68;
    private const int MillisecondsTimeout = 2000;

    public async Task<List<string>> Search(PrinterType[] printerTypesToFind)
    {
        var printers = new List<string>();
        await RunAcrossAllInterfacesAsync(ai => BroadcastToNetworkInterfaceAsync(ai, printerTypesToFind, printers));
        return printers.ToList();
    }

    private static async Task RunAcrossAllInterfacesAsync(Func<IPAddress, Task> action)
    {
        var allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
        var networkInterfaceInfo = allNetworkInterfaces.Select(i => new NetworkInterfaceInfo(i)).ToList();

        await Task.WhenAll(networkInterfaceInfo.SelectMany(i => i.Addresses).Select(action));
    }

    private async Task BroadcastToNetworkInterfaceAsync(
        IPAddress address,
        PrinterType[] printerTypesToFind,
        List<string> printers)
    {
        try
        {
            using var client = new UdpClient(new IPEndPoint(address, LocateReceivePort)) {EnableBroadcast = true};

            try
            {
                foreach (var requestString in printerTypesToFind)
                {
                    var requestData = Encoding.ASCII.GetBytes($"{{Locate {requestString}}}");

                    _ = await client.SendAsync(requestData, requestData.Length,
                        new IPEndPoint(IPAddress.Broadcast, LocateSendPort));
                }

                using var tokenSource = new CancellationTokenSource();
                var dataTask = ReceiveDataAsync(client, printers, tokenSource.Token);

                if (await Task.WhenAny(dataTask, Task.Delay(MillisecondsTimeout, tokenSource.Token)) != dataTask)
                    tokenSource.Cancel();

                client.Close();
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Socket error: {e}");
            }
        }
        catch (SocketException e)
        {
            //if (e.ErrorCode == 10049) Console.WriteLine($"Address {address} is not valid and will be skipped");
        }
        catch (Exception)
        {
        }
    }

    private async Task ReceiveDataAsync(
        UdpClient client,
        List<string> printers,
        CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var serverResponseData = await client.ReceiveAsync();
                var serverResponse = Encoding.ASCII.GetString(serverResponseData.Buffer);

                Console.WriteLine($"Received {serverResponse} from {serverResponseData.RemoteEndPoint.Address}");

                printers.Add(serverResponseData.RemoteEndPoint.Address + " " + serverResponse);
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    internal sealed class NetworkInterfaceInfo
    {
        private readonly NetworkInterface _networkInterface;

        public NetworkInterfaceInfo(NetworkInterface networkInterface)
        {
            _networkInterface = networkInterface;
        }

        public string Description => _networkInterface.Description;

        public IEnumerable<IPAddress> Addresses =>
            _networkInterface.GetIPProperties().UnicastAddresses
                .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address);

        public List<string> Ipv4Addresses => Addresses.Select(a => a.ToString()).ToList();
    }
}