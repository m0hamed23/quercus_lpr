using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace QuercusSimulator
{


    public class UdpLargeMessageHandler
    {
        private const int MaxUdpSize = 65507; // Maximum theoretical size for UDP

        public static async Task<(byte[] Buffer, System.Net.IPEndPoint RemoteEndPoint)> ReceiveLargeMessageAsync(UdpClient udpClient)
        {
            try
            {
                UdpReceiveResult result = await udpClient.ReceiveAsync();

                if (result.Buffer.Length > MaxUdpSize)
                {
                    Console.WriteLine($"Warning: Received message size ({result.Buffer.Length} bytes) exceeds maximum UDP datagram size.");
                }

                Console.WriteLine($"Received message of size: {result.Buffer.Length} bytes");

                return (result.Buffer, result.RemoteEndPoint);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket error occurred: {ex.Message}");
                return (null, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while receiving the message: {ex.Message}");
                return (null, null);
            }
        }
    }
}
