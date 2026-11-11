using System.Collections.Generic;

namespace RecursionApp
{
    // Один ход в ханойских башнях: снять кольцо со стержня From и положить на стержень To.
    // Стержни нумеруем 0, 1, 2 (A, B, C)
    public class Move
    {
        public int From;
        public int To;

        public Move(int from, int to)
        {
            From = from;
            To = to;
        }
    }

    // Задача для стека: перенести N колец со стержня From на стержень To через Via
    public class HanoiTask
    {
        public int N;
        public int From;
        public int To;
        public int Via;

        public HanoiTask(int n, int from, int to, int via)
        {
            N = n;
            From = from;
            To = to;
            Via = via;
        }
    }

    public static class Algorithms
    {
        public static string[] RodNames = { "A", "B", "C" };

        // ---------- Быстрое возведение в степень ----------
        // n чётное   -> a^n = (a*a)^(n/2)
        // n нечётное -> a^n = a * a^(n-1)
        public static double Power(double a, int n)
        {
            if (n == 0) return 1;
            if (n % 2 == 0) return Power(a * a, n / 2);
            return a * Power(a, n - 1);
        }

        // ---------- Перевод в двоичную систему ----------
        // переводим n/2 и дописываем остаток от деления на 2
        public static string ToBinary(int n)
        {
            if (n < 2) return n.ToString();
            return ToBinary(n / 2) + (n % 2);
        }

        // ---------- Все подмножества ----------
        // каждый элемент либо не берём, либо берём
        public static void Subsets(string[] items, int index, List<string> current, List<string> result)
        {
            if (index == items.Length)
            {
                result.Add("{" + string.Join(", ", current) + "}");
                return;
            }

            // вариант 1: элемент не берём
            Subsets(items, index + 1, current, result);

            // вариант 2: берём
            current.Add(items[index]);
            Subsets(items, index + 1, current, result);
            current.RemoveAt(current.Count - 1); // возвращаем как было
        }

        // ---------- Функции для графиков (считают количество операций той же рекурсией) ----------

        // Сколько умножений делает Power для показателя n
        public static int PowerMultiplications(int n)
        {
            if (n == 0) return 0;
            if (n % 2 == 0) return 1 + PowerMultiplications(n / 2); // a*a
            return 1 + PowerMultiplications(n - 1);                 // a * ...
        }

        // Сколько раз вызовется ToBinary для числа n
        public static int BinaryCalls(int n)
        {
            if (n < 2) return 1;
            return 1 + BinaryCalls(n / 2);
        }

        // Сколько раз вызовется Subsets для n элементов: 1 + 2 + 4 + ... + 2^n
        public static long SubsetsCalls(int n, int index)
        {
            if (index == n) return 1;
            return 1 + 2 * SubsetsCalls(n, index + 1);
        }

        // ---------- Ханойские башни, рекурсия ----------
        // Перенести n колец с from на to через via:
        // 1) n-1 колец переносим на via
        // 2) самое большое кольцо переносим на to
        // 3) n-1 колец переносим с via на to
        public static void HanoiRecursive(int n, int from, int to, int via, List<Move> moves)
        {
            if (n == 0) return;

            HanoiRecursive(n - 1, from, via, to, moves);
            moves.Add(new Move(from, to));
            HanoiRecursive(n - 1, via, to, from, moves);
        }

        // ---------- Ханойские башни, через стек (доп. задание) ----------
        // Вместо рекурсивных вызовов кладём задачи в стек.
        // Стек отдаёт последнюю положенную задачу, поэтому кладём в обратном порядке: 3, 2, 1
        public static void HanoiStack(int n, List<Move> moves)
        {
            Stack<HanoiTask> stack = new Stack<HanoiTask>();
            stack.Push(new HanoiTask(n, 0, 2, 1));

            while (stack.Count > 0)
            {
                HanoiTask t = stack.Pop();

                if (t.N == 0) continue;

                if (t.N == 1)
                {
                    moves.Add(new Move(t.From, t.To));
                    continue;
                }

                stack.Push(new HanoiTask(t.N - 1, t.Via, t.To, t.From)); // шаг 3
                stack.Push(new HanoiTask(1, t.From, t.To, t.Via));       // шаг 2
                stack.Push(new HanoiTask(t.N - 1, t.From, t.Via, t.To)); // шаг 1
            }
        }
    }

    // Тексты для описания алгоритмов (название, сложность, описание)
    public static class AlgorithmInfo
    {
        public static string[] Names =
        {
            "Быстрое возведение в степень",
            "Перевод числа в двоичную систему",
            "Генерация всех подмножеств",
            "Ханойские башни (рекурсия)",
            "Ханойские башни (стек)"
        };

        public static string[] Complexity = { "O(log n)", "O(log n)", "O(n · 2^n)", "O(2^n)", "O(2^n)" };

        public static string[] Memory =
        {
            "O(log n) - глубина рекурсии",
            "O(log n) - глубина рекурсии",
            "O(n) - глубина рекурсии, плюс 2^n подмножеств в ответе",
            "O(n) - глубина рекурсии, плюс список ходов",
            "O(n) - размер стека, плюс список ходов"
        };

        public static string[] Description =
        {
            "Считает a^n примерно за 2·log2(n) умножений вместо n. Если показатель чётный, " +
            "основание возводим в квадрат, а показатель делим пополам. Если нечётный - " +
            "выносим один множитель a, и показатель становится чётным.",

            "Число делим на 2, пока не останется 0 или 1. Остатки в обратном порядке дают двоичную запись. " +
            "Рекурсия сама разворачивает порядок: сначала переводится n/2, а остаток дописывается в конец.",

            "Перебор с возвратом. Для каждого элемента два варианта: не брать его или взять. " +
            "Получается двоичное дерево вызовов глубины n с 2^n листьями, каждый лист - одно подмножество.",

            "Головоломка Эдуарда Люка (1883). Чтобы перенести n колец, переносим n-1 колец на вспомогательный " +
            "стержень, затем самое большое кольцо на нужный, затем n-1 колец сверху. Минимум ходов: 2^n - 1.",

            "То же решение, но без рекурсии. Подзадачи (n, откуда, куда, через что) хранятся в стеке, " +
            "который мы ведём сами вместо стека вызовов."
        };

        public static string[] BaseCase =
        {
            "n = 0, ответ 1",
            "n меньше 2, ответ - само число",
            "index = n (все элементы просмотрены), подмножество записывается в ответ",
            "n = 0, ничего делать не нужно",
            "задача с n = 0 пропускается, с n = 1 - один ход"
        };

        public static string[] Decomposition =
        {
            "n чётное: a^n = (a·a)^(n/2)\nn нечётное: a^n = a · a^(n-1)",
            "bin(n) = bin(n / 2) + (n mod 2)",
            "Subsets(index+1) без элемента, затем Subsets(index+1) с элементом",
            "Hanoi(n-1, A→B), ход A→C, Hanoi(n-1, B→C)",
            "задачу из n колец заменяем тремя: n-1, 1 и n-1"
        };
    }
}
