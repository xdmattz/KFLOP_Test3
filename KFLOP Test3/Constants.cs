using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFLOP_Test3
{
    // attempting to learn how to use constants in C# - since they don't like #define anymore...

        // constants in the code that don't have any relation to the hardware or 
    class CSConst
    {
        public const int PC_CMD = 0;

        public const int P_STATUS = 4;
        public const int P_RPM = 5;
    }

    // constants relating to the persist variables
    // see also BP308_Persist.h in KFLOP Code (Thread1 and Thread2 code etc.)
    class PVConst
    {
        // Persist UserData variables used for status
        public const int P_STATUS           = 120;
        public const int P_STATUS_REPORT    = 104;
        public const int P_TLAUX_STATUS     = 121;
        public const int P_MPG_STATUS       = 122;
        public const int P_DEVICE_STATUS    = 123;
        public const int P_SERIAL_PENDING   = 124;
        public const int P_MSG_PTR          = 125;
        public const int P_MSG_PTR_H        = 126;
        public const int P_MPG_RESYNC       = 127;
        public const int P_SPINDLE_STATUS   = 128;
        public const int P_SPINDLE_RPM      = 129;

        // perisist UserData variables used for Thread 2 communications
        public const int P_NOTIFY           = 131;
        public const int P_NOTIFY_ARGUMENT  = 132;
        public const int P_NOTIFY_ARGUMENT2 = 133;
        public const int P_REMOTE_CMD       = 134;

        // Bit definitions of  P_STATUS and P_STATUS_REPORT 
        public const int SB_ACTIVE          = 0;
        public const int SB_ACTIVE_MASK     = 0x0001;
        public const int SB_ESTOP           = 1;
        public const int SB_ESTOP_MASK      = 0x0002;

        public const int SB_ERROR_STATUS_MASK = 0x000002fc;
        public const int SB_TLAUX_OK        = 2;
        public const int SB_TLAUX_PRES      = 3;
        public const int SB_MPG_OK          = 4;
        public const int SB_MPG_PRES        = 5;
        public const int SB_DEVICE_OK       = 6;
        public const int SB_DEVICE_PRES     = 7;
        public const int SB_AIR_OK          = 8;
        public const int SB_PWR_MODULE_OK   = 9;
        public const int SB_AXIS_OK         = 10;

        public const int SB_WARNING_STATUS_MASK = 0x0000f000;
        public const int SB_OIL_OK          = 11;
        public const int SB_HOME            = 12;
        public const int SB_LIMIT_MASK      = 0x0000e000;
        public const int SB_X_LIMIT         = 13;
        public const int SB_Y_LIMIT         = 14;
        public const int SB_Z_LIMIT         = 15;

        // Bit definitions of P_HOME_STATUS 
        public const int SB_HOME_STATUS_MASK = 0x00870000;
        public const int SB_X_HOME          = 16;
        public const int SB_Y_HOME          = 17;
        public const int SB_Z_HOME          = 18;
        public const int SB_A_HOME          = 19;
        public const int SB_SPIN_HOME       = 23;


    }

    class AXConst
    {
        public const int X_AXIS = 0;
        public const int Y_AXIS = 1;
        public const int Z_AXIS = 2;
        public const int A_AXIS = 3;
        public const int SPINDLE_AXIS = 7;

        public const int X_AXIS_MASK = 0x01;
        public const int Y_AXIS_MASK = 0x02;
        public const int Z_AXIS_MASK = 0x04;
        public const int A_AXIS_MASK = 0x08;
        public const int SPINDLE_AXIS_MASK = 0x0080;

    }

    // constants for the IO locations - see also BP308_IO.h in the KFLOP C code.
    class IOConst
    {
        // KANALOG Connections
        //
        // Inputs
        public const int ESTOP = 128;       // ESTOP Switch - OPTO_0
        public const int SPINDLE_FAULT = 129;       // Pins - need to double check the name here - OPTO_1
        public const int POWER_MODULE_READY = 130;  // - OPTO_2
        public const int AXIS_FAULT = 131;      // - OPTO_3
        public const int X_LIMIT = 132;     // Normally Closed - OPTO_4
        public const int X_HOME = 134;      // Normally Open - OPTO_6
        public const int Y_LIMIT = 133;     // Normally Closed - OPTO_5
        public const int Y_HOME = 135;      // Normally Open - OPTO_7
        public const int Z_LIMIT = 136;     // Normally Closed - OPTO_8
        public const int Z_HOME = 138;      // Normally Open - OPTO_10
        public const int OPTO_9 = 137;      // UNUSED - OPTO_9
        public const int HEAD_WHITE = 139;      // OPTO_11
        public const int HEAD_RED = 140;        // OPTO_12
        public const int HEAD_GREEN = 141;      // OPTO_13
        public const int HEAD_BLUE = 142;       // OPTO_14
        public const int HEAD_YELLOW = 143;     // OPTO_15
        public const int TOOL_RELEASE = 140;     // same as red wire.

        // Outputs
        // 24V FET drive
        public const int ESTOP_RELAY = 152;     // 24V FET 0
        public const int SPINDLE_ENABLE = 153;  // 24V FET 1
        public const int RELAY3 = 154;      // 24V FET 2
        public const int Z_BRAKE = 155;     // 24V FET 3
        public const int FET4 = 156;
        public const int FET5 = 157;
        public const int FET6 = 158;
        public const int FET7 = 159;

        // Isolated AC Control
        // the first four AC controls are 110V
        public const int OIL_LUBE = 160;        // AC0 Bijur oiler
        public const int FLOOD_MOTOR = 161;     // AC1 Flood coolant relay control
        public const int DOOR_FAN = 162;        // AC2 electronic cabinet door fan
        public const int AC3_110 = 163;     // available				
                                            // the other 4 AC controls are 220V
        public const int AC4_220 = 164;     // no triac installed
        public const int AC5_220 = 165;     // no triac installed
        public const int AC6_220 = 166;     // available
        public const int AC7_220 = 167;     // available

        // KONNECT Connections
        // 32 Inputs
        // Board 0 addresses
        // Extended IO
        public const int ENC_X_R = 1024;    // X_Axis Index
        public const int ENC_Y_R = 1025;    // Y_Axis Index
        public const int ENC_Z_R = 1026;    // Z_Axis Index
        public const int ENC_A_R = 1027;    // A_Axis Index - currently not connected
        public const int ENC_CH4_R = 1028;  // B_Axis Index - currently not connected
        public const int ENC_CH5_R = 1029;  // C_Axis Index - currently not connected
        public const int ENC_CH6_R = 1030;  // D_Axis Index - currently not connected
        public const int SPINDLE_R = 1031;  // Spindle Index
        public const int MPG_A = 1032;  //
        public const int MPG_B = 1033;  //
        public const int KON_IN_1034 = 1034;    //
        public const int KON_IN_1035 = 1035;    //
        public const int KON_IN_1036 = 1036;    //
        public const int KON_IN_1037 = 1037;    //
        public const int KON_IN_1038 = 1038;    //
        public const int KON_IN_1039 = 1039;    //
        public const int AUX_ISO_0 = 1040;  //
        public const int AUX_ISO_1 = 1041;  //
        public const int AUX_ISO_2 = 1042;  //
        public const int AUX_ISO_3 = 1043;  //
        public const int LUBE_MON = 1044;   // Lube Oil level
        public const int COOLANT_MON = 1045;    // Coolant motor relay monitor
        public const int AIR_MON = 1046;    // Air input PSI monitor
        public const int AUX_ISO_7 = 1047;  //
        public const int KON_IN_1048 = 1048;
        public const int KON_IN_1049 = 1049;
        public const int KON_IN_1050 = 1050;
        public const int KON_IN_1051 = 1051;
        public const int KON_IN_1052 = 1052;
        public const int KON_IN_1053 = 1053;
        public const int KON_IN_1054 = 1054;
        public const int KON_IN_1055 = 1055;

        // 16 Konnect outputs 
        // Board 0 addresses
        public const int KON_OUT_48 = 48;
        public const int KON_OUT_49 = 49;
        public const int KON_OUT_50 = 50;
        public const int KON_OUT_51 = 51;
        public const int KON_OUT_52 = 52;
        public const int KON_OUT_53 = 53;
        public const int KON_OUT_54 = 54;
        public const int KON_OUT_55 = 55;
        public const int KON_OUT_56 = 56;
        public const int KON_OUT_57 = 57;
        public const int KON_OUT_58 = 58;
        public const int KON_OUT_59 = 59;
        public const int KON_OUT_60 = 60;
        public const int KON_OUT_61 = 61;
        public const int KON_OUT_62 = 62;
        public const int KON_OUT_63 = 63;
        
    }

    class T2Const
    {

        // Thread 2 Notify Commands
        // the bottom 16 bits of the command is separated into two bytes
        // upper byte is the command, lower byte is the argument
        public const int CMD_MASK = 0xff00;
        public const int ARG_MASK = 0x00ff;
        //
        public const int T2_NO_CMD = 0;
        public const int T2_STATUS = 0x0100;
        public const int T2_TEST_MSG = 0x0200;
        public const int T2_SER_TEST_MSG = 0x0300;

        // Zeroing Commands - command byte 0x0400
        public const int T2_ZERO_AXIS = 0x0400;
        public const int T2_ZERO_X = 0x0400;
        public const int T2_ZERO_Y = 0x0401;
        public const int T2_ZERO_Z = 0x0402;
        public const int T2_ZERO_A = 0x0403;

        // Homing Commands - command byte 0x00800
        public const int T2_HOME_AXIS = 0x0800;
        public const int T2_HOME_X = 0x0801;
        public const int T2_HOME_Y = 0x0802;
        public const int T2_HOME_Z = 0x0803;
        public const int T2_HOME_A = 0x0804;
        public const int T2_HOME_SPINDLE = 0x0807;
        public const int T2_HOME_ALL = 0x080f;

        // Limit Commands   - command byte 0x0a00
        public const int T2_LIMIT_BACKOFF = 0x0a00;
        public const int T2_LIM_XP = 0x0a01;
        public const int T2_LIM_XN = 0x0a02;
        public const int T2_LIM_YP = 0x0a03;
        public const int T2_LIM_YN = 0x0a04;
        public const int T2_LIM_ZP = 0x0a05;
        public const int T2_LIM_ZN = 0x0a06;

        // Tool changer commands - command bytes 0x0c00, 0x0d00 and 0x0e00
        public const int T2_SEL_TOOL = 0x0c00;
        public const int T2_TOOL_CLAMP = 0x0d00;
        public const int T2_TOOL_AIR = 0x0d01;
        public const int T2_TOOL_RELA = 0x0d02;
        public const int T2_TOOL_REL = 0x0d03;
        public const int T2_TOOL_GRAB = 0x0d04;
        public const int T2_TOLL_ARM = 0x0E00;

        // Spindle commands - command byte 0x0500
        public const int T2_SPINDLE = 0x0500;
        public const int T2_SPINDLE_EN = 0x0501;
        public const int T2_SPINDLE_DIS = 0x0502;
        public const int T2_SPINDLE_CW = 0x0503;
        public const int T2_SPINDLE_CCW = 0x0504;
        public const int T2_SPINDLE_STOP = 0x0505;
        // #define T2_SPINDLE_HOME 0x0506 // - this was moved to the Homing section
        public const int T2_SPINDLE_ZERO = 0x0507;
        public const int T2_SPINDLE_PID = 0x0510;
        public const int T2_SPINDLE_RPM = 0x0511;
    }

    class BitOps
    {
        public int SetBit(int b)
        { return (1 << b); }

        public void SetBit(ref int a, int b)
        { a |= (1 << b); }

        public int ClearBit(int b)
        { return ~(1 << b); }

        public void ClearBit(ref int a, int b)
        { a &= ~(1 << b); }

        public bool BitIsSet(int var, int bit)  // if the bit number is set in var return true
        {
            if ((var & (1 << bit)) == 0) return false;
            return true;
        }

        public bool AnyInMask(int var, int mask)   // if any bit in the mask is set return true
        {
            if ((var & mask) == 0) return false;
            return true;
        }

        public bool AllInMask(int var, int mask) // if all bits in the mask are set return true otherwise false
        {
            if ((var & mask) == mask) return true;
            return false;
        }
    }
    
}
