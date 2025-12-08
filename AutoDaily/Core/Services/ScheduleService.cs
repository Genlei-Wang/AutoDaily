using System;
using System.Linq;
using System.Timers;
using System.Windows.Forms;
using AutoDaily.Core.Models;

namespace AutoDaily.Core.Services
{
    /// <summary>
    /// 定时调度服务：负责在指定时间自动执行任务
    /// 功能：智能调度（根据距离目标时间动态调整检查频率）、防重复触发、开机自启
    /// </summary>
    public class ScheduleService : IDisposable
    {
        #region 常量定义
        /// <summary>默认检查间隔：60秒（当距离目标时间较远时）</summary>
        private const double DEFAULT_CHECK_INTERVAL_MS = 60000;
        
        /// <summary>中等检查间隔：10秒（距离目标时间1-5分钟时）</summary>
        private const double MEDIUM_CHECK_INTERVAL_MS = 10000;
        
        /// <summary>精确检查间隔：1秒（距离目标时间小于1分钟时，确保精确触发）</summary>
        private const double PRECISE_CHECK_INTERVAL_MS = 1000;
        
        /// <summary>切换到中等检查的时间阈值：5分钟</summary>
        private const double MEDIUM_THRESHOLD_MINUTES = 5;
        
        /// <summary>切换到精确检查的时间阈值：1分钟</summary>
        private const double PRECISE_THRESHOLD_MINUTES = 1;
        
        /// <summary>时间误差容忍度：30秒（超过此时间不再触发）</summary>
        private const double TIME_TOLERANCE_SECONDS = 30;
        
        /// <summary>防重复触发的最小间隔：1分钟</summary>
        private const double MIN_TRIGGER_INTERVAL_MINUTES = 1;
        
        /// <summary>Windows注册表启动项路径</summary>
        private const string REGISTRY_RUN_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        
        /// <summary>注册表中的应用名称</summary>
        private const string REGISTRY_APP_NAME = "AutoDaily";
        #endregion

        #region 私有字段
        private readonly System.Timers.Timer _timer;
        private readonly TaskService _taskService;
        private readonly Action<Task> _onTaskTriggered;
        private DateTime? _lastTriggerTime; // 防止重复触发
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化调度服务
        /// </summary>
        /// <param name="taskService">任务服务，用于获取当前任务</param>
        /// <param name="onTaskTriggered">任务触发时的回调函数</param>
        public ScheduleService(TaskService taskService, Action<Task> onTaskTriggered)
        {
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _onTaskTriggered = onTaskTriggered ?? throw new ArgumentNullException(nameof(onTaskTriggered));

            // 创建定时器，初始间隔为默认值（60秒）
            _timer = new System.Timers.Timer(DEFAULT_CHECK_INTERVAL_MS);
            _timer.Elapsed += Timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Start();
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 定时器事件处理：检查是否需要执行任务，并动态调整检查间隔
        /// </summary>
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CheckSchedule();
            AdjustTimerInterval();
        }

        /// <summary>
        /// 智能调整定时器检查间隔
        /// 策略：距离目标时间越近，检查频率越高，确保精确触发
        /// </summary>
        private void AdjustTimerInterval()
        {
            var task = _taskService.GetCurrentTask();
            if (!task.Schedule.Enabled)
            {
                // 未启用定时任务时，使用默认间隔，节省资源
                _timer.Interval = DEFAULT_CHECK_INTERVAL_MS;
                return;
            }

            var now = DateTime.Now;
            var scheduleTime = CalculateScheduleTime(now, task.Schedule);
            var timeUntilSchedule = (scheduleTime - now).TotalMinutes;

            // 根据距离目标时间的远近，动态调整检查间隔
            if (timeUntilSchedule > MEDIUM_THRESHOLD_MINUTES)
            {
                // 距离目标时间 > 5分钟：每60秒检查一次（节省资源）
                _timer.Interval = DEFAULT_CHECK_INTERVAL_MS;
            }
            else if (timeUntilSchedule > PRECISE_THRESHOLD_MINUTES)
            {
                // 距离目标时间 1-5分钟：每10秒检查一次（提高响应速度）
                _timer.Interval = MEDIUM_CHECK_INTERVAL_MS;
            }
            else
            {
                // 距离目标时间 < 1分钟：每1秒检查一次（确保精确触发）
                _timer.Interval = PRECISE_CHECK_INTERVAL_MS;
            }
        }

        /// <summary>
        /// 计算下一次调度时间（今天或明天）
        /// </summary>
        private DateTime CalculateScheduleTime(DateTime now, Schedule schedule)
        {
            var scheduleTime = new DateTime(now.Year, now.Month, now.Day, 
                schedule.Hour, schedule.Minute, 0);
            
            // 如果今天已经过了设定时间，计算明天的
            if (now > scheduleTime)
            {
                scheduleTime = scheduleTime.AddDays(1);
            }
            
            return scheduleTime;
        }

        /// <summary>
        /// 检查是否到达执行时间，如果到达则触发任务
        /// </summary>
        private void CheckSchedule()
        {
            var task = _taskService.GetCurrentTask();
            if (!task.Schedule.Enabled)
            {
                _lastTriggerTime = null;
                return;
            }

            var now = DateTime.Now;
            var scheduleTime = CalculateScheduleTime(now, task.Schedule);

            // 精确时间检查：当前时间 >= 设定时间，且误差在容忍范围内
            bool isTimeReached = now >= scheduleTime && 
                (now - scheduleTime).TotalSeconds <= TIME_TOLERANCE_SECONDS;
            
            // 检查今天是否已经运行过（每天只执行一次）
            bool notRunToday = !task.LastRun.HasValue || task.LastRun.Value.Date != now.Date;
            
            // 防止重复触发：如果上次触发时间与当前时间间隔太短，不触发
            bool notRecentlyTriggered = !_lastTriggerTime.HasValue || 
                (now - _lastTriggerTime.Value).TotalMinutes >= MIN_TRIGGER_INTERVAL_MINUTES;

            // 所有条件满足时，触发任务
            if (isTimeReached && notRunToday && notRecentlyTriggered)
            {
                _lastTriggerTime = now;
                
                // 更新最后运行时间，防止重复触发
                task.LastRun = now;
                _taskService.UpdateCurrentTask(task);
                
                // 触发任务执行回调
                _onTaskTriggered?.Invoke(task);
            }
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 获取下一次运行时间
        /// </summary>
        /// <returns>下一次运行时间，如果未启用定时任务则返回null</returns>
        public DateTime? GetNextRunTime()
        {
            var task = _taskService.GetCurrentTask();
            if (!task.Schedule.Enabled)
                return null;

            var now = DateTime.Now;
            return CalculateScheduleTime(now, task.Schedule);
        }

        /// <summary>
        /// 设置/取消开机自启动
        /// 通过写入Windows注册表实现，无需管理员权限（使用HKCU）
        /// </summary>
        /// <param name="enable">true=设置开机自启，false=取消开机自启</param>
        public void SetStartup(bool enable)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(REGISTRY_RUN_KEY, true))
                {
                    if (key == null)
                    {
                        LogService.LogWarning("无法打开注册表启动项，可能权限不足");
                        return;
                    }

                    if (enable)
                    {
                        // 设置开机自启：写入注册表，使用引号包裹路径以支持路径中的空格
                        string appPath = Application.ExecutablePath;
                        key.SetValue(REGISTRY_APP_NAME, $"\"{appPath}\"");
                        LogService.Log($"已设置开机自启: {appPath}");
                    }
                    else
                    {
                        // 取消开机自启：删除注册表项
                        if (key.GetValue(REGISTRY_APP_NAME) != null)
                        {
                            key.DeleteValue(REGISTRY_APP_NAME);
                            LogService.Log("已取消开机自启");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序运行（可能是权限问题或注册表被锁定）
                LogService.LogError($"设置开机自启失败", ex);
            }
        }
        #endregion

        #region IDisposable实现
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }
        #endregion
    }
}

