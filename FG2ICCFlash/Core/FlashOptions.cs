using System;

namespace FG2ICCFlasher.Core
{
    /// <summary>
    /// Configuration for a flash run. Defaults reflect the best evidence for the MY12 FG
    /// FDIM (0x7A6, MS-CAN 125 kbps, GDS v2003). Values that the supplied material does not
    /// pin down exactly (DFI, programming session, erase command, block size) are exposed here
    /// with evidence-based defaults so they can be adjusted against a live module.
    /// </summary>
    public sealed class FlashOptions
    {
        // ---- Target / transport ----
        public string DeviceName = null;           // null = first installed J2534 device
        public CanBus Bus = CanBus.MsCan125;        // MY12 FDIM lives on MS-CAN 125k (pins 3/11)
        public uint TxId = 0x7A6;                   // tester -> FDIM
        public uint RxId = 0x7AE;                   // FDIM -> tester

        // ---- Session / security ----
        public byte ProgrammingSession = 0x85;      // GDS ECUProgrammingMode ($85). ($87 = adjustment)
        public byte SecurityLevel = 0x01;           // requestSeed sub-function; sendKey = level+1 ($02)
        public byte[] KeyWord = FordSecurity.KeyJanis; // MK2 "Janis" by default

        // ---- Download parameters ----
        /// <summary>dataFormatIdentifier for RequestDownload. GDS recommends 0x00; the PHF header
        /// DOWNLOAD FORMAT is 0x01. Set from the PHF at load time; 0x00 is the safe GDS default.</summary>
        public byte DataFormatIdentifier = 0x00;
        /// <summary>Max data bytes per TransferData. 0 = derive from the RequestDownload response
        /// (maxNumberOfBlockLength - 1 for the $36 SID), clamped to [1, 4093].</summary>
        public int BlockDataSizeOverride = 0;
        public byte FillByte = 0xFF;                // gap fill for erased flash

        // ---- Sequence toggles ----
        public bool DownloadSbl = true;             // download the flash driver (SBL) to RAM first
        /// <summary>Optional $31 StartRoutine (routine id + args) to activate the RAM-resident flash
        /// driver after its transfer-exit. null = rely on the module auto-executing on $37. The exact
        /// activation requirement/id is module-specific (FDIM SSDS) and unconfirmed in the supplied
        /// material, so this is left null by default and surfaced as a warning.</summary>
        public byte[] SblActivationRoutine = null;
        public bool EraseBeforeAppDownload = true;  // $B1 00 B2 Flash Memory Erase
        public bool VerifyAfter = true;             // log/verify checksum after programming
        public bool EcuResetAfter = true;           // $11 01 at the end

        // ---- Timing (ms) ----
        public uint TesterPresentIntervalMs = 2000; // < S3 (5 s)
        public int P2TimeoutMs = 2000;              // normal service response (P2)
        public int P2StarTimeoutMs = 5000;          // enhanced wait after a $78 "response pending" (P2*)
        public int TransferTimeoutMs = 5000;        // per TransferData block
        public int EraseTimeoutMs = 60000;          // Flash Erase can take many seconds (+$78)
        public int RoutineTimeoutMs = 20000;        // routines / checksum

        // ---- Safety ----
        public double MinBatteryVolts = 12.0;       // warn below this (0 = unknown/unsupported)

        public FlashOptions Clone() => (FlashOptions)MemberwiseClone();
    }
}
