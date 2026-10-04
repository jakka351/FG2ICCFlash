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
using System.Linq;

namespace FG2ICCFlasher.Core
{
    /// <summary>A contiguous block of program data at an absolute memory address.</summary>
    public sealed class MemorySegment
    {
        public uint Address { get; }
        public byte[] Data { get; }

        public MemorySegment(uint address, byte[] data)
        {
            Address = address;
            Data = data ?? new byte[0];
        }

        public uint EndAddress => Address + (uint)Data.Length;   // exclusive
        public int Length => Data.Length;

        public override string ToString()
            => $"0x{Address:X8}..0x{EndAddress - 1:X8} ({Data.Length} bytes)";
    }

    /// <summary>
    /// An ordered set of memory segments making up a downloadable image (SBL or application),
    /// with helpers to coalesce adjacent runs and compute span / total size.
    /// </summary>
    public sealed class MemoryImage
    {
        private readonly List<MemorySegment> _segments = new List<MemorySegment>();

        public IReadOnlyList<MemorySegment> Segments => _segments;

        public void Add(MemorySegment seg)
        {
            if (seg != null && seg.Length > 0) _segments.Add(seg);
        }

        /// <summary>Lowest address across all segments (the download start address).</summary>
        public uint StartAddress => _segments.Count == 0 ? 0 : _segments.Min(s => s.Address);

        /// <summary>Highest end address (exclusive) across all segments.</summary>
        public uint EndAddress => _segments.Count == 0 ? 0 : _segments.Max(s => s.EndAddress);

        /// <summary>Contiguous span from StartAddress to EndAddress (may include gaps).</summary>
        public uint Span => EndAddress - StartAddress;

        /// <summary>Total number of actual data bytes (sum of segment lengths; excludes gaps).</summary>
        public int TotalDataBytes => _segments.Sum(s => s.Length);

        public bool IsEmpty => _segments.Count == 0;

        /// <summary>
        /// Sort segments by address and merge any that are directly adjacent (end == next start).
        /// </summary>
        public void Coalesce()
        {
            if (_segments.Count < 2) { _segments.Sort((a, b) => a.Address.CompareTo(b.Address)); return; }
            _segments.Sort((a, b) => a.Address.CompareTo(b.Address));
            var merged = new List<MemorySegment>();
            var curAddr = _segments[0].Address;
            var curData = new List<byte>(_segments[0].Data);
            for (int i = 1; i < _segments.Count; i++)
            {
                var s = _segments[i];
                if (s.Address == curAddr + (uint)curData.Count)
                {
                    curData.AddRange(s.Data);
                }
                else
                {
                    merged.Add(new MemorySegment(curAddr, curData.ToArray()));
                    curAddr = s.Address;
                    curData = new List<byte>(s.Data);
                }
            }
            merged.Add(new MemorySegment(curAddr, curData.ToArray()));
            _segments.Clear();
            _segments.AddRange(merged);
        }

        /// <summary>
        /// Flatten to a single contiguous byte[] spanning StartAddress..EndAddress, filling
        /// any gaps with the given pad byte (default 0xFF, the erased-flash value).
        /// </summary>
        public byte[] Flatten(byte pad = 0xFF)
        {
            if (IsEmpty) return new byte[0];
            uint start = StartAddress;
            var outp = new byte[Span];
            for (int i = 0; i < outp.Length; i++) outp[i] = pad;
            foreach (var s in _segments)
                Buffer.BlockCopy(s.Data, 0, outp, (int)(s.Address - start), s.Data.Length);
            return outp;
        }
    }
}
