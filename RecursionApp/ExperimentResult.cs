namespace RecursionApp
{
    // Одна строка таблицы с результатами замера
    public class ExperimentResult
    {
        public int N { get; set; }
        public double AvgTimeMs { get; set; }
        public double StdDevMs { get; set; }
        public int Runs { get; set; }
    }

    // Одна строка таблицы "Ход выполнения"
    public class TraceRow
    {
        public int Step { get; set; }
        public string Action { get; set; }
        public int Depth { get; set; }
        public string Result { get; set; }
    }
}
