﻿// using SixLabors.ImageSharp.PixelFormats;
// using SixLabors.ImageSharp;
// using System.Diagnostics;
// using System.Net.Sockets;
// using System.Net;
// using static QuercusSimulator.MessageBuilder;
// using Serilog;
// using QuercusSimulator;
// public static class CurrentFrame
// {
//     private const int MaxUdpSize = 65507;
//     public static string LPRServerIP = JsonConfigManager.GetValueForKey("LPRServerIP");

//     public static int SingleAttemptTimeout = Convert.ToInt32(JsonConfigManager.GetValueForKey("SingleAttemptTimeout"));
//     public static int OverallTimeout = Convert.ToInt32(JsonConfigManager.GetValueForKey("OverallTimeout"));
//     public static int MaxRetries = Convert.ToInt32(JsonConfigManager.GetValueForKey("MaxRetries"));
//     public static int RealCameraPort = Convert.ToInt32(JsonConfigManager.GetValueForKey("RealCameraPort"));
//     public static int ExposureTimesUpdated = Convert.ToInt32(JsonConfigManager.GetValueForKey("ExposureTimesUpdated"));

//     private static Dictionary<string, int[]> cameraExposureTimes = new Dictionary<string, int[]>();
//     private static Dictionary<string, int> cameraReceivePorts = new Dictionary<string, int>();

//     static int[] ids;
//     static double LowBrightness;
//     static double HighBrightness;

//     static CurrentFrame()
//     {
//         // Initialize the configurations
//         InitializeConfigurations();
//     }
//     // Before: Single array for all cameras
//     // static int[] exposureTimes = { 4000, 16000 };

//     // After: Separate arrays for each camera
//     //private static Dictionary<string, int[]> cameraExposureTimes = new Dictionary<string, int[]>
//     //{
//     //    { "10.0.0.110", new int[] { 4096, 2048, 16384, 32768 } },
//     //    { "10.0.0.181", new int[] { 4096, 2048, 16384, 32768 } },
//     //    { "10.0.0.182", new int[] { 4096, 2048, 16384, 32768 } }
//     //};

//     //512,2048,4096,16384,32768
//     // New: Dictionary to store receive ports for each camera
//     //private static Dictionary<string, int> cameraReceivePorts = new Dictionary<string, int>
//     //{
//     //    { "10.0.0.110", 6101 },
//     //    { "10.0.0.181", 6181 },
//     //    { "10.0.0.182", 6182 }
//     //};

//     //static int[] ids = { 100, 200 ,300, 400};
//     //static double LowBrightness = 0.1;
//     //static double HighBrightness = 0.6;
//     public static async Task<bool> GetAndSaveImages(
//     uint cameraId,
//     string cameraIP,
//     string outputDirectory)
//     {
//         Log.Information("LPR Camera Image Capture and Brightness Calculation starting...");
//         Stopwatch stopwatch = new Stopwatch();
//         stopwatch.Start();
//         bool imageSaved = false;

//         try
//         {
//             using (var overallCts = new CancellationTokenSource(OverallTimeout))
//             using (UdpClient udpClient = new UdpClient())
//             {
//                 udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
//                 IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse(cameraIP), RealCameraPort);
//                 IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, cameraReceivePorts[cameraIP]);
//                 udpClient.Client.Bind(localEndPoint);
//                 Directory.CreateDirectory(outputDirectory);

//                 List<(byte[] imageData, double brightness, int exposureTime)> images = new List<(byte[], double, int)>();

//                 int[] currentExposureTimes = cameraExposureTimes[cameraIP];
//                 bool bestImageFound = false;

//                 for (int i = 0; i < currentExposureTimes.Length && !bestImageFound; i++)
//                 {
//                     bool success = false;
//                     for (int retry = 0; retry <= MaxRetries && !success; retry++)
//                     {
//                         if (overallCts.IsCancellationRequested)
//                         {
//                             Log.Warning("Overall timeout reached. Stopping image capture.");
//                             break;
//                         }

//                         try
//                         {
//                             DateTime currentTime = DateTime.Now;
//                             string timestamp = currentTime.ToString("yyyyMMdd_HHmmss");
//                             byte[] request = CreateCurrentFrameRequest(currentExposureTimes[i], ids[i] + (retry * 2), cameraId);

//                             await udpClient.SendAsync(request, request.Length, remoteEndPoint);
//                             Log.Information($"Request sent to camera for image {i + 1}, attempt {retry + 1}");

//                             using (var attemptCts = new CancellationTokenSource(SingleAttemptTimeout))
//                             using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(attemptCts.Token, overallCts.Token))
//                             {
//                                 byte[] imageData = await ReceiveCurrentFrameResponseAsyncWithTimeout(udpClient, linkedCts.Token);
//                                 Log.Information($"Received {imageData.Length} bytes of image data for image {i + 1}");

//                                 double brightness = await CalculateImageBrightness(imageData);
//                                 images.Add((imageData, brightness, currentExposureTimes[i]));
//                                 Log.Information($"Image {i + 1} processed - Exposure Time: {currentExposureTimes[i]}ms, Brightness: {brightness:F2}");

//                                 success = true;

//                                 // Check if this image meets the best image criteria
//                                 if (brightness > LowBrightness && brightness < HighBrightness)
//                                 {
//                                     bestImageFound = true;
//                                     // Move this exposure time to the start of the array
//                                     if (ExposureTimesUpdated == 1)
//                                     {
//                                         int bestExposureTime = currentExposureTimes[i];
//                                         Array.Copy(currentExposureTimes, 0, currentExposureTimes, 1, i);
//                                         currentExposureTimes[0] = bestExposureTime;
//                                         cameraExposureTimes[cameraIP] = currentExposureTimes;
//                                     }

//                                     Log.Information($"Best image found. Updated exposure times for camera {cameraIP}: [{string.Join(", ", currentExposureTimes)}]");
//                                     break;
//                                 }
//                             }
//                         }
//                         catch (Exception ex)
//                         {
//                             if (ex is OperationCanceledException || overallCts.IsCancellationRequested)
//                             {
//                                 Log.Warning($"Operation timed out for image {i + 1}, attempt {retry + 1}");
//                                 if (retry < MaxRetries)
//                                 {
//                                     Log.Information($"Retrying image {i + 1}, attempt {retry + 2}");
//                                     continue;
//                                 }
//                                 else
//                                 {
//                                     Log.Error($"Failed to capture image {i + 1} after {MaxRetries + 1} attempts");
//                                     break;
//                                 }
//                             }

//                             Log.Warning($"Error occurred for image {i + 1}, attempt {retry + 1}. Error: {ex.Message}");

//                             if (retry == MaxRetries)
//                             {
//                                 Log.Error($"Failed to capture image {i + 1} after {MaxRetries + 1} attempts");
//                                 break;
//                             }
//                         }
//                     }

//                         if (overallCts.IsCancellationRequested)
//                     {
//                         Log.Warning("Overall timeout reached. Stopping image capture.");
//                         break;
//                     }
//                 }

//                 if (images.Count > 0)
//                 {
//                     Log.Information("Summary of all captured images:");
//                     foreach (var (_, brightness, exposureTime) in images)
//                     {
//                         Log.Information($"Exposure Time: {exposureTime}ms, Brightness: {brightness:F2}");
//                     }

//                     var (bestImageData, bestExposureTime) = GetBestImage(images);
//                     string outputPath = await SaveImage(bestImageData, DateTime.Now.ToString("yyyyMMdd_HHmmss"), bestExposureTime, cameraId, cameraIP, outputDirectory);
//                     Log.Information($"Best image saved: {outputPath}");

//                     // Find the brightness of the best image
//                     var bestImageInfo = images.First(img => img.exposureTime == bestExposureTime);
//                     Log.Information($"Best image details - Exposure Time: {bestExposureTime}ms, Brightness: {bestImageInfo.brightness:F2}");
//                     imageSaved = true;

//                 }
//                 else
//                 {
//                     Log.Error("No images were successfully captured");
//                 }
//             }
//         }
//         catch (OperationCanceledException)
//         {
//             Log.Error("The overall operation timed out");
//         }
//         catch (Exception ex)
//         {
//             Log.Error($"An unexpected error occurred: {ex.Message}");
//             Log.Error($"Stack trace: {ex.StackTrace}");
//         }
//         finally
//         {
//             stopwatch.Stop();
//             Log.Information($"Total time taken: {stopwatch.Elapsed}");
//         }
//         return imageSaved;

//     }

//     private static async Task<byte[]> ReceiveCurrentFrameResponseAsyncWithTimeout(UdpClient udpClient, CancellationToken cancellationToken)
//     {
//         try
//         {
//             var result = await udpClient.ReceiveAsync(cancellationToken);
//             byte[] response = result.Buffer;

//             if (response.Length < 21)
//                 throw new Exception("Incomplete response received");
//             if (response[0] != 0x02)
//                 throw new Exception("Invalid STX in response");

//             int totalSize = BitConverter.ToInt32(response, 5);
//             ushort messageType = BitConverter.ToUInt16(response, 9);
//             int imageSize = BitConverter.ToInt32(response, 17);

//             if (messageType != 136)
//                 throw new Exception($"Unexpected message type: {messageType}");
//             if (imageSize <= 0 || imageSize > MaxUdpSize)
//                 throw new Exception("Invalid or corrupted image size");
//             if (response.Length < totalSize)
//                 throw new Exception("Incomplete message received");

//             byte[] imageData = new byte[imageSize];
//             Buffer.BlockCopy(response, 21, imageData, 0, imageSize);
//             return imageData;
//         }
//         catch (OperationCanceledException)
//         {
//             Log.Warning("Receive operation timed out");
//             throw;
//         }
//     }

//     private static (byte[] imageData, int exposureTime) GetBestImage(List<(byte[] imageData, double brightness, int exposureTime)> images)
//     {
//         const double targetBrightness = 0.35;
//         var bestImage = images.OrderBy(img => Math.Abs(img.brightness - targetBrightness)).First();
//         return (bestImage.imageData, bestImage.exposureTime);
//     }


//     private static async Task<string> SaveImage(byte[] imageData, string timestamp, int exposureTime, uint UnitId, string CameraIP, string OutputDirectory)
//     {
//         using (MemoryStream ms = new MemoryStream(imageData))
//         using (Image<Rgba32> image = Image.Load<Rgba32>(ms))
//         {
//             string lastOctet = CameraIP.Split('.').Last();
//             string fileName = $"lastimage_{lastOctet}.jpg";
//             string outputPath = Path.Combine(OutputDirectory, fileName);
//             await image.SaveAsJpegAsync(outputPath);
//             return outputPath;
//         }
//     }

//     private static async Task<double> CalculateImageBrightness(byte[] imageData)
//     {
//         using (MemoryStream ms = new MemoryStream(imageData))
//         using (Image<Rgba32> image = Image.Load<Rgba32>(ms))
//         {
//             double totalBrightness = 0;
//             int pixelCount = image.Width * image.Height;

//             image.ProcessPixelRows(accessor =>
//             {
//                 for (int y = 0; y < accessor.Height; y++)
//                 {
//                     Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
//                     for (int x = 0; x < pixelRow.Length; x++)
//                     {
//                         ref Rgba32 pixel = ref pixelRow[x];
//                         double pixelBrightness = (0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B) / 255.0;
//                         totalBrightness += pixelBrightness;
//                     }
//                 }
//             });

//             return totalBrightness / pixelCount;
//         }

//     }
//     private static void InitializeConfigurations()
//     {
//         // Get camera configurations
//         for (uint i = 1; i <= 3; i++)
//         {
//             var (ip, _, receivePort, exposureTimes) = JsonConfigManager.GetCameraInfoByUnitId(i);
//             if (ip != null)
//             {
//                 cameraExposureTimes[ip] = exposureTimes;
//                 cameraReceivePorts[ip] = receivePort;
//             }
//         }
//         ids = JsonConfigManager.GetIDs();
//         LowBrightness = JsonConfigManager.GetLowBrightness();
//         HighBrightness = JsonConfigManager.GetHighBrightness();

//     }

// }