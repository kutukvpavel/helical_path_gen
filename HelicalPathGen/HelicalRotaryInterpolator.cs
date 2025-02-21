using System;

namespace HelicalPathGen
{
    /// <summary>
    /// In contrast to non-rotary helical interpolation, introduction of a rotary axis
    /// makes the motion linear, so this is not an interpolator per se, but a 'simple' GCode generator.
    /// </summary>
    /// <param name="h"></param>
    /// <param name="p"></param>
    public class HelicalRotaryInterpolator(Helix h, CuttingParameters p)
    {
        private double GetFeedRate(double cutDiameter)
        {
            double l2 = TargetShape.Length * TargetShape.Length;
            double n2 = TargetShape.NumberOfTurns * TargetShape.NumberOfTurns;
            return Parameters.CutFeedRate * Math.Sqrt(
                (l2 + 129600.0 * n2) / (l2 + n2 * Math.PI * Math.PI * cutDiameter * cutDiameter)
            );
        }

        public Helix TargetShape { get; } = h;
        public CuttingParameters Parameters { get; } = p;

        /// <summary>
        /// Calculates pivot points for linear interpolation by the CNC machine itself (G1).
        /// </summary>
        /// <returns>An array of points to be processed by an external Gcode generator</returns>
        public List<PointD> GetToolPath()
        {
            //Sanity checks
            if (TargetShape.TargetCutWidth < Parameters.InstrumentDiameter)
                throw new ArgumentOutOfRangeException(nameof(TargetShape.TargetCutWidth));
            if ((TargetShape.TargetCutDepth * 2) > TargetShape.StockDiameter)
                throw new ArgumentOutOfRangeException(nameof(TargetShape.TargetCutDepth));
            if ((TargetShape.NumberOfTurns * TargetShape.TargetCutWidth) > TargetShape.Length)
                throw new ArgumentOutOfRangeException(nameof(TargetShape.NumberOfTurns));
            if (Parameters.LastPassCuttingDepth > Parameters.MaxCutDepth)
                throw new ArgumentOutOfRangeException(nameof(Parameters.LastPassCuttingDepth));

            //Calculate motion parameters
            //Leave room for the last fine cut and spread the rest of cutting distance evenly between rough cuts
            int zRoughPasses = (int)Math.Ceiling(
                (TargetShape.TargetCutDepth - Parameters.LastPassCuttingDepth) / Parameters.MaxCutDepth);
            int yRoughPasses = (int)Math.Ceiling(
                (TargetShape.TargetCutWidth - Parameters.LastPassCuttingDepth) / Parameters.MaxCutDepth);
            double zRoughStep = (TargetShape.TargetCutDepth - Parameters.LastPassCuttingDepth) / zRoughPasses;
            double yRoughStep = (TargetShape.TargetCutWidth - Parameters.LastPassCuttingDepth * 2) / yRoughPasses;
            //Cutting feed rate is a vector sum of axial and tangential velocities (see mathematica nb),
            //that get translated into X and A feed rates, respectively. They both change as the cut diameter gets smaller.
            double feedRate = GetFeedRate(TargetShape.StockDiameter);
            double aTarget = 360 * TargetShape.NumberOfTurns; //Assumes the axis is calibrated in degrees

            //Initialize tracking variables
            double currentX = Parameters.EnableXYOffsetCompensation ? Parameters.InitialXOffset : 0;
            double currentY = Parameters.EnableXYOffsetCompensation ? Parameters.InitialYOffset : 0;
            double currentZ = Parameters.InitialZOffset;
            List<PointD> points = new List<PointD>();

            //Move to the surface of the stock
            if (Parameters.EnableXYOffsetCompensation)
            {
                currentX = -currentX - Parameters.InstrumentDiameter / 2;
                currentY = -currentY - Parameters.InstrumentDiameter / 2 - TargetShape.StockDiameter / 2;
            }
            currentZ = -currentZ;
            points.Add(new PointD(currentX, currentY, currentZ, 0, null) { Rapid = true });

            //Start rough cutting
            int totalYPasses = 0;
            double centerY = currentY;
            for (int i = 0; i < zRoughPasses; i++)
            {
                currentZ -= zRoughStep;
                feedRate = GetFeedRate(TargetShape.StockDiameter - zRoughStep * (i + 1));
                points.Add(new PointD(null, null, currentZ, null, Parameters.CutFeedRate));
                for (int j = 0; j < yRoughPasses; j++)
                {
                    if (totalYPasses++ % 2 == 0)
                    {
                        currentY = centerY - yRoughStep * j;
                        points.Add(new PointD(null, currentY, null, null, feedRate));
                        points.Add(new PointD(TargetShape.Length, null, null, aTarget, feedRate));
                    }
                    else
                    {
                        currentY = centerY + yRoughStep * j;
                        points.Add(new PointD(null, currentY, null, null, feedRate));
                        points.Add(new PointD(currentX, null, null, 0, feedRate));
                    }
                }
            }

            //Start fine cutting: Y requires 2 sides of the "channel" to be finished, while Z requies only single elevation change
            currentZ -= Parameters.LastPassCuttingDepth;
            if (totalYPasses % 2 == 0)
                currentY = centerY - TargetShape.TargetCutWidth / 2;
            else
                currentY = centerY + TargetShape.TargetCutWidth / 2;
            points.Add(new PointD(null, currentY, currentZ, null, feedRate));
            if (totalYPasses++ % 2 == 0)
            {
                points.Add(new PointD(TargetShape.Length, null, null, aTarget, feedRate));
                currentY = centerY + TargetShape.TargetCutWidth / 2;
            }
            else
            {
                points.Add(new PointD(currentX, null, null, 0, feedRate));
                currentY = centerY - TargetShape.TargetCutWidth / 2;
            }
            points.Add(new PointD(null, currentY, null, null, feedRate));
            if (totalYPasses++ % 2 == 0)
                points.Add(new PointD(TargetShape.Length, null, null, aTarget, feedRate));
            else
                points.Add(new PointD(currentX, null, null, 0, feedRate));
            
            //Extract the instrument and go to 0
            points.Add(new PointD(null, null, 0, null, null) { Rapid = true });
            points.Add(new PointD(0, 0, 0, 0, null) { Rapid = true });

            return points;
        }
    }
}