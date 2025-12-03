using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using AutoDaily.Core.Models;

namespace AutoDaily.Core.Services
{
    public class TaskService
    {
        // 数据存储在exe同目录下的Data文件夹
        private static readonly string DataDirectory = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory,
            "Data");
        private static readonly string TasksFilePath = Path.Combine(DataDirectory, "tasks.json");

        private TaskData _taskData;

        public TaskService()
        {
            EnsureDataDirectory();
            LoadTasks();
        }

        private void EnsureDataDirectory()
        {
            if (!Directory.Exists(DataDirectory))
            {
                Directory.CreateDirectory(DataDirectory);
            }
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
            return task.Actions != null && task.Actions.Count > 0;
        }
    }
}

