using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KMotion_dotNet;

namespace KFLOP_Test3
{
    public class MotionParams_Copy
    {
        public MotionParams_Copy()
        {

        }

        public bool ArcsToSegs { get; set; }
        public double BreakAngle { get; set; }
        public double CollinearTolerance { get; set; }
        public double CornerTolerance { get; set; }
        public double CountsPerInchA { get; set; }
        public double CountsPerInchB { get; set; }
        public double CountsPerInchC { get; set; }
        public double CountsPerInchX { get; set; }
        public double CountsPerInchY { get; set; }
        public double CountsPerInchZ { get; set; }
        public bool DegreesA { get; set; }
        public bool DegreesB { get; set; }
        public bool DegreesC { get; set; }
        public double FacetAngle { get; set; }
        public double MaxAccelA { get; set; }
        public double MaxAccelB { get; set; }
        public double MaxAccelC { get; set; }
        public double MaxAccelX { get; set; }
        public double MaxAccelY { get; set; }
        public double MaxAccelZ { get; set; }
        public double MaxLinearLength { get; set; }
        public double MaxVelA { get; set; }
        public double MaxVelB { get; set; }
        public double MaxVelC { get; set; }
        public double MaxVelX { get; set; }
        public double MaxVelY { get; set; }
        public double MaxVelZ { get; set; }
        public double RadiusA { get; set; }
        public double RadiusB { get; set; }
        public double RadiusC { get; set; }
        public double TPLookahead { get; set; }
        public bool UseOnlyLinearSegments { get; set; }

        // method to copy the parameters to the KMotion controller class.
        public void CopyParams(KM_MotionParams X)
        {
            X.BreakAngle = BreakAngle;
            X.CollinearTolerance = CollinearTolerance;
            X.CornerTolerance = CornerTolerance;
            X.FacetAngle = FacetAngle;
            X.TPLookahead = TPLookahead;
            X.RadiusA = RadiusA;
            X.RadiusB = RadiusB;
            X.RadiusC = RadiusC;
            X.MaxAccelA = MaxAccelA;
            X.MaxAccelB = MaxAccelB;
            X.MaxAccelC = MaxAccelC;
            X.MaxAccelX = MaxAccelX;
            X.MaxAccelY = MaxAccelY;
            X.MaxAccelZ = MaxAccelZ;
            X.MaxVelA = MaxVelA;
            X.MaxVelB = MaxVelB;
            X.MaxVelC = MaxVelC;
            X.MaxVelX = MaxVelX;
            X.MaxVelY = MaxVelY;
            X.MaxVelZ = MaxVelZ;
            X.CountsPerInchA = CountsPerInchA;
            X.CountsPerInchB = CountsPerInchB;
            X.CountsPerInchC = CountsPerInchC;
            X.CountsPerInchX = CountsPerInchX;
            X.CountsPerInchY = CountsPerInchY;
            X.CountsPerInchZ = CountsPerInchZ;
            X.MaxLinearLength = MaxLinearLength;
            X.UseOnlyLinearSegments = UseOnlyLinearSegments;
            X.ArcsToSegs = ArcsToSegs;
            X.DegreesA = DegreesA;
            X.DegreesB = DegreesB;
            X.DegreesC = DegreesC;
        }
    }
}
