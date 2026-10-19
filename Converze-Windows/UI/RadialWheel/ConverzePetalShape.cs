using System;
using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Converze.UI.RadialWheel
{
    // MARK: - Converze Radial Wheel Geometry & Polar Math

    public static class ConverzeWheelMetrics
    {
        public const double InnerRadius = 48.64;
        public const double OuterRadius = 119.52;
        public const double BackdropRadius = 125.92;
        public const double ContainerSize = 272.0;
        public const double RingInset = 10.08;
        public const double GapWidth = 4.0;
        public const double CornerRadius = 20.16;
        public const double LabelInnerRadius = 40.0;
        public const double CenterHoleRadius = 42.24;

        /// <summary>
        /// Center angle (radians) for petal index out of count.
        /// Starts at top (pi/2) and proceeds clockwise.
        /// </summary>
        public static double CenterAngle(int index, int count)
        {
            if (count <= 0) return 0;
            double startAngle = (count == 3) ? Math.PI : (Math.PI / 2.0);
            return startAngle - (2.0 * Math.PI * index) / count;
        }

        /// <summary>
        /// Polar to Cartesian conversion using top-down screen coordinates (+Y = DOWN).
        /// </summary>
        public static Point PolarPoint(double cx, double cy, double radius, double angle)
        {
            return new Point(
                cx + radius * Math.Cos(angle),
                cy - radius * Math.Sin(angle)
            );
        }

        /// <summary>
        /// Label position at midpoint of a slice.
        /// </summary>
        public static Point LabelOffset(int index, int count, double innerRadius = LabelInnerRadius, double outerRadius = OuterRadius)
        {
            double midRadius = (innerRadius + outerRadius) * 0.5;
            double angle = CenterAngle(index, count);
            return new Point(
                midRadius * Math.Cos(angle),
                -midRadius * Math.Sin(angle)
            );
        }

        /// <summary>
        /// Hit-test a pointer offset (dx, dy) from wheel center into a slice index (or null if in center hole / outside).
        /// Uses top-down screen coordinates (+Y = DOWN).
        /// </summary>
        public static int? HitTestSlice(double dx, double dy, int count)
        {
            if (count <= 0) return null;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < CenterHoleRadius || dist > OuterRadius + 38.0)
            {
                return null;
            }

            double pointerAngle = Math.Atan2(-dy, dx);
            int bestIndex = 0;
            double smallestDiff = double.MaxValue;

            for (int i = 0; i < count; i++)
            {
                double sliceAngle = CenterAngle(i, count);
                double diff = Math.Atan2(Math.Sin(pointerAngle - sliceAngle), Math.Cos(pointerAngle - sliceAngle));
                double absDiff = Math.Abs(diff);
                if (absDiff < smallestDiff)
                {
                    smallestDiff = absDiff;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }
    }

    // MARK: - Rounded Wedge Petal Geometry Builder

    public static class ConverzePetalGeometry
    {
        public static StreamGeometry Build(
            int index,
            int count,
            double width,
            double height,
            double innerRadius = ConverzeWheelMetrics.InnerRadius,
            double outerRadius = ConverzeWheelMetrics.OuterRadius,
            double gap = ConverzeWheelMetrics.GapWidth,
            double cornerRadius = ConverzeWheelMetrics.CornerRadius)
        {
            var geom = new StreamGeometry();
            if (count <= 0) return geom;

            double cx = width * 0.5;
            double cy = height * 0.5;

            double sliceSpan = (2.0 * Math.PI) / count;
            double midAngle = ConverzeWheelMetrics.CenterAngle(index, count);
            double startAngle = midAngle - sliceSpan * 0.5;
            double endAngle = midAngle + sliceSpan * 0.5;

            double radialThickness = outerRadius - innerRadius;
            double outerChord = 2.0 * outerRadius * Math.Sin(sliceSpan * 0.5);
            double innerChord = 2.0 * innerRadius * Math.Sin(sliceSpan * 0.5);
            double maxAllowedCorner = Math.Min(radialThickness * 0.45, Math.Min(outerChord, innerChord) * 0.36);

            double outerCorner = Math.Min(cornerRadius, maxAllowedCorner);
            double innerCorner = Math.Min(cornerRadius * 0.45, maxAllowedCorner * 0.55);

            double halfGap = gap * 0.5;
            double innerGapAngle = Math.Asin(Math.Min(0.95, halfGap / Math.Max(1.0, innerRadius)));
            double outerGapAngle = Math.Asin(Math.Min(0.95, halfGap / Math.Max(1.0, outerRadius)));

            double innerCornerAngle = innerCorner / Math.Max(1.0, innerRadius);
            double outerCornerAngle = outerCorner / Math.Max(1.0, outerRadius);

            double edgeInnerInset = Math.Asin(Math.Min(0.95, halfGap / Math.Max(1.0, innerRadius + innerCorner)));
            double edgeOuterInset = Math.Asin(Math.Min(0.95, halfGap / Math.Max(1.0, outerRadius - outerCorner)));

            Point pInnerStart = ConverzeWheelMetrics.PolarPoint(cx, cy, innerRadius, startAngle + innerGapAngle + innerCornerAngle);
            Point pInnerEnd   = ConverzeWheelMetrics.PolarPoint(cx, cy, innerRadius, endAngle - innerGapAngle - innerCornerAngle);

            Point ctrlInnerEnd = ConverzeWheelMetrics.PolarPoint(cx, cy, innerRadius, endAngle - innerGapAngle);
            Point pEdgeEndInner = ConverzeWheelMetrics.PolarPoint(cx, cy, innerRadius + innerCorner, endAngle - edgeInnerInset);

            Point pEdgeEndOuter = ConverzeWheelMetrics.PolarPoint(cx, cy, outerRadius - outerCorner, endAngle - edgeOuterInset);
            Point ctrlOuterEnd  = ConverzeWheelMetrics.PolarPoint(cx, cy, outerRadius, endAngle - outerGapAngle);
            Point pOuterEnd     = ConverzeWheelMetrics.PolarPoint(cx, cy, outerRadius, endAngle - outerGapAngle - outerCornerAngle);

            Point pOuterStart    = ConverzeWheelMetrics.PolarPoint(cx, cy, outerRadius, startAngle + outerGapAngle + outerCornerAngle);
            Point ctrlOuterStart = ConverzeWheelMetrics.PolarPoint(cx, cy, outerRadius, startAngle + outerGapAngle);
            Point pEdgeStartOuter = ConverzeWheelMetrics.PolarPoint(cx, cy, outerRadius - outerCorner, startAngle + edgeOuterInset);

            Point pEdgeStartInner = ConverzeWheelMetrics.PolarPoint(cx, cy, innerRadius + innerCorner, startAngle + edgeInnerInset);
            Point ctrlInnerStart  = ConverzeWheelMetrics.PolarPoint(cx, cy, innerRadius, startAngle + innerGapAngle);

            using (StreamGeometryContext ctx = geom.Open())
            {
                ctx.BeginFigure(pInnerStart, isFilled: true, isClosed: true);

                // 1. Inner arc from startAngle -> endAngle (counter-clockwise in top-down screen space)
                DrawPolarArc(
                    ctx,
                    cx, cy,
                    innerRadius,
                    startAngle + innerGapAngle + innerCornerAngle,
                    endAngle - innerGapAngle - innerCornerAngle,
                    steps: 18);

                // 2. Inner-end rounded corner -> radial outer line -> outer-end rounded corner
                ctx.QuadraticBezierTo(ctrlInnerEnd, pEdgeEndInner, isStroked: true, isSmoothJoin: true);
                ctx.LineTo(pEdgeEndOuter, isStroked: true, isSmoothJoin: true);
                ctx.QuadraticBezierTo(ctrlOuterEnd, pOuterEnd, isStroked: true, isSmoothJoin: true);

                // 3. Outer arc from endAngle -> startAngle (clockwise in top-down screen space)
                DrawPolarArc(
                    ctx,
                    cx, cy,
                    outerRadius,
                    endAngle - outerGapAngle - outerCornerAngle,
                    startAngle + outerGapAngle + outerCornerAngle,
                    steps: 28);

                // 4. Outer-start rounded corner -> radial inner line -> inner-start rounded corner
                ctx.QuadraticBezierTo(ctrlOuterStart, pEdgeStartOuter, isStroked: true, isSmoothJoin: true);
                ctx.LineTo(pEdgeStartInner, isStroked: true, isSmoothJoin: true);
                ctx.QuadraticBezierTo(ctrlInnerStart, pInnerStart, isStroked: true, isSmoothJoin: true);
            }

            geom.Freeze();
            return geom;
        }

        private static void DrawPolarArc(
            StreamGeometryContext ctx,
            double cx,
            double cy,
            double radius,
            double fromAngle,
            double toAngle,
            int steps)
        {
            for (int s = 1; s <= steps; s++)
            {
                double t = (double)s / steps;
                double a = fromAngle + (toAngle - fromAngle) * t;
                Point pt = ConverzeWheelMetrics.PolarPoint(cx, cy, radius, a);
                ctx.LineTo(pt, isStroked: true, isSmoothJoin: true);
            }
        }
    }

    // MARK: - Donut Frosted Backdrop Ring Geometry (Outer Circle Minus Center Cutout)

    public static class DonutRingGeometry
    {
        public static Geometry Build(double width, double height, double holeRadius)
        {
            Point center = new Point(width * 0.5, height * 0.5);
            double outerR = Math.Min(width, height) * 0.5;

            var group = new GeometryGroup
            {
                FillRule = FillRule.EvenOdd
            };
            group.Children.Add(new EllipseGeometry(center, outerR, outerR));
            group.Children.Add(new EllipseGeometry(center, holeRadius, holeRadius));
            group.Freeze();
            return group;
        }
    }
}
