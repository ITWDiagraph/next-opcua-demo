using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommandLine;
using Workstation.ServiceModel.Ua;
using Workstation.ServiceModel.Ua.Channels;

namespace IJ_OPC_Test;

public class Program
{
    private static async Task Main(string[] args)
    {
        //read commandline arguments
        var ip = "10.1.2.3";
        var opcCommand = "";
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(commandLine =>
            {
                if (string.IsNullOrEmpty(commandLine.IP))
                {
                    Console.WriteLine("Enter ip of the IJ printer (" + ip + ")");
                    var enteredIP = Console.ReadLine();
                    if (IsValidateIP(enteredIP)) ip = enteredIP;
                }
                else
                {
                    ip = commandLine.IP;
                    opcCommand = commandLine.OPCCommand;
                    if (!string.IsNullOrEmpty(opcCommand)) return;
                    ConsoleOut("--opc command not set. Valid items are:", true);
                    ConsoleOut("CancelPrinting (int)Task number");
                    ConsoleOut("CompareFile (string)FileName (string)md5Hash");
                    ConsoleOut("EnableLocalNotification (bool)Enable (int)Port");
                    ConsoleOut("GetConfiguration");
                    ConsoleOut("GetDiagnostics (int)Task number");
                    ConsoleOut("GetFileByName (string)FileName");
                    ConsoleOut("GetPrinterConfiguration (int)Task number");
                    ConsoleOut("GetStatusInformation (int)Task number");
                    ConsoleOut("GetStoredMessageList");
                    ConsoleOut("PrintPrd (string)PRD xml (int)Task number");
                    ConsoleOut("PrintPreview (string)prd xml data (int)Task number");
                    ConsoleOut("PrintPreviewCurrent (int)Task number");
                    ConsoleOut("PrintStoredMessage (string)The message name");
                    ConsoleOut("RecallMessage (string)The message name");
                    ConsoleOut("ResumePrinting (int)Task number");
                    ConsoleOut("SendFile (string)FileName (string)File");
                    ConsoleOut("SetConfiguration (string)Configuration");
                    ConsoleOut("SetNotificationURL (string)URL");
                    ConsoleOut("StopPrinting (int)Task number");
                    ConsoleOut("StoreMessage (string)The message name  (string)The message to store", true, true);
                    Environment.Exit(0);
                }
            });


        //define client
        var clientDescription = new ApplicationDescription
        {
            ApplicationName = "IJ.UaClient.Tests",
            ApplicationUri = $"urn:{Dns.GetHostName()}:IJ.UaClient.Tests",
            ApplicationType = ApplicationType.Client
        };

        var url = "opc.tcp://" + ip + ":16664";
        ConsoleOut("trying to connect to " + url);

        //create opc channel
        var channel = new UaTcpSessionChannel(
            clientDescription,
            null,
            new AnonymousIdentity(),
            url,
            SecurityPolicyUris.None);

        try
        {
            //open opc channel
            await channel.OpenAsync();
            if (channel.State == CommunicationState.Opened)
            {
                ConsoleOut("connected to " + url, true);

                if (!string.IsNullOrEmpty(opcCommand))
                {
                    //populate commandline arguments
                    var sp = opcCommand.Split(' ');
                    var variants = new List<Variant>();
                    foreach (var argument in sp.Skip(1))
                        if (int.TryParse(argument, out _))
                            variants.Add(Convert.ToInt32(argument));
                        else if (bool.TryParse(argument, out _))
                            variants.Add(Convert.ToBoolean(argument));
                        else
                            variants.Add(argument);

                    //make custom opc call
                    var response = await MakeOpcuaCall(channel, sp[0], variants.ToArray());
                    var serviceResult = response?.ResponseHeader?.ServiceResult;

                    if (response?.Results != null && serviceResult.HasValue && StatusCode.IsGood(serviceResult.Value))
                        if (serviceResult.Value != StatusCodes.GoodPostActionFailed)
                            if (response.Results.Length > 0 && response.Results[0]!.OutputArguments!.Length > 0)
                                if (response.Results[0]!.OutputArguments.Length > 1)
                                {
                                    var outputArgument = response.Results[0]!.OutputArguments[1];
                                    var output = outputArgument.Value;
                                    for (var i = 0; i < outputArgument.ArrayDimensions[0]; i++)
                                        ConsoleOut(((string[])output)[i]);
                                }
                }
                else
                {
                    ConsoleOut("press enter to read the status information", false, true);

                    //Read Status
                    var response = await MakeOpcuaCall(channel, "GetStatusInformation", new[] { new Variant(1) });
                    var serviceResult = response?.ResponseHeader?.ServiceResult;

                    if (response?.Results != null && serviceResult.HasValue && StatusCode.IsGood(serviceResult.Value))
                        if (serviceResult.Value != StatusCodes.GoodPostActionFailed)
                            if (response.Results.Length > 0 && response.Results[0]!.OutputArguments!.Length > 7)
                            {
                                ConsoleOut("Status: " + response.Results[0].OutputArguments[1].Value);
                                ConsoleOut("MessageName: " + response.Results[0].OutputArguments[2].Value);
                                ConsoleOut("LineSpeed: " + response.Results[0].OutputArguments[3].Value);
                                foreach (var value in
                                         (response.Results[0].OutputArguments[4].Value as IEnumerable<object>)!)
                                    ConsoleOut(value.ToString());
                                foreach (var value in
                                         (response.Results[0].OutputArguments[5].Value as IEnumerable<object>)!)
                                    ConsoleOut("Consumable: " + value);
                                foreach (var value in
                                         (response.Results[0].OutputArguments[6].Value as IEnumerable<object>)!)
                                    ConsoleOut("Error: " + value);
                                foreach (var value in
                                         (response.Results[0].OutputArguments[7].Value as IEnumerable<object>)!)
                                    ConsoleOut("Warning: " + value);
                            }

                    ConsoleOut();

                    //Stop Task1 
                    ConsoleOut("press enter to send 'StopPrinting' to printers task1 at " + ip, false, true);
                    await MakeOpcuaCall(channel, "StopPrinting", new[] { new Variant(1) });
                    ConsoleOut("StopPrinting executed", true);


                    //Upload 
                    ConsoleOut("press enter to send 'StoreMessage' to printer " + ip, false, true);
                    var fi = new FileInfo(AppDomain.CurrentDomain.BaseDirectory + @"\NewMessage.next");
                    await MakeOpcuaCall(channel, "StoreMessage",
                        new[] { new Variant(fi.Name), new Variant(File.ReadAllText(fi.FullName)) });
                    ConsoleOut("Message sent to printer " + ip, true);

                    //Print
                    ConsoleOut("press enter to print message " + fi.Name, false, true);
                    await MakeOpcuaCall(channel, "PrintPrd", new[] { new Variant(fi.Name) });
                }
            }

            //close opc channel
            await channel.CloseAsync();
        }
        catch (Exception ex)
        {
            await channel.AbortAsync();
            Console.WriteLine(ex.Message);
        }

        ConsoleOut();
        ConsoleOut("press enter to quit", false, true);
    }

    private static void ConsoleOut(string text = "", bool newLine = false, bool readLine = false)
    {
        Console.WriteLine(text);
        if (readLine)
            Console.ReadLine();
        if (newLine)
            Console.WriteLine();
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

            var callRequest = new CallRequest { MethodsToCall = new[] { request } };

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

    public static bool IsValidateIP(string ip)
    {
        var check = new Regex(
            @"^([1-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])(\.([0-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])){3}$");
        return !string.IsNullOrEmpty(ip) && check.IsMatch(ip, 0);
    }

    private class Options
    {
        [Option("ip", Required = false, HelpText = "IJ printer IP")]
        public string IP { get; set; }

        [Option("opc", Required = false, HelpText = "OPC Command with arguments")]
        public string OPCCommand { get; set; }
    }
}