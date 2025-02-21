using System;
using YamlDotNet.Serialization;

namespace HelicalPathGen
{
    [YamlSerializable]
    public class Helix
    {
        // Machine units: mm

        public double Length { get; set; }
        public double StockDiameter { get; set; }
        public double NumberOfTurns { get; set; }
        public double TargetCutDepth { get; set; }
        public double TargetCutWidth { get; set; }
    }
}