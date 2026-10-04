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
    /// Ford "Mark I" SecurityAccess (service 0x27) seed-key algorithm for the FG Falcon
    /// FDIM (0x7A6). A 24-bit seed in, a 24-bit key out, driven by a Galois-LFSR style
    /// transform seeded with the constant 0xC541A9 and two 32-iteration mixing passes
    /// (one over the seed word, one over a word built from the 5 secret key bytes),
    /// followed by a fixed nibble permutation.
    ///
    /// The per-module secret is the ASCII of a code-word:
    ///   MK1 FDIM = "BradW" = 42 72 61 64 57
    ///   MK2 FDIM = "Janis" = 4A 61 6E 69 73   (use this for MK2 FG clusters)
    ///
    /// The transform below is reproduced verbatim from the known-good implementation and is
    /// deliberately NOT re-factored — it is locked by the known-answer vectors in SelfTest().
    /// </summary>
    public static class FordSecurity
    {
        /// <summary>Named secret key-word presets for the FDIM.</summary>
        public static readonly byte[] KeyBradW = { 0x42, 0x72, 0x61, 0x64, 0x57 }; // MK1
        public static readonly byte[] KeyJanis = { 0x4A, 0x61, 0x6E, 0x69, 0x73 }; // MK2

        public sealed class KeyWord
        {
            public string Name { get; }
            public byte[] Bytes { get; }
            public KeyWord(string name, byte[] bytes) { Name = name; Bytes = bytes; }
            public override string ToString() => Name;
        }

        /// <summary>5-byte ASCII secret from a code-word (case-sensitive).</summary>
        private static byte[] Ascii5(string s) => System.Text.Encoding.ASCII.GetBytes(s);

        /// <summary>
        /// Key-words offered in the UI. "pLaRM" (the secret baked into the working FG2ICCComms app's
        /// key_from_seed for this FDIM) is first and is the DEFAULT selection, followed by the two
        /// documented 0x7A6 secrets and candidate code-words. The box is editable, so a custom 5-byte
        /// hex key can also be typed/pasted directly.
        /// </summary>
        public static readonly IReadOnlyList<KeyWord> KnownKeys = new List<KeyWord>
        {
            new KeyWord("pLaRM (FG2ICCComms)", new byte[] { 0x70, 0x4C, 0x61, 0x52, 0x4D }),
            new KeyWord("Janis (MK2)", KeyJanis),
            new KeyWord("BradW (MK1)", KeyBradW),
            new KeyWord("Carol", Ascii5("Carol")),
            new KeyWord("JAMES", Ascii5("JAMES")),
            new KeyWord("Bosch", Ascii5("Bosch")),
            new KeyWord("FAITH", Ascii5("FAITH")),
            new KeyWord("REMAT", Ascii5("REMAT")),
            new KeyWord("Rowan", Ascii5("Rowan")),
            new KeyWord("JaMes", Ascii5("JaMes")),
            new KeyWord("SAMMY", Ascii5("SAMMY")),
            new KeyWord("Lupin", Ascii5("Lupin")),
            new KeyWord("nowaR", Ascii5("nowaR")),
        };

        /// <summary>
        /// Compute the 24-bit key for a 24-bit <paramref name="seed"/> using the 5 secret bytes.
        /// </summary>
        public static int KeyGenMkI(int s, int sknum, int sknum2, int sknum3, int sknum4, int sknum5)
        {
            var sknum13 = (int)((byte)(s >> 0x10 & 0xFF));
            var b2 = (byte)(s >> 8 & 0xFF);
            var b3 = (byte)(s & 0xFF);
            var sknum6 = (sknum13 << 0x10) + ((int)b2 << 8) + (int)b3;
            var sknum7 = (sknum6 & 0xFF0000) >> 0x10 | (sknum6 & 0xFF00) | sknum << 0x18 | (sknum6 & 0xFF) << 0x10;
            var sknum8 = 0xC541A9;
            for (int i = 0; i < 0x20; i++)
            {
                int sknum10;
                int sknum9;
                sknum8 = (((sknum9 = (sknum10 = (((sknum7 >> i & 1) ^ (sknum8 & 1)) << 0x17 | sknum8 >> 1))) & 0xEF6FD7) | ((sknum9 & 0x100000) >> 0x14 ^ (sknum10 & 0x800000) >> 0x17) << 0x14 | ((sknum8 >> 1 & 0x8000) >> 0xF ^ (sknum10 & 0x800000) >> 0x17) << 0xF | ((sknum8 >> 1 & 0x1000) >> 0xC ^ (sknum10 & 0x800000) >> 0x17) << 0xC | 0x20 * ((sknum8 >> 1 & 0x20) >> 5 ^ (sknum10 & 0x800000) >> 0x17) | 8 * ((sknum8 >> 1 & 8) >> 3 ^ (sknum10 & 0x800000) >> 0x17));
            }
            for (int j = 0; j < 0x20; j++)
            {
                int sknum12;
                int sknum11;
                sknum8 = (((sknum11 = (sknum12 = ((((sknum5 << 0x18 | sknum4 << 0x10 | sknum2 | sknum3 << 8) >> j & 1) ^ (sknum8 & 1)) << 0x17 | sknum8 >> 1))) & 0xEF6FD7) | ((sknum11 & 0x100000) >> 0x14 ^ (sknum12 & 0x800000) >> 0x17) << 0x14 | ((sknum8 >> 1 & 0x8000) >> 0xF ^ (sknum12 & 0x800000) >> 0x17) << 0xF | ((sknum8 >> 1 & 0x1000) >> 0xC ^ (sknum12 & 0x800000) >> 0x17) << 0xC | 0x20 * ((sknum8 >> 1 & 0x20) >> 5 ^ (sknum12 & 0x800000) >> 0x17) | 8 * ((sknum8 >> 1 & 8) >> 3 ^ (sknum12 & 0x800000) >> 0x17));
            }
            return (sknum8 & 0xF0000) >> 0x10 | 0x10 * (sknum8 & 0xF) | ((sknum8 & 0xF00000) >> 0x14 | (sknum8 & 0xF000) >> 8) << 8 | (sknum8 & 0xFF0) >> 4 << 0x10;
        }

        /// <summary>Compute the 3-byte key for a 3-byte seed and a 5-byte key-word.</summary>
        public static byte[] ComputeKey(byte[] seed3, byte[] keyWord5)
        {
            if (seed3 == null || seed3.Length < 3) throw new ArgumentException("seed must be 3 bytes", nameof(seed3));
            if (keyWord5 == null || keyWord5.Length < 5) throw new ArgumentException("key-word must be 5 bytes", nameof(keyWord5));
            int seed = (seed3[0] << 16) | (seed3[1] << 8) | seed3[2];
            int key = KeyGenMkI(seed, keyWord5[0], keyWord5[1], keyWord5[2], keyWord5[3], keyWord5[4]) & 0xFFFFFF;
            return new byte[] { (byte)(key >> 16), (byte)(key >> 8), (byte)key };
        }

        /// <summary>
        /// Known-answer self test. Returns true when the algorithm produces the verified
        /// vectors for both key-words. Run at startup to guard against accidental edits.
        /// </summary>
        public static bool SelfTest(out string report)
        {
            var cases = new (byte[] key, int seed, int expect)[]
            {
                (KeyBradW, 0x000000, 0xF1AC1A),
                (KeyBradW, 0x123456, 0x36A867),
                (KeyBradW, 0xAFBB7F, 0xB5FC7F),
                (KeyBradW, 0xFFFFFF, 0x9A51C3),
                (KeyJanis, 0x000000, 0xDBC467),
                (KeyJanis, 0x123456, 0x1CC01A),
                (KeyJanis, 0xAFBB7F, 0x9F9402),
                (KeyJanis, 0xFFFFFF, 0xB039BE),
            };
            bool ok = true;
            var sb = new System.Text.StringBuilder();
            foreach (var c in cases)
            {
                int got = KeyGenMkI(c.seed, c.key[0], c.key[1], c.key[2], c.key[3], c.key[4]) & 0xFFFFFF;
                bool pass = got == c.expect;
                ok &= pass;
                if (!pass)
                    sb.AppendLine($"FAIL seed={c.seed:X6} expected={c.expect:X6} got={got:X6}");
            }
            report = ok ? "SecurityAccess algorithm self-test: PASS (8/8 vectors)" : "SecurityAccess self-test FAILED:\r\n" + sb;
            return ok;
        }
    }
}
