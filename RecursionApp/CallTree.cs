using System.Collections.Generic;

namespace RecursionApp
{
    // Узел дерева = один вызов функции
    public class CallNode
    {
        public int Id;
        public int Depth;
        public string Text = "";
        public string Result = "";
        public List<CallNode> Children = new List<CallNode>();
        public double X; // координата для рисования
    }

    // Шаг выполнения: вход в функцию или возврат из неё
    public class CallStep
    {
        public CallNode Node;
        public bool IsReturn;

        public CallStep(CallNode node, bool isReturn)
        {
            Node = node;
            IsReturn = isReturn;
        }
    }

    public class CallTree
    {
        public List<CallNode> Nodes = new List<CallNode>();
        public List<CallStep> Steps = new List<CallStep>();

        // Вызываем в начале функции
        public CallNode Enter(CallNode parent, string text)
        {
            CallNode node = new CallNode();
            node.Id = Nodes.Count;
            node.Text = text;
            if (parent == null) node.Depth = 0;
            else node.Depth = parent.Depth + 1;

            if (parent != null) parent.Children.Add(node);
            Nodes.Add(node);
            Steps.Add(new CallStep(node, false));
            return node;
        }

        // Вызываем перед выходом из функции
        public void Exit(CallNode node, string result)
        {
            node.Result = result;
            Steps.Add(new CallStep(node, true));
        }

        public int MaxDepth()
        {
            int max = 0;
            foreach (CallNode node in Nodes)
            {
                if (node.Depth > max) max = node.Depth;
            }
            return max;
        }
    }

    // Те же алгоритмы, только каждый вызов записывается в дерево.
    // Сделано отдельно от Algorithms, чтобы запись не влияла на замеры времени.
    public static class TracedAlgorithms
    {
        public static double Power(double a, int n, CallNode parent, CallTree tree)
        {
            CallNode me = tree.Enter(parent, "Power(" + a + ", " + n + ")");

            double result;
            if (n == 0) result = 1;
            else if (n % 2 == 0) result = Power(a * a, n / 2, me, tree);
            else result = a * Power(a, n - 1, me, tree);

            tree.Exit(me, result.ToString("G6"));
            return result;
        }

        public static string ToBinary(int n, CallNode parent, CallTree tree)
        {
            CallNode me = tree.Enter(parent, "ToBinary(" + n + ")");

            string result;
            if (n < 2) result = n.ToString();
            else result = ToBinary(n / 2, me, tree) + (n % 2);

            tree.Exit(me, "\"" + result + "\"");
            return result;
        }

        public static void Subsets(string[] items, int index, List<string> current, List<string> result,
                                   CallNode parent, CallTree tree)
        {
            CallNode me = tree.Enter(parent, "i=" + index + "  {" + string.Join(",", current) + "}");

            if (index == items.Length)
            {
                string subset = "{" + string.Join(", ", current) + "}";
                result.Add(subset);
                tree.Exit(me, "записано " + subset);
                return;
            }

            Subsets(items, index + 1, current, result, me, tree);

            current.Add(items[index]);
            Subsets(items, index + 1, current, result, me, tree);
            current.RemoveAt(current.Count - 1);

            tree.Exit(me, "готово");
        }

        public static void Hanoi(int n, int from, int to, int via, CallNode parent, CallTree tree)
        {
            string text = "Hanoi(" + n + ", " + Algorithms.RodNames[from] + "→" + Algorithms.RodNames[to] + ")";
            CallNode me = tree.Enter(parent, text);

            if (n == 0)
            {
                tree.Exit(me, "ничего");
                return;
            }

            Hanoi(n - 1, from, via, to, me, tree);
            Hanoi(n - 1, via, to, from, me, tree);

            tree.Exit(me, "ход " + Algorithms.RodNames[from] + "→" + Algorithms.RodNames[to]);
        }
    }
}
