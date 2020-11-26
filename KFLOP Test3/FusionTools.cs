using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFLOP_Test3
{
    // cut and pasted (Paste Special) straight from the fusion360 exported json file! - Awesome!
    public class FusionTools
    {
        public Datum[] data { get; set; }
        public int version { get; set; }
    }

    public class Datum
    {
        public string BMC { get; set; }
        public string GRADE { get; set; }
        public string description { get; set; }
        public Geometry geometry { get; set; }
        public string guid { get; set; }
        public Holder holder { get; set; }
        public long last_modified { get; set; }
        public PostProcess postprocess { get; set; }
        public string productid { get; set; }
        public string productlink { get; set; }
        public StartValues startvalues { get; set; }
        public string type { get; set; }
        public string unit { get; set; }
        public string vendor { get; set; }
        public string reference_guid { get; set; }
        public string taperedtype { get; set; }
    }

    public class Geometry
    {
        public bool CSP { get; set; }
        public float DC { get; set; }
        public bool HAND { get; set; }
        public float LB { get; set; }
        public float LCF { get; set; }
        public int NOF { get; set; }
        public int NT { get; set; }
        public float OAL { get; set; }
        public float RE { get; set; }
        public float SFDM { get; set; }
        public int SIG { get; set; }
        public int TA { get; set; }
        public float TP { get; set; }
        public float shoulderlength { get; set; }
        public int threadprofileangle { get; set; }
        public float tipdiameter { get; set; }
        public int tiplength { get; set; }
        public int tipoffset { get; set; }
    }

    public class Holder
    {
        public string description { get; set; }
        public string guid { get; set; }
        public long last_modified { get; set; }
        public string productid { get; set; }
        public Segment[] segments { get; set; }
        public string type { get; set; }
        public string unit { get; set; }
        public string vendor { get; set; }
    }

    public class Segment
    {
        public float height { get; set; }
        public float lowerdiameter { get; set; }
        public float upperdiameter { get; set; }
    }

    public class PostProcess
    {
        public bool breakcontrol { get; set; }
        public string comment { get; set; }
        public int diameteroffset { get; set; }
        public int lengthoffset { get; set; }
        public bool live { get; set; }
        public bool manualtoolchange { get; set; }
        public int number { get; set; }
        public int turret { get; set; }
    }

    public class StartValues
    {
        public Preset[] presets { get; set; }
    }

    public class Preset
    {
        public string description { get; set; }
        public float f_n { get; set; }
        public float f_z { get; set; }
        public string guid { get; set; }
        public int n { get; set; }
        public int n_ramp { get; set; }
        public string name { get; set; }
        public string toolcoolant { get; set; }
        public bool usestepdown { get; set; }
        public bool usestepover { get; set; }
        public float v_c { get; set; }
        public int v_f { get; set; }
        public int v_f_leadIn { get; set; }
        public int v_f_leadOut { get; set; }
        public float v_f_plunge { get; set; }
        public float v_f_ramp { get; set; }
        public float v_f_retract { get; set; }
    }
}

