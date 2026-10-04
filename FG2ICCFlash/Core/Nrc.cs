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

using System.Collections.Generic;

namespace FG2ICCFlasher.Core
{
    /// <summary>
    /// Negative Response Code ($7F) definitions per Ford CAN GDS v2003 / KWP2000.
    /// $78 (requestCorrectlyReceived-ResponsePending) is a WAIT, not a failure.
    /// </summary>
    public static class Nrc
    {
        public const byte ResponsePending = 0x78;

        private static readonly Dictionary<byte, string> Map = new Dictionary<byte, string>
        {
            { 0x10, "General Reject" },
            { 0x11, "Service Not Supported" },
            { 0x12, "Sub-Function Not Supported / Invalid Format" },
            { 0x13, "Incorrect Message Length Or Invalid Format" },
            { 0x14, "Response Too Long" },
            { 0x21, "Busy - Repeat Request" },
            { 0x22, "Conditions Not Correct Or Request Sequence Error" },
            { 0x23, "Routine Not Complete Or Service In Progress" },
            { 0x24, "Request Sequence Error" },
            { 0x25, "No Response From Subnet Component" },
            { 0x26, "Failure Prevents Execution Of Requested Action" },
            { 0x31, "Request Out Of Range (bad address/size/DID/dataFormatIdentifier)" },
            { 0x33, "Security Access Denied" },
            { 0x35, "Invalid Key" },
            { 0x36, "Exceeded Number Of Attempts" },
            { 0x37, "Required Time Delay Not Expired" },
            { 0x40, "Download Not Accepted" },
            { 0x41, "Improper Download Type" },
            { 0x42, "Can Not Download To Specified Address" },
            { 0x43, "Can Not Download Number Of Bytes Requested" },
            { 0x50, "Upload Not Accepted" },
            { 0x51, "Improper Upload Type" },
            { 0x52, "Can Not Upload From Specified Address" },
            { 0x53, "Can Not Upload Number Of Bytes Requested" },
            { 0x70, "Upload/Download Not Accepted" },
            { 0x71, "Transfer Data Suspended" },
            { 0x72, "General Programming Failure / Transfer Aborted" },
            { 0x73, "Wrong Block Sequence Counter" },
            { 0x74, "Illegal Byte Count In Block Transfer" },
            { 0x75, "Illegal Byte Count In Block Transfer" },
            { 0x78, "Request Correctly Received - Response Pending (wait)" },
            { 0x79, "Incorrect Byte Count During Block Transfer" },
            { 0x7E, "Sub-Function Not Supported In Active Session" },
            { 0x7F, "Service Not Supported In Active Session" },
        };

        public static string Describe(byte code)
        {
            string v;
            return Map.TryGetValue(code, out v) ? $"${code:X2} {v}" : $"${code:X2} Unknown/Manufacturer-Specific";
        }

        public static bool IsResponsePending(byte code) => code == ResponsePending;
    }
}
