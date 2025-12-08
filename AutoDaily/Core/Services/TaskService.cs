using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using AutoDaily.Core.Models;

namespace AutoDaily.Core.Services
{
    /// <summary>
    /// 任务服务：负责任务的持久化存储和读取
    /// 数据格式：JSON文件，存储在exe同目录下的Data文件夹
    /// </summary>
    public class TaskService
    {
        #region 常量定义
        /// <summary>数据文件夹名称</summary>
        private const string DATA_FOLDER_NAME = "Data";
        
        /// <summary>任务数据文件名</summary>
        private const string TASKS_FILE_NAME = "tasks.json";
        #endregion

        #region 私有字段
        /// <summary>数据存储目录（exe同目录下的Data文件夹）</summary>
        private static readonly string DataDirectory = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory,
            DATA_FOLDER_NAME);
        
        /// <summary>任务数据文件完整路径</summary>
        private static readonly string TasksFilePath = Path.Combine(DataDirectory, TASKS_FILE_NAME);

        /// <summary>当前任务数据（内存缓存）</summary>
        private TaskData _taskData;
        
        /// <summary>JSON序列化器（使用.NET Framework内置的JavaScriptSerializer）</summary>
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化任务服务：确保数据目录存在，并加载任务数据
        /// </summary>
        public TaskService()
        {
            EnsureDataDirectory();
            LoadTasks();
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 确保数据目录存在，如果不存在则创建
        /// </summary>
        private void EnsureDataDirectory()
        {
            try
            {
                if (!Directory.Exists(DataDirectory))
                {
                    Directory.CreateDirectory(DataDirectory);
                    LogService.Log($"创建数据目录: {DataDirectory}");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"创建数据目录失败: {DataDirectory}", ex);
                throw; // 数据目录创建失败是严重错误，需要抛出异常
            }
        }

        /// <summary>
        /// 从文件加载任务数据
        /// 如果文件不存在或读取失败，创建新的空任务数据
        /// </summary>
        private void LoadTasks()
        {
            if (File.Exists(TasksFilePath))
            {
                try
                {
                    var json = File.ReadAllText(TasksFilePath, System.Text.Encoding.UTF8);
                    _taskData = _serializer.Deserialize<TaskData>(json) ?? new TaskData();
                    LogService.Log($"成功加载任务数据: {TasksFilePath}");
                }
                catch (Exception ex)
                {
                    // 文件损坏或格式错误，创建新的任务数据
                    LogService.LogError($"加载任务数据失败，将创建新数据: {TasksFilePath}", ex);
                    _taskData = new TaskData();
                }
            }
            else
            {
                // 文件不存在，创建新的任务数据
                _taskData = new TaskData();
                LogService.Log($"任务数据文件不存在，创建新数据: {TasksFilePath}");
            }

            // 确保至少有一个任务（防止空列表导致程序异常）
            if (_taskData.Tasks.Count == 0)
            {
                _taskData.Tasks.Add(new Task());
                LogService.Log("创建默认任务");
            }
        }

        /// <summary>
        /// 保存任务数据到文件
        /// </summary>
        private void SaveTasks()
        {
            try
            {
                var json = _serializer.Serialize(_taskData);
                File.WriteAllText(TasksFilePath, json, System.Text.Encoding.UTF8);
                LogService.Log($"成功保存任务数据: {TasksFilePath}");
            }
            catch (Exception ex)
            {
                // 保存失败是严重错误，记录日志但不抛出异常（避免影响用户体验）
                LogService.LogError($"保存任务数据失败: {TasksFilePath}", ex);
            }
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 获取当前任务（第一个任务）
        /// </summary>
        /// <returns>当前任务，如果不存在则返回新任务</returns>
        public Task GetCurrentTask()
        {
            return _taskData.Tasks.FirstOrDefault() ?? new Task();
        }

        /// <summary>
        /// 更新当前任务并保存到文件
        /// </summary>
        /// <param name="task">要更新的任务</param>
        public void UpdateCurrentTask(Task task)
        {
            if (task == null)
            {
                LogService.LogWarning("尝试更新空任务，操作已忽略");
                return;
            }

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

        /// <summary>
        /// 检查当前任务是否已录制动作
        /// </summary>
        /// <returns>true=已录制动作，false=未录制动作</returns>
        public bool HasRecordedActions()
        {
            var task = GetCurrentTask();
            return task.Actions != null && task.Actions.Count > 0;
        }
        #endregion
    }
}

