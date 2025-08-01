using System;

class TestDebug
{
    static void Main()
    {
        // 10001を分析
        long value = 10001;
        var groups = new long[4];
        
        for (int i = 0; i < 4 && value > 0; i++)
        {
            groups[i] = value % 10000;
            value /= 10000;
        }
        
        Console.WriteLine("10001の分析:");
        Console.WriteLine($"groups[0] = {groups[0]} (個の位グループ)");
        Console.WriteLine($"groups[1] = {groups[1]} (万の位グループ)");
        Console.WriteLine($"groups[2] = {groups[2]} (億の位グループ)");
        Console.WriteLine($"groups[3] = {groups[3]} (万億の位グループ)");
        
        Console.WriteLine("\n処理の流れ:");
        Console.WriteLine("1. i=1: groups[1]=1 → '一' + '万' = '一万'");
        Console.WriteLine("2. i=0: groups[0]=1 → ここで零が必要！");
        Console.WriteLine("\n問題: groups[1]とgroups[0]の間にゼログループがない！");
        Console.WriteLine("でも10001は「一万零一」と読むべき");
    }
}