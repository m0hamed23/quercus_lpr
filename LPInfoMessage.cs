using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuercusSimulator
{
    public static class LPInfoMessage
    {
        public static byte[] CreateLicensePlateInfoMessage(uint unitId, uint id, uint carId, uint triggerId, string detectedChars)
        {
            // Total size of the message should be 188 bytes
            byte[] message = new byte[188];

            // STX (Start of Text)
            message[0] = 0x02;

            // Unit ID (4 bytes) - Convert from decimal to little-endian
            Buffer.BlockCopy(BitConverter.GetBytes(unitId), 0, message, 1, 4);

            // Size (4 bytes) - BC000000 (188 bytes, little-endian)
            message[5] = 0xBC;
            message[6] = 0x00;
            message[7] = 0x00;
            message[8] = 0x00;

            // Type (2 bytes) - License Info (0200)
            message[9] = 0x02;
            message[10] = 0x00;

            // Version (2 bytes) - Version (0200)
            message[11] = 0x02;
            message[12] = 0x00;

            // ID (4 bytes) - Convert from decimal to little-endian
            Buffer.BlockCopy(BitConverter.GetBytes(id), 0, message, 13, 4);

            // Car Id (4 bytes) - Convert from decimal to little-endian
            Buffer.BlockCopy(BitConverter.GetBytes(carId), 0, message, 17, 4);

            // Trigger Id (4 bytes) - Convert from decimal to little-endian
            Buffer.BlockCopy(BitConverter.GetBytes(triggerId), 0, message, 21, 4);

            // Timestamp (4 bytes) - Get the current UNIX timestamp (seconds since 1st Jan 1970)
            uint timestamp = (uint)(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Buffer.BlockCopy(BitConverter.GetBytes(timestamp), 0, message, 25, 4);

            // TimestampUSec (4 bytes) - Microseconds part of the current time
            uint timestampUsec = (uint)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1000000) * 1000;
            Buffer.BlockCopy(BitConverter.GetBytes(timestampUsec), 0, message, 29, 4);

            // Detected Chars (40 bytes) - Convert detected characters to bytes
            byte[] detectedCharsBytes = Encoding.ASCII.GetBytes(detectedChars);
            Buffer.BlockCopy(detectedCharsBytes, 0, message, 33, detectedCharsBytes.Length);

            // Qualities (40 bytes) - Set all qualities to 100 (0x64)
            for (int i = 73; i < 113; i++)
            {
                message[i] = 0x64; // Quality = 100
            }

            // Grammar Ok (1 byte) - Set to 1 (grammar is OK)
            message[113] = 0x01;

            // Printable String (40 bytes) - Detected Chars with spaces between letters and numbers
            string printableString = AddSpaceBetweenCharsAndNumbers(detectedChars);
            byte[] printableStringBytes = Encoding.ASCII.GetBytes(printableString);
            Buffer.BlockCopy(printableStringBytes, 0, message, 114, printableStringBytes.Length);

            // Country (32 bytes) - Example value (e.g., "ET")
            string country = "ET(B)";
            byte[] countryBytes = Encoding.ASCII.GetBytes(country);
            Buffer.BlockCopy(countryBytes, 0, message, 154, countryBytes.Length);

            // BCC (Block Check Character) - XOR from STX to the last byte of Message Data
            message[186] = CalculateXOR(message, 0, 186);

            // ETX (End of Text)
            message[187] = 0x03;

            return message;
        }

        // Helper method to add a space between letters and numbers in a string
        private static string AddSpaceBetweenCharsAndNumbers(string input)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                result.Append(input[i]);
                if (i < input.Length - 1 && char.IsDigit(input[i]) && char.IsLetter(input[i + 1]))
                {
                    result.Append(' ');
                }
            }
            return result.ToString();
        }

        // Helper method to calculate the XOR for BCC
        private static byte CalculateXOR(byte[] data, int start, int length)
        {
            byte xor = 0x00;
            for (int i = start; i < length; i++)
            {
                xor ^= data[i];
            }
            return xor;
        }

}
}
