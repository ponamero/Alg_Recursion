using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace RecursionApp
{
    public partial class MainWindow : Window
    {
        // ---------- Цвета (те же, что в App.xaml) ----------
        static Brush Hex(string color)
        {
            return (Brush)new BrushConverter().ConvertFromString(color);
        }

        Brush accent = Hex("#2F6FED");
        Brush accentLight = Hex("#EAF1FE");
        Brush green = Hex("#12B76A");
        Brush greenLight = Hex("#E7F9EF");
        Brush gray = Hex("#6B7280");
        Brush lightGray = Hex("#C9CED6");
        Brush dark = Hex("#334155");

        // ---------- Поля для ханойских башен ----------
        List<int>[] rods = { new List<int>(), new List<int>(), new List<int>() }; // кольца на стержнях, последнее - верхнее
        List<Move> moves = new List<Move>();   // все ходы, считаем заранее
        int moveIndex = 0;                     // сколько ходов уже сделано
        int ringsCount = 8;
        DispatcherTimer hanoiTimer = new DispatcherTimer();

        Brush[] ringColors =
        {
            Hex("#2F6FED"), Hex("#F97316"), Hex("#10B981"), Hex("#A855F7"), Hex("#EAB308"),
            Hex("#EF4444"), Hex("#06B6D4"), Hex("#EC4899"), Hex("#84CC16"), Hex("#6366F1")
        };

        // ---------- Поля для фрактала ----------
        int carpetCalls = 0;
        int carpetHoles = 0;

        // ---------- Поля для вкладки "Рекурсивные алгоритмы" ----------
        int algoShown = -1;                  // какой алгоритм сейчас показан (-1 - никакой)
        string[] algoParts = new string[0];  // входные данные последнего запуска

        // ---------- Поля для дерева вызовов ----------
        CallTree tree = null;
        int treeStep = 0;          // сколько шагов уже показано
        string treeAnswer = "";
        int leafCounter = 0;       // чтобы расставить листья слева направо

        const double NodeW = 150;
        const double NodeH = 46;
        const double StepX = 165;
        const double StepY = 80;
        const double Pad = 10;

        // ---------- Поля для замеров ----------
        static double sink = 0; // сюда складываем результаты, чтобы компилятор не выкинул вызовы
        List<ExperimentResult> benchChartResults = null;
        double[] benchTheoryTimes = null;
        double benchChartFactor = 1;
        string benchChartUnit = "мс";
        bool benchChartDragging = false;
        Point benchChartLastMouse;
        double benchChartYaw = -0.62;
        double benchChartPitch = 0.48;

        sealed class BenchContext
        {
            public List<string> Current = new List<string>();
            public List<string> Subsets = new List<string>();
            public List<Move> Moves = new List<Move>();
        }

        public MainWindow()
        {
            InitializeComponent();

            hanoiTimer.Interval = TimeSpan.FromMilliseconds(300);
            hanoiTimer.Tick += HanoiTimer_Tick;

            ResetHanoi();
            DrawFractal();
            UpdateAlgoInfo();
            UpdateTreeInfo();
            UpdateBenchInfo();
        }

        // Рисование на Canvas

        void AddRect(Canvas canvas, double x, double y, double w, double h, Brush fill, double radius, Brush stroke = null)
        {
            Rectangle r = new Rectangle();
            r.Width = w;
            r.Height = h;
            r.Fill = fill;
            r.RadiusX = radius;
            r.RadiusY = radius;
            if (stroke != null)
            {
                r.Stroke = stroke;
                r.StrokeThickness = 1;
            }
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, y);
            canvas.Children.Add(r);
        }

        void AddText(Canvas canvas, string text, double x, double y, double size, Brush color = null, bool bold = true)
        {
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontSize = size;
            if (bold) t.FontWeight = FontWeights.Bold;
            if (color != null) t.Foreground = color;
            Canvas.SetLeft(t, x);
            Canvas.SetTop(t, y);
            canvas.Children.Add(t);
        }

        // ЗАДАНИЕ 1. ФРАКТАЛ (ковёр Серпинского)

        void DrawFractal_Click(object sender, RoutedEventArgs e)
        {
            DrawFractal();
        }

        void DrawFractal()
        {
            int depth;
            if (!int.TryParse(DepthBox.Text, out depth) || depth < 0 || depth > 5)
            {
                FractalInfo.Text = "Глубина должна быть числом от 0 до 5";
                return;
            }

            FractalCanvas.Children.Clear();
            carpetCalls = 0;
            carpetHoles = 0;

            // сначала рисуем целый квадрат, потом вырезаем из него дырки
            AddRect(FractalCanvas, 0, 0, 486, 486, accent, 0);
            DrawCarpet(0, 0, 486, depth);

            FractalInfo.Text = "Вызовов функции: " + carpetCalls + ", вырезано квадратов: " + carpetHoles;
        }

        // Квадрат делим на 9 частей, центральную вырезаем (рисуем белым),
        // для остальных 8 частей делаем то же самое
        void DrawCarpet(double x, double y, double size, int depth)
        {
            carpetCalls++;

            if (depth == 0) return; // база рекурсии

            double s = size / 3;
            AddRect(FractalCanvas, x + s, y + s, s, s, Brushes.White, 0);
            carpetHoles++;

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    if (row == 1 && col == 1) continue; // центр уже вырезан
                    DrawCarpet(x + col * s, y + row * s, s, depth - 1);
                }
            }
        }

        // ЗАДАНИЕ 2. ХАНОЙСКИЕ БАШНИ

        // Ставим все кольца на первый стержень и очищаем ходы
        bool ResetHanoi()
        {
            StopAuto();

            int n;
            if (!int.TryParse(RingsBox.Text, out n) || n < 1 || n > 10)
            {
                MessageBox.Show("Количество колец должно быть от 1 до 10.");
                return false;
            }
            ringsCount = n;

            for (int i = 0; i < 3; i++) rods[i].Clear();
            for (int size = n; size >= 1; size--) rods[0].Add(size);

            moves.Clear();
            moveIndex = 0;
            LogBox.Items.Clear();

            int total = (1 << n) - 1;
            HanoiInfo.Text = "Колец: " + n + ". Минимальное число ходов: 2^" + n + " - 1 = " + total;
            DrawHanoi();
            return true;
        }

        // Считаем все ходы заранее (рекурсией или стеком), потом просто проигрываем их
        bool EnsureMoves()
        {
            if (moves.Count > 0) return true;
            if (!ResetHanoi()) return false;

            if (HanoiMethodCombo.SelectedIndex == 0)
                Algorithms.HanoiRecursive(ringsCount, 0, 2, 1, moves);
            else
                Algorithms.HanoiStack(ringsCount, moves);
            return true;
        }

        bool StepForward()
        {
            if (!EnsureMoves()) return false;
            if (moveIndex >= moves.Count) return false;

            Move m = moves[moveIndex];

            // снимаем верхнее кольцо и кладём на другой стержень
            List<int> fromRod = rods[m.From];
            int ring = fromRod[fromRod.Count - 1];
            fromRod.RemoveAt(fromRod.Count - 1);
            rods[m.To].Add(ring);
            moveIndex++;

            string line = moveIndex + ". кольцо " + ring + ": " + Algorithms.RodNames[m.From] + " → " + Algorithms.RodNames[m.To];
            LogBox.Items.Add(line);
            LogBox.ScrollIntoView(line);

            UpdateHanoiInfo();
            DrawHanoi();
            return true;
        }

        void StepBack()
        {
            if (moveIndex == 0) return;

            moveIndex--;
            Move m = moves[moveIndex];

            // возвращаем кольцо обратно
            List<int> toRod = rods[m.To];
            int ring = toRod[toRod.Count - 1];
            toRod.RemoveAt(toRod.Count - 1);
            rods[m.From].Add(ring);

            LogBox.Items.RemoveAt(LogBox.Items.Count - 1); // убираем последнюю строку лога
            UpdateHanoiInfo();
            DrawHanoi();
        }

        void UpdateHanoiInfo()
        {
            string method;
            if (HanoiMethodCombo.SelectedIndex == 0) method = "рекурсией";
            else method = "через стек";

            if (moveIndex == moves.Count)
                HanoiInfo.Text = "Готово! Решено " + method + " за " + moves.Count + " ходов";
            else
                HanoiInfo.Text = "Ход " + moveIndex + " из " + moves.Count + " (решение " + method + ")";
        }

        void StopAuto()
        {
            hanoiTimer.Stop();
            AutoButton.Content = "Авто";
        }

        void HanoiTimer_Tick(object sender, EventArgs e)
        {
            // когда ходы закончились - останавливаем таймер
            if (!StepForward()) StopAuto();
        }

        void HanoiAuto_Click(object sender, RoutedEventArgs e)
        {
            if (hanoiTimer.IsEnabled)
            {
                StopAuto();
                return;
            }

            if (!EnsureMoves()) return;
            if (moveIndex >= moves.Count) return;

            hanoiTimer.Start();
            AutoButton.Content = "Пауза";
        }

        void HanoiForward_Click(object sender, RoutedEventArgs e)
        {
            StopAuto();
            StepForward();
        }

        void HanoiBack_Click(object sender, RoutedEventArgs e)
        {
            StopAuto();
            StepBack();
        }

        void HanoiReset_Click(object sender, RoutedEventArgs e)
        {
            ResetHanoi();
        }

        void HanoiMethodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return; // окно ещё создаётся
            ResetHanoi();
        }

        void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // берём значение из e, а не из SpeedSlider: событие может прийти, пока окно ещё создаётся
            hanoiTimer.Interval = TimeSpan.FromMilliseconds(e.NewValue);
        }

        void SaveLog_Click(object sender, RoutedEventArgs e)
        {
            if (LogBox.Items.Count == 0)
            {
                MessageBox.Show("Лог пока пустой.");
                return;
            }

            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.Filter = "Текстовый файл (*.txt)|*.txt";
            dialog.FileName = "hanoi_log.txt";
            if (dialog.ShowDialog() != true) return;

            List<string> lines = new List<string>();
            foreach (object item in LogBox.Items) lines.Add(item.ToString());
            File.WriteAllLines(dialog.FileName, lines);
            MessageBox.Show("Лог сохранён.");
        }

        void DrawHanoi()
        {
            HanoiCanvas.Children.Clear();

            double baseY = 280;
            double ringH = 20;

            for (int i = 0; i < 3; i++)
            {
                double cx = 100 + i * 200; // середина стержня

                AddRect(HanoiCanvas, cx - 95, baseY, 190, 10, dark, 3); // основание
                AddRect(HanoiCanvas, cx - 4, 50, 8, baseY - 50, dark, 3); // стержень
                AddText(HanoiCanvas, Algorithms.RodNames[i], cx - 5, baseY + 14, 15);

                for (int j = 0; j < rods[i].Count; j++)
                {
                    int size = rods[i][j];
                    double w = 26 + size * 16;
                    AddRect(HanoiCanvas, cx - w / 2, baseY - (j + 1) * ringH, w, ringH - 2, ringColors[size - 1], 6);
                }
            }
        }

        // Собирает строку ввода из полей: для степени это "a n", для остальных - первое поле
        string MakeInput(int algo, TextBox box1, TextBox box2)
        {
            if (algo == 0) return box1.Text.Trim() + " " + box2.Text.Trim();
            return box1.Text;
        }

        // Проверяет ввод и запускает нужный алгоритм с записью дерева вызовов.
        // maxItems - максимум элементов для подмножеств и колец для ханойских башен.
        // Если ввод неправильный, показывает сообщение и возвращает false.
        bool RunTraced(int algo, string input, CallTree t, int maxItems, out string answer)
        {
            answer = "";
            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (algo == 0)
            {
                double a;
                int n;
                if (parts.Length != 2 || !double.TryParse(parts[0], out a) || !int.TryParse(parts[1], out n) || n < 0 || n > 100000)
                {
                    MessageBox.Show("Заполните оба поля: a - число, n - целое от 0 до 100000.");
                    return false;
                }
                double result = TracedAlgorithms.Power(a, n, null, t);
                answer = a + "^" + n + " = " + result.ToString("G10");
            }
            else if (algo == 1)
            {
                int number;
                if (parts.Length != 1 || !int.TryParse(parts[0], out number) || number < 0)
                {
                    MessageBox.Show("Введите одно целое неотрицательное число.");
                    return false;
                }
                string bin = TracedAlgorithms.ToBinary(number, null, t);
                answer = number + " в двоичной системе: " + bin;
            }
            else if (algo == 2)
            {
                if (parts.Length < 1 || parts.Length > maxItems)
                {
                    MessageBox.Show("Введите от 1 до " + maxItems + " элементов через пробел.");
                    return false;
                }
                List<string> result = new List<string>();
                TracedAlgorithms.Subsets(parts, 0, new List<string>(), result, null, t);
                answer = "Подмножеств: " + result.Count;
            }
            else
            {
                int rings;
                if (parts.Length != 1 || !int.TryParse(parts[0], out rings) || rings < 1 || rings > maxItems)
                {
                    MessageBox.Show("Введите количество колец от 1 до " + maxItems + ".");
                    return false;
                }
                TracedAlgorithms.Hanoi(rings, 0, 2, 1, null, t);
                answer = "Колец: " + rings + ", ходов: " + ((1 << rings) - 1);
            }

            return true;
        }

        // РЕКУРСИВНЫЕ АЛГОРИТМЫ (степень, двоичная система, подмножества)

        void AlgoCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateAlgoInfo();
        }

        // Описание алгоритма, пример ввода, очистка результатов прошлого алгоритма
        void UpdateAlgoInfo()
        {
            int algo = AlgoCombo.SelectedIndex;

            if (algo == 0)
            {
                AlgoInputLabel.Text = "Основание a";
                AlgoInputBox.Text = "2";
                AlgoInputBox.Width = 110;
                AlgoInputLabel2.Text = "Показатель n";
                AlgoInputBox2.Text = "10";
                AlgoField2Panel.Visibility = Visibility.Visible;
            }
            else if (algo == 1)
            {
                AlgoInputLabel.Text = "Число n (от 0)";
                AlgoInputBox.Text = "13";
                AlgoInputBox.Width = 110;
                AlgoField2Panel.Visibility = Visibility.Collapsed;
            }
            else
            {
                AlgoInputLabel.Text = "Элементы через пробел (до 10)";
                AlgoInputBox.Text = "1 2 3";
                AlgoInputBox.Width = 220;
                AlgoField2Panel.Visibility = Visibility.Collapsed;
            }

            AlgoInfo.Show(algo);

            algoShown = -1;
            AlgoResultText.Text = "";
            AlgoTraceGrid.ItemsSource = null;
            AlgoChart.Series = new ISeries[0];
            AlgoVisualCanvas.Children.Clear();
            AlgoChartTitle.Text = "График";
            AlgoVisualTitle.Text = "Как работает";
        }

        void AlgoRun_Click(object sender, RoutedEventArgs e)
        {
            int algo = AlgoCombo.SelectedIndex;

            CallTree t = new CallTree();
            string answer;
            string input = MakeInput(algo, AlgoInputBox, AlgoInputBox2);
            if (!RunTraced(algo, input, t, 10, out answer)) return;

            algoShown = algo;
            algoParts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            AlgoResultText.Text = answer + "   |   вызовов функции: " + t.Nodes.Count + ", глубина рекурсии: " + t.MaxDepth();

            // таблица хода выполнения: каждый вход в функцию и каждый возврат
            List<TraceRow> rows = new List<TraceRow>();
            for (int i = 0; i < t.Steps.Count; i++)
            {
                CallStep s = t.Steps[i];
                TraceRow row = new TraceRow();
                row.Step = i + 1;
                row.Depth = s.Node.Depth;
                if (s.IsReturn)
                {
                    row.Action = "возврат из " + s.Node.Text;
                    row.Result = s.Node.Result;
                }
                else
                {
                    row.Action = "вызов " + s.Node.Text;
                    row.Result = "";
                }
                rows.Add(row);
            }
            AlgoTraceGrid.ItemsSource = rows;

            if (algo == 0) BuildPowerChart();
            else if (algo == 1) BuildBinaryChart();
            else BuildSubsetsChart();

            DrawAlgoVisual();
        }

        // Точка на графике для введённого n
        ISeries MarkerSeries(double x, double y, string name)
        {
            return new ScatterSeries<ObservablePoint>
            {
                Values = new ObservablePoint[] { new ObservablePoint(x, y) },
                Name = name,
                GeometrySize = 14,
                Fill = new SolidColorPaint(SKColor.Parse("#EF4444")),
                Stroke = null
            };
        }

        // Рисует две линии и точку введённого n
        void ShowAlgoChart(string title, string xName, string yName, string name1, List<ObservablePoint> line1,
                           string name2, List<ObservablePoint> line2, ISeries marker)
        {
            AlgoChartTitle.Text = title;
            AlgoChart.Series = new ISeries[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = line1, Name = name1, Fill = null, GeometrySize = 0, LineSmoothness = 0,
                    Stroke = new SolidColorPaint(SKColor.Parse("#2F6FED"), 2)
                },
                new LineSeries<ObservablePoint>
                {
                    Values = line2, Name = name2, Fill = null, GeometrySize = 0,
                    Stroke = new SolidColorPaint(SKColor.Parse("#F97316"), 2)
                },
                marker
            };
            AlgoChart.XAxes = new Axis[] { new Axis { Name = xName } };
            AlgoChart.YAxes = new Axis[] { new Axis { Name = yName } };
            AlgoChart.LegendPosition = LegendPosition.Bottom;
        }

        // Сколько умножений делает быстрое возведение в степень для разных n
        void BuildPowerChart()
        {
            int n = int.Parse(algoParts[1]);
            int nMax = Math.Max(64, Math.Min(n * 2, 1000000));
            int step = Math.Max(1, nMax / 400);

            List<ObservablePoint> fast = new List<ObservablePoint>();
            List<ObservablePoint> bound = new List<ObservablePoint>();
            for (int x = 1; x <= nMax; x += step)
            {
                fast.Add(new ObservablePoint(x, Algorithms.PowerMultiplications(x)));
                bound.Add(new ObservablePoint(x, 2 * Math.Log2(x)));
            }

            ShowAlgoChart("Количество умножений от показателя n (обычным способом для n = " + n + " их было бы " + n + ")",
                          "Показатель степени n", "Умножений",
                          "Быстрое возведение (умножений)", fast, "2·log2(n)", bound,
                          MarkerSeries(n, Algorithms.PowerMultiplications(n), "n = " + n));
        }

        // Сколько раз вызывается функция перевода для чисел до 2n
        void BuildBinaryChart()
        {
            int n = int.Parse(algoParts[0]);
            int nMax = (int)Math.Min(Math.Max(64L, n * 2L), int.MaxValue);
            int step = Math.Max(1, nMax / 400);

            List<ObservablePoint> calls = new List<ObservablePoint>();
            List<ObservablePoint> log = new List<ObservablePoint>();
            for (long x = 1; x <= nMax; x += step)
            {
                calls.Add(new ObservablePoint(x, Algorithms.BinaryCalls((int)x)));
                log.Add(new ObservablePoint(x, Math.Log2(x) + 1));
            }

            ShowAlgoChart("Количество вызовов (= количество двоичных цифр) от числа n",
                          "Число n", "Вызовов",
                          "Вызовов ToBinary", calls, "log2(n) + 1", log,
                          MarkerSeries(n, Algorithms.BinaryCalls(n), "n = " + n));
        }

        // Количество вызовов и подмножеств растёт как 2^n
        void BuildSubsetsChart()
        {
            int n = algoParts.Length;
            int kMax = Math.Max(10, n + 2);

            List<ObservablePoint> calls = new List<ObservablePoint>();
            List<ObservablePoint> subsets = new List<ObservablePoint>();
            for (int k = 0; k <= kMax; k++)
            {
                calls.Add(new ObservablePoint(k, Algorithms.SubsetsCalls(k, 0)));
                subsets.Add(new ObservablePoint(k, Math.Pow(2, k)));
            }

            ShowAlgoChart("Количество вызовов функции и подмножеств от числа элементов",
                          "Количество элементов n", "Количество",
                          "Вызовов Subsets (2^(n+1) - 1)", calls, "Подмножеств (2^n)", subsets,
                          MarkerSeries(n, Algorithms.SubsetsCalls(n, 0), "n = " + n));
        }

        // Наглядная схема под графиком
        void DrawAlgoVisual()
        {
            AlgoVisualCanvas.Children.Clear();

            if (algoShown == 0) DrawPowerChain();
            else if (algoShown == 1) DrawBinaryBits();
            else if (algoShown == 2) DrawSubsetsTable();
        }

        // Цепочка преобразований: 2^10 -> 4^5 -> 4 * 4^4 -> ...
        void DrawPowerChain()
        {
            AlgoVisualTitle.Text = "Как меняются основание и показатель на каждом вызове";

            double a = double.Parse(algoParts[0]);
            int n = int.Parse(algoParts[1]);
            double factor = 1; // то, что вынесли за скобки на нечётных шагах

            double x = 4;
            double boxW = 120;
            double boxH = 52;
            double gap = 70;
            double y = 40;

            while (true)
            {
                AddRect(AlgoVisualCanvas, x, y, boxW, boxH, accentLight, 8, accent);
                AddText(AlgoVisualCanvas, a.ToString("G5") + " ^ " + n, x + 10, y + 8, 14);
                if (factor != 1)
                    AddText(AlgoVisualCanvas, "× " + factor.ToString("G5"), x + 10, y + 30, 11, gray, false);

                if (n == 0) break; // база рекурсии

                string rule;
                if (n % 2 == 0)
                {
                    rule = "a·a, n/2";
                    a = a * a;
                    n = n / 2;
                }
                else
                {
                    rule = "a·(n-1)";
                    factor = factor * a;
                    n = n - 1;
                }

                AddText(AlgoVisualCanvas, rule, x + boxW + 8, y + boxH / 2 - 22, 10, gray, false);
                AddText(AlgoVisualCanvas, "→", x + boxW + gap / 2 - 8, y + boxH / 2 - 12, 16, gray, false);

                x = x + boxW + gap;
            }

            AddText(AlgoVisualCanvas, "a^0 = 1 - база рекурсии, дальше результаты перемножаются на обратном пути",
                    4, y + boxH + 20, 12, gray, false);

            AlgoVisualCanvas.Width = x + boxW + 10;
            AlgoVisualCanvas.Height = y + boxH + 50;
        }

        // Двоичные разряды числа и деление на 2 по шагам
        void DrawBinaryBits()
        {
            AlgoVisualTitle.Text = "Двоичная запись: остатки от деления на 2";

            int n = int.Parse(algoParts[0]);
            string bits = Algorithms.ToBinary(n);

            double cell = 40;
            for (int i = 0; i < bits.Length; i++)
            {
                bool one = bits[i] == '1';
                double x = 4 + i * (cell + 6);

                if (one) AddRect(AlgoVisualCanvas, x, 4, cell, cell, accent, 6, gray);
                else AddRect(AlgoVisualCanvas, x, 4, cell, cell, Brushes.White, 6, gray);

                Brush digitColor = one ? Brushes.White : gray;
                AddText(AlgoVisualCanvas, bits[i].ToString(), x + 14, 12, 16, digitColor);
                AddText(AlgoVisualCanvas, "2^" + (bits.Length - 1 - i), x + 8, cell + 8, 10, gray, false);
            }

            // строчки деления: 13 / 2 = 6, остаток 1
            double y = cell + 34;
            int m = n;
            while (m >= 2)
            {
                AddText(AlgoVisualCanvas, m + " / 2 = " + (m / 2) + ",  остаток " + (m % 2), 4, y, 12, null, false);
                y += 18;
                m = m / 2;
            }
            AddText(AlgoVisualCanvas, m + " - база рекурсии (0 или 1)", 4, y, 12, gray, false);

            AlgoVisualCanvas.Width = Math.Max(4 + bits.Length * (cell + 6), 280);
            AlgoVisualCanvas.Height = y + 24;
        }

        // Таблица: строки - подмножества, столбцы - элементы, закрашено - элемент взят
        void DrawSubsetsTable()
        {
            AlgoVisualTitle.Text = "Какие элементы попали в каждое подмножество";

            string[] items = algoParts;

            if (items.Length > 6)
            {
                AddText(AlgoVisualCanvas, "Таблица показывается для 6 элементов и меньше (иначе больше 64 строк)", 4, 4, 12, gray, false);
                AlgoVisualCanvas.Width = 500;
                AlgoVisualCanvas.Height = 30;
                return;
            }

            double cell = 24;
            double left = 110;
            int n = items.Length;
            int count = 1 << n;

            for (int j = 0; j < n; j++)
                AddText(AlgoVisualCanvas, items[j], left + j * cell + 7, 0, 12, gray);

            // Порядок такой же, как выдаёт рекурсия: сначала "не берём", потом "берём".
            // Это совпадает с двоичным счётом, где первый элемент - старший бит
            for (int i = 0; i < count; i++)
            {
                double y = 20 + i * cell;
                List<string> subset = new List<string>();

                for (int j = 0; j < n; j++)
                {
                    bool taken = ((i >> (n - 1 - j)) & 1) == 1;
                    if (taken) subset.Add(items[j]);

                    if (taken) AddRect(AlgoVisualCanvas, left + j * cell + 1, y + 1, cell - 2, cell - 2, accent, 3, gray);
                    else AddRect(AlgoVisualCanvas, left + j * cell + 1, y + 1, cell - 2, cell - 2, Brushes.White, 3, gray);
                }

                AddText(AlgoVisualCanvas, "{" + string.Join(", ", subset) + "}", 4, y + 3, 12, null, false);
            }

            AlgoVisualCanvas.Width = left + n * cell + 10;
            AlgoVisualCanvas.Height = 20 + count * cell + 6;
        }

        // ДЕРЕВО ВЫЗОВОВ

        void TreeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateTreeInfo();
        }

        // Показываем подсказку, пример ввода и описание алгоритма
        void UpdateTreeInfo()
        {
            int algo = TreeCombo.SelectedIndex;
            TreeField2Panel.Visibility = Visibility.Collapsed;

            if (algo == 0)
            {
                TreeHint.Text = "Основание a";
                TreeInputBox.Text = "2";
                TreeHint2.Text = "Показатель n";
                TreeInputBox2.Text = "10";
                TreeField2Panel.Visibility = Visibility.Visible;
            }
            else if (algo == 1)
            {
                TreeHint.Text = "Число n (от 0)";
                TreeInputBox.Text = "13";
            }
            else if (algo == 2)
            {
                TreeHint.Text = "Элементы через пробел (до 4)";
                TreeInputBox.Text = "1 2 3";
            }
            else
            {
                TreeHint.Text = "Количество колец (от 1 до 4)";
                TreeInputBox.Text = "3";
            }

            TreeInfo.Show(algo);
        }

        void TreeBuild_Click(object sender, RoutedEventArgs e)
        {
            CallTree newTree = new CallTree();
            string answer;
            if (!RunTraced(TreeCombo.SelectedIndex, MakeInput(TreeCombo.SelectedIndex, TreeInputBox, TreeInputBox2), newTree, 4, out answer)) return;

            tree = newTree;
            treeAnswer = answer;
            treeStep = 0;

            leafCounter = 0;
            LayoutNode(tree.Nodes[0]);
            DrawTree();
        }

        // Листья ставим по порядку слева направо, родителя - посередине над детьми
        void LayoutNode(CallNode node)
        {
            if (node.Children.Count == 0)
            {
                node.X = leafCounter * StepX;
                leafCounter++;
                return;
            }

            foreach (CallNode child in node.Children)
                LayoutNode(child);

            node.X = (node.Children[0].X + node.Children[node.Children.Count - 1].X) / 2;
        }

        void TreeForward_Click(object sender, RoutedEventArgs e)
        {
            if (tree == null || treeStep >= tree.Steps.Count) return;
            treeStep++;
            DrawTree();
        }

        void TreeBack_Click(object sender, RoutedEventArgs e)
        {
            if (tree == null || treeStep == 0) return;
            treeStep--;
            DrawTree();
        }

        void TreeStart_Click(object sender, RoutedEventArgs e)
        {
            if (tree == null) return;
            treeStep = 0;
            DrawTree();
        }

        void TreeEnd_Click(object sender, RoutedEventArgs e)
        {
            if (tree == null) return;
            treeStep = tree.Steps.Count;
            DrawTree();
        }

        void DrawTree()
        {
            TreeCanvas.Children.Clear();
            if (tree == null) return;

            // По первым treeStep шагам смотрим, какие вызовы уже начались и какие закончились
            bool[] entered = new bool[tree.Nodes.Count];
            bool[] returned = new bool[tree.Nodes.Count];
            for (int i = 0; i < treeStep; i++)
            {
                CallStep s = tree.Steps[i];
                if (s.IsReturn) returned[s.Node.Id] = true;
                else entered[s.Node.Id] = true;
            }

            CallNode current = null;
            if (treeStep > 0) current = tree.Steps[treeStep - 1].Node;

            // линии от родителя к детям
            foreach (CallNode node in tree.Nodes)
            {
                foreach (CallNode child in node.Children)
                {
                    Line line = new Line();
                    line.X1 = node.X + NodeW / 2 + Pad;
                    line.Y1 = node.Depth * StepY + NodeH + Pad;
                    line.X2 = child.X + NodeW / 2 + Pad;
                    line.Y2 = child.Depth * StepY + Pad;

                    if (entered[child.Id])
                    {
                        line.Stroke = accent;
                        line.StrokeThickness = 2;
                    }
                    else
                    {
                        line.Stroke = lightGray;
                        line.StrokeThickness = 1;
                    }
                    TreeCanvas.Children.Add(line);
                }
            }

            // узлы
            foreach (CallNode node in tree.Nodes)
            {
                Border box = new Border();
                box.Width = NodeW;
                box.Height = NodeH;
                box.CornerRadius = new CornerRadius(6);
                box.BorderThickness = new Thickness(1);
                box.BorderBrush = gray;
                box.Background = Brushes.White;

                StackPanel panel = new StackPanel();
                panel.VerticalAlignment = VerticalAlignment.Center;
                panel.Margin = new Thickness(6, 0, 6, 0);

                TextBlock call = new TextBlock();
                call.Text = node.Text;
                call.FontSize = 12;
                call.FontWeight = FontWeights.SemiBold;
                call.TextTrimming = TextTrimming.CharacterEllipsis;
                panel.Children.Add(call);

                if (returned[node.Id])
                {
                    box.Background = greenLight;
                    box.BorderBrush = green;

                    TextBlock res = new TextBlock();
                    res.Text = "→ " + node.Result;
                    res.FontSize = 11;
                    res.TextTrimming = TextTrimming.CharacterEllipsis;
                    panel.Children.Add(res);
                }
                else if (entered[node.Id])
                {
                    box.Background = accentLight;
                    box.BorderBrush = accent;
                }
                else
                {
                    box.Opacity = 0.4;
                }

                // текущий шаг выделяем рамкой потолще
                if (node == current)
                {
                    box.BorderThickness = new Thickness(3);
                    box.BorderBrush = Brushes.OrangeRed;
                }

                box.Child = panel;
                Canvas.SetLeft(box, node.X + Pad);
                Canvas.SetTop(box, node.Depth * StepY + Pad);
                TreeCanvas.Children.Add(box);
            }

            // размер холста по самому правому узлу
            double maxX = 0;
            foreach (CallNode node in tree.Nodes)
            {
                if (node.X > maxX) maxX = node.X;
            }
            TreeCanvas.Width = maxX + NodeW + 2 * Pad;
            TreeCanvas.Height = tree.MaxDepth() * StepY + NodeH + 2 * Pad;

            UpdateTreeStepText(current);
        }

        void UpdateTreeStepText(CallNode current)
        {
            if (current == null)
            {
                TreeStepText.Text = "Шаг 0 из " + tree.Steps.Count + ". Всего вызовов: " + tree.Nodes.Count +
                                    ", глубина рекурсии: " + tree.MaxDepth() + ". Нажмите «Вперёд».";
                return;
            }

            CallStep step = tree.Steps[treeStep - 1];
            string what;
            if (step.IsReturn) what = "возврат из " + step.Node.Text + " → " + step.Node.Result;
            else what = "вызов " + step.Node.Text;

            TreeStepText.Text = "Шаг " + treeStep + " из " + tree.Steps.Count + ": " + what + " (глубина " + step.Node.Depth + ")";
            if (treeStep == tree.Steps.Count)
                TreeStepText.Text += "\nОтвет: " + treeAnswer;

            // прокручиваем, чтобы текущий узел был виден
            TreeScroll.ScrollToHorizontalOffset(Math.Max(0, current.X - TreeScroll.ViewportWidth / 2));
            TreeScroll.ScrollToVerticalOffset(Math.Max(0, current.Depth * StepY - TreeScroll.ViewportHeight / 2));
        }

        // ЗАМЕРЫ ВРЕМЕНИ

        void BenchCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateBenchInfo();
        }

        // Подставляем значения по умолчанию для выбранного алгоритма
        void UpdateBenchInfo()
        {
            int algo = BenchCombo.SelectedIndex;
            benchChartResults = null;
            benchTheoryTimes = null;
            benchChartYaw = -0.62;
            benchChartPitch = 0.48;
            BenchChartCanvas.Children.Clear();

            if (algo == 0)
            {
                BenchNMeaning.Text = "n - показатель степени";
                BenchNMaxBox.Text = "1000000";
                BenchStepBox.Text = "20000";
            }
            else if (algo == 1)
            {
                BenchNMeaning.Text = "n - само число";
                BenchNMaxBox.Text = "1000000000";
                BenchStepBox.Text = "20000000";
            }
            else if (algo == 2)
            {
                BenchNMeaning.Text = "n - количество элементов";
                BenchNMaxBox.Text = "18";
                BenchStepBox.Text = "1";
            }
            else
            {
                BenchNMeaning.Text = "n - количество колец";
                BenchNMaxBox.Text = "20";
                BenchStepBox.Text = "1";
            }

            BenchInfo.Show(algo);
        }

        void BenchChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (benchChartResults == null) return;
            BenchmarkChart3D.Draw(BenchChartCanvas, benchChartResults, benchTheoryTimes,
                                  benchChartFactor, benchChartUnit, benchChartYaw, benchChartPitch);
        }

        void BenchChartCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (benchChartResults == null) return;
            benchChartDragging = true;
            benchChartLastMouse = e.GetPosition(BenchChartCanvas);
            BenchChartCanvas.CaptureMouse();
            e.Handled = true;
        }

        void BenchChartCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!benchChartDragging || e.LeftButton != MouseButtonState.Pressed) return;
            Point current = e.GetPosition(BenchChartCanvas);
            double dx = current.X - benchChartLastMouse.X;
            double dy = current.Y - benchChartLastMouse.Y;
            benchChartLastMouse = current;

            benchChartYaw += dx * 0.012;
            benchChartPitch = Math.Clamp(benchChartPitch + dy * 0.01, -1.0, 1.0);
            BenchmarkChart3D.Draw(BenchChartCanvas, benchChartResults, benchTheoryTimes,
                                  benchChartFactor, benchChartUnit, benchChartYaw, benchChartPitch);
            e.Handled = true;
        }

        void BenchChartCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!benchChartDragging) return;
            benchChartDragging = false;
            BenchChartCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }

        // Выполняет алгоритм на заранее подготовленных входных и вспомогательных данных.
        double RunAlgorithm(int algo, int n, string[] items, BenchContext context)
        {
            if (algo == 0)
            {
                return Algorithms.Power(1.0000001, n);
            }
            if (algo == 1)
            {
                return Algorithms.ToBinary(n).Length;
            }
            if (algo == 2)
            {
                Algorithms.Subsets(items, 0, context.Current, context.Subsets);
                return context.Subsets.Count;
            }

            if (algo == 3) Algorithms.HanoiRecursive(n, 0, 2, 1, context.Moves);
            else Algorithms.HanoiStack(n, context.Moves);
            return context.Moves.Count;
        }

        // Теоретическая функция f(n) для кривой c * f(n)
        double Theory(int algo, int n)
        {
            if (algo == 0 || algo == 1) return Math.Log2(Math.Max(n, 1));
            if (algo == 2) return n * Math.Pow(2, n);
            return Math.Pow(2, n);
        }

        // Подготовка входа вынесена за секундомер.
        // Быстрый алгоритм запускается пачками до накопления 5 мс.
        double MeasureOnce(int algo, int n)
        {
            string[] items = null;
            if (algo == 2)
            {
                items = new string[n];
                for (int i = 0; i < n; i++) items[i] = (i + 1).ToString();
            }

            long repeats = 0;
            int batch = 1;
            double totalMs = 0;

            while (totalMs < 5)
            {
                // Контейнеры для результатов также создаются до замера.
                BenchContext[] contexts = new BenchContext[batch];
                if (algo >= 2)
                    for (int i = 0; i < batch; i++) contexts[i] = new BenchContext();

                Stopwatch timer = Stopwatch.StartNew();
                for (int i = 0; i < batch; i++)
                    sink += RunAlgorithm(algo, n, items, contexts[i]);
                timer.Stop();

                totalMs += timer.Elapsed.TotalMilliseconds;
                repeats += batch;
                if (batch < (1 << 20)) batch *= 2;
            }

            return totalMs / repeats;
        }

        List<ExperimentResult> RunExperiment(int algo, int nMax, int step, int runs)
        {
            List<ExperimentResult> results = new List<ExperimentResult>();

            for (long n = step; n <= nMax; n += step)
            {
                double[] times = new double[runs];
                for (int r = 0; r < runs; r++)
                    times[r] = MeasureOnce(algo, (int)n);

                double sum = 0;
                for (int r = 0; r < runs; r++) sum += times[r];
                double avg = sum / runs;

                double sumSq = 0;
                for (int r = 0; r < runs; r++) sumSq += (times[r] - avg) * (times[r] - avg);
                double std = Math.Sqrt(sumSq / runs);

                ExperimentResult row = new ExperimentResult();
                row.N = (int)n;
                row.AvgTimeMs = avg;
                row.StdDevMs = std;
                row.Runs = runs;
                results.Add(row);
            }

            return results;
        }

        async void BenchRun_Click(object sender, RoutedEventArgs e)
        {
            int algo = BenchCombo.SelectedIndex;
            int nMax;
            int step;
            int runs;

            if (!int.TryParse(BenchNMaxBox.Text, out nMax) || nMax <= 0 ||
                !int.TryParse(BenchStepBox.Text, out step) || step <= 0 ||
                !int.TryParse(BenchRunsBox.Text, out runs) || runs <= 0)
            {
                MessageBox.Show("N max, шаг и количество запусков должны быть положительными числами.");
                return;
            }
            if (nMax / step > 300)
            {
                MessageBox.Show("Получается больше 300 точек, увеличьте шаг.");
                return;
            }
            if (algo >= 2 && nMax > 22)
            {
                MessageBox.Show("Для этого алгоритма N max не больше 22 (иначе слишком долго считать).");
                return;
            }

            BenchRunButton.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                // считаем в фоне, чтобы окно не зависало
                List<ExperimentResult> results = await Task.Run(() => RunExperiment(algo, nMax, step, runs));
                ShowBenchResults(algo, results);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                BenchRunButton.IsEnabled = true;
            }
        }

        void ShowBenchResults(int algo, List<ExperimentResult> results)
        {
            BenchGrid.ItemsSource = results;

            // Коэффициент c для теоретической кривой c * f(n) методом наименьших квадратов:
            // c = сумма(f * y) / сумма(f * f)
            double sumFy = 0;
            double sumFF = 0;
            double maxTime = 0;
            foreach (ExperimentResult r in results)
            {
                double f = Theory(algo, r.N);
                sumFy += f * r.AvgTimeMs;
                sumFF += f * f;
                if (r.AvgTimeMs > maxTime) maxTime = r.AvgTimeMs;
            }
            double c = 0;
            if (sumFF > 0) c = sumFy / sumFF;

            // выбираем единицы измерения, чтобы числа на графике были читаемыми
            double factor = 1;
            string unit = "мс";
            if (maxTime < 0.001) { factor = 1000000; unit = "нс"; }
            else if (maxTime < 1) { factor = 1000; unit = "мкс"; }

            double[] theoryTimes = new double[results.Count];
            for (int i = 0; i < results.Count; i++)
                theoryTimes[i] = c * Theory(algo, results[i].N);

            benchChartResults = results;
            benchTheoryTimes = theoryTimes;
            benchChartFactor = factor;
            benchChartUnit = unit;
            BenchmarkChart3D.Draw(BenchChartCanvas, results, theoryTimes, factor, unit,
                                  benchChartYaw, benchChartPitch);
        }
    }
}
