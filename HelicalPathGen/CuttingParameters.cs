using System;
using YamlDotNet.Serialization;

namespace HelicalPathGen
{
    [YamlSerializable]
    public class CuttingParameters
    {
        //Machine Units: mm, mm/min

        public double CutFeedRate { get; set; }
        public double FastFeedRate { get; set; }
        public double FastFeedRateZ { get; set; }
        public double MaxCutDepth { get; set; }
        public double InstrumentDiameter { get; set; }
        public double InitialZOffset { get; set; } //Instrument tip from the cylinder stock surface
        public bool EnableXYOffsetCompensation { get; set; } = false;
        public double InitialYOffset { get; set; } //Instrument edge from the cylinder stock edge
        public double InitialXOffset { get; set; } //Instrument edge from the desired cut start (center) point
        public double LastPassCuttingDepth { get; set; }
    }
}