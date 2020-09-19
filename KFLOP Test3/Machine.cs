using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KMotion_dotNet;

namespace KFLOP_Test3
{
    // What can I say about this?
    // This is the Machine Class
    // Its main purpose is to provide an interface to the actual machine (in this case the BP308)
    // It does this by communicating with the KFlop C program running in Thread 0
    // The communication is handled by passing flags through several of the persist.UserData variables
    // This is very tightly coupled with the code that is running in Thread1 on the KFlop
    // All the persist.UserData variables used are also defined in the BP308_persist.h file in the Thread1 code base
    // If these ever get out of sync there will be trouble!
    // 
    //
   
    // Here is a bit from that file:
    /// 
    // definitions of the persist variables used in the BP308
    //
    // can be use like this:
    // persist.UserData[P_STATUS] |= (1 << SB_HOME);    - set the home bit in the status word.
    // P_XXX means Persist
    // SB_XXX means Status Bit

    // persistant variable map
    // the KFLOP communicates between threads and with the PC via "persist variables"  
    // there are 200 UserData variables defined in KMotionDef.h 
    // Each user data is described in various sections of the KFLOP documentation.
    // 0 - 99  MACH3 User DROs 1 - 50 - 2 words each 
    //     5   MACH3 
    //     6   MACH3 notify message
    //     50-61 MACH3 Current positions of the defined axes.
    //     62  MACH3 Probe Status
    //     
    //
    // 100 - 107 PC_COMM_PERSIST - defined in PC-DSP.h  Special Vars constanty loaded with Bulk Status 
    // 110 - 114 PC_COMM_CSS Mode - Constant Surface Speed variables.
    //
    // 120 - 129 BP308 status variables
    //     120     P_STATUS - BP308 status 
    //     121     P_TLAUX_STATUS - BP308 tool changer status
    //     122     P_MPG_STATUS - BP308 MPG status
    //     123     P_DEVICE_STATUS - some future device?
    //     124     P_SERIAL_PENDING
    //     125     P_MSG_PTR
    //     126     P_MSG_PTR_H
    //     127     P_RESYNC_MPG    - flag to cause MPG Resync
    // 
    // 130 - 139  BP308 Home routines (Thread 2) communications
    //     130     Notify Command Message - similar to MACH3 Notify Message ie the command to execute   
    //     131     Notify Argument - any data that may accompany a message - 
    ///  
    //
    // monitoring the Machine stat
    class Machine
    {
        // remember these are the index numbers in persist.UserData[index] 
        const int P_STATUS = 120;           // the main status word of the machine
        const int P_TLAUX_STATUS = 121;     // latest status recieved from the TLUX Tool Changer Query
        const int P_MPG_STATUS = 122;       // latest status recieved from the MPG Query
        const int P_DEVICE_STATUS = 123;    // Some other TBD device status 
        const int P_SERIAL_PENDING = 124;   // flags that indicate a message has been sent to a peripheral device
        const int P_MSG_PTR = 125;          // Contains a pointer to the message to send - from another thread. - low byte
        const int P_MSG_PTR_H = 126;        // High byte
        const int P_MPG_RESYNC = 127;      // making this non zero will cause the MPG to resync 
        const int P_SPINDLE_STATUS = 128;   // 
        const int P_SPINDLE_RPM = 129;      // calcualted in spinle monitor
        const int P_NOTIFY = 130;           // command to Thread 2 functions
        const int P_NOTIFY_ARGUMENT = 131;  // Argument passed to a Notify Command
        const int P_REMOTE_CMD = 133;       // a non zero value here indicates a command from another Thread or the PC

        // BP308_STATUS bit definitions for P_STATUS
        //#define SB_ESTOP            0   // ESTOP sense 1 = OK, 0 = in ESTOP - this way it always defaults to ESTOP at startup
        //#define SB_TLAUX_OK         1   // indicates the summary status of the TLAUX, 1 = TLAUX is OK, 0 = some fault has occured 
        //        // - this bit is managed by the TLAUX Query Response
        //#define SB_TLAUX_PRES       2   // The TLAUX is responding to querys, 1 = TLAUX present, 0 = TLAUX doesn't answer 
        //#define SB_MPG_OK           3
        //#define SB_MPG_PRES         4   // 1 = MPG present, 0 = MPG not present
        //#define SB_DEVICE_OK        5   
        //#define SB_DEVICE_PRES      6   // 1 = Device present, 0 = Device not present
        //#define SB_OIL_OK           7   // 1 = Oil OK, 0 = Oil low
        //#define SB_AIR_OK           8   // 1 = Air OK, 0 = Air pressure low
        //#define SB_FLOOD_MOTOR_OK   9   // 1 = motor switch tested OK
        //#define SB_PWR_MODULE_OK    10  // power module 1 = OK, 0 = not ready fault
        //#define SB_AXIS_OK          11  // Axis Fault 1 = AXIS OK, 0 = AXIS Fault
        //#define SB_SPINDLE_OK       12  // Spindle fault 1 = OK, 0 = fault
        //#define SB_HOME             13  // 1 = machine has been homed, 0 = not yet homed
        //#define SB_X_LIMIT          14  // 1 = on X_Limit, 0 = normal
        //#define SB_Y_LIMIT          15  // 1 = on Y_Limit, 0 = normal
        //#define SB_Z_LIMIT          16  // 1 = on Z_Limit, 0 = normal

        const int PStart = 120;     // start of index table so P_STATUS - PStart = 0;
        const int NumOfPvars = 14;
        private int[] Pvars; 


        Machine()
        {
            Pvars = new int[NumOfPvars];
        }

        public int Get_P_Status()
        {
            return Pvars[P_STATUS - PStart];
        }
        public void Set_P_Status(int Status)
        {
            Pvars[P_STATUS - PStart] = Status;
        }
        
    }


}
