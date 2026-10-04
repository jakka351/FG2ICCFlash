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

        /// <summary>
        /// Parse a hex string into bytes. Tokens are split on whitespace/commas; a leading "0x"/"0X"
        /// on a token is stripped (so "0x31 0x02 0x00" parses as 31 02 00, not 00 31 00 ...). A token
        /// with an odd number of hex digits is left-padded with a leading zero.
        /// </summary>
        public static byte[] FromHex(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new byte[0];
            var outBytes = new List<byte>();
            foreach (var tokenRaw in s.Split(new[] { ' ', '\t', '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string tok = tokenRaw;
                if (tok.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) tok = tok.Substring(2);
                var digits = new StringBuilder(tok.Length);
                foreach (var c in tok) if (Uri.IsHexDigit(c)) digits.Append(c);
                if (digits.Length == 0) continue;
                string hex = digits.ToString();
                if ((hex.Length & 1) == 1) hex = "0" + hex;
                for (int i = 0; i < hex.Length; i += 2)
                    outBytes.Add(byte.Parse(hex.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }
            return outBytes.ToArray();
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

        /// <summary>Split a value into 3 big-endian bytes (MSB first). Throws if it exceeds 24 bits,
        /// so an oversized download size can never be silently truncated into the $34 size field.</summary>
        public static byte[] BE24(uint v)
        {
            if (v > 0xFFFFFF) throw new ArgumentOutOfRangeException(nameof(v), $"value 0x{v:X} exceeds 24 bits");
            return new byte[] { (byte)(v >> 16), (byte)(v >> 8), (byte)v };
        }
    }
}
