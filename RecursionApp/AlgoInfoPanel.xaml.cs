using System.Windows.Controls;

namespace RecursionApp
{
    // Карточка с описанием алгоритма: название, сложность, память, описание, база и декомпозиция
    public partial class AlgoInfoPanel : UserControl
    {
        public AlgoInfoPanel()
        {
            InitializeComponent();
        }

        // algo - номер алгоритма в массивах AlgorithmInfo
        public void Show(int algo)
        {
            NameText.Text = AlgorithmInfo.Names[algo];
            TimeText.Text = AlgorithmInfo.Complexity[algo].Replace("^n", "ⁿ"); // красивая степень
            MemoryText.Text = AlgorithmInfo.Memory[algo];
            DescriptionText.Text = AlgorithmInfo.Description[algo];
            BaseText.Text = AlgorithmInfo.BaseCase[algo];
            DecompText.Text = AlgorithmInfo.Decomposition[algo];
        }
    }
}
