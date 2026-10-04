// ////////////////////////////////////////////////////////////////////////////////////////////////////
//                            Tester Present Specialist Automotive Solutions                         //
// ////////////////////////////////////////////////////////////////////////////////////////////////////        
                                                             
//                                           .  ....       .            ..                            
//                                          .:::. .:.... ...           .=+.                           
//                                           .::::::::::::::.         .=++-.                          
//                                          .:::::::::::::..          .++++:                          
//                                 .:-=.   .::::::::::::::.:          :++++-                          
//                               .=+++++-:..:::::::::::::.  .         :+++++-=.                       
//                              :+++++++++:::::::::::::::::...        :++++++++.                      
//                           ...-+++++++++::::::::::::::::::::.  .    +++++++++:                      
//                         .-=.+++++++++++-:::::::::::::::::::-++:  .=+++++++++=.                     
//                         -++++++++++++++-:::::::::::::::::::-+++++++++++++++++-                     
//                        .-++++++++++++++-:::::::::::::::::::=+++++++++++++++++-                     
//                       .=+++++++++++++++-:::::::::::::::::::=++++++++++++++++++..                   
//                     .:+++++++++++++++++=:::::::::::::::::::=++++++++++++++++++++=.                 
//                ..-+++++++++++++++++++++=:::::::::::::::::::+++++++++++++++++++++++=.               
//            .:=+++++++++++++++++++++++++=:::::::::::::::::::++++++++++++++++++++++++-               
//         ..:++++++++++++++++++++++++++++=:::::::::::::::::::+++++++++++++++++++++++++.              
//        :.+++++++++++++++++++++++++++++++:::::::::::::::::::+++++++++++++++++++++++++++-.           
//        -++++++++++++++++++++++++++++++++.::::JAKKA351::::::+++++++++++++++++++++++++++=            
//        :++++++++++++++++++++++++++++++++.::::::::::::::::::++++++++++++++++++++++++++++=.          
//        -++++++++++++++++++++++++++++++++.:-------------::::======++++++++++++++++++++++++.         
//        .=+++++++++++++++++++++++++++++++-+++++++++++++++++++++++=+++++++++++++++++++++++++:        
//        .+-++++++++++++++++++++++++++++++-+++++++++++++++++++++++=+++++++++++++++++++++++++-        
//        .:+++++++++++++++++++++++++++++++-+++++++++++++++++++++++=+++++++++++++++++++++++++:        
//          .++++++++++++++++++++++++++++++-+++++++++++++++++++++++----======++++++++==++=+==:        
//           .+++++++++++++++++++++++++++++=+++++++++++++++++++++++-===============-=========-.       
//             =++++++++++++++++++++++++++++=++++++++++++++++++++++-=========================.        
//             .++++++++++++++++++++++++++++=++==++++++++++++++++++-========================..        
//              :+++++++++++++++++++++++=:.       ..:-+++++++++++++-=======================-.         
//               .+++++++++++++++++=:.               .-+++++==+++++=======================-.          
//               .++++++++++++++++:                    .+++:.++++++-====================-:.           
//               .=++++++++:......                     .==  =:+++++=+:=================:              
//              .-++++++=.                                 ..:+++++=+++:==========--=-.               
//                 ..:..                                  .... .=+=+++++=----==-===--:.               
//                                                              -+=+++++++++++++-==-.                 
//                                                              .=-+++++++++++++=:-:.                 
//                                                                ..:=+++++++++:::.                   
//                                                                    ..  .:-.                        
//         TESTER PRESENT SPECIALIST AUTOMOTIVE SOLUTIONS              .:.     .                       
//                                                                     ....   ..                      
//                                                                      :=====-                       
//                                                                      .=====:                       
//                                                                      .-===-.                       
//                                                                        ::.                         
//

using System;
using System.Collections.Generic;

namespace FG2ICCFlasher.Core
{
    /// <summary>
    /// Intel HEX parser. Handles the ASCII form (<c>:LLAAAATT....CC</c>) used by the .hex
    /// files, and the binary-encoded form embedded in a Ford PHF payload (identical record
    /// layout but stored as raw bytes: 0x3A marker, length, 2-byte big-endian address,
    /// type, data, 1-byte checksum). Record types 00 (data), 01 (EOF), 02 (extended segment
    /// address), 04 (extended linear address) and 03/05 (start address, ignored) are supported.
    /// </summary>
    public static class IntelHex
    {
        private const byte RecMark = 0x3A;      // ':'
        private const int TypeData = 0x00;
        private const int TypeEof = 0x01;
        private const int TypeExtSeg = 0x02;
        private const int TypeStartSeg = 0x03;
        private const int TypeExtLinear = 0x04;
        private const int TypeStartLinear = 0x05;

        /// <summary>Parse ASCII Intel HEX text into a MemoryImage.</summary>
        public static MemoryImage ParseAscii(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var image = new MemoryImage();
            uint baseAddress = 0;
            bool sawEof = false;
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] != ':') continue;
                if ((line.Length - 1) % 2 != 0)
                    throw new FormatException("Odd-length Intel HEX record: " + line);

                int n = (line.Length - 1) / 2;
                var rec = new byte[n];
                for (int i = 0; i < n; i++)
                    rec[i] = (byte)((HexUtil.HexDigit(line[1 + i * 2]) << 4) | HexUtil.HexDigit(line[2 + i * 2]));

                if (ProcessRecord(rec, 0, image, ref baseAddress)) { sawEof = true; break; } // EOF
            }
            if (!sawEof) throw new FormatException("Intel HEX ended without an EOF (type 01) record — file is truncated.");
            image.Coalesce();
            return image;
        }

        /// <summary>
        /// Parse a sequence of binary-encoded Intel HEX records starting at <paramref name="offset"/>
        /// in <paramref name="data"/> (e.g. a PHF payload). Stops at the EOF record or end of buffer.
        /// </summary>
        public static MemoryImage ParseBinaryRecords(byte[] data, int offset)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var image = new MemoryImage();
            uint baseAddress = 0;
            bool sawEof = false;
            int p = offset;
            while (p < data.Length)
            {
                // Skip any filler until the next record marker.
                if (data[p] != RecMark) { p++; continue; }
                if (p + 5 > data.Length) break;           // need marker+len+addr(2)+type
                int len = data[p + 1];
                int recLen = 1 + 1 + 2 + 1 + len + 1;     // marker,len,addr,type,data,checksum
                if (p + recLen > data.Length) break;       // truncated trailing record

                // Build a record WITHOUT the leading marker so ProcessRecord sees len,addr,type,...
                var rec = new byte[1 + 2 + 1 + len + 1];
                Buffer.BlockCopy(data, p + 1, rec, 0, rec.Length);
                bool eof = ProcessRecord(rec, 0, image, ref baseAddress);
                p += recLen;
                if (eof) { sawEof = true; break; }
            }
            if (!sawEof) throw new FormatException("PHF payload ended without an Intel HEX EOF (type 01) record — firmware is truncated or corrupt.");
            image.Coalesce();
            return image;
        }

        /// <summary>
        /// Process one record given as [len][addrHi][addrLo][type][data...][checksum].
        /// Returns true when an EOF record (type 01) is seen.
        /// </summary>
        private static bool ProcessRecord(byte[] rec, int start, MemoryImage image, ref uint baseAddress)
        {
            int len = rec[start];
            int addr = (rec[start + 1] << 8) | rec[start + 2];
            int type = rec[start + 3];
            int dataStart = start + 4;

            // Verify checksum (two's complement of the sum of all preceding bytes).
            int sum = 0;
            for (int i = start; i < dataStart + len; i++) sum += rec[i];
            byte expected = rec[dataStart + len];
            byte actual = (byte)((0x100 - (sum & 0xFF)) & 0xFF);
            if (expected != actual)
                throw new FormatException(
                    $"Intel HEX checksum error (type {type:X2}, addr {addr:X4}): got {expected:X2}, expected {actual:X2}");

            switch (type)
            {
                case TypeData:
                    var payload = new byte[len];
                    Buffer.BlockCopy(rec, dataStart, payload, 0, len);
                    image.Add(new MemorySegment(baseAddress + (uint)addr, payload));
                    return false;
                case TypeEof:
                    return true;
                case TypeExtSeg:
                    baseAddress = (uint)(((rec[dataStart] << 8) | rec[dataStart + 1]) << 4);
                    return false;
                case TypeExtLinear:
                    baseAddress = (uint)(((rec[dataStart] << 8) | rec[dataStart + 1]) << 16);
                    return false;
                case TypeStartSeg:
                case TypeStartLinear:
                    return false; // start address: no memory effect for us
                default:
                    return false; // unknown record type: ignore
            }
        }
    }
}
