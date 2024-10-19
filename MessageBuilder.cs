using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace QuercusSimulator
{
    public static class MessageBuilder
    {
        public static string ImagePath = JsonConfigManager.GetValueForKey("ImagePath");
        public static int ImageWidth = Convert.ToInt32(JsonConfigManager.GetValueForKey("ImageWidth"));
        public static int ImageHeight = Convert.ToInt32(JsonConfigManager.GetValueForKey("ImageHeight"));
        private const int MaxUdpSize = 65507;

        public static byte[] CreateStatusResponse(byte[] request)
        {
            // The response should be exactly 26 bytes (25 bytes + ETX)
            byte[] response = new byte[25];

            // STX (Start of Text)
            response[0] = 0x02;

            // Unit ID (4 bytes) - assuming it's the same as the request
            Buffer.BlockCopy(request, 1, response, 1, 4);

            // Size (4 bytes) - Total size of 25 bytes (0x19000000 in little-endian format)
            response[5] = 0x19; // Set message size to 25 bytes
            response[6] = 0x00;
            response[7] = 0x00;
            response[8] = 0x00;

            // Type (2 bytes) - Status Response (0x8400)
            response[9] = 0x84;
            response[10] = 0x00;

            // Version (2 bytes) - Version (0x0100)
            //response[11] = 0x01;
            //response[12] = 0x00;
            Buffer.BlockCopy(request, 11, response, 11, 2);

            // ID (4 bytes) - same as request
            Buffer.BlockCopy(request, 13, response, 13, 4);

            // Message Data (6 bytes) - Status data
            response[17] = 0x01; // Active
            response[18] = 0x01; // I/O Card Status
            response[19] = 0x01; // Camera Status
            response[20] = 0x01; // Additional status
            response[21] = 0x00; // Reserved
            response[22] = 0x00; // Reserved

            // BCC (Block Check Character) - XOR from STX to the last byte of Message Data
            response[23] = CalculateXOR(response, 0, 23);

            // ETX (End of Text)
            response[24] = 0x03;

            // Log the response message for verification
            Log.Debug("Raw Status Response (hex): " + BitConverter.ToString(response).Replace("-", ""));

            return response;
        }

        public static async Task ForwardStatusRequest(byte[] incomingRequest)
        {
            // Extract the Unit ID from the incoming request
            uint unitId = BitConverter.ToUInt32(incomingRequest, 1);

            // Get the unit's IP and port based on the Unit ID
            //(string unitIp, int unitPort) = JsonConfigManager.GetCameraInfoByUnitId(unitId);
            (string unitIp, int unitPort, int ReceivePort, int[] ExposureTimes) = JsonConfigManager.GetCameraInfoByUnitId(unitId);
            if (string.IsNullOrEmpty(unitIp) || unitPort == 0)
            {
                Log.Error($"Unable to find IP and port for Unit ID: {unitId}");
                return;
            }

            // Forward the request to the unit
            using (var client = new UdpClient())
            {
                try
                {
                    await client.SendAsync(incomingRequest, incomingRequest.Length, unitIp, unitPort);
                    Log.Debug($"Forwarded Status Request to unit at {unitIp}:{unitPort}");
                }
                catch (Exception ex)
                {
                    Log.Error($"Error forwarding status request to unit {unitId}: {ex.Message}");
                }
            }
        }
        public static byte[] SendStatusRequest(uint unitId, uint messageId)
        {
            // The request should be exactly 19 bytes (18 bytes + ETX)
            byte[] request = new byte[19];

            // STX (Start of Text)
            request[0] = 0x02;

            // Unit ID (4 bytes)
            Buffer.BlockCopy(BitConverter.GetBytes(unitId), 0, request, 1, 4);

            // Size (4 bytes) - Total size of 19 bytes (0x13000000 in little-endian format)
            request[5] = 0x13;
            request[6] = 0x00;
            request[7] = 0x00;
            request[8] = 0x00;

            // Type (2 bytes) - Status Request (0x4400)
            request[9] = 0x44;
            request[10] = 0x00;

            // Version (2 bytes) - Version (0x0100)
            request[11] = 0x01;
            request[12] = 0x00;

            // ID (4 bytes)
            Buffer.BlockCopy(BitConverter.GetBytes(messageId), 0, request, 13, 4);

            // BCC (Block Check Character) - XOR from STX to the last byte before BCC
            request[17] = CalculateXOR(request, 0, 17);

            // ETX (End of Text)
            request[18] = 0x03;

            // Log the request message for verification
            Log.Debug("Sent Status Request (hex): " + BitConverter.ToString(request).Replace("-", ""));

            return request;
        }
        public static byte[] CreateTriggerResponse(byte[] request)
        {

            // The response should be exactly 19 bytes
            byte[] response = new byte[19];

            // STX (Start of Text)
            response[0] = 0x02;

            // Unit ID (4 bytes) - Copy from request
            Buffer.BlockCopy(request, 1, response, 1, 4);

            // Size (4 bytes) - Fixed size of 19 bytes (0x13000000 in little-endian format)
            response[5] = 0x13; // 19 bytes
            response[6] = 0x00;
            response[7] = 0x00;
            response[8] = 0x00;

            // Type (2 bytes) - ACK type (0xC000)
            response[9] = 0xC0;
            response[10] = 0x00;

            // Version (2 bytes) - Same as request
            Buffer.BlockCopy(request, 11, response, 11, 2);

            // ID (4 bytes) - Same as request
            Buffer.BlockCopy(request, 13, response, 13, 4);

            // BCC (Block Check Character) - XOR from STX to the last byte before BCC
            response[17] = CalculateXOR(response, 0, 17);

            // ETX (End of Text)
            response[18] = 0x03;

            // Log the response message for verification
            Log.Information("Raw Trigger Ack Message (hex): " + BitConverter.ToString(response).Replace("-", ""));
            Log.Information($"Trigger Ack Time: {DateTime.Now}");

            return response;
        }
        public static byte[] CreateLPNImageResponse(byte[] request)
        {
            Log.Information($"############# Creating Image Response: {DateTime.Now.ToString("HH:mm:ss.fff")}");

            //string imagePath = @"D:\lp.jpg";
            //string imagePath = @"C:\EventImages\lastimage.jpg";

            // Extract the Unit ID (assuming it's at byte index 1)
            uint unitId = BitConverter.ToUInt32(request, 1);

            // Get RealCam IP and Port based on UnitId
            //(string realCamIP, int realCamMainPort) = JsonConfigManager.GetCameraInfoByUnitId(unitId);
            (string realCamIP, int realCamMainPort, int ReceivePort, int[] ExposureTimes) = JsonConfigManager.GetCameraInfoByUnitId(unitId);
            // Extract the last octet of the IP address
            string lastOctet = realCamIP.Split('.').Last();

            // Construct the image file path using the last octet
            //string imageFilePath = $"{ImagePath}_{lastOctet}.jpg";
            // string imageFilePath = Path.Combine(ImagePath, $"lastimage_{lastOctet}.jpg");
            // Get all files matching the pattern and select the most recent one
            string imageFilePath = Directory.GetFiles(ImagePath, $"lastimage_{lastOctet}*")
                                        .OrderByDescending(f => File.GetLastWriteTime(f))
                                        .FirstOrDefault();

            if (string.IsNullOrEmpty(imageFilePath))
            {
                throw new FileNotFoundException($"No image file found for camera {lastOctet}");
            }
            //string imageFilePath = $"{ImagePath}{realCamIP}.jpg";

            byte[] imageData = File.ReadAllBytes(imageFilePath);
            int imageSize = imageData.Length;


            //int imageWidth = 1280;
            //int imageHeight = 600;

            // Define center coordinates
            int centerX = ImageWidth / 2; // 640
            int centerY = ImageHeight / 2; // 300

            // Define ROI size (50% of the image size)
            int roiWidth = (int)(ImageWidth * 0.5); // 640
            int roiHeight = (int)(ImageHeight * 0.5); // 300

            // Calculate ROI coordinates
            ushort roiLeft = (ushort)(centerX - (roiWidth / 2)); // 320
            ushort roiRight = (ushort)(centerX + (roiWidth / 2)); // 960
            ushort roiTop = (ushort)(centerY - (roiHeight / 2)); // 150
            ushort roiBottom = (ushort)(centerY + (roiHeight / 2)); // 450

            //// Define ROI coordinates (example values)
            //ushort roiTop = 100;
            //ushort roiLeft = 200;
            //ushort roiBottom = 150;
            //ushort roiRight = 300;

            // Calculate total response size
            int totalSize = 1 + 4 + 4 + 2 + 2 + 4 + 8 + 4 + imageSize + 1 + 1;
            byte[] response = new byte[totalSize];

            // STX (Start of Text)
            response[0] = 0x02;

            // Unit ID (4 bytes) - Copy from request
            Buffer.BlockCopy(request, 1, response, 1, 4);

            // Size (4 bytes) - Total size of the message
            BitConverter.GetBytes((uint)totalSize).CopyTo(response, 5);

            // Type (2 bytes) - LPN Image Response (0x8700)
            response[9] = 0x87;
            response[10] = 0x00;

            // Version (2 bytes) - Copy from request
            Buffer.BlockCopy(request, 11, response, 11, 2);

            // ID (4 bytes) - Copy from request
            Buffer.BlockCopy(request, 13, response, 13, 4);

            // ROI coordinates (8 bytes)
            BitConverter.GetBytes(roiTop).CopyTo(response, 17);
            BitConverter.GetBytes(roiLeft).CopyTo(response, 19);
            BitConverter.GetBytes(roiBottom).CopyTo(response, 21);
            BitConverter.GetBytes(roiRight).CopyTo(response, 23);

            // Image Size (4 bytes)
            BitConverter.GetBytes((uint)imageSize).CopyTo(response, 25);

            // Image Data (variable)
            Buffer.BlockCopy(imageData, 0, response, 29, imageSize);

            // BCC (Block Check Character) - XOR from STX to the last byte before BCC
            response[totalSize - 2] = CalculateXOR(response, 0, totalSize - 2);

            // ETX (End of Text)
            response[totalSize - 1] = 0x03;
            Log.Information($"############# Sending Image to ZR: {DateTime.Now.ToString("HH:mm:ss.fff")}");

            return response;
        }
        public static byte[] CreatePingResponse(byte[] request)
        {
            // The response should be exactly 19 bytes (size for ACK response)
            byte[] response = new byte[19];

            // STX (Start of Text)
            response[0] = 0x02;

            // Unit ID (4 bytes) - Copy from the request
            Buffer.BlockCopy(request, 1, response, 1, 4);

            // Size (4 bytes) - Fixed size of 19 bytes
            response[5] = 0x13; // 19 bytes
            response[6] = 0x00;
            response[7] = 0x00;
            response[8] = 0x00;

            // Type (2 bytes) - ACK type (0xC000)
            response[9] = 0xC0;
            response[10] = 0x00;

            // Version (2 bytes) - Same as request
            Buffer.BlockCopy(request, 11, response, 11, 2);

            // ID (4 bytes) - Same as request
            Buffer.BlockCopy(request, 13, response, 13, 4);

            // BCC (Block Check Character) - XOR from STX to the last byte before BCC
            response[17] = CalculateXOR(response, 0, 17);

            // ETX (End of Text)
            response[18] = 0x03;

            // Log the response message for verification
            Log.Information("Raw Ping Response (hex): " + BitConverter.ToString(response).Replace("-", ""));

            return response;
        }

        public static async Task SendTriggerRequestAsync(IPEndPoint remoteEndPoint, uint unitId, uint Id, uint triggerId)
        {
            using (var udpClient = new UdpClient())
            {
                byte[] request = new byte[23];
                request[0] = 0x02; // STX

                // UnitId (4 bytes, little endian)
                BitConverter.GetBytes(unitId).CopyTo(request, 1);

                // Size (4 bytes, little endian, always 23)
                BitConverter.GetBytes(23).CopyTo(request, 5);

                // Type (2 bytes, little endian, always 0x4300)
                request[9] = 0x43;
                request[10] = 0x00;

                // Version (2 bytes, little endian, make it 0)
                BitConverter.GetBytes((ushort)0).CopyTo(request, 11);

                // ID (4 bytes, little endian, parameter)
                BitConverter.GetBytes(Id).CopyTo(request, 13);

                // Trigger ID (4 bytes, little endian, parameter)
                BitConverter.GetBytes(triggerId).CopyTo(request, 17);

                // Calculate BCC (XOR from STX to the last data byte before ETX)
                request[21] = CalculateXOR(request, 0, 21);

                request[22] = 0x03; // ETX

                await udpClient.SendAsync(request, request.Length, remoteEndPoint);
                Log.Information($"Trigger request Forwarded (hex): {BitConverter.ToString(request).Replace("-", "")}");
            }
        }
        public static async Task SendLPNImageRequestAsync(UdpClient udpClient, IPEndPoint remoteEndPoint, uint carId)
        {
            byte[] request = new byte[19];

            // STX (1 byte)
            request[0] = 0x02;

            // Unit ID (4 bytes) - Use a hardcoded or predefined Unit ID
            BitConverter.GetBytes(1).CopyTo(request, 1);

            // Size (4 bytes) - Fixed size of 19 bytes for LPN image request
            BitConverter.GetBytes(19).CopyTo(request, 5);

            // Type (2 bytes) - LPN Image Request (0x4700 in little-endian)
            request[9] = 0x47;
            request[10] = 0x00;

            // Version (2 bytes) - Use version 1.0 for example
            BitConverter.GetBytes(1).CopyTo(request, 11);

            // ID (4 bytes) - Set an ID (unique for each conversation)
            BitConverter.GetBytes(carId).CopyTo(request, 13);

            // BCC (Block Check Character) - XOR from STX to the last byte before BCC
            request[17] = CalculateXOR(request, 0, 17);

            // ETX (1 byte) - End of message
            request[18] = 0x03;

            // Send the request
            await udpClient.SendAsync(request, request.Length, remoteEndPoint);
            LogMessage("LPN Image Request", "Camera", request);
        }

        public static byte[] CreateCurrentFrameRequest(int exposureTime, int id, uint UnitId)
        {
            byte[] request = new byte[23];

            request[0] = 0x02; // STX

            byte[] unitIdBytes = BitConverter.GetBytes(UnitId);
            byte[] sizeBytes = BitConverter.GetBytes(23);
            byte[] typeBytes = BitConverter.GetBytes((ushort)72);
            byte[] versionBytes = BitConverter.GetBytes((ushort)0);
            byte[] idBytes = BitConverter.GetBytes(id);
            byte[] exposureTimeBytes = BitConverter.GetBytes(exposureTime);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(unitIdBytes);
                Array.Reverse(sizeBytes);
                Array.Reverse(typeBytes);
                Array.Reverse(versionBytes);
                Array.Reverse(idBytes);
                Array.Reverse(exposureTimeBytes);
            }

            unitIdBytes.CopyTo(request, 1);
            sizeBytes.CopyTo(request, 5);
            typeBytes.CopyTo(request, 9);
            versionBytes.CopyTo(request, 11);
            idBytes.CopyTo(request, 13);
            exposureTimeBytes.CopyTo(request, 17);

            request[21] = CalculateXOR(request, 0, 21);
            request[22] = 0x03; // ETX

            return request;
        }
        public static async Task<byte[]> ReceiveCurrentFrameResponseAsync(UdpClient udpClient)
        {

            UdpReceiveResult result = await udpClient.ReceiveAsync();
            byte[] response = result.Buffer;

            if (response.Length < 21)
                throw new Exception("Incomplete response received");

            if (response[0] != 0x02)
                throw new Exception("Invalid STX in response");

            int totalSize = BitConverter.ToInt32(response, 5);
            ushort messageType = BitConverter.ToUInt16(response, 9);
            int imageSize = BitConverter.ToInt32(response, 17);

            if (messageType != 136)
                throw new Exception($"Unexpected message type: {messageType}");

            if (imageSize <= 0 || imageSize > MaxUdpSize)
                throw new Exception("Invalid or corrupted image size");

            if (response.Length < totalSize)
                throw new Exception("Incomplete message received");

            byte[] imageData = new byte[imageSize];
            Buffer.BlockCopy(response, 21, imageData, 0, imageSize);

            return imageData;
        }

        public static byte CalculateXOR(byte[] data, int start, int length)
        {
            byte xor = 0;
            for (int i = start; i < start + length; i++)
            {
                xor ^= data[i];
            }
            return xor;
        }

        public static void LogMessage(string source, string destination, byte[] message)
        {
            Log.Information($"Source: {source}, Destination: {destination}");
            Log.Information($"Message (Hex): {BitConverter.ToString(message).Replace("-", "")}");
        }
    }
}
