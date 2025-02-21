using System;
using CommandLine;
using Gcodes.Ast;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HelicalPathGen
{
    /// <summary>
    /// Null means no change
    /// </summary>
    public class PointD(double? x, double? y, double? z, double? a, double? rate)
    {
        public double? X = x;
        public double? Y = y;
        public double? Z = z;
        public double? A = a;
        public bool Rapid = false;
        public double? FeedRate = rate;
    }

    internal class Program
    {
        private class Options
        {
            [Option('c', "cutting-config", Required = true, HelpText = "Machine configuration file, that contains feed rates, tool info etc")]
            public required string CuttingConfigFile { get; set; }
            [Option('s', "shape-config", Required = true, HelpText = "Target shape configuration file, contains target and stock dimensions")]
            public required string TargetShapeConfigFile { get; set; }
            [Option('o', "output", Required = false, HelpText = "Output GCode file path (or empty to use stdout)")]
            public string? GcodeOutputFile { get; set; }
            [Option('e', "generate-examples", Required = false, HelpText = "Generate markup example files on specified paths")]
            public bool GenerateExamples { get; set; } = false;
        }
        private enum ExitCodes
        {
            OK = 0,
            UnknownError,
            UnableToWriteExamples,
            UnableToDeserializeShape,
            UnknownShape,
            UnableToReadConfig,
            InterpolatorFailed,
            UnableToWriteOutput
        }

        public static IEnumerable<string> PointsToGcode(List<PointD> points)
        {
            yield return "G55";
            yield return "G10 L20 P0 X0 Y0 Z0 A0";
            foreach (var point in points)
            {
                var args = new List<Argument>();
                if (point.X != null) args.Add(new Argument(ArgumentKind.X, point.X.Value, new Gcodes.Tokens.Span()));
                if (point.Y != null) args.Add(new Argument(ArgumentKind.X, point.Y.Value, new Gcodes.Tokens.Span()));
                if (point.Z != null) args.Add(new Argument(ArgumentKind.X, point.Z.Value, new Gcodes.Tokens.Span()));
                if (point.A != null) args.Add(new Argument(ArgumentKind.X, point.A.Value, new Gcodes.Tokens.Span()));
                if (point.FeedRate != null)
                    args.Add(new Argument(ArgumentKind.FeedRate, point.FeedRate.Value, new Gcodes.Tokens.Span()));
                Gcode code = new Gcode(point.Rapid ? 0 : 1, args, new Gcodes.Tokens.Span());
                yield return code.ToString();
            }
            yield return "G54";
        }

        static int Main(string[] args)
        {
            Console.WriteLine("Custom shape GCode generator v0.1");
            ExitCodes exitCode = ExitCodes.UnknownError;
            CommandLine.Parser.Default.ParseArguments<Options>(args).WithParsed((o) =>
            {
                if (o.GenerateExamples)
                {
                    var cuttingExample = new CuttingParameters()
                    {
                        CutFeedRate = 12.0, //mm/min
                        EnableXYOffsetCompensation = false,
                        FastFeedRate = 300.0,
                        InitialXOffset = 0,
                        InitialYOffset = 0,
                        InitialZOffset = 0,
                        InstrumentDiameter = 4.0, //mm
                        LastPassCuttingDepth = 0.2, //mm
                        MaxCutDepth = 1.0 //mm
                    };
                    var shapeExample = new ShapeConfig()
                    {
                        Shape = Shapes.Helix,
                        Helix = new Helix()
                        {
                            Length = 50,
                            NumberOfTurns = 3,
                            StockDiameter = 35,
                            TargetCutDepth = 3,
                            TargetCutWidth = 6
                        }
                    };
                    var serializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
                    try
                    {
                        using TextWriter writerCuttingParams = new StreamWriter(o.CuttingConfigFile);
                        serializer.Serialize(writerCuttingParams, cuttingExample, typeof(CuttingParameters));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to write cutting config file!");
                        Console.WriteLine(ex);
                        exitCode = ExitCodes.UnableToWriteExamples;
                        return;
                    }
                    try
                    {
                        using TextWriter writerShapeParams = new StreamWriter(o.TargetShapeConfigFile);
                        serializer.Serialize(writerShapeParams, shapeExample, typeof(ShapeConfig));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to write shape config file!");
                        Console.WriteLine(ex);
                        exitCode = ExitCodes.UnableToWriteExamples;
                        return;
                    }
                    Console.WriteLine("Exmaples generated OK.");
                }
                else
                {
                    CuttingParameters cuttingParams;
                    ShapeConfig shapeParams;
                    try
                    {
                        var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
                        using TextReader readerCutParams = new StreamReader(o.CuttingConfigFile);
                        cuttingParams = deserializer.Deserialize<CuttingParameters>(readerCutParams);
                        using TextReader readerShapeParams = new StreamReader(o.TargetShapeConfigFile);
                        shapeParams = deserializer.Deserialize<ShapeConfig>(readerShapeParams);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to read config file(s)!");
                        Console.WriteLine(ex);
                        exitCode = ExitCodes.UnableToReadConfig;
                        return;
                    }
                    switch (shapeParams.Shape)
                    {
                        case Shapes.Helix:
                            if (shapeParams.Helix != null)
                            {
                                IEnumerable<string> lines;
                                try
                                {
                                    var interpolator = new HelicalRotaryInterpolator(shapeParams.Helix, cuttingParams);
                                    lines = PointsToGcode(interpolator.GetToolPath());
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Interpolator failed!");
                                    Console.WriteLine(ex);
                                    exitCode = ExitCodes.InterpolatorFailed;
                                    return;
                                }
                                try
                                {
                                    using TextWriter writer = o.GcodeOutputFile == null ?
                                        new StreamWriter(Console.OpenStandardOutput()) : new StreamWriter(o.GcodeOutputFile);
                                    foreach (var item in lines)
                                    {
                                        writer.WriteLine(item);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Unable to write GCode file!");
                                    Console.WriteLine(ex);
                                    exitCode = ExitCodes.UnableToWriteOutput;
                                    return;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Unable to deserialize Helix!");
                                exitCode = ExitCodes.UnableToDeserializeShape;
                                return;
                            }
                            break;
                        default:
                            Console.WriteLine("Unknown shape type!");
                            exitCode = ExitCodes.UnknownShape;
                            return;
                    }
                }
                exitCode = ExitCodes.OK;
            });
            return (int)exitCode;
        }
    }
}