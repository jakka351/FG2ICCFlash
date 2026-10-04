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
using System.Globalization;
using System.Text;

namespace FG2ICCFlasher.Core
{
    /// <summary>Hex parsing / formatting helpers used across the flasher.</summary>
    public static class HexUtil
    {
        /// <summary>Format a byte array as space-separated upper-case hex, e.g. "27 01 AF".</summary>
        public static string ToHex(byte[] data)
        {
            return ToHex(data, 0, data?.Length ?? 0);
        }

        public static string ToHex(byte[] data, int offset, int count)
        {
            if (data == null || count <= 0) return string.Empty;
            var sb = new StringBuilder(count * 3);
            int end = offset + count;
            for (int i = offset; i < end && i < data.Length; i++)
            {
                if (i > offset) sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>Parse a hex string (optionally space / 0x separated) into bytes.</summary>
        public static byte[] FromHex(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new byte[0];
            var cleaned = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (Uri.IsHexDigit(c)) cleaned.Append(c);
                // skip spaces, commas, 0x prefixes, punctuation
            }
            string hex = cleaned.ToString();
            if ((hex.Length & 1) == 1) hex = "0" + hex; // pad odd nibble count
            var outp = new byte[hex.Length / 2];
            for (int i = 0; i < outp.Length; i++)
                outp[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return outp;
        }

        /// <summary>True if the string is exactly two hex digits.</summary>
        public static bool IsHexPair(string s)
        {
            return s != null && s.Length == 2 && Uri.IsHexDigit(s[0]) && Uri.IsHexDigit(s[1]);
        }

        /// <summary>Convert a single hex char to its 0-15 value, or -1 if invalid.</summary>
        public static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        /// <summary>Concatenate byte arrays.</summary>
        public static byte[] Concat(params byte[][] arrays)
        {
            int total = 0;
            foreach (var a in arrays) total += a?.Length ?? 0;
            var outp = new byte[total];
            int pos = 0;
            foreach (var a in arrays)
            {
                if (a == null || a.Length == 0) continue;
                Buffer.BlockCopy(a, 0, outp, pos, a.Length);
                pos += a.Length;
            }
            return outp;
        }

        /// <summary>Split a 32-bit value into 4 big-endian bytes (MSB first).</summary>
        public static byte[] BE32(uint v)
        {
            return new byte[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
        }

        /// <summary>Split the low 24 bits of a value into 3 big-endian bytes (MSB first).</summary>
        public static byte[] BE24(uint v)
        {
            return new byte[] { (byte)(v >> 16), (byte)(v >> 8), (byte)v };
        }
    }
}
