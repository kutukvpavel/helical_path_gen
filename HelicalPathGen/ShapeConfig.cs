using System;
using YamlDotNet.Serialization;

namespace HelicalPathGen
{
    [YamlSerializable]
    public enum Shapes
    {
        None,
        Helix
    }

    [YamlSerializable]
    public class ShapeConfig
    {
        [YamlMember]
        public Shapes Shape { get; set; } = Shapes.None;
        [YamlMember]
        public Helix? Helix { get; set; }
    }
}