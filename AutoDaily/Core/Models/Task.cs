using System;
using System.Collections.Generic;

namespace AutoDaily.Core.Models
{
    public class Task
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Morning Report";
        public Schedule Schedule { get; set; } = new Schedule();
        public WindowInfo TargetWindow { get; set; } = new WindowInfo();
        public List<Action> Actions { get; set; } = new List<Action>();
        public DateTime? LastRun { get; set; }
    }

    public class Schedule
    {
        public bool Enabled { get; set; } = false;
        public int Hour { get; set; } = 9;
        public int Minute { get; set; } = 0;
        public bool CatchUp { get; set; } = true; // 补卡机制
    }

    public class WindowInfo
    {
        public string Title { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public WindowRect Rect { get; set; } = new WindowRect();
    }

    public class WindowRect
    {
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 768;
    }

    public class Action
    {
        public string Type { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public bool Relative { get; set; } = true;
        public string Button { get; set; } = "Left";
        public string Text { get; set; } = "";
        public int Param { get; set; } // 用于Wait等操作的参数
    }
}

