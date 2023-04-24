# README

This is a C# console application for making custom OPC UA calls to Industrial Inkjet Ltd. printers. The application uses the Workstation.ServiceModel.Ua package to create a session channel with the OPC server of the printer and perform OPC UA method calls. 

## Getting Started

### Prerequisites

To use this application, you need the following:

* Visual Studio or any other .NET development environment
* .NET Core 3.1 or higher
* Access to an Industrial Diagraph Resmark Inkjet printer with OPC UA server enabled
* Basic knowledge of OPC UA method calls and C# programming

### Installation

1. Clone the repository.
2. Open the solution file in Visual Studio or any other .NET development environment.
3. Build the solution to restore the packages.

## Usage

You can run the application with the following command:

```
dotnet run -- -i <printer_ip_address> [-o <opc_command>]
```

If the `-i` parameter is not provided, the application will search for IJ printers and ask you to select one from the list or manually enter an IP address.

If the `-o` parameter is not provided, the application will read the status information of the printer.

If the `-o` parameter is provided, the application will make a custom OPC UA call to the printer with the specified OPC UA method and parameters.

### Available OPC UA methods

* CancelPrinting (int)Task number
* CompareFile (string)FileName (string)md5Hash
* EnableLocalNotification (bool)Enable (int)Port
* GetConfiguration
* GetDiagnostics (int)Task number
* GetFileByName (string)FileName
* GetPrinterConfiguration (int)Task number
* GetStatusInformation (int)Task number
* GetStoredMessageList
* PrintPrd (string)PRD xml (int)Task number
* PrintPreview (string)prd xml data (int)Task number
* PrintPreviewCurrent (int)Task number
* PrintStoredMessage (string)The message name
* RecallMessage (string)The message name
* ResumePrinting (int)Task number
* SendFile (string)FileName (string)File
* SetConfiguration (string)Configuration
* SetNotificationURL (string)URL
* StopPrinting (int)Task number
* StoreMessage (string)The message name (string)The message to store

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.