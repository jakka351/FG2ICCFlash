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

namespace FG2ICCFlasher.Core
{
    /// <summary>Which physical CAN bus / J2534 pin-set and baud to use.</summary>
    public enum CanBus
    {
        /// <summary>Ford MS-CAN: 125 kbps on J1962 pins 3 &amp; 11 (ISO15765_PS, J1962_PINS=0x030B).
        /// This is where the MY12 FG FDIM (0x7A6) lives.</summary>
        MsCan125,
        /// <summary>HS-CAN: 500 kbps on J1962 pins 6 &amp; 14 (plain ISO15765).</summary>
        HsCan500,
    }

    /// <summary>
    /// A request/response CAN transport addressed to a single ECU (txId/rxId), carrying ISO-TP
    /// (ISO 15765-2) reassembled UDS payloads. Implementations: the real J2534 channel and the
    /// dry-run simulator.
    /// </summary>
    public interface ICanChannel : IDisposable
    {
        bool IsOpen { get; }
        uint TxId { get; }
        uint RxId { get; }

        event Action<string> Log;
        /// <summary>Raw frame trace: direction ("TX"/"RX"), the UDS payload (no CAN-id header).</summary>
        event Action<string, byte[]> Frame;

        /// <summary>Open the device and configure the channel/filter for txId/rxId on the given bus.</summary>
        bool Open(string deviceName, CanBus bus, uint txId, uint rxId);

        /// <summary>
        /// Send a UDS request (service id + parameters, WITHOUT the 4-byte CAN-id header) and
        /// return the first reassembled response payload (also without the CAN-id header),
        /// or null on timeout / error.
        /// </summary>
        byte[] SendReceive(byte[] uds, int timeoutMs);

        /// <summary>Read a further response frame without sending (used for $78 pending loops).</summary>
        byte[] ReadNext(int timeoutMs);

        void StartTesterPresent(uint intervalMs);
        void StopTesterPresent();

        /// <summary>Battery/ignition voltage in volts, or 0 if unavailable.</summary>
        double ReadBatteryVoltage();

        void Close();
    }
}
