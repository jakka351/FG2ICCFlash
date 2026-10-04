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
using System.IO;
using System.Text;

namespace FG2ICCFlasher.Core
{
    /// <summary>
    /// Parser for the Ford PHF ("Programmable Hex File") container used for FDM/FDIM firmware.
    ///
    /// Layout:
    ///   [ ASCII header ]  a run of NUL-terminated "KEY>VALUE" records, e.g.
    ///         APPLICATION>FDM Application\0 MASK NUMBER>299812\0 ... FLASH ERASE SECTORS>0x00\0
    ///   [ "$" \0 ]        a single '$' marker record ending the header
    ///   [ binary payload ] Intel HEX records in binary form (0x3A marker, len, 2-byte BE
    ///                      address, type, data, checksum) terminated by an EOF (type 01) record.
    ///
    /// Observed header keys: APPLICATION, MASK NUMBER, FILE NAME, RELEASE DATE, MODULE TYPE,
    /// PRODUCTION MODULE PART NUMBER, WERS NOTICE, COMMENTS, RELEASED BY, MODULE NAME,
    /// MODULE ID (=0x7A6 for the FDIM), DOWNLOAD FORMAT (=0x01), FILE CHECKSUM, FLASH INDICATOR
    /// (0 = flash driver/SBL, 1 = application), FLASH ERASE SECTORS.
    /// </summary>
    public sealed class PhfFile
    {
        public string SourcePath { get; private set; }
        public IReadOnlyDictionary<string, string> Header => _header;
        public MemoryImage Image { get; private set; }

        private readonly Dictionary<string, string> _header =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ---- Convenience typed accessors over the header ----
        public string Application => Get("APPLICATION");
        public string MaskNumber => Get("MASK NUMBER");
        public string FileName => Get("FILE NAME");
        public string ReleaseDate => Get("RELEASE DATE");
        public string Comments => Get("COMMENTS");

        /// <summary>MODULE ID as an integer, e.g. 0x7A6. 0 if absent/unparseable.</summary>
        public uint ModuleId => ParseHexValue(Get("MODULE ID"));
        /// <summary>DOWNLOAD FORMAT byte (dataFormatIdentifier hint), e.g. 0x01.</summary>
        public byte DownloadFormat => (byte)ParseHexValue(Get("DOWNLOAD FORMAT"));
        /// <summary>FILE CHECKSUM (16-bit) as stored in the header, e.g. 0x4F08.</summary>
        public uint FileChecksum => ParseHexValue(Get("FILE CHECKSUM"));
        /// <summary>FLASH INDICATOR: 0 = flash driver / secondary bootloader, 1 = application.</summary>
        public int FlashIndicator
        {
            get { int v; return int.TryParse(Get("FLASH INDICATOR"), out v) ? v : -1; }
        }
        public uint FlashEraseSectors => ParseHexValue(Get("FLASH ERASE SECTORS"));

        /// <summary>True when this PHF is the flash driver / secondary bootloader (FLASH INDICATOR = 0).</summary>
        public bool IsFlashDriver => FlashIndicator == 0;

        public uint StartAddress => Image?.StartAddress ?? 0;
        public uint EndAddress => Image?.EndAddress ?? 0;
        public int TotalDataBytes => Image?.TotalDataBytes ?? 0;

        private string Get(string key)
        {
            string v;
            return _header.TryGetValue(key, out v) ? v : null;
        }

        public static PhfFile Load(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var phf = Parse(bytes);
            phf.SourcePath = path;
            return phf;
        }

        public static PhfFile Parse(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            var phf = new PhfFile();
            int payloadOffset = phf.ParseHeader(bytes);
            phf.Image = IntelHex.ParseBinaryRecords(bytes, payloadOffset);
            return phf;
        }

        /// <summary>Reads NUL-terminated ASCII header tokens; returns the payload start offset.</summary>
        private int ParseHeader(byte[] bytes)
        {
            int i = 0;
            while (i < bytes.Length)
            {
                // Read one NUL-terminated token.
                int tokStart = i;
                while (i < bytes.Length && bytes[i] != 0x00) i++;
                int tokLen = i - tokStart;
                // i now points at the NUL (or end). Advance past the NUL for the next token.
                int afterNul = (i < bytes.Length) ? i + 1 : i;

                string token = Encoding.ASCII.GetString(bytes, tokStart, tokLen);

                // End-of-header marker: a lone '$'.
                if (token == "$")
                    return afterNul;

                int gt = token.IndexOf('>');
                if (gt > 0)
                {
                    string key = token.Substring(0, gt).Trim();
                    string val = token.Substring(gt + 1).Trim();
                    if (key.Length > 0) _header[key] = val;
                }
                else if (tokLen == 0)
                {
                    // Empty token: if the very next byte starts a valid HEX record, the header is done.
                    if (afterNul < bytes.Length && bytes[afterNul] == 0x3A && LooksLikeRecord(bytes, afterNul))
                        return afterNul;
                }
                else
                {
                    // Non key/value token that begins a valid binary HEX record => payload start.
                    if (bytes[tokStart] == 0x3A && LooksLikeRecord(bytes, tokStart))
                        return tokStart;
                }

                i = afterNul;
            }
            return bytes.Length; // no payload found
        }

        /// <summary>Sanity check that offset begins a plausible Intel HEX binary record.</summary>
        private static bool LooksLikeRecord(byte[] b, int off)
        {
            if (off + 5 > b.Length || b[off] != 0x3A) return false;
            int len = b[off + 1];
            int type = b[off + 4];
            if (type > 0x05) return false;
            return off + 1 + 1 + 2 + 1 + len + 1 <= b.Length;
        }

        private static uint ParseHexValue(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            uint v;
            return uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        /// <summary>
        /// Simple 16-bit additive checksum over all data bytes, for advisory comparison against
        /// the header FILE CHECKSUM. NOTE: Ford's exact checksum algorithm for this field is not
        /// documented in the supplied material, so a mismatch here is informational, not fatal.
        /// </summary>
        public ushort ComputeAdditiveChecksum16()
        {
            uint sum = 0;
            foreach (var seg in Image.Segments)
                foreach (var b in seg.Data) sum += b;
            return (ushort)(sum & 0xFFFF);
        }

        /// <summary>
        /// Pre-flash integrity check: image non-empty, addressed to this module (0x7A6), and the
        /// 16-bit additive checksum over the payload matches the header FILE CHECKSUM. (This additive
        /// algorithm was verified to match the header on every supplied FDM file.)
        /// </summary>
        public bool Validate(out string error)
        {
            error = null;
            if (Image == null || Image.IsEmpty) { error = "firmware image is empty."; return false; }
            if (ModuleId != 0 && ModuleId != 0x7A6) { error = $"MODULE ID is 0x{ModuleId:X3}, not 0x7A6 (FDIM)."; return false; }
            if (FileChecksum != 0)
            {
                ushort calc = ComputeAdditiveChecksum16();
                if (calc != (ushort)FileChecksum)
                { error = $"checksum mismatch: header 0x{FileChecksum:X4} vs computed 0x{calc:X4} — file may be corrupt."; return false; }
            }
            return true;
        }

        public string DescribeHeader()
        {
            var sb = new StringBuilder();
            foreach (var kv in _header) sb.AppendLine($"  {kv.Key} = {kv.Value}");
            return sb.ToString();
        }
    }
}
