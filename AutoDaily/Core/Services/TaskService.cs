using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using AutoDaily.Core.Models;

namespace AutoDaily.Core.Services
{
    public class TaskService
    {
        private static readonly string DataDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string TasksFilePath = Path.Combine(DataDirectory, "tasks.json");

        private TaskData _taskData;

        public TaskService()
        {
            // EnsureDataDirectory(); // No need to create BaseDirectory usually, but if it's separate logic?
            // BaseDirectory is EXE dir. It exists.
            LoadTasks();
        }

        private void EnsureDataDirectory()
        {
             // No-op or remove
        }

        public void LoadTasks()
        {
            if (File.Exists(TasksFilePath))
            {
                try
                {
                    var json = File.ReadAllText(TasksFilePath);
                    var serializer = new JavaScriptSerializer();
                    _taskData = serializer.Deserialize<TaskData>(json) ?? new TaskData();
                }
                catch
                {
                    _taskData = new TaskData();
                }
            }
            else
            {
                _taskData = new TaskData();
            }

            // 确保至少有一个任务
            if (_taskData.Tasks.Count == 0)
            {
                _taskData.Tasks.Add(new Task());
            }
        }

        public void SaveTasks()
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var json = serializer.Serialize(_taskData);
                File.WriteAllText(TasksFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存任务失败: {ex.Message}");
            }
        }

        public Task GetCurrentTask()
        {
            return _taskData.Tasks.FirstOrDefault() ?? new Task();
        }

        public void UpdateCurrentTask(Task task)
        {
            if (_taskData.Tasks.Count > 0)
            {
                _taskData.Tasks[0] = task;
            }
            else
            {
                _taskData.Tasks.Add(task);
            }
            SaveTasks();
        }

        public bool HasRecordedActions()
        {
            var task = GetCurrentTask();
            return task.Events != null && task.Events.Count > 0;
        }
    }
}

