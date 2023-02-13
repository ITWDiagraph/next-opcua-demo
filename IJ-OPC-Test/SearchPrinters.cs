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

public class SearchPrinters
{
    // Enum to define the different types of printers
    public enum PrinterType
    {
        // Unknown printer type
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

    // Constants for ports and timeout
    private const int LocateSendPort = 2201;
    private const int LocateReceivePort = 1706;
    private const int IJBootPPort = 68;
    private const int MillisecondsTimeout = 2000;

    // Method to search printers
    public async Task<List<string>> Search(PrinterType[] printerTypesToFind)
    {
        // List to store the found printers
        var printers = new List<string>();
        // Call the method to broadcast to all network interfaces
        await RunAcrossAllInterfacesAsync(ai => BroadcastToNetworkInterfaceAsync(ai, printerTypesToFind, printers));
        // Return the found printers
        return printers.ToList();
    }

    // Method to run an action across all network interfaces
    private static async Task RunAcrossAllInterfacesAsync(Func<IPAddress, Task> action)
    {
        // Get all network interfaces
        var allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
        // Get the information for each network interface
        var networkInterfaceInfo = allNetworkInterfaces.Select(i => new NetworkInterfaceInfo(i)).ToList();

        // Perform the action for each address in each network interface
        await Task.WhenAll(networkInterfaceInfo.SelectMany(i => i.Addresses).Select(action));
    }

    // Method to broadcast to a network interface
    private async Task BroadcastToNetworkInterfaceAsync(
        IPAddress address,
        PrinterType[] printerTypesToFind,
        List<string> printers)
    {
        try
        {
            // Create a new UDP client for the receive port
            using var client = new UdpClient(new IPEndPoint(address, LocateReceivePort)) {EnableBroadcast = true};

            try
            {
                // Loop through each printer type to find
                foreach (var requestString in printerTypesToFind)
                {
                    // Create the request data
                    var requestData = Encoding.ASCII.GetBytes($"{{Locate {requestString}}}");

                    // Send the request data
                    _ = await client.SendAsync(requestData, requestData.Length,
                        new IPEndPoint(IPAddress.Broadcast, LocateSendPort));
                }

                // The client is closed after the data is received or the timeout expires.
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


    // This private async method receives data asynchronously through a given UDP client.
    // The received data is stored in a list of strings, representing printers.
    // The method continues to run until a cancellation is requested through the given CancellationToken.
    private async Task ReceiveDataAsync(
        UdpClient client, // The UDP client through which data is received.
        List<string> printers, // A list of strings representing printers.
        CancellationToken token) // A CancellationToken to signal the end of the task.
    {
        try
        {
            // Continuously receive data as long as a cancellation is not requested.
            while (!token.IsCancellationRequested)
            {
                // Receive data from the server asynchronously.
                var serverResponseData = await client.ReceiveAsync();
                // Convert the received data from a byte array to a string.
                var serverResponse = Encoding.ASCII.GetString(serverResponseData.Buffer);

                // Write the received data to the console.
                Console.WriteLine($"Received {serverResponse} from {serverResponseData.RemoteEndPoint.Address}");

                // Add the received data to the list of printers.
                printers.Add(serverResponseData.RemoteEndPoint.Address + " " + serverResponse);
            }
        }
        catch (ObjectDisposedException)
        {
            // Do nothing, just catch the exception.
        }
    }

    // Constructor to initialize network interface
    internal sealed class NetworkInterfaceInfo
    {
        private readonly NetworkInterface _networkInterface;

        public NetworkInterfaceInfo(NetworkInterface networkInterface)
        {
            _networkInterface = networkInterface;
        }

        // Property to get the description of the network interface
        public string Description => _networkInterface.Description;

        // Property to get the IPv4 addresses of the network interface
        public IEnumerable<IPAddress> Addresses =>
            _networkInterface.GetIPProperties().UnicastAddresses
                .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address);

        // Property to get the IPv4 addresses as a list of strings
        public List<string> Ipv4Addresses => Addresses.Select(a => a.ToString()).ToList();
    }
}