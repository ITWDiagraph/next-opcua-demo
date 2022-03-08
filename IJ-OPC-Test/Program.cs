using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Workstation.ServiceModel.Ua;
using Workstation.ServiceModel.Ua.Channels;

namespace IJ_OPC_Test;

public class Program
{
    private static async Task Main(string[] args)
    {
        var clientDescription = new ApplicationDescription
        {
            ApplicationName = "IJ.UaClient.Tests",
            ApplicationUri = $"urn:{Dns.GetHostName()}:IJ.UaClient.Tests",
            ApplicationType = ApplicationType.Client
        };

        var channel = new UaTcpSessionChannel(
            clientDescription,
            null,
            new AnonymousIdentity(),
            "opc.tcp://192.168.1.237:16664",
            SecurityPolicyUris.None);
        try
        {
            await channel.OpenAsync();
            if (channel.State == CommunicationState.Opened)
            {
                //Read Status
                var response = await MakeOpcuaCall(channel, "GetStatusInformation", new[] {new Variant(1)});
                var serviceResult = response?.ResponseHeader?.ServiceResult;

                if (response?.Results != null && serviceResult.HasValue && StatusCode.IsGood(serviceResult.Value))
                    if (serviceResult.Value != StatusCodes.GoodPostActionFailed)
                        if (response.Results.Length > 0 && response.Results[0]!.OutputArguments!.Length > 7)
                        {
                            Console.WriteLine("Status: {0}", response.Results[0].OutputArguments[1].Value);
                            Console.WriteLine("MessageName: {0}", response.Results[0].OutputArguments[2].Value);
                            Console.WriteLine("LineSpeed: {0}", response.Results[0].OutputArguments[3].Value);
                            foreach (var value in
                                     (response.Results[0].OutputArguments[4].Value as IEnumerable<object>)!)
                                Console.WriteLine(value);
                            foreach (var value in
                                     (response.Results[0].OutputArguments[5].Value as IEnumerable<object>)!)
                                Console.WriteLine("Consumable: {0}", value);
                            foreach (var value in
                                     (response.Results[0].OutputArguments[6].Value as IEnumerable<object>)!)
                                Console.WriteLine("Error: {0}", value);
                            foreach (var value in
                                     (response.Results[0].OutputArguments[7].Value as IEnumerable<object>)!)
                                Console.WriteLine("Warning: {0}", value);
                        }


                //Stop Task1 
                await MakeOpcuaCall(channel, "StopPrinting", new[] {new Variant(1)});

                //Upload 
                var fi = new FileInfo(AppDomain.CurrentDomain.BaseDirectory + @"\NewMessage.next");
                await MakeOpcuaCall(channel, "StoreMessage",
                    new[] {new Variant(fi.Name), new Variant(File.ReadAllText(fi.FullName))});

                //Print
                await MakeOpcuaCall(channel, "PrintPrd", new[] {new Variant(fi.Name)});
            }

            await channel.CloseAsync();
        }
        catch (Exception ex)
        {
            await channel.AbortAsync();
            Console.WriteLine(ex.Message);
        }

        Console.ReadLine();
    }

    private static async Task<CallResponse> MakeOpcuaCall(UaTcpSessionChannel channel, string methodName,
        Variant[] inputParams)
    {
        CallResponse response = null;

        if (channel?.State == CommunicationState.Opened)
        {
            var request = new CallMethodRequest
            {
                MethodId = NodeId.Parse("ns=2;s=" + methodName),
                ObjectId = NodeId.Parse(ObjectIds.ObjectsFolder),
                InputArguments = inputParams
            };

            var callRequest = new CallRequest {MethodsToCall = new[] {request}};

            try
            {
                response = await channel.CallAsync(callRequest);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                channel.Fault(ex); // Cause the channel to reconnect when this happens
            }
        }
        else
        {
            Console.WriteLine("Not connected to printer gateway");
        }

        return response;
    }
}