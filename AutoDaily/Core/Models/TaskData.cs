using System.Collections.Generic;

namespace AutoDaily.Core.Models
{
    public class TaskData
    {
        public string Version { get; set; } = "1.0";
        public List<Task> Tasks { get; set; } = new List<Task>();
    }
}

