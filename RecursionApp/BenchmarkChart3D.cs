using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RecursionApp
{
    /// <summary>
    /// Draws a 3D projection of benchmark results without adding a chart package.
    /// X is input size, Y is mean execution time, and Z is standard deviation.
    /// </summary>
    public static class BenchmarkChart3D
    {
        static readonly Brush AxisBrush = Brush("#64748B");
        static readonly Brush GridBrush = Brush("#E2E8F0");
        static readonly Brush ExperimentBrush = Brush("#2F6FED");
        static readonly Brush TheoryBrush = Brush("#F97316");
        static readonly Brush TextBrush = Brush("#334155");

        static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color);

        public static void Draw(Canvas canvas, IReadOnlyList<ExperimentResult> results,
                                IReadOnlyList<double> theoryTimes, double factor, string unit,
                                double yaw = -0.62, double pitch = 0.48)
        {
            canvas.Children.Clear();
            if (results == null || results.Count == 0) return;

            double width = canvas.ActualWidth > 200 ? canvas.ActualWidth : 760;
            double height = canvas.ActualHeight > 200 ? canvas.ActualHeight : 480;
            // Reserve room for the rotated Y-axis title and its tick labels.
            double left = 112;
            double top = 70;
            double right = width > 780 ? 230 : 38;
            double bottom = 62;
            double plotWidth = Math.Max(250, width - left - right - 30);
            double plotHeight = Math.Max(190, height - top - bottom - 35);
            double depth = Math.Min(150, plotWidth * 0.42);

            double cosYaw = Math.Cos(yaw), sinYaw = Math.Sin(yaw);
            double cosPitch = Math.Cos(pitch), sinPitch = Math.Sin(pitch);

            Point ProjectRaw(double x, double y, double z)
            {
                double dx = (x - 0.5) * plotWidth;
                double dy = (y - 0.5) * plotHeight;
                double dz = (z - 0.5) * depth;
                double rotatedX = dx * cosYaw - dz * sinYaw;
                double rotatedZ = dx * sinYaw + dz * cosYaw;
                double rotatedY = dy * cosPitch - rotatedZ * sinPitch;
                return new Point(rotatedX, rotatedY);
            }

            double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
            double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
            for (int ix = 0; ix <= 1; ix++)
                for (int iy = 0; iy <= 1; iy++)
                    for (int iz = 0; iz <= 1; iz++)
                    {
                        Point corner = ProjectRaw(ix, iy, iz);
                        minX = Math.Min(minX, corner.X); maxX = Math.Max(maxX, corner.X);
                        minY = Math.Min(minY, corner.Y); maxY = Math.Max(maxY, corner.Y);
                    }

            double availableWidth = Math.Max(220, width - left - right);
            double availableHeight = Math.Max(180, height - top - bottom);
            double scale = Math.Min(availableWidth / Math.Max(1, maxX - minX),
                                    availableHeight / Math.Max(1, maxY - minY));
            double offsetX = left + (availableWidth - (maxX - minX) * scale) / 2 - minX * scale;
            double offsetY = top + (availableHeight - (maxY - minY) * scale) / 2 + maxY * scale;

            int nMin = results.Min(r => r.N);
            int nMax = results.Max(r => r.N);
            double maxMean = results.Max(r => r.AvgTimeMs * factor);
            double maxTheory = theoryTimes == null || theoryTimes.Count == 0
                ? 0 : theoryTimes.Max(v => v * factor);
            double maxTime = Math.Max(maxMean, maxTheory);
            if (maxTime <= 0) maxTime = 1;
            maxTime *= 1.08;

            double maxStd = results.Max(r => r.StdDevMs * factor);
            if (maxStd <= 0) maxStd = 1;
            maxStd *= 1.08;

            Point Project(double x, double y, double z)
            {
                Point point = ProjectRaw(x, y, z);
                return new Point(offsetX + point.X * scale, offsetY - point.Y * scale);
            }

            AddText(canvas, "Замеры времени в 3D", left, 16, 19, TextBrush, true);
            AddText(canvas, "Перетаскивайте график левой кнопкой мыши, чтобы вращать. X - вход, Y - время, Z - σ",
                    left, 41, 12, TextBrush);

            // Horizontal time grid and depth grid form the floor of the 3D plot.
            for (int i = 0; i <= 4; i++)
            {
                double t = i / 4.0;
                Segment(canvas, Project(0, t, 0), Project(1, t, 0), GridBrush, 1);
                Segment(canvas, Project(0, 0, t), Project(1, 0, t), GridBrush, 1);
                Segment(canvas, Project(0, 0, t), Project(0, 1, t), GridBrush, 1);
                Point yTick = Project(0, t, 0);
                AddText(canvas, (maxTime * t).ToString("0.###", CultureInfo.CurrentCulture),
                        yTick.X + 34, yTick.Y - 7, 10, TextBrush, false, 58);
                Point zTick = Project(1, 0, t);
                Segment(canvas, zTick, new Point(zTick.X + 3, zTick.Y - 3), AxisBrush, 1);
                AddText(canvas, (maxStd * t).ToString("0.###", CultureInfo.CurrentCulture),
                        zTick.X - 58, zTick.Y - 7, 10, TextBrush, false, 54);
            }

            // Three coordinate axes.
            Segment(canvas, Project(0, 0, 0), Project(1, 0, 0), AxisBrush, 2);
            Segment(canvas, Project(0, 0, 0), Project(0, 1, 0), AxisBrush, 2);
            Segment(canvas, Project(1, 0, 0), Project(1, 0, 1), AxisBrush, 2);

            for (int i = 0; i <= 4; i++)
            {
                double t = i / 4.0;
                int n = (int)Math.Round(nMin + (nMax - nMin) * t);
                Point p = Project(t, 0, 0);
                Segment(canvas, p, new Point(p.X, p.Y + 5), AxisBrush, 1);
                AddText(canvas, n.ToString("N0", CultureInfo.CurrentCulture), p.X - 18, p.Y + 7, 10, TextBrush);
            }

            Point xEnd = Project(1, 0, 0);
            Point origin = Project(0, 0, 0);
            Point yEnd = Project(0, 1, 0);
            Point zEnd = Project(1, 0, 1);
            AddText(canvas, "n", xEnd.X - 10, xEnd.Y + 20, 13, TextBrush, true);
            const double timeLabelWidth = 180;
            const double timeLabelHeight = 18;
            double yAxisDx = yEnd.X - origin.X;
            double yAxisDy = yEnd.Y - origin.Y;
            double yAxisLength = Math.Max(1, Math.Sqrt(yAxisDx * yAxisDx + yAxisDy * yAxisDy));
            double labelCenterX = (origin.X + yEnd.X) / 2 + yAxisDy / yAxisLength * 68;
            double labelCenterY = (origin.Y + yEnd.Y) / 2 - yAxisDx / yAxisLength * 68;
            TextBlock timeLabel = AddText(canvas, "Среднее время, " + unit,
                labelCenterX - timeLabelWidth / 2, labelCenterY - timeLabelHeight / 2,
                12, TextBrush, true, timeLabelWidth);
            timeLabel.Height = timeLabelHeight;
            timeLabel.RenderTransformOrigin = new Point(0.5, 0.5);
            timeLabel.RenderTransform = new RotateTransform(
                Math.Atan2(yAxisDy, yAxisDx) * 180 / Math.PI);
            AddText(canvas, "Ст. отклонение, " + unit, zEnd.X + 14, zEnd.Y + 14,
                    11, TextBrush, true, 170);

            Point? previousExperiment = null;
            Point? previousTheory = null;
            for (int i = 0; i < results.Count; i++)
            {
                ExperimentResult row = results[i];
                double x = nMax == nMin ? 0.5 : (row.N - nMin) / (double)(nMax - nMin);
                Point experiment = Project(x, row.AvgTimeMs * factor / maxTime,
                                           row.StdDevMs * factor / maxStd);
                if (previousExperiment.HasValue)
                    Segment(canvas, previousExperiment.Value, experiment, ExperimentBrush, 2.6);
                Circle(canvas, experiment, 4.5, ExperimentBrush);
                previousExperiment = experiment;

                if (theoryTimes != null && i < theoryTimes.Count)
                {
                    Point theory = Project(x, theoryTimes[i] * factor / maxTime, 0);
                    if (previousTheory.HasValue)
                        Segment(canvas, previousTheory.Value, theory, TheoryBrush, 2.2);
                    previousTheory = theory;
                }
            }

            double legendX = width - right + 14;
            double legendY = top + 24;
            Segment(canvas, new Point(legendX, legendY), new Point(legendX + 28, legendY), ExperimentBrush, 3);
            Circle(canvas, new Point(legendX + 14, legendY), 4, ExperimentBrush);
            AddText(canvas, "Эксперимент", legendX + 36, legendY - 8, 11, TextBrush);
            Segment(canvas, new Point(legendX, legendY + 28), new Point(legendX + 28, legendY + 28), TheoryBrush, 2.2);
            AddText(canvas, "Теория", legendX + 36, legendY + 20, 11, TextBrush);
            AddText(canvas, "Ось Z показывает разброс повторных замеров.", legendX, legendY + 58,
                    10, TextBrush, false, 145);
            AddText(canvas, "Повторов на точку: " + results[0].Runs, legendX, legendY + 96,
                    10, TextBrush);
        }

        static void Segment(Canvas canvas, Point a, Point b, Brush brush, double thickness)
        {
            canvas.Children.Add(new Line
            {
                X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y,
                Stroke = brush, StrokeThickness = thickness,
                SnapsToDevicePixels = true
            });
        }

        static void Circle(Canvas canvas, Point center, double radius, Brush fill)
        {
            Ellipse dot = new Ellipse
            {
                Width = radius * 2, Height = radius * 2,
                Fill = fill, Stroke = Brushes.White, StrokeThickness = 1
            };
            Canvas.SetLeft(dot, center.X - radius);
            Canvas.SetTop(dot, center.Y - radius);
            canvas.Children.Add(dot);
        }

        static TextBlock AddText(Canvas canvas, string text, double x, double y,
                                 double size, Brush brush, bool bold = false, double width = double.NaN)
        {
            TextBlock label = new TextBlock
            {
                Text = text, FontSize = size, Foreground = brush,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                TextWrapping = double.IsNaN(width) ? TextWrapping.NoWrap : TextWrapping.Wrap,
                Width = width
            };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);
            canvas.Children.Add(label);
            return label;
        }
    }
}
