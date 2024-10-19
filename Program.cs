// // See https://aka.ms/new-console-template for more information
// Console.WriteLine("Hello, World!");
using QuercusSimulator;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using static QuercusSimulator.MessageBuilder;
using Serilog.Events;
using System.Collections.Concurrent;
namespace QuercusSimulator
{
    public static class LPRSimulator
    {

        public static int SimulatorMainPort = Convert.ToInt32(JsonConfigManager.GetValueForKey("CameraMainPort"));
        public static int SimulatorConfigPort = Convert.ToInt32(JsonConfigManager.GetValueForKey("CameraConfigPort"));
        public static string ZRIP = JsonConfigManager.GetValueForKey("ServerIP");
        //public static string RealCamIP = JsonConfigManager.GetValueForKey("RealCamIP");
        //public static int RealCamMainPort = Convert.ToInt32(JsonConfigManager.GetValueForKey("RealCamMainPort"));
        public static int ZRMainPort = Convert.ToInt32(JsonConfigManager.GetValueForKey("ServerMainPort"));
        public static int ZRConfigPort = Convert.ToInt32(JsonConfigManager.GetValueForKey("ServerConfigPort"));

        public static string MinLogLevel = JsonConfigManager.GetValueForKey("MinLogLevel");
        //public static string LogFilePath = JsonConfigManager.GetValueForKey("LogFilePath");
        private static readonly string LogFilePath = "C:\\LPR\\EventImages\\Logs\\"; // Hardcoded JSON file path
                                                                                     //private const string OutputDirectory = @"D:\LPR\EventImages";
        public static string OutputDirectory = JsonConfigManager.GetValueForKey("OutputDirectory");
        //public static uint UnitId = Convert.ToUInt32(JsonConfigManager.GetValueForKey("UnitId"));
        //public static string TriggerText = JsonConfigManager.GetValueForKey("TriggerText");
        // Use ConcurrentDictionary for thread-safe operations


        static uint lastid = 2;
        static uint lastcarId = 1;
        static uint id = 2;
        static uint carId = 1;
        //private const int CameraMainPort = 7051; // First port to listen on
        //private const int CameraConfigPort = 7041; // Second port to listen on
        //private const string ServerIP = "10.0.0.10";
        //private const string RealCamIP = "10.0.0.110";
        //private const int RealCamMainPort = 6051; // Second port to listen on
        //private const int ServerMainPort = 7050;    // Default port for sending responses
        //private const int ServerConfigPort = 7040; // Port for sending ping responses
        //static uint lastid = 1;
        //static uint lastcarId = 1;
        //static uint id = 1;
        //static uint carId = 1;
        static async Task Main(string[] args)
        {
            ConfigureLogging(); // Call the method to set up Serilog

            Log.Information("LPR Simulator starting...");
            //Log.CloseAndFlush(); // Ensure logs are flushed before application exit

            // Start the camera simulators for both ports
            Task listenOnPort1 = RunCameraSimulatorAsync(SimulatorMainPort);
            Task listenOnPort2 = RunCameraSimulatorAsync(SimulatorConfigPort);

            // Wait for both tasks to complete (they run indefinitely)
            await Task.WhenAll(listenOnPort1, listenOnPort2);

        }
        private static async Task RunCameraSimulatorAsync(int port)
        {
            using (var udpClient = new UdpClient(port))
            {
                Log.Information($"Camera simulator listening on port {port}");

                while (true)
                {
                    try
                    {
                        var (message, remoteEndPoint) = await UdpLargeMessageHandler.ReceiveLargeMessageAsync(udpClient);

                        if (message == null || message.Length < 19)
                        {
                            Log.Information("Invalid or too short UDP message. Ignoring.");
                            continue;
                        }

                        // Process each message in a separate task to handle concurrency
                        _ = Task.Run(async () =>
                        {
                            await ProcessCameraMessageAsync(udpClient, message);
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error in camera simulator: {ex.Message}");
                    }
                }
            }
        }

        // Method to configure Serilog
        private static void ConfigureLogging()
        {
            // Set up Serilog to log to the file specified in LogFilePath
            //Log.Logger = new LoggerConfiguration()
            //    .WriteTo.File($"{LogFilePath}simulatorlog.txt", // Save logs to logfile.txt in the specified folder
            //                  rollingInterval: RollingInterval.Day, // Creates a new log file daily
            //                  retainedFileCountLimit: 7, // Retain last 7 log files
            //                  rollOnFileSizeLimit: true)
            //    .CreateLogger();

            // Determine the minimum log level
            LogEventLevel minLogLevel;
            if (!Enum.TryParse(MinLogLevel, true, out minLogLevel))
            {
                minLogLevel = LogEventLevel.Information; // Default to Information if parsing fails
            }

            // Set up Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(minLogLevel)
                .WriteTo.File(Path.Combine(LogFilePath, "simulatorlog_.txt"),
                              rollingInterval: RollingInterval.Day,
                              retainedFileCountLimit: 7,
                              rollOnFileSizeLimit: true)
                .CreateLogger();
        }

        private static async Task ProcessCameraMessageAsync(UdpClient udpClient, byte[] message)
        {
            if (message.Length < 19)
            {
                Log.Information($"Received message is too short (length: {message.Length})");
                return;
            }
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse(ZRIP), ZRMainPort);
            //IPEndPoint RealCameraEndPoint = new IPEndPoint(IPAddress.Parse(RealCamIP), RealCamMainPort);

            // Read message type and adjust for endianness
            ushort messageType = BitConverter.ToUInt16(message, 9);
            if (BitConverter.IsLittleEndian)
            {
                messageType = (ushort)((messageType << 8) | (messageType >> 8));
            }

            byte[]? response = null;
            switch (messageType)
            {
                case 0x4400: // Status Request
                    response = CreateStatusResponse(message);
                    Log.Debug($">>>>Zr sent Status Request");
                    // Forward the original request to the corresponding unit
                    await ForwardStatusRequest(message);

                    break;
                case 0x4300: // Trigger Request
                             //await ResendTriggerRequest(message);
                    Log.Information($"############################################################");
                    Log.Information($">>>>Zr sent Trigger : {DateTime.Now.ToString("HH:mm:ss.fff")}");
                    
                    response = CreateTriggerResponse(message);
                    break;
                case 0x4700: // LPN Image Request
                    Log.Information($">>>>Zr sent Image Request");
                    response = CreateLPNImageResponse(message);
                    break;
                case 0x6000: // Ping
                    Log.Information($">>>>Zr sent Ping");
                    response = CreatePingResponse(message);
                    // Ping response must be sent to 10.0.0.10:7040
                    remoteEndPoint = new IPEndPoint(IPAddress.Parse(ZRIP), ZRConfigPort);
                    break;
                case 0xC000: // Ack
                    Log.Information($">>>>Zr sent ACK Message");
                    break;
                case 0xC100: // NAK
                    Log.Information($">>>>Zr sent NAK Message");
                    break;
                default:
                    Log.Information($">>>>ZR sent Message of unsupported type: 0x{messageType:X4}");
                    return;
            }

            if (response != null)
            {

                await udpClient.SendAsync(response, response.Length, remoteEndPoint);
                //LogMessage("Camera", "Server", response);
            }
            else
            {
                Log.Warning($"No need for Response / Response returned by MessageBuilder is null");

            }
            if (messageType == 0x4300) // Trigger Request
            {
                Log.Information($"#############  Trigger Was acknowledged: {DateTime.Now.ToString("HH:mm:ss.fff")}");
                // Start timing for image processing
                var imageProcessingStopwatch = System.Diagnostics.Stopwatch.StartNew();

                //uint Id = 13;
                //uint TriggerId = 11;
                uint unitId = BitConverter.ToUInt32(message, 1);
                uint Id = BitConverter.ToUInt32(message, 13);
                uint TriggerId = BitConverter.ToUInt32(message, 17);

                // Initialize cameraEndPoint with default values
                // IPEndPoint cameraEndPoint = null;

                // Get RealCam IP and Port based on UnitId
                (string realCamIP, int realCamMainPort, int ReceivePort, int[] ExposureTimes) = JsonConfigManager.GetCameraInfoByUnitId(unitId);
                   Log.Information($"Camera IP: {realCamIP}, Port: {realCamMainPort}");
                // Send status request before getting and saving images
                //byte[] statusRequest = SendStatusRequest(unitId, Id);
                //await SendRequestAsync(statusRequest, realCamIP, cameraSendPort);

                //if (realCamIP != null && realCamMainPort != 0)
                //{
                //    cameraEndPoint = new IPEndPoint(IPAddress.Parse(realCamIP), realCamMainPort);
                //    Log.Information($"Camera IP: {realCamIP}, Port: {realCamMainPort}");
                //    await SendTriggerRequestAsync(cameraEndPoint, unitId, Id, TriggerId);
                //}
                //else
                //{
                //    Log.Error($"Invalid camera configuration for UnitId {unitId}");
                //}


                //IPEndPoint cameraEndPoint = new IPEndPoint(IPAddress.Parse(RealCamIP), RealCamMainPort);

                //LPNResult LastLPNResult = await QuercusSimulator.LPRService.CaptureLPNAsync("10.0.0.111");
                //int cameraReceivePort = 6050;
                //string OutputDirectory = @"C:\LPR\EventImages";
                //int[] exposureTimes = { 4000, 16000 };
                //int[] ids = { 100, 200 };

                bool imageWasSaved = await CurrentFrame.GetAndSaveImages(unitId, realCamIP, OutputDirectory);
                   Log.Information($"imageWasSaved: {imageWasSaved.ToString()}");

                imageProcessingStopwatch.Stop();
                long imageProcessingTime = imageProcessingStopwatch.ElapsedMilliseconds;
                var lpnCaptureStopwatch = System.Diagnostics.Stopwatch.StartNew();
                LPNResult? LastLPNResult = null;
                if (imageWasSaved)
                {
                    LastLPNResult = await QuercusSimulator.LPRService.CaptureLPNAsync(realCamIP);

                    Console.WriteLine("An image was successfully saved.");
                }
                else
                {
                    Console.WriteLine("No image was saved.");
                }

                if (LastLPNResult != null && LastLPNResult.ArabicLPN != null)
                    {
                        string newDetectedChars = LastLPNResult.ArabicLPN;

                        id = lastid + 2;
                        carId = lastcarId + 1;
                        lastid = id;
                        lastcarId = carId;
                        // Create the license plate info message with the extracted Unit ID and Trigger ID
                        byte[] licensePlateInfoMessage = LPInfoMessage.CreateLicensePlateInfoMessage(unitId, id, carId, TriggerId, newDetectedChars);
                        await udpClient.SendAsync(licensePlateInfoMessage, licensePlateInfoMessage.Length, remoteEndPoint);
                        Log.Information($"############# Sent LPN to ZR: {DateTime.Now.ToString("HH:mm:ss.fff")}");
                        lpnCaptureStopwatch.Stop();
                        long lpnCaptureTime = lpnCaptureStopwatch.ElapsedMilliseconds;
                    // Calculate total time
                    long totalTime = imageProcessingTime + lpnCaptureTime;

                    // Log the timings
                    Log.Information($"Time for best image capture: {imageProcessingTime} ms");
                    Log.Information($"Time for LPN Recognition & Sending LPN: {lpnCaptureTime} ms");
                    Log.Information($"############### Total processing time: {totalTime} ms ###############");

                }
                else
                    {
                        Log.Information($"LastLPNResult is null");


                    }


                }
            }
        private static async Task SendRequestAsync(byte[] request, string ip, int port)
        {
            using (var client = new UdpClient())
            {
                try
                {
                    await client.SendAsync(request, request.Length, ip, port);
                    Log.Information($"Status request sent to {ip}:{port}");
                }
                catch (Exception ex)
                {
                    Log.Error($"Error sending status request to {ip}:{port}: {ex.Message}");
                }
            }
        }
    }

    }

